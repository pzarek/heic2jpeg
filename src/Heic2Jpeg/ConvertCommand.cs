using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using ImageMagick;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Heic2Jpeg;

public sealed class ConvertSettings : CommandSettings
{
    [CommandArgument(0, "<SOURCE>")]
    [Description("Folder containing .heic/.heif files.")]
    public string Source { get; init; } = string.Empty;

    [CommandArgument(1, "<DESTINATION>")]
    [Description("Folder to write .jpg files to. Created if it does not exist.")]
    public string Destination { get; init; } = string.Empty;

    [CommandOption("-q|--quality <QUALITY>")]
    [Description("JPEG quality, 1-100.")]
    [DefaultValue(90)]
    public int Quality { get; init; }

    public override ValidationResult Validate()
    {
        if (!Directory.Exists(Source))
        {
            return ValidationResult.Error($"Source folder does not exist: {Source}");
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
    private static readonly string[] SearchPatterns = ["*.heic", "*.heif"];

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        ConvertSettings settings,
        CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(settings.Source);
        var destination = Path.GetFullPath(settings.Destination);

        var files = FindHeicFiles(source);
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]No .heic/.heif files found in[/] {source}");
            return 0;
        }

        Directory.CreateDirectory(destination);

        AnsiConsole.MarkupLineInterpolated($"[grey]Source:[/]      {source}");
        AnsiConsole.MarkupLineInterpolated($"[grey]Destination:[/] {destination}");
        AnsiConsole.MarkupLineInterpolated($"[grey]Quality:[/]     {settings.Quality}");
        AnsiConsole.WriteLine();

        var failures = new ConcurrentBag<(string File, string Error)>();
        var stopwatch = Stopwatch.StartNew();

        await AnsiConsole.Progress()
            .AutoClear(true)
            .HideCompleted(true)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var overall = ctx.AddTask($"[bold]Converting {files.Count} files[/]", maxValue: files.Count);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                };

                await Task.Run(() => Parallel.ForEach(files, options, file =>
                {
                    var task = ctx.AddTask(Markup.Escape(Path.GetFileName(file))).IsIndeterminate();
                    try
                    {
                        Convert(file, destination, settings.Quality);
                    }
                    catch (Exception ex)
                    {
                        failures.Add((file, ex.Message));
                    }
                    finally
                    {
                        task.Value = task.MaxValue;
                        task.StopTask();
                        overall.Increment(1);
                    }
                }), cancellationToken);
            });

        stopwatch.Stop();
        PrintSummary(files.Count, failures, destination, stopwatch.Elapsed);

        return failures.IsEmpty ? 0 : 1;
    }

    private static List<string> FindHeicFiles(string folder)
    {
        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
        };

        return SearchPatterns
            .SelectMany(pattern => Directory.EnumerateFiles(folder, pattern, options))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Convert(string sourceFile, string destinationFolder, int quality)
    {
        var targetFile = Path.Combine(
            destinationFolder,
            Path.GetFileNameWithoutExtension(sourceFile) + ".jpg");

        using var image = new MagickImage(sourceFile);
        image.AutoOrient();
        image.Quality = (uint)quality;
        image.Format = MagickFormat.Jpeg;
        image.Write(targetFile);
    }

    private static void PrintSummary(
        int total,
        ConcurrentBag<(string File, string Error)> failures,
        string destination,
        TimeSpan elapsed)
    {
        var converted = total - failures.Count;

        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[grey]Converted[/]", $"[green]{converted}[/]");
        grid.AddRow("[grey]Failed[/]", failures.IsEmpty ? "0" : $"[red]{failures.Count}[/]");
        grid.AddRow("[grey]Elapsed[/]", $"{elapsed.TotalSeconds:F1} s");
        grid.AddRow("[grey]Output[/]", Markup.Escape(destination));

        AnsiConsole.Write(new Panel(grid).Header("Summary").Border(BoxBorder.Rounded));

        if (failures.IsEmpty)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("[red]Failures[/]");
        table.AddColumn("File");
        table.AddColumn("Error");

        foreach (var (file, error) in failures.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(Path.GetFileName(file)), Markup.Escape(error));
        }

        AnsiConsole.Write(table);
    }
}
