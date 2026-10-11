using System.Reflection;
using Heic2Jpeg;
using Spectre.Console.Cli;

var app = new CommandApp<ConvertCommand>();

app.Configure(config =>
{
    config.SetApplicationName("heic2jpeg");
    config.SetApplicationVersion(GetVersion());
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg", "--quality", "85");
});

return app.Run(args);

static string GetVersion()
{
    var informational = typeof(ConvertCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    // Strip the "+<commit>" suffix that SourceLink appends.
    return informational?.Split('+')[0] ?? "0.0.0";
}
