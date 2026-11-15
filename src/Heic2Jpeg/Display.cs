namespace Heic2Jpeg;

/// <summary>Formatting helpers for the interactive screens.</summary>
internal static class Display
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Shortens the home folder to "~".</summary>
    public static string Path(string path)
    {
        if (Home.Length == 0)
        {
            return path;
        }

        if (string.Equals(path, Home, PathComparison))
        {
            return "~";
        }

        var homePrefix = System.IO.Path.EndsInDirectorySeparator(Home) ? Home : Home + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(homePrefix, PathComparison) ? "~" + path[(homePrefix.Length - 1)..] : path;
    }

    /// <summary>Expands a leading "~" to the home folder.</summary>
    public static string ExpandHome(string path)
    {
        if (path == "~")
        {
            return Home;
        }

        return path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal)
            ? System.IO.Path.Join(Home, path[2..])
            : path;
    }

    public static bool SamePath(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, PathComparison);

    public static string Photos(int count) => count == 1 ? "1 HEIC file" : $"{count} HEIC files";

    public static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }

    /// <summary>Cuts the end off text that is too long, so a list item never wraps onto a second line.</summary>
    public static string FitEnd(string text, int width) =>
        text.Length <= width ? text : text[..Math.Max(0, width - 3)] + "...";

    /// <summary>Cuts the start off a path that is too long, keeping the folder names closest to the end.</summary>
    public static string FitStart(string text, int width) =>
        text.Length <= width ? text : "..." + text[^Math.Max(0, width - 3)..];
}
