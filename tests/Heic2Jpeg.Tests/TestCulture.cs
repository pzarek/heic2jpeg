using System.Globalization;

namespace Heic2Jpeg.Tests;

/// <summary>
/// Switches the language and number formats to a culture until disposed, as if the system were set to it.
/// The culture flows into awaited and parallel code, and does not leak into other tests.
/// </summary>
public sealed class TestCulture : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUI = CultureInfo.CurrentUICulture;

    private TestCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static TestCulture Use(string name) => new(new CultureInfo(name));

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previous;
        CultureInfo.CurrentUICulture = _previousUI;
    }
}
