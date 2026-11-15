using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Heic2Jpeg;

public sealed class ConvertSettings : CommandSettings
{
    [CommandArgument(0, "<SOURCE>")]
    [LocalizedDescription(nameof(Strings.SourceArgumentDescription))]
    public string Source { get; init; } = string.Empty;

    [CommandArgument(1, "<DESTINATION>")]
    [LocalizedDescription(nameof(Strings.DestinationArgumentDescription))]
    public string Destination { get; init; } = string.Empty;

    [CommandOption("-q|--quality <QUALITY>")]
    [LocalizedDescription(nameof(Strings.QualityOptionDescription))]
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
            return ValidationResult.Error(Strings.QualityOutOfRange);
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
