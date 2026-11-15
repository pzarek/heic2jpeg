using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using Spectre.Console;

namespace Heic2Jpeg;

public enum BrowseMode
{
    Source,
    Destination,
}

/// <summary>
/// A keyboard-driven browser that lists folders and .zip files, for picking a source or destination.
/// </summary>
public sealed class PathBrowser(IAnsiConsole console)
{
    private static readonly EnumerationOptions ListOptions = new() { IgnoreInaccessible = true };

    private static readonly StringComparer NameComparer = StringComparer.CurrentCultureIgnoreCase;

    private static readonly Entry BackEntry = new(Kind.Back, "[[Back]]");

    private enum Kind
    {
        UseFolder,
        NewFolder,
        NewZip,
        Parent,
        Folder,
        Zip,
        Drive,
        TypePath,
        Back,
    }

    private sealed record Entry(Kind Kind, string Label, string Path = "");

    private sealed record Listing(List<string> Folders, List<(string Path, long Size)> Zips, int PhotoCount);

    /// <param name="mode">A source must contain photos; a destination may also be a new folder or ZIP file.</param>
    /// <param name="startFolder">Where browsing starts.</param>
    /// <param name="newName">Suggested name for a new destination folder or ZIP file, without extension.</param>
    /// <returns>The chosen folder or ZIP file, or null if the user went back.</returns>
    public async Task<string?> BrowseAsync(
        BrowseMode mode,
        string startFolder,
        string newName,
        CancellationToken cancellationToken)
    {
        string? folder = Path.GetFullPath(startFolder); // null shows the list of drives.
        string? message = null;
        string? highlight = null;

        while (true)
        {
            Listing? listing = null;
            if (folder is not null && !TryList(folder, out listing, out var error))
            {
                message = $"[red]Cannot open {Markup.Escape(Display.Path(folder))}:[/] {Markup.Escape(error)}";
                highlight = folder;
                folder = Directory.GetParent(folder)?.FullName;
                continue;
            }

            console.Clear();
            var headerLines = WriteHeader(mode, folder, message);
            message = null;

            var entries = folder is null ? DriveEntries() : FolderEntries(mode, folder, listing!);
            entries.Add(new Entry(Kind.TypePath, "[[Type a path]]"));
            entries.Add(BackEntry);

            var preselected = entries.FirstOrDefault(entry =>
                entry.Kind is Kind.Folder or Kind.Zip or Kind.Drive && Display.SamePath(entry.Path, highlight));
            highlight = null;

            var choice = await new SelectionPrompt<Entry>()
                .UseConverter(entry => entry.Label)
                .AddChoices(entries)
                .DefaultValue(preselected ?? entries[0])
                .AddCancelResult(BackEntry)
                .WrapAround()
                .PageSize(Math.Max(3, console.Profile.Height - headerLines - 3))
                .MoreChoicesText("[grey](Move up and down to see more, Esc to go back)[/]")
                .ShowAsync(console, cancellationToken);

            switch (choice.Kind)
            {
                case Kind.Back:
                    return null;

                case Kind.UseFolder when mode == BrowseMode.Source && listing!.PhotoCount == 0:
                    message = "[yellow]There are no .heic/.heif files in this folder.[/]";
                    break;

                case Kind.UseFolder:
                    return folder;

                case Kind.NewFolder:
                    return Path.Combine(folder!, await AskNameAsync(folder!, newName, zip: false, cancellationToken));

                case Kind.NewZip:
                    return Path.Combine(folder!, await AskNameAsync(folder!, newName, zip: true, cancellationToken));

                case Kind.Parent:
                    highlight = folder;
                    folder = Directory.GetParent(folder!)?.FullName;
                    break;

                case Kind.Folder or Kind.Drive:
                    folder = choice.Path;
                    break;

                case Kind.Zip:
                    message = CheckZip(mode, choice.Path);
                    if (message is null)
                    {
                        return choice.Path;
                    }

                    highlight = choice.Path;
                    break;

                case Kind.TypePath:
                    var (path, pathError) = await AskPathAsync(folder, cancellationToken);
                    if (path is null)
                    {
                        message = pathError;
                    }
                    else if (Directory.Exists(path))
                    {
                        folder = path;
                    }
                    else if (File.Exists(path))
                    {
                        message = Locations.HasZipExtension(path)
                            ? CheckZip(mode, path)
                            : $"[red]Not a folder or a .zip file:[/] {Markup.Escape(Display.Path(path))}";

                        if (message is null)
                        {
                            return path;
                        }
                    }
                    else if (mode == BrowseMode.Destination)
                    {
                        // A new folder or ZIP file; it is created when converting.
                        return path;
                    }
                    else
                    {
                        message = $"[red]Not found:[/] {Markup.Escape(Display.Path(path))}";
                    }

                    break;
            }
        }
    }

    private int WriteHeader(BrowseMode mode, string? folder, string? message)
    {
        console.Write(new Rule(mode == BrowseMode.Source
            ? "[bold]Choose the source[/]"
            : "[bold]Choose the destination[/]").LeftJustified());

        var description = mode == BrowseMode.Source
            ? "A folder with .heic/.heif photos, or a .zip file that contains them."
            : "A folder for the .jpg files, or a .zip file to put them in.";
        console.MarkupLineInterpolated($"[grey]{description}[/]");

        var location = folder is null ? "Drives" : Display.FitStart(Display.Path(folder), console.Profile.Width - 1);
        console.MarkupLineInterpolated($"[bold]{location}[/]");

        if (message is not null)
        {
            console.MarkupLine(message);
        }

        console.WriteLine();

        // Rule, location and blank line, plus the lines the description and message wrap onto.
        var width = Math.Max(1, console.Profile.Width);
        int LinesFor(string text) => Math.Max(1, (text.Length + width - 1) / width);
        return 3 + LinesFor(description) + (message is null ? 0 : LinesFor(Markup.Remove(message)));
    }

    private List<Entry> FolderEntries(BrowseMode mode, string folder, Listing listing)
    {
        var entries = new List<Entry>();

        if (mode == BrowseMode.Source)
        {
            entries.Add(new Entry(Kind.UseFolder, $"[[Use this folder]] [grey]({Display.Photos(listing.PhotoCount)})[/]"));
        }
        else
        {
            entries.Add(new Entry(Kind.UseFolder, "[[Use this folder]]"));
            entries.Add(new Entry(Kind.NewFolder, "[[New folder]]"));
            entries.Add(new Entry(Kind.NewZip, "[[New ZIP file]]"));
        }

        // On Windows, going up from a drive's root shows the list of drives.
        if (Directory.GetParent(folder) is not null || OperatingSystem.IsWindows())
        {
            entries.Add(new Entry(Kind.Parent, ".."));
        }

        // Leave room for the "> " cursor so that no item wraps onto a second line.
        var width = console.Profile.Width - 3;

        foreach (var path in listing.Folders)
        {
            var name = Path.GetFileName(path) + Path.DirectorySeparatorChar;
            entries.Add(new Entry(Kind.Folder, Markup.Escape(Display.FitEnd(name, width)), path));
        }

        foreach (var (path, size) in listing.Zips)
        {
            var details = $"  ({Display.Size(size)})";
            var name = Display.FitEnd(Path.GetFileName(path), width - details.Length);
            entries.Add(new Entry(Kind.Zip, $"{Markup.Escape(name)}[grey]{details}[/]", path));
        }

        return entries;
    }

    private static List<Entry> DriveEntries()
    {
        var entries = new List<Entry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "" : $"  [grey]{Markup.Escape(drive.VolumeLabel)}[/]";
                entries.Add(new Entry(Kind.Drive, Markup.Escape(drive.Name) + label, drive.RootDirectory.FullName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Skip drives that disappear or refuse access while being listed.
            }
        }

        return entries;
    }

    private static bool TryList(string folder, [NotNullWhen(true)] out Listing? listing, out string error)
    {
        try
        {
            var folders = new List<string>();
            var zips = new List<(string Path, long Size)>();
            var photos = 0;

            foreach (var info in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", ListOptions))
            {
                if (info is DirectoryInfo)
                {
                    folders.Add(info.FullName);
                }
                else if (info is FileInfo file && Locations.HasZipExtension(file.Name))
                {
                    zips.Add((file.FullName, file.Length));
                }
                else if (PhotoSource.IsHeicFileName(info.Name))
                {
                    photos++;
                }
            }

            folders.Sort((a, b) => NameComparer.Compare(Path.GetFileName(a), Path.GetFileName(b)));
            zips.Sort((a, b) => NameComparer.Compare(Path.GetFileName(a.Path), Path.GetFileName(b.Path)));

            listing = new Listing(folders, zips, photos);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            listing = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Returns an error message, or null when the ZIP file can be used.</summary>
    private static string? CheckZip(BrowseMode mode, string path)
    {
        var name = Markup.Escape(Path.GetFileName(path));
        try
        {
            if (mode == BrowseMode.Source)
            {
                using var source = new ZipSource(path);
                return source.Photos.Count == 0 ? $"[yellow]There are no .heic/.heif files in {name}.[/]" : null;
            }

            using var archive = ZipFile.OpenRead(path);
            _ = archive.Entries;
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return $"[red]Cannot read {name}:[/] {Markup.Escape(ex.Message)}";
        }
    }

    private async Task<string> AskNameAsync(string folder, string suggestedName, bool zip, CancellationToken cancellationToken)
    {
        var name = await new TextPrompt<string>(zip ? "Name of the new ZIP file" : "Name of the new folder")
            .DefaultValue(zip ? suggestedName + ".zip" : suggestedName)
            .Validate(input => ValidateName(input, folder, zip))
            .ShowAsync(console, cancellationToken);

        name = name.Trim();
        return zip && !Locations.HasZipExtension(name) ? name + ".zip" : name;
    }

    private static ValidationResult ValidateName(string input, string folder, bool zip)
    {
        var name = input.Trim();
        if (name.Length == 0 || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return ValidationResult.Error("[red]That is not a valid name.[/]");
        }

        if (zip)
        {
            var file = Locations.HasZipExtension(name) ? name : name + ".zip";
            return Directory.Exists(Path.Combine(folder, file))
                ? ValidationResult.Error("[red]There is already a folder with that name.[/]")
                : ValidationResult.Success();
        }

        if (Locations.HasZipExtension(name))
        {
            return ValidationResult.Error("[red]A folder name cannot end in .zip. Choose [[New ZIP file]] instead.[/]");
        }

        return File.Exists(Path.Combine(folder, name))
            ? ValidationResult.Error("[red]There is already a file with that name.[/]")
            : ValidationResult.Success();
    }

    private async Task<(string? Path, string? Error)> AskPathAsync(string? folder, CancellationToken cancellationToken)
    {
        var text = await new TextPrompt<string>("Folder or .zip file [grey](leave empty to go back)[/]:")
            .AllowEmpty()
            .ShowAsync(console, cancellationToken);

        // "Copy as path" in Windows Explorer wraps the path in quotes.
        text = text.Trim().Trim('"').Trim();
        if (text.Length == 0)
        {
            return (null, null);
        }

        try
        {
            return (Path.GetFullPath(Display.ExpandHome(text), folder ?? Environment.CurrentDirectory), null);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return (null, $"[red]Not a valid path:[/] {Markup.Escape(text)}");
        }
    }
}
