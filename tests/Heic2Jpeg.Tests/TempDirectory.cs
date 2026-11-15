namespace Heic2Jpeg.Tests;

/// <summary>
/// A unique, empty folder that is deleted when disposed.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "heic2jpeg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    public string FullPath { get; }

    public string File(string name) => Path.Combine(FullPath, name);

    public string[] Files(string pattern = "*") =>
        Directory.Exists(FullPath)
            ? Directory.GetFiles(FullPath, pattern).Select(path => Path.GetFileName(path)).Order().ToArray()
            : [];

    /// <summary>Every file below the folder, as '/'-separated relative paths.</summary>
    public string[] AllFiles() =>
        Directory.Exists(FullPath)
            ? Directory.GetFiles(FullPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(FullPath, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

    public string Folder(string name)
    {
        var path = Path.Combine(FullPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; a leftover temp folder is not worth failing a test over.
        }
    }
}
