using Heic2Jpeg;
using Spectre.Console.Cli;

var app = new CommandApp<ConvertCommand>();

app.Configure(config =>
{
    config.SetApplicationName("heic2jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg");
    config.AddExample("~/Pictures/heic", "~/Pictures/jpeg", "--quality", "85");
});

return app.Run(args);
