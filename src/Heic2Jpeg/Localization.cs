using System.ComponentModel;
using System.Globalization;

namespace Heic2Jpeg;

/// <summary>
/// A [Description] for Spectre.Console.Cli whose text is looked up in <see cref="Strings"/>, so --help
/// describes the arguments in the user's language. Attributes only take constants, hence the lookup by name.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class LocalizedDescriptionAttribute(string name) : DescriptionAttribute
{
    public override string Description => Strings.ResourceManager.GetString(name, Strings.Culture) ?? name;
}

internal static class Plural
{
    /// <summary>Fills a count into the form of the text that the user's language uses for that number.</summary>
    /// <param name="one">For exactly 1.</param>
    /// <param name="few">Polish only: for 2-4, 22-24, 32-34, ... but not 12-14. English passes the same text as <paramref name="many"/>.</param>
    /// <param name="many">For every other number, including 0.</param>
    public static string Format(int count, string one, string few, string many)
    {
        var form = count == 1 ? one
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl" && IsPolishFew(count) ? few
            : many;

        return string.Format(CultureInfo.CurrentCulture, form, count);
    }

    private static bool IsPolishFew(int count) => count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14;
}
