namespace Heic2Jpeg.Tests;

public sealed class ConversionTests : IDisposable
{
    private readonly TempDirectory _source = new();
    private readonly TempDirectory _destination = new();

    public void Dispose()
    {
        _source.Dispose();
        _destination.Dispose();
    }

    [Fact]
    public async Task Converts_heic_to_jpeg_with_the_same_dimensions()
    {
        Fixtures.CopyExampleTo(_source.File("IMG_0001.heic"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+1\b");
        result.StdOut.Should().MatchRegex(@"Failed\s+0\b");

        var jpeg = _destination.File("IMG_0001.jpg");
        File.Exists(jpeg).Should().BeTrue();

        var info = new MagickImageInfo(jpeg);
        info.Format.Should().Be(MagickFormat.Jpeg);
        info.Width.Should().Be(Fixtures.ExampleWidth);
        info.Height.Should().Be(Fixtures.ExampleHeight);
    }

    [Fact]
    public async Task Matches_heic_and_heif_extensions_regardless_of_case()
    {
        Fixtures.CopyExampleTo(_source.File("lower.heic"));
        Fixtures.CopyExampleTo(_source.File("UPPER.HEIC"));
        Fixtures.CopyExampleTo(_source.File("other.heif"));
        Fixtures.CopyExampleTo(_source.File("OTHER2.HEIF"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+4\b");
        _destination.Files().Should().BeEquivalentTo("lower.jpg", "OTHER2.jpg", "other.jpg", "UPPER.jpg");
    }

    [Fact]
    public async Task Ignores_files_that_are_not_heic()
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));
        File.WriteAllText(_source.File("notes.txt"), "not an image");
        File.WriteAllText(_source.File("README.md"), "# nothing to see");
        File.WriteAllBytes(_source.File("already.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Converted\s+1\b");
        _destination.Files().Should().BeEquivalentTo("photo.jpg");
    }

    [Fact]
    public async Task Handles_file_names_containing_markup_characters()
    {
        Fixtures.CopyExampleTo(_source.File("photo [3].heic"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.Output.Should().NotContainEquivalentOf("exception");
        File.Exists(_destination.File("photo [3].jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task Reports_corrupt_files_and_still_converts_the_rest()
    {
        Fixtures.CopyExampleTo(_source.File("good.heic"));
        Fixtures.WriteCorruptHeic(_source.File("corrupt.heic"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(1);
        result.StdOut.Should().MatchRegex(@"Converted\s+1\b");
        result.StdOut.Should().MatchRegex(@"Failed\s+1\b");
        result.StdOut.Should().Contain("corrupt.heic");
        _destination.Files().Should().BeEquivalentTo("good.jpg");
    }

    [Fact]
    public async Task Creates_the_destination_folder_when_it_does_not_exist()
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));
        var nested = _destination.File(Path.Combine("deeper", "still-deeper"));

        var result = await AppRunner.RunAsync(_source.FullPath, nested);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        File.Exists(Path.Combine(nested, "photo.jpg")).Should().BeTrue();
    }

    [Fact]
    public async Task Overwrites_an_existing_output_file()
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));
        File.WriteAllText(_destination.File("photo.jpg"), "stale content");

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        new MagickImageInfo(_destination.File("photo.jpg")).Format.Should().Be(MagickFormat.Jpeg);
    }

    [Fact]
    public async Task Uses_quality_90_by_default()
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex(@"Quality:\s+90\b");
        using var image = new MagickImage(_destination.File("photo.jpg"));
        image.Quality.Should().Be(90);
    }

    [Fact]
    public async Task Applies_the_requested_quality()
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));
        using var lowQualityDestination = new TempDirectory();

        var defaultRun = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);
        var lowQualityRun = await AppRunner.RunAsync(_source.FullPath, lowQualityDestination.FullPath, "--quality", "50");

        defaultRun.ExitCode.Should().Be(0, "app output: {0}", defaultRun.Output);
        lowQualityRun.ExitCode.Should().Be(0, "app output: {0}", lowQualityRun.Output);

        using var lowQualityImage = new MagickImage(lowQualityDestination.File("photo.jpg"));
        lowQualityImage.Quality.Should().Be(50);

        var defaultSize = new FileInfo(_destination.File("photo.jpg")).Length;
        var lowQualitySize = new FileInfo(lowQualityDestination.File("photo.jpg")).Length;
        lowQualitySize.Should().BeLessThan(defaultSize);
    }

    [Fact]
    public async Task Converts_many_files_in_one_run()
    {
        const int count = 12;
        for (var i = 0; i < count; i++)
        {
            Fixtures.CopyExampleTo(_source.File($"IMG_{i:D4}.heic"));
        }

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().MatchRegex($@"Converted\s+{count}\b");
        _destination.Files("*.jpg").Should().HaveCount(count);
    }
}
