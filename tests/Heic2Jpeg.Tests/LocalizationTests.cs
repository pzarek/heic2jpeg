using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console.Testing;
using static Heic2Jpeg.Tests.InteractiveTests;

namespace Heic2Jpeg.Tests;

/// <summary>
/// The app speaks the language of CultureInfo.CurrentUICulture: Polish for "pl", English for anything else.
/// All other tests run in English (see xunit.runner.json and <see cref="AppRunner.RunAsync"/>).
/// </summary>
public sealed class LocalizationTests : IDisposable
{
    private const string WindowsIgnoresLocale = "Windows takes the language from its display language, not from LANG/LC_ALL.";

    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    [Fact]
    public void Every_text_has_a_Polish_translation_that_fills_in_the_same_values()
    {
        var english = Texts(CultureInfo.InvariantCulture);
        var polish = Texts(new CultureInfo("pl"));

        english.Keys.Except(polish.Keys).Should().BeEmpty("Strings.pl.resx must translate every text in Strings.resx");
        polish.Keys.Except(english.Keys).Should().BeEmpty("every text in Strings.pl.resx must also be in Strings.resx");
        foreach (var (name, text) in english)
        {
            Placeholders(polish[name]).Should().Equal(Placeholders(text), "the Polish {0} must use the same {{0}}, {{1}}, ...", name);
        }
    }

    [Theory]
    [InlineData(0, "0 plików HEIC")]
    [InlineData(1, "1 plik HEIC")]
    [InlineData(2, "2 pliki HEIC")]
    [InlineData(4, "4 pliki HEIC")]
    [InlineData(5, "5 plików HEIC")]
    [InlineData(12, "12 plików HEIC")]
    [InlineData(14, "14 plików HEIC")]
    [InlineData(21, "21 plików HEIC")]
    [InlineData(22, "22 pliki HEIC")]
    [InlineData(112, "112 plików HEIC")]
    [InlineData(124, "124 pliki HEIC")]
    public void Counts_photos_with_the_Polish_plural_forms(int count, string expected)
    {
        using var culture = TestCulture.Use("pl-PL");

        Display.Photos(count).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0 HEIC files")]
    [InlineData(1, "1 HEIC file")]
    [InlineData(2, "2 HEIC files")]
    [InlineData(22, "22 HEIC files")]
    public void Counts_photos_in_English(int count, string expected)
    {
        Display.Photos(count).Should().Be(expected);
    }

    [Fact]
    public async Task Menu_is_in_Polish_with_the_values_lined_up()
    {
        using var culture = TestCulture.Use("pl-PL");
        var console = Terminal();
        Press(console, ConsoleKey.UpArrow, ConsoleKey.Enter); // Zakończ

        var exitCode = await new InteractiveMenu(console, "1.2.3", _root.FullPath).RunAsync(CancellationToken.None);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Uruchom heic2jpeg --help, aby zobaczyć opcje wiersza poleceń");
        console.Output.Should().Contain("Źródło       nie wybrano");
        console.Output.Should().Contain("Cel          nie wybrano");
        console.Output.Should().Contain("Jakość JPEG  90");
        console.Output.Should().Contain("Konwertuj");
        console.Output.Should().Contain("Zakończ");
    }

    [Fact]
    public async Task Browser_is_in_Polish()
    {
        Fixtures.CopyExampleTo(_root.File("a.heic"));
        Fixtures.CopyExampleTo(_root.File("b.heic"));
        using var culture = TestCulture.Use("pl-PL");
        var console = Terminal();
        Press(console, ConsoleKey.Escape);

        await new PathBrowser(console).BrowseAsync(BrowseMode.Source, _root.FullPath, "", CancellationToken.None);

        console.Output.Should().Contain("Wybierz źródło");
        console.Output.Should().Contain("[Użyj tego folderu] (2 pliki HEIC)");
        console.Output.Should().Contain("[Wpisz ścieżkę]");
        console.Output.Should().Contain("[Wstecz]");
    }

    [Fact]
    public async Task Browser_explains_a_mistake_in_Polish()
    {
        using var culture = TestCulture.Use("pl-PL");
        var console = Terminal();
        Press(console, ConsoleKey.DownArrow, ConsoleKey.Enter); // [Nowy folder]
        console.Input.PushTextWithEnter("zdjecia.zip");
        console.Input.PushTextWithEnter("zdjecia");

        await new PathBrowser(console).BrowseAsync(BrowseMode.Destination, _root.FullPath, "zdjecia", CancellationToken.None);

        console.Output.Should().Contain("Nazwa folderu nie może kończyć się na .zip. Wybierz zamiast tego [Nowy plik ZIP].");
    }

    [Fact]
    public async Task Conversion_summary_is_in_Polish()
    {
        var source = _root.Folder("heic");
        Fixtures.CopyExampleTo(Path.Combine(source, "dobre.heic"));
        Fixtures.WriteCorruptHeic(Path.Combine(source, "zepsute.heic"));
        using var culture = TestCulture.Use("pl-PL");
        var console = new TestConsole().Width(120);

        var exitCode = await Converter.RunAsync(console, source, _root.File("jpeg"), 90, CancellationToken.None);

        exitCode.Should().Be(1, "one photo cannot be converted; output: {0}", console.Output);
        console.Output.Should().Contain("Źródło: ");
        console.Output.Should().Contain("Podsumowanie");
        console.Output.Should().Contain("Przekonwertowane");
        console.Output.Should().Contain("Zapisano w");
        console.Output.Should().MatchRegex(@"Czas\s+\d+,\d s", "Polish writes a decimal comma");
        console.Output.Should().Contain("Błędy");
        console.Output.Should().Contain("zepsute.heic");
    }

    [Fact]
    public async Task Help_follows_the_system_language()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsIgnoresLocale);

        var result = await AppRunner.RunWithLocaleAsync("pl_PL.UTF-8", "--help");

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().Contain("UŻYCIE:");
        result.StdOut.Should().Contain("heic2jpeg <SOURCE> <DESTINATION> [OPCJE]");
        result.StdOut.Should().Contain("ARGUMENTY:");
        result.StdOut.Should().Contain("Folder lub plik .zip z plikami .heic/.heif");
        result.StdOut.Should().Contain("DOMYŚLNIE");
        result.StdOut.Should().Contain("Wyświetla pomoc");
        result.StdOut.Should().NotContain("USAGE");
    }

    [Fact]
    public async Task Errors_follow_the_system_language()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsIgnoresLocale);

        var result = await AppRunner.RunWithLocaleAsync("pl_PL.UTF-8", _root.File("brak"), _root.File("jpeg"));

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("Błąd: Folder źródłowy nie istnieje");
    }

    [Fact]
    public async Task Other_languages_get_English_rather_than_a_mix()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsIgnoresLocale);

        // Spectre.Console.Cli ships German help headings; the app must not mix them into its English help.
        var result = await AppRunner.RunWithLocaleAsync("de_DE.UTF-8", "--help");

        result.ExitCode.Should().Be(0, "app output: {0}", result.Output);
        result.StdOut.Should().Contain("USAGE:");
        result.StdOut.Should().Contain("Prints help information");
        result.StdOut.Should().NotContain("VERWENDUNG");
    }

    private static Dictionary<string, string> Texts(CultureInfo culture) =>
        Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);

    private static List<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+[^}]*\}").Select(match => match.Value).Order().ToList();
}
