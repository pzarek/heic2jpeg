using System.Collections.Concurrent;
using System.Diagnostics;
using ImageMagick;
using Spectre.Console;

namespace Heic2Jpeg;

/// <summary>
/// Converts every photo in a source (folder or ZIP) to JPEG in a destination (folder or ZIP),
/// showing progress and a summary on the given console. Used by both the command line and the menu.
/// </summary>
public static class Converter
{
    public const int CancelledExitCode = 130;

    public static async Task<int> RunAsync(
        IAnsiConsole console,
        string sourcePath,
        string destinationPath,
        int quality,
        CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        IPhotoSource photoSource;
        try
        {
            photoSource = PhotoSource.Open(source);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            console.MarkupLineInterpolated($"[red]{Strings.ErrorPrefix}[/] {string.Format(Strings.CannotRead, source)}: {ex.Message}");
            return 1;
        }

        using (photoSource)
        {
            var photos = photoSource.Photos;
            if (photos.Count == 0)
            {
                console.MarkupLineInterpolated($"[yellow]{Strings.NoHeicFilesFoundIn}[/] {source}");
                return 0;
            }

            IPhotoDestination photoDestination;
            try
            {
                photoDestination = PhotoDestination.Open(destination);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                console.MarkupLineInterpolated($"[red]{Strings.ErrorPrefix}[/] {string.Format(Strings.CannotWriteTo, destination)}: {ex.Message}");
                return 1;
            }

            using (photoDestination)
            {
                return await ConvertAllAsync(
                    console, photoSource, photoDestination, source, destination, quality, cancellationToken);
            }
        }
    }

    private static async Task<int> ConvertAllAsync(
        IAnsiConsole console,
        IPhotoSource source,
        IPhotoDestination destination,
        string sourcePath,
        string destinationPath,
        int quality,
        CancellationToken cancellationToken)
    {
        var photos = source.Photos;
        var toZip = destination is ZipDestination;

        // Line the values up one space after the longest label.
        var labelWidth = new[] { Strings.SourceLabel, Strings.DestinationLabel, Strings.QualityLabel }.Max(label => label.Length) + 1;
        console.MarkupLineInterpolated($"[grey]{Strings.SourceLabel.PadRight(labelWidth)}[/]{sourcePath}");
        console.MarkupLineInterpolated($"[grey]{Strings.DestinationLabel.PadRight(labelWidth)}[/]{destinationPath}");
        console.MarkupLineInterpolated($"[grey]{Strings.QualityLabel.PadRight(labelWidth)}[/]{quality}");
        console.WriteLine();

        var failures = new ConcurrentBag<(string File, string Error)>();
        var converted = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await console.Progress()
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
                    var title = Markup.Escape(string.Format(Strings.ConvertingFiles, photos.Count));
                    var overall = ctx.AddTask($"[bold]{title}[/]", maxValue: photos.Count);

                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellationToken,
                    };

                    await Task.Run(() => Parallel.ForEach(photos, options, photo =>
                    {
                        var task = ctx.AddTask(Markup.Escape(photo.RelativePath)).IsIndeterminate();
                        try
                        {
                            Convert(source, destination, photo, quality);
                            Interlocked.Increment(ref converted);
                        }
                        catch (Exception ex)
                        {
                            failures.Add((photo.RelativePath, ex.Message));
                        }
                        finally
                        {
                            task.Value = task.MaxValue;
                            task.StopTask();
                            overall.Increment(1);
                        }
                    }), cancellationToken);
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var outcome = toZip
                ? string.Format(Strings.NothingWrittenTo, destinationPath)
                : string.Format(Strings.ConvertedOf, converted, photos.Count);
            console.MarkupLineInterpolated($"[yellow]{Strings.Cancelled}[/] {outcome}");
            return CancelledExitCode;
        }

        // A ZIP destination may be the source itself, which has to be closed before it is replaced.
        source.Dispose();
        try
        {
            destination.Commit();
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            console.MarkupLineInterpolated($"[red]{Strings.ErrorPrefix}[/] {string.Format(Strings.CannotWrite, destinationPath)}: {ex.Message}");
            return 1;
        }

        stopwatch.Stop();
        PrintSummary(console, photos.Count, failures, destinationPath, stopwatch.Elapsed);

        return failures.IsEmpty ? 0 : 1;
    }

    private static void Convert(IPhotoSource source, IPhotoDestination destination, SourcePhoto photo, int quality)
    {
        using var image = source.Read(photo);
        image.AutoOrient();
        image.Quality = (uint)quality;
        image.Format = MagickFormat.Jpeg;
        destination.Write(photo.JpegPath, image);
    }

    private static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException;

    private static void PrintSummary(
        IAnsiConsole console,
        int total,
        ConcurrentBag<(string File, string Error)> failures,
        string destination,
        TimeSpan elapsed)
    {
        var converted = total - failures.Count;

        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow(Label(Strings.SummaryConverted), $"[green]{converted}[/]");
        grid.AddRow(Label(Strings.SummaryFailed), failures.IsEmpty ? "0" : $"[red]{failures.Count}[/]");
        grid.AddRow(Label(Strings.SummaryElapsed), $"{elapsed.TotalSeconds:F1} s");
        grid.AddRow(Label(Strings.SummaryOutput), Markup.Escape(destination));

        console.Write(new Panel(grid).Header(Markup.Escape(Strings.Summary)).Border(BoxBorder.Rounded));

        if (failures.IsEmpty)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title($"[red]{Markup.Escape(Strings.Failures)}[/]");
        table.AddColumn(Markup.Escape(Strings.FailuresFile));
        table.AddColumn(Markup.Escape(Strings.FailuresError));

        foreach (var (file, error) in failures.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(file), Markup.Escape(error));
        }

        console.Write(table);

        static string Label(string text) => $"[grey]{Markup.Escape(text)}[/]";
    }
}
