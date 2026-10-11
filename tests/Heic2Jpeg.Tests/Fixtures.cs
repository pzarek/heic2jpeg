namespace Heic2Jpeg.Tests;

public static class Fixtures
{
    /// <summary>Sample image from the libheif project (examples/example.heic), 1280x854.</summary>
    public static string ExampleHeic => Path.Combine(AppContext.BaseDirectory, "Fixtures", "example.heic");

    public const uint ExampleWidth = 1280;
    public const uint ExampleHeight = 854;

    public static void CopyExampleTo(string path) => File.Copy(ExampleHeic, path);

    public static void WriteCorruptHeic(string path)
    {
        var garbage = new byte[4096];
        Random.Shared.NextBytes(garbage);
        File.WriteAllBytes(path, garbage);
    }
}
