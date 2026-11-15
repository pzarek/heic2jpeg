using Spectre.Console;

namespace Heic2Jpeg;

/// <summary>
/// The menu shown when heic2jpeg is started without arguments in a terminal: pick a source and
/// destination with <see cref="PathBrowser"/>, optionally change the quality, and convert.
/// </summary>
public sealed class InteractiveMenu
{
    private readonly IAnsiConsole _console;
    private readonly PathBrowser _browser;
    private readonly string _version;
    private readonly string _startFolder;

    private string? _source;
    private string _sourceDetails = "";
    private string? _destination;
    private int _quality = 90;

    public InteractiveMenu(IAnsiConsole console, string version, string? startFolder = null)
    {
        _console = console;
        _browser = new PathBrowser(console);
        _version = version;
        _startFolder = Path.GetFullPath(startFolder ?? Environment.CurrentDirectory);
    }

    private enum Action
    {
        Source,
        Destination,
        Quality,
        Convert,
        Exit,
    }

    /// <summary>True when both keyboard and screen are a terminal that can draw the menu.</summary>
    public static bool IsSupported(IAnsiConsole console) =>
        !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && console.Profile.Capabilities.Interactive
        && console.Profile.Capabilities.Ansi;

    /// <returns>The exit code of the last conversion, or 0 if nothing was converted.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var exitCode = 0;
        try
        {
            while (true)
            {
                switch (await ShowMenuAsync(cancellationToken))
                {
                    case Action.Source:
                        await ChooseSourceAsync(cancellationToken);
                        break;

                    case Action.Destination:
                        await ChooseDestinationAsync(cancellationToken);
                        break;

                    case Action.Quality:
                        await ChooseQualityAsync(cancellationToken);
                        break;

                    case Action.Convert:
                        // Walk through whatever is still missing before converting.
                        if ((_source is null && !await ChooseSourceAsync(cancellationToken))
                            || (_destination is null && !await ChooseDestinationAsync(cancellationToken)))
                        {
                            break;
                        }

                        exitCode = await ConvertAsync(cancellationToken);
                        if (cancellationToken.IsCancellationRequested || !await AskBackToMenuAsync(cancellationToken))
                        {
                            return exitCode;
                        }

                        break;

                    case Action.Exit:
                        return exitCode;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Converter.CancelledExitCode;
        }
        finally
        {
            // A prompt cancelled with Ctrl+C leaves the cursor hidden.
            _console.Cursor.Show();
        }
    }

    private async Task<Action> ShowMenuAsync(CancellationToken cancellationToken)
    {
        _console.Clear();
        _console.Write(new Rule($"[bold]heic2jpeg[/] [grey]{Markup.Escape(_version)}[/]").LeftJustified());
        _console.MarkupLine("[grey]Converts HEIC/HEIF photos to JPEG. Run [bold]heic2jpeg --help[/] for the command-line options.[/]");
        _console.WriteLine();

        var next = _source is null ? Action.Source : _destination is null ? Action.Destination : Action.Convert;

        return await new SelectionPrompt<Action>()
            .UseConverter(Describe)
            .AddChoices(Action.Source, Action.Destination, Action.Quality, Action.Convert, Action.Exit)
            .DefaultValue(next)
            .WrapAround()
            .ShowAsync(_console, cancellationToken);
    }

    private string Describe(Action action)
    {
        string PathWithDetails(string path, string details)
        {
            var suffix = details.Length > 0 ? $" ({details})" : "";

            // Leave room for the "> " cursor and the label column so that no item wraps.
            var pathWidth = Math.Max(10, _console.Profile.Width - 2 - 14 - suffix.Length - 1);

            return Markup.Escape(Display.FitStart(Display.Path(path), pathWidth)) + $"[grey]{Markup.Escape(suffix)}[/]";
        }

        return action switch
        {
            Action.Source => "Source        " + (_source is null
                ? "[grey]not chosen[/]"
                : PathWithDetails(_source, _sourceDetails)),
            Action.Destination => "Destination   " + (_destination is null
                ? "[grey]not chosen[/]"
                : PathWithDetails(_destination, DescribeDestination(_destination))),
            Action.Quality => $"JPEG quality  {_quality}",
            Action.Convert => "[green]Convert[/]",
            _ => "Exit",
        };
    }

    private async Task<bool> ChooseSourceAsync(CancellationToken cancellationToken)
    {
        var source = await _browser.BrowseAsync(BrowseMode.Source, StartFolderFor(_source), "", cancellationToken);
        if (source is null)
        {
            return false;
        }

        _source = source;
        _sourceDetails = DescribeSource(source);
        return true;
    }

    private async Task<bool> ChooseDestinationAsync(CancellationToken cancellationToken)
    {
        // Start next to the source: a new folder there becomes a sibling of the source folder.
        var start = _destination is not null
            ? StartFolderFor(_destination)
            : _source is not null ? StartFolderFor(Path.GetDirectoryName(_source) ?? _source) : _startFolder;

        var destination = await _browser.BrowseAsync(BrowseMode.Destination, start, SuggestedName(), cancellationToken);
        if (destination is null)
        {
            return false;
        }

        _destination = destination;
        return true;
    }

    private async Task ChooseQualityAsync(CancellationToken cancellationToken)
    {
        _console.Clear();
        _console.Write(new Rule("[bold]JPEG quality[/]").LeftJustified());
        _console.MarkupLine("[grey]90 is a good default for photos. Lower it for smaller files.[/]");
        _console.WriteLine();

        _quality = await new TextPrompt<int>("JPEG quality, 1-100")
            .DefaultValue(_quality)
            .Validate(quality => quality is >= 1 and <= 100
                ? ValidationResult.Success()
                : ValidationResult.Error("[red]Enter a number from 1 to 100.[/]"))
            .ShowAsync(_console, cancellationToken);
    }

    private async Task<int> ConvertAsync(CancellationToken cancellationToken)
    {
        _console.Clear();

        var error = Locations.ValidateSource(_source!) ?? Locations.ValidateDestination(_destination!);
        if (error is not null)
        {
            _console.MarkupLineInterpolated($"[red]Error:[/] {error}");
            return 1;
        }

        var exitCode = await Converter.RunAsync(_console, _source!, _destination!, _quality, cancellationToken);

        // The source may now hold different photos, for example after converting into the source ZIP itself.
        _sourceDetails = DescribeSource(_source!);
        return exitCode;
    }

    private async Task<bool> AskBackToMenuAsync(CancellationToken cancellationToken)
    {
        _console.WriteLine();
        const string back = "Back to the menu";
        var choice = await new SelectionPrompt<string>()
            .AddChoices(back, "Exit")
            .AddCancelResult(back)
            .ShowAsync(_console, cancellationToken);

        return choice == back;
    }

    /// <summary>The folder itself, or for a file or a folder that does not exist yet, the closest existing parent.</summary>
    private string StartFolderFor(string? path)
    {
        for (var folder = path; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (Directory.Exists(folder))
            {
                return folder;
            }
        }

        return _startFolder;
    }

    private string SuggestedName()
    {
        var name = _source is null ? "" : Path.GetFileNameWithoutExtension(Path.TrimEndingDirectorySeparator(_source));
        return name.Length == 0 ? "jpeg" : name + "-jpeg";
    }

    private static string DescribeSource(string source)
    {
        try
        {
            using var photos = PhotoSource.Open(source);
            var count = Display.Photos(photos.Photos.Count);
            return Directory.Exists(source) ? count : "ZIP file, " + count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return "cannot be read";
        }
    }

    private static string DescribeDestination(string destination)
    {
        if (Locations.IsZipDestination(destination))
        {
            return File.Exists(destination) ? "existing ZIP file" : "new ZIP file";
        }

        return Directory.Exists(destination) ? "" : "new folder";
    }
}
