namespace Heic2Jpeg;

/// <summary>
/// What SOURCE and DESTINATION may point at: a folder or a .zip file.
/// </summary>
public static class Locations
{
    public static bool HasZipExtension(string path) =>
        Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A destination is a ZIP file when its name ends in .zip, unless a folder with that name already exists.
    /// </summary>
    public static bool IsZipDestination(string path) => !Directory.Exists(path) && HasZipExtension(path);

    /// <summary>Returns an error message, or null when the source can be read from.</summary>
    public static string? ValidateSource(string path)
    {
        if (Directory.Exists(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            return HasZipExtension(path) ? null : string.Format(Strings.SourceNotFolderOrZip, path);
        }

        return HasZipExtension(path)
            ? string.Format(Strings.SourceZipNotFound, path)
            : string.Format(Strings.SourceFolderNotFound, path);
    }

    /// <summary>Returns an error message, or null when the destination can be written to.</summary>
    public static string? ValidateDestination(string path) =>
        File.Exists(path) && !HasZipExtension(path)
            ? string.Format(Strings.DestinationNotFolderOrZip, path)
            : null;
}
