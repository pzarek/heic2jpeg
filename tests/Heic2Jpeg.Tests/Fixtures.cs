namespace Heic2Jpeg.Tests;

public static class Fixtures
{
    /// <summary>Sample image from the libheif project (examples/example.heic), 1280x854.</summary>
    public static string ExampleHeic => Path.Combine(AppContext.BaseDirectory, "Fixtures", "example.heic");

    public const uint ExampleWidth = 1280;
    public const uint ExampleHeight = 854;

    public static void CopyExampleTo(string path) => File.Copy(ExampleHeic, path);

    public static void WriteCorruptHeic(string path) => File.WriteAllBytes(path, CorruptBytes());

    public static byte[] CorruptBytes()
    {
        var garbage = new byte[4096];
        Random.Shared.NextBytes(garbage);
        return garbage;
    }

    /// <summary>
    /// Creates a ZIP file. Entries map a name to its content: <see cref="Example"/> for the sample
    /// photo, a string for text, or a byte array.
    /// </summary>
    public static void CreateZip(string path, params (string Name, object Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var bytes = content switch
            {
                ExampleMarker => File.ReadAllBytes(ExampleHeic),
                string text => System.Text.Encoding.UTF8.GetBytes(text),
                byte[] data => data,
                _ => throw new ArgumentException($"Unsupported content for {name}"),
            };

            using var stream = archive.CreateEntry(name).Open();
            stream.Write(bytes);
        }
    }

    /// <summary>Stands for the sample HEIC photo in <see cref="CreateZip"/>.</summary>
    public static readonly object Example = new ExampleMarker();

    public static string[] ZipEntryNames(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
    }

    public static byte[] ReadZipEntry(string path, string name)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(name) ?? throw new FileNotFoundException($"No entry {name} in {path}");
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class ExampleMarker;
}
