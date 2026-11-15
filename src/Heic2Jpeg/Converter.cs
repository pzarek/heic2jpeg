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
            console.MarkupLineInterpolated($"[red]Error:[/] Cannot read {source}: {ex.Message}");
            return 1;
        }

        using (photoSource)
        {
            var photos = photoSource.Photos;
            if (photos.Count == 0)
            {
                console.MarkupLineInterpolated($"[yellow]No .heic/.heif files found in[/] {source}");
                return 0;
            }

            IPhotoDestination photoDestination;
            try
            {
                photoDestination = PhotoDestination.Open(destination);
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                console.MarkupLineInterpolated($"[red]Error:[/] Cannot write to {destination}: {ex.Message}");
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

        console.MarkupLineInterpolated($"[grey]Source:[/]      {sourcePath}");
        console.MarkupLineInterpolated($"[grey]Destination:[/] {destinationPath}");
        console.MarkupLineInterpolated($"[grey]Quality:[/]     {quality}");
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
                    var overall = ctx.AddTask($"[bold]Converting {photos.Count} files[/]", maxValue: photos.Count);

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
            console.MarkupLine(toZip
                ? $"[yellow]Cancelled.[/] Nothing was written to {Markup.Escape(destinationPath)}."
                : $"[yellow]Cancelled[/] after converting {converted} of {photos.Count} files.");
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
            console.MarkupLineInterpolated($"[red]Error:[/] Cannot write {destinationPath}: {ex.Message}");
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
        grid.AddRow("[grey]Converted[/]", $"[green]{converted}[/]");
        grid.AddRow("[grey]Failed[/]", failures.IsEmpty ? "0" : $"[red]{failures.Count}[/]");
        grid.AddRow("[grey]Elapsed[/]", $"{elapsed.TotalSeconds:F1} s");
        grid.AddRow("[grey]Output[/]", Markup.Escape(destination));

        console.Write(new Panel(grid).Header("Summary").Border(BoxBorder.Rounded));

        if (failures.IsEmpty)
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded).Title("[red]Failures[/]");
        table.AddColumn("File");
        table.AddColumn("Error");

        foreach (var (file, error) in failures.OrderBy(f => f.File, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(Markup.Escape(file), Markup.Escape(error));
        }

        console.Write(table);
    }
}
