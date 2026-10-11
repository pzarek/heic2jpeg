namespace Heic2Jpeg.Tests;

public sealed class CliTests : IDisposable
{
    private readonly TempDirectory _source = new();
    private readonly TempDirectory _destination = new();

    public void Dispose()
    {
        _source.Dispose();
        _destination.Dispose();
    }

    [Fact]
    public async Task Shows_help()
    {
        var result = await AppRunner.RunAsync("--help");

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().Contain("USAGE");
        result.StdOut.Should().Contain("<SOURCE>");
        result.StdOut.Should().Contain("<DESTINATION>");
        result.StdOut.Should().Contain("--quality");
    }

    [Fact]
    public async Task Fails_when_the_source_folder_does_not_exist()
    {
        var missing = _source.File("does-not-exist");

        var result = await AppRunner.RunAsync(missing, _destination.FullPath);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Source folder does not exist");
        Directory.Exists(_destination.FullPath).Should().BeTrue("the destination must not be touched");
        _destination.Files().Should().BeEmpty();
    }

    [Fact]
    public async Task Fails_when_the_destination_argument_is_missing()
    {
        var result = await AppRunner.RunAsync(_source.FullPath);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("DESTINATION");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-5")]
    public async Task Rejects_quality_outside_1_to_100(string quality)
    {
        Fixtures.CopyExampleTo(_source.File("photo.heic"));

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath, "--quality", quality);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Quality must be between 1 and 100");
        _destination.Files().Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_a_non_numeric_quality()
    {
        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath, "--quality", "high");

        result.ExitCode.Should().NotBe(0);
        _destination.Files().Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_when_the_source_folder_has_no_heic_files()
    {
        File.WriteAllText(_source.File("notes.txt"), "nothing here");

        var result = await AppRunner.RunAsync(_source.FullPath, _destination.FullPath);

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().Contain("No .heic/.heif files found");
        _destination.Files().Should().BeEmpty();
    }
}
