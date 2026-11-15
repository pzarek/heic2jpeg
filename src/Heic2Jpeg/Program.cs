using System.Reflection;
using Heic2Jpeg;
using Spectre.Console;
using Spectre.Console.Cli;

// The first Ctrl+C stops converting cleanly (a half-written ZIP file is removed); a second one exits at once.
var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    if (!cancellation.IsCancellationRequested)
    {
        e.Cancel = true;
        cancellation.Cancel();
    }
};

if (args.Length == 0 && InteractiveMenu.IsSupported(AnsiConsole.Console))
{
    return await new InteractiveMenu(AnsiConsole.Console, GetVersion()).RunAsync(cancellation.Token);
}

// Every text is in Strings.resx / Strings.pl.resx, in the language of CultureInfo.CurrentUICulture.
var app = new CommandApp<ConvertCommand>()
    .WithDescription(Strings.AppDescription);

app.Configure(config =>
{
    config.SetApplicationName("heic2jpeg");
    config.SetApplicationVersion(GetVersion());
    config.SetHelpProvider(new LocalizedHelpProvider());
    config.SetExceptionHandler(WriteError);
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg", "--quality", "85");
    config.AddExample("~/Downloads/iCloud.zip", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg.zip");
});

return await app.RunAsync(args, cancellation.Token);

// What Spectre.Console.Cli prints by default, but with "Error:" in the user's language.
static int WriteError(Exception ex, ITypeResolver? resolver)
{
    if (ex is CommandAppException { Pretty: { } pretty })
    {
        // A parse error that points at the bad argument. Spectre writes it, in English.
        AnsiConsole.Write(pretty);
    }
    else
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(Strings.ErrorPrefix)}[/] {Markup.Escape(ex.Message)}");
    }

    return -1;
}

static string GetVersion()
{
    var informational = typeof(ConvertCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    // Strip the "+<commit>" suffix that SourceLink appends.
    return informational?.Split('+')[0] ?? "0.0.0";
}
