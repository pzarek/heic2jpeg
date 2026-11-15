using System.Collections.Concurrent;
using System.IO.Compression;
using ImageMagick;

namespace Heic2Jpeg;

/// <summary>
/// A HEIC/HEIF photo in a source, addressed by its '/'-separated path relative to the source root.
/// </summary>
public sealed record SourcePhoto(string RelativePath, int EntryIndex = -1)
{
    /// <summary>The '/'-separated path of the JPEG this photo is converted to.</summary>
    public string JpegPath => RelativePath[..RelativePath.LastIndexOf('.')] + ".jpg";
}

/// <summary>
/// A folder or ZIP file to read photos from. <see cref="Read"/> is safe to call from several threads.
/// </summary>
internal interface IPhotoSource : IDisposable
{
    IReadOnlyList<SourcePhoto> Photos { get; }

    MagickImage Read(SourcePhoto photo);
}

internal static class PhotoSource
{
    public static IPhotoSource Open(string path) =>
        Directory.Exists(path) ? new FolderSource(path) : new ZipSource(path);

    public static bool IsHeicFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return (extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
            // "._IMG_0001.HEIC" is macOS metadata (AppleDouble), not a photo.
            && !fileName.StartsWith("._", StringComparison.Ordinal);
    }
}

/// <summary>The .heic/.heif files directly inside a folder (not in subfolders).</summary>
internal sealed class FolderSource : IPhotoSource
{
    private readonly string _folder;

    public FolderSource(string folder)
    {
        _folder = folder;

        var options = new EnumerationOptions { IgnoreInaccessible = true };
        Photos = Directory.EnumerateFiles(folder, "*", options)
            .Select(path => Path.GetFileName(path))
            .Where(PhotoSource.IsHeicFileName)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new SourcePhoto(name))
            .ToList();
    }

    public IReadOnlyList<SourcePhoto> Photos { get; }

    public MagickImage Read(SourcePhoto photo) => new(Path.Combine(_folder, photo.RelativePath));

    public void Dispose()
    {
    }
}

/// <summary>Every .heic/.heif entry in a ZIP file, in any folder of the archive.</summary>
internal sealed class ZipSource : IPhotoSource
{
    private readonly string _zipPath;

    // ZipArchive is not thread-safe, so each converting thread borrows its own instance.
    private readonly ConcurrentBag<ZipArchive> _archives = [];

    public ZipSource(string zipPath)
    {
        _zipPath = zipPath;

        var archive = ZipFile.OpenRead(zipPath);
        try
        {
            Photos = archive.Entries
                .Select((entry, index) => new SourcePhoto(ZipNames.Normalize(entry.FullName), index))
                .Where(photo => IsPhoto(photo.RelativePath))
                .OrderBy(photo => photo.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            archive.Dispose();
            throw;
        }

        _archives.Add(archive);
    }

    public IReadOnlyList<SourcePhoto> Photos { get; }

    public MagickImage Read(SourcePhoto photo)
    {
        if (!_archives.TryTake(out var archive))
        {
            archive = ZipFile.OpenRead(_zipPath);
        }

        try
        {
            using var stream = archive.Entries[photo.EntryIndex].Open();
            return new MagickImage(stream);
        }
        finally
        {
            _archives.Add(archive);
        }
    }

    public void Dispose()
    {
        while (_archives.TryTake(out var archive))
        {
            archive.Dispose();
        }
    }

    private static bool IsPhoto(string name)
    {
        if (name.Length == 0 || name.EndsWith('/'))
        {
            return false;
        }

        var segments = name.Split('/');

        // macOS adds a __MACOSX folder full of "._*" metadata files to the ZIPs it creates.
        return !segments.Any(segment => segment.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase))
            && PhotoSource.IsHeicFileName(segments[^1]);
    }
}

internal static class ZipNames
{
    /// <summary>
    /// Uses '/' as the separator and drops empty and "." segments, so "\a\.\b.heic" becomes "a/b.heic".
    /// ".." is kept on purpose: destinations refuse it instead of writing outside their folder.
    /// </summary>
    public static string Normalize(string entryName)
    {
        var trailingSlash = entryName.EndsWith('/') || entryName.EndsWith('\\');
        var segments = entryName
            .Split('/', '\\')
            .Where(segment => segment is not ("" or "."));

        var normalized = string.Join('/', segments);
        return trailingSlash && normalized.Length > 0 ? normalized + "/" : normalized;
    }
}
