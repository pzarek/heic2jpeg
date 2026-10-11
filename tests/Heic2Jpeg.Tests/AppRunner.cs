using System.Diagnostics;

namespace Heic2Jpeg.Tests;

public sealed record AppResult(int ExitCode, string StdOut, string StdErr)
{
    public string Output => StdOut + StdErr;
}

/// <summary>
/// Runs the real heic2jpeg executable as a separate process, exactly as a user would.
/// </summary>
public static class AppRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    // The app is a project reference, so its dll (and runtimeconfig) sit next to the test assembly.
    private static readonly string AppDll = typeof(ConvertCommand).Assembly.Location;

    private static readonly string DotnetHost =
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    public static async Task<AppResult> RunAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo(DotnetHost)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(AppDll),
        };

        startInfo.ArgumentList.Add(AppDll);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Keep output plain so assertions do not have to deal with ANSI escape codes.
        startInfo.Environment["NO_COLOR"] = "1";
        startInfo.Environment["TERM"] = "dumb";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start heic2jpeg.");

        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"heic2jpeg did not exit within {Timeout}.");
        }

        return new AppResult(process.ExitCode, await stdOut, await stdErr);
    }
}
