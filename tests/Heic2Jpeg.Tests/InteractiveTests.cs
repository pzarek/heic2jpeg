using Spectre.Console.Testing;

namespace Heic2Jpeg.Tests;

/// <summary>
/// Drives the folder browser and the menu in-process with a fake terminal and scripted key presses.
/// </summary>
public sealed class InteractiveTests : IDisposable
{
    private static readonly char Separator = Path.DirectorySeparatorChar;

    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    private static TestConsole Terminal()
    {
        var console = new TestConsole().Interactive().Width(120).Height(40);
        console.Profile.Capabilities.Ansi = true;
        return console;
    }

    private static void Press(TestConsole console, params ConsoleKey[] keys)
    {
        foreach (var key in keys)
        {
            console.Input.PushKey(key);
        }
    }

    private Task<string?> Browse(TestConsole console, BrowseMode mode, string? start = null, string newName = "photos") =>
        new PathBrowser(console).BrowseAsync(mode, start ?? _root.FullPath, newName, CancellationToken.None);

    // Source list:      [Use this folder], .., folders..., zip files..., [Type a path], [Back]
    // Destination list: [Use this folder], [New folder], [New ZIP file], .., folders..., zip files..., [Type a path], [Back]

    [Fact]
    public async Task Browser_lists_folders_and_zip_files_but_not_other_files()
    {
        _root.Folder("Album");
        Fixtures.CreateZip(_root.File("photos.zip"), ("IMG_0001.heic", Fixtures.Example));
        File.WriteAllText(_root.File("notes.txt"), "not a photo");
        Fixtures.CopyExampleTo(_root.File("loose.heic"));
        var console = Terminal();
        Press(console, ConsoleKey.Escape);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().BeNull("Esc goes back without choosing anything");
        console.Output.Should().Contain("[Use this folder] (1 HEIC file)");
        console.Output.Should().Contain($"Album{Separator}");
        console.Output.Should().Contain("photos.zip");
        console.Output.Should().NotContain("notes.txt");
        console.Output.Should().NotContain("loose.heic");
    }

    [Fact]
    public async Task Browser_enters_a_folder_and_uses_it_as_the_source()
    {
        var album = _root.Folder("Album");
        Fixtures.CopyExampleTo(Path.Combine(album, "IMG_0001.heic"));
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().Be(album);
    }

    [Fact]
    public async Task Browser_picks_a_zip_file_as_the_source()
    {
        _root.Folder("Album");
        var zip = _root.File("photos.zip");
        Fixtures.CreateZip(zip, ("IMG_0001.heic", Fixtures.Example));
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().Be(zip);
    }

    [Fact]
    public async Task Browser_goes_up_and_highlights_the_folder_it_came_from()
    {
        Fixtures.CopyExampleTo(_root.File("loose.heic"));
        _root.Folder("A");
        var album = _root.Folder("B");
        _root.Folder("C");
        Fixtures.CopyExampleTo(Path.Combine(album, "IMG_0001.heic"));
        var console = Terminal();
        // Up to the parent, then Enter on the highlighted entry, which should be B again.
        Press(console, ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Enter, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Source, start: album);

        picked.Should().Be(album);
    }

    [Fact]
    public async Task Browser_will_not_use_a_source_folder_without_photos()
    {
        var console = Terminal();
        Press(console, ConsoleKey.Enter, ConsoleKey.Escape);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().BeNull();
        console.Output.Should().Contain("There are no .heic/.heif files in this folder");
    }

    [Fact]
    public async Task Browser_will_not_use_a_zip_file_without_photos_as_the_source()
    {
        Fixtures.CreateZip(_root.File("documents.zip"), ("notes.txt", "not a photo"));
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Escape);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().BeNull();
        console.Output.Should().Contain("There are no .heic/.heif files in documents.zip");
    }

    [Fact]
    public async Task Browser_reports_a_corrupt_zip_file()
    {
        File.WriteAllBytes(_root.File("broken.zip"), Fixtures.CorruptBytes());
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Escape);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().BeNull();
        console.Output.Should().Contain("Cannot read broken.zip");
    }

    [Fact]
    public async Task Browser_creates_a_new_zip_file_as_the_destination()
    {
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter("out");

        var picked = await Browse(console, BrowseMode.Destination);

        picked.Should().Be(_root.File("out.zip"));
        File.Exists(picked).Should().BeFalse("the ZIP file is only written when converting");
    }

    [Fact]
    public async Task Browser_suggests_a_name_for_a_new_destination_folder()
    {
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Destination, newName: "heic-jpeg");

        picked.Should().Be(_root.File("heic-jpeg"));
    }

    [Fact]
    public async Task Browser_does_not_allow_a_new_folder_named_like_a_zip_file()
    {
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter("photos.zip");
        console.Input.PushTextWithEnter("photos");

        var picked = await Browse(console, BrowseMode.Destination);

        picked.Should().Be(_root.File("photos"));
        console.Output.Should().Contain("cannot end in .zip");
    }

    [Fact]
    public async Task Browser_picks_an_existing_zip_file_as_the_destination()
    {
        var zip = _root.File("existing.zip");
        Fixtures.CreateZip(zip, ("readme.txt", "hello"));
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Destination);

        picked.Should().Be(zip);
    }

    [Fact]
    public async Task Browser_goes_to_a_typed_path()
    {
        var album = _root.Folder("Album");
        Fixtures.CopyExampleTo(Path.Combine(album, "IMG_0001.heic"));
        var console = Terminal();
        // Up wraps around to [Back]; once more is [Type a path].
        Press(console, ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter("\"Album\"");
        Press(console, ConsoleKey.Enter);

        var picked = await Browse(console, BrowseMode.Source);

        picked.Should().Be(album);
    }

    [Fact]
    public async Task Browser_accepts_a_typed_path_that_does_not_exist_yet_as_the_destination()
    {
        var console = Terminal();
        Press(console, ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.Enter);
        console.Input.PushTextWithEnter(Path.Combine("new", "place"));

        var picked = await Browse(console, BrowseMode.Destination);

        picked.Should().Be(Path.Combine(_root.FullPath, "new", "place"));
    }

    // Menu: Source, Destination, JPEG quality, Convert, Exit.

    [Fact]
    public async Task Menu_converts_a_zip_file_into_a_new_folder()
    {
        Fixtures.CreateZip(_root.File("photos.zip"), ("Album/IMG_0001.heic", Fixtures.Example));
        var console = Terminal();
        Press(console,
            ConsoleKey.Enter,                                                  // Source (highlighted first)
            ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter,      // photos.zip
            ConsoleKey.Enter,                                                  // Destination (highlighted next)
            ConsoleKey.DownArrow, ConsoleKey.Enter,                            // [New folder]
            ConsoleKey.Enter,                                                  // accept "photos-jpeg"
            ConsoleKey.Enter,                                                  // Convert (highlighted last)
            ConsoleKey.DownArrow, ConsoleKey.Enter);                           // Exit

        var exitCode = await new InteractiveMenu(console, "1.2.3", _root.FullPath).RunAsync(CancellationToken.None);

        exitCode.Should().Be(0, "output: {0}", console.Output);
        _root.AllFiles().Should().Equal("photos-jpeg/Album/IMG_0001.jpg", "photos.zip");
        console.Output.Should().MatchRegex(@"Converted\s+1\b");
    }

    [Fact]
    public async Task Menu_converts_a_folder_into_a_new_zip_file_with_the_chosen_quality()
    {
        var heic = _root.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(heic, "photo.heic"));
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);  // JPEG quality
        console.Input.PushTextWithEnter("70");
        Press(console,
            ConsoleKey.Enter,                                                  // Source
            ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter,      // heic/
            ConsoleKey.Enter,                                                  // [Use this folder]
            ConsoleKey.Enter,                                                  // Destination, starts next to heic/
            ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter,      // [New ZIP file]
            ConsoleKey.Enter,                                                  // accept "heic-jpeg.zip"
            ConsoleKey.Enter,                                                  // Convert
            ConsoleKey.DownArrow, ConsoleKey.Enter);                           // Exit

        var exitCode = await new InteractiveMenu(console, "1.2.3", _root.FullPath).RunAsync(CancellationToken.None);

        exitCode.Should().Be(0, "output: {0}", console.Output);
        console.Output.Should().Contain("JPEG quality  70");
        var zip = _root.File("heic-jpeg.zip");
        Fixtures.ZipEntryNames(zip).Should().Equal("photo.jpg");
        using var image = new MagickImage(Fixtures.ReadZipEntry(zip, "photo.jpg"));
        image.Quality.Should().Be(70);
    }

    [Fact]
    public async Task Menu_convert_asks_for_whatever_has_not_been_chosen_yet()
    {
        var heic = _root.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(heic, "photo.heic"));
        var console = Terminal();
        Press(console,
            ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.Enter,          // Convert, without a source or destination
            ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter,      // heic/
            ConsoleKey.Enter,                                                  // [Use this folder]
            ConsoleKey.DownArrow, ConsoleKey.Enter,                            // [New folder]
            ConsoleKey.Enter,                                                  // accept "heic-jpeg"
            ConsoleKey.Enter,                                                  // Back to the menu (after converting)
            ConsoleKey.DownArrow, ConsoleKey.Enter);                           // Exit, just below the highlighted Convert

        var exitCode = await new InteractiveMenu(console, "1.2.3", _root.FullPath).RunAsync(CancellationToken.None);

        exitCode.Should().Be(0, "output: {0}", console.Output);
        File.Exists(Path.Combine(_root.FullPath, "heic-jpeg", "photo.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task Menu_returns_from_the_browser_with_escape()
    {
        var console = Terminal();
        Press(console, ConsoleKey.Enter, ConsoleKey.Escape, ConsoleKey.UpArrow, ConsoleKey.Enter);

        var exitCode = await new InteractiveMenu(console, "1.2.3", _root.FullPath).RunAsync(CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Source        not chosen");
    }
}
