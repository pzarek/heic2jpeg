namespace Heic2Jpeg.Tests;

public sealed class ZipTests : IDisposable
{
    private readonly TempDirectory _work = new();
    private readonly TempDirectory _destination = new();

    public void Dispose()
    {
        _work.Dispose();
        _destination.Dispose();
    }

    [Fact]
    public async Task Converts_photos_from_a_zip_file_keeping_its_folders()
    {
        var zip = _work.File("iCloud Photos.zip");
        Fixtures.CreateZip(zip,
            ("IMG_0001.HEIC", Fixtures.Example),
            ("Album/IMG_0002.heic", Fixtures.Example),
            ("Album/Deeper/IMG_0003.heif", Fixtures.Example),
            ("notes.txt", "not a photo"));

        var result = await AppRunner.RunAsync(zip, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+3\b");
        _destination.AllFiles().Should().Equal("Album/Deeper/IMG_0003.jpg", "Album/IMG_0002.jpg", "IMG_0001.jpg");

        var info = new MagickImageInfo(_destination.File("IMG_0001.jpg"));
        info.Format.Should().Be(MagickFormat.Jpeg);
        info.Width.Should().Be(Fixtures.ExampleWidth);
        info.Height.Should().Be(Fixtures.ExampleHeight);
    }

    [Fact]
    public async Task Skips_macOS_metadata_inside_a_zip_file()
    {
        var zip = _work.File("photos.zip");
        Fixtures.CreateZip(zip,
            ("photos/IMG_0001.HEIC", Fixtures.Example),
            ("__MACOSX/photos/._IMG_0001.HEIC", Fixtures.CorruptBytes()),
            ("photos/._IMG_0002.HEIC", Fixtures.CorruptBytes()));

        var result = await AppRunner.RunAsync(zip, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+1\b");
        result.StdOut.Should().MatchRegex(@"Failed\s+0\b");
        _destination.AllFiles().Should().Equal("photos/IMG_0001.jpg");
    }

    [Fact]
    public async Task Writes_jpegs_into_a_new_zip_file()
    {
        var source = _work.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "a.heic"));
        Fixtures.CopyExampleTo(Path.Combine(source, "b.HEIC"));
        var zip = _destination.File(Path.Combine("nested", "out.zip"));

        var result = await AppRunner.RunAsync(source, zip);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+2\b");
        Fixtures.ZipEntryNames(zip).Should().Equal("a.jpg", "b.jpg");

        var info = new MagickImageInfo(Fixtures.ReadZipEntry(zip, "a.jpg"));
        info.Format.Should().Be(MagickFormat.Jpeg);
        info.Width.Should().Be(Fixtures.ExampleWidth);
        _destination.AllFiles().Should().Equal(["nested/out.zip"], "no temporary file may be left behind");
    }

    [Fact]
    public async Task Converts_from_one_zip_file_into_another()
    {
        var source = _work.File("in.zip");
        Fixtures.CreateZip(source, ("Album/IMG_0001.heic", Fixtures.Example));
        var destination = _destination.File("out.zip");

        var result = await AppRunner.RunAsync(source, destination);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        Fixtures.ZipEntryNames(destination).Should().Equal("Album/IMG_0001.jpg");
    }

    [Fact]
    public async Task Adds_to_an_existing_zip_file_and_replaces_entries_with_the_same_name()
    {
        var source = _work.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "photo.heic"));
        var zip = _destination.File("out.zip");
        Fixtures.CreateZip(zip, ("readme.txt", "keep me"), ("photo.jpg", "stale content"));

        var result = await AppRunner.RunAsync(source, zip);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        Fixtures.ZipEntryNames(zip).Should().Equal("photo.jpg", "readme.txt");
        System.Text.Encoding.UTF8.GetString(Fixtures.ReadZipEntry(zip, "readme.txt")).Should().Be("keep me");
        new MagickImageInfo(Fixtures.ReadZipEntry(zip, "photo.jpg")).Format.Should().Be(MagickFormat.Jpeg);
    }

    [Fact]
    public async Task Can_add_the_jpegs_to_the_source_zip_file_itself()
    {
        var zip = _work.File("photos.zip");
        Fixtures.CreateZip(zip, ("IMG_0001.heic", Fixtures.Example));

        var result = await AppRunner.RunAsync(zip, zip);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        Fixtures.ZipEntryNames(zip).Should().Equal("IMG_0001.heic", "IMG_0001.jpg");
        Fixtures.ReadZipEntry(zip, "IMG_0001.heic").Should().Equal(File.ReadAllBytes(Fixtures.ExampleHeic));
    }

    [Fact]
    public async Task Reports_a_corrupt_photo_inside_a_zip_file_and_still_converts_the_rest()
    {
        var zip = _work.File("photos.zip");
        Fixtures.CreateZip(zip, ("good.heic", Fixtures.Example), ("Album/corrupt.heic", Fixtures.CorruptBytes()));

        var result = await AppRunner.RunAsync(zip, _destination.FullPath);

        result.ExitCode.Should().Be(1);
        result.StdOut.Should().MatchRegex(@"Converted\s+1\b");
        result.StdOut.Should().MatchRegex(@"Failed\s+1\b");
        result.StdOut.Should().Contain("Album/corrupt.heic");
        _destination.AllFiles().Should().Equal("good.jpg");
    }

    [Fact]
    public async Task Refuses_zip_entries_that_would_be_written_outside_the_destination()
    {
        var zip = _work.File("evil.zip");
        Fixtures.CreateZip(zip, ("../escaped.heic", Fixtures.Example), ("fine.heic", Fixtures.Example));
        var destination = _work.File("out");

        var result = await AppRunner.RunAsync(zip, destination);

        result.ExitCode.Should().Be(1);
        result.StdOut.Should().MatchRegex(@"Failed\s+1\b");
        result.StdOut.Should().Contain("Refusing");
        _work.AllFiles().Should().Equal("evil.zip", "out/fine.jpg");
    }

    [Fact]
    public async Task Fails_when_the_source_zip_file_is_corrupt()
    {
        var zip = _work.File("broken.zip");
        File.WriteAllBytes(zip, Fixtures.CorruptBytes());

        var result = await AppRunner.RunAsync(zip, _destination.FullPath);

        result.ExitCode.Should().Be(1);
        result.Output.Should().Contain("Cannot read");
        _destination.AllFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task Fails_when_the_source_zip_file_does_not_exist()
    {
        var result = await AppRunner.RunAsync(_work.File("missing.zip"), _destination.FullPath);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Source ZIP file does not exist");
    }

    [Fact]
    public async Task Fails_when_the_source_is_a_file_but_not_a_zip_file()
    {
        var file = _work.File("photo.heic");
        Fixtures.CopyExampleTo(file);

        var result = await AppRunner.RunAsync(file, _destination.FullPath);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Source must be a folder or a .zip file");
        _destination.AllFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task Fails_when_the_destination_is_a_file_but_not_a_zip_file()
    {
        var source = _work.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "photo.heic"));
        var file = _destination.File("notes.txt");
        File.WriteAllText(file, "keep me");

        var result = await AppRunner.RunAsync(source, file);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Destination must be a folder or a .zip file");
        File.ReadAllText(file).Should().Be("keep me");
    }

    [Fact]
    public async Task Leaves_a_corrupt_destination_zip_file_alone()
    {
        var source = _work.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "photo.heic"));
        var zip = _destination.File("out.zip");
        var garbage = Fixtures.CorruptBytes();
        File.WriteAllBytes(zip, garbage);

        var result = await AppRunner.RunAsync(source, zip);

        result.ExitCode.Should().Be(1);
        result.Output.Should().Contain("Cannot write to");
        File.ReadAllBytes(zip).Should().Equal(garbage);
        _destination.AllFiles().Should().Equal("out.zip");
    }

    [Fact]
    public async Task Treats_an_existing_folder_named_like_a_zip_file_as_a_folder()
    {
        var source = _work.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "photo.heic"));
        var folder = _destination.Folder("looks-like.zip");

        var result = await AppRunner.RunAsync(source, folder);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        File.Exists(Path.Combine(folder, "photo.jpg")).Should().BeTrue();
    }
}
