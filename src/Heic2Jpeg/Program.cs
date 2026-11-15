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

var app = new CommandApp<ConvertCommand>()
    .WithDescription("Converts HEIC/HEIF photos to JPEG. Run it without arguments to choose the folders from a menu.");

app.Configure(config =>
{
    config.SetApplicationName("heic2jpeg");
    config.SetApplicationVersion(GetVersion());
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg", "--quality", "85");
    config.AddExample("~/Downloads/iCloud.zip", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg.zip");
});

return await app.RunAsync(args, cancellation.Token);

static string GetVersion()
{
    var informational = typeof(ConvertCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    // Strip the "+<commit>" suffix that SourceLink appends.
    return informational?.Split('+')[0] ?? "0.0.0";
}
