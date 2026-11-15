using System.IO.Compression;
using ImageMagick;

namespace Heic2Jpeg;

/// <summary>
/// A folder or ZIP file to write JPEGs to. <see cref="Write"/> is safe to call from several threads;
/// nothing is final until <see cref="Commit"/> has been called.
/// </summary>
internal interface IPhotoDestination : IDisposable
{
    /// <param name="jpegPath">'/'-separated path relative to the destination root.</param>
    void Write(string jpegPath, MagickImage image);

    void Commit();
}

internal static class PhotoDestination
{
    public static IPhotoDestination Open(string path) =>
        Locations.IsZipDestination(path) ? new ZipDestination(path) : new FolderDestination(path);

    /// <summary>Splits a relative path, refusing anything that could point outside the destination.</summary>
    public static string[] SplitSafely(string relativePath)
    {
        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException(string.Format(Strings.RefusingToWriteOutside, relativePath));
        }

        return segments;
    }
}

/// <summary>Writes JPEGs as files, recreating the source's subfolders (ZIP sources can have them).</summary>
internal sealed class FolderDestination : IPhotoDestination
{
    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    private readonly string _root;
    private readonly string _rootPrefix;

    public FolderDestination(string root)
    {
        _root = Path.GetFullPath(root);
        _rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(_root);
    }

    public void Write(string jpegPath, MagickImage image)
    {
        var segments = PhotoDestination.SplitSafely(jpegPath);
        if (segments.Any(segment => segment.IndexOfAny(InvalidNameChars) >= 0))
        {
            throw new IOException(string.Format(Strings.NotAValidFileName, jpegPath));
        }

        var target = Path.GetFullPath(Path.Combine([_root, .. segments]));
        if (!target.StartsWith(_rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException(string.Format(Strings.RefusingToWriteOutside, jpegPath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        image.Write(target);
    }

    public void Commit()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Writes JPEGs into a ZIP file. An existing ZIP keeps its other entries; entries with the same name
/// are replaced. The new archive is built next to the old one and swapped in by <see cref="Commit"/>,
/// so a failed or cancelled run leaves the original untouched.
/// </summary>
internal sealed class ZipDestination : IPhotoDestination
{
    private readonly string _zipPath;
    private readonly string _tempPath;
    private readonly ZipArchive _output;
    private readonly HashSet<string> _written = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private ZipArchive? _existing;
    private bool _committed;

    public ZipDestination(string zipPath)
    {
        _zipPath = Path.GetFullPath(zipPath);
        var folder = Path.GetDirectoryName(_zipPath)!;
        Directory.CreateDirectory(folder);

        if (File.Exists(_zipPath))
        {
            // Fail now, not after converting everything, if the file is not a readable ZIP.
            _existing = ZipFile.OpenRead(_zipPath);
            try
            {
                _ = _existing.Entries;
            }
            catch
            {
                _existing.Dispose();
                throw;
            }
        }

        _tempPath = Path.Combine(folder, $".{Path.GetFileName(_zipPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            _output = ZipFile.Open(_tempPath, ZipArchiveMode.Create);
        }
        catch
        {
            _existing?.Dispose();
            throw;
        }
    }

    public void Write(string jpegPath, MagickImage image)
    {
        PhotoDestination.SplitSafely(jpegPath);

        // Encode outside the lock so threads only queue up for the (fast) copy into the archive.
        var jpeg = image.ToByteArray();

        lock (_lock)
        {
            if (!_written.Add(jpegPath))
            {
                throw new IOException(string.Format(Strings.AlreadySavedAs, jpegPath));
            }

            // JPEG data does not deflate, so store it as is.
            var entry = _output.CreateEntry(jpegPath, CompressionLevel.NoCompression);
            using var stream = entry.Open();
            stream.Write(jpeg);
        }
    }

    public void Commit()
    {
        if (_existing is not null)
        {
            _output.Comment = _existing.Comment;

            foreach (var entry in _existing.Entries)
            {
                if (!_written.Contains(ZipNames.Normalize(entry.FullName)))
                {
                    CopyEntry(entry);
                }
            }

            // Windows cannot replace a file that is still open.
            _existing.Dispose();
            _existing = null;
        }

        _output.Dispose();
        File.Move(_tempPath, _zipPath, overwrite: true);
        _committed = true;
    }

    public void Dispose()
    {
        _existing?.Dispose();
        if (_committed)
        {
            return;
        }

        try
        {
            _output.Dispose();
        }
        catch (IOException)
        {
            // The temp file is deleted below either way.
        }

        try
        {
            File.Delete(_tempPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; a leftover temp file is harmless.
        }
    }

    private void CopyEntry(ZipArchiveEntry entry)
    {
        // Keep stored entries stored (photos, other ZIPs) and compress the rest.
        var level = entry.CompressedLength < entry.Length ? CompressionLevel.Optimal : CompressionLevel.NoCompression;

        var copy = _output.CreateEntry(entry.FullName, level);
        copy.LastWriteTime = entry.LastWriteTime;
        copy.ExternalAttributes = entry.ExternalAttributes;
        copy.Comment = entry.Comment;

        using var from = entry.Open();
        using var to = copy.Open();
        from.CopyTo(to);
    }
}
