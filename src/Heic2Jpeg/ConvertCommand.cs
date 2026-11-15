using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Heic2Jpeg;

public sealed class ConvertSettings : CommandSettings
{
    [CommandArgument(0, "<SOURCE>")]
    [Description("Folder or .zip file containing .heic/.heif files.")]
    public string Source { get; init; } = string.Empty;

    [CommandArgument(1, "<DESTINATION>")]
    [Description("Folder or .zip file to write .jpg files to. Created if it does not exist.")]
    public string Destination { get; init; } = string.Empty;

    [CommandOption("-q|--quality <QUALITY>")]
    [Description("JPEG quality, 1-100.")]
    [DefaultValue(90)]
    public int Quality { get; init; }

    public override ValidationResult Validate()
    {
        var error = Locations.ValidateSource(Source) ?? Locations.ValidateDestination(Destination);
        if (error is not null)
        {
            return ValidationResult.Error(error);
        }

        if (Quality is < 1 or > 100)
        {
            return ValidationResult.Error("Quality must be between 1 and 100.");
        }

        return ValidationResult.Success();
    }
}

public sealed class ConvertCommand : AsyncCommand<ConvertSettings>
{
    public override Task<int> ExecuteAsync(
        CommandContext context,
        ConvertSettings settings,
        CancellationToken cancellationToken) =>
        Converter.RunAsync(AnsiConsole.Console, settings.Source, settings.Destination, settings.Quality, cancellationToken);
}
