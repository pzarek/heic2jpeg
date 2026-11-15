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

    /// <summary>Runs the app in English, whatever the language of this machine.</summary>
    public static Task<AppResult> RunAsync(params string[] args) => RunCoreAsync(locale: null, args);

    /// <summary>
    /// Runs the app with the language set the way Linux and macOS users set it, e.g. "pl_PL.UTF-8".
    /// Windows takes the language from its display language instead, so this has no effect there.
    /// </summary>
    public static Task<AppResult> RunWithLocaleAsync(string locale, params string[] args) => RunCoreAsync(locale, args);

    private static async Task<AppResult> RunCoreAsync(string? locale, string[] args)
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

        if (locale is null)
        {
            // The invariant culture gets the English texts on every OS, including a Windows set to Polish.
            startInfo.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        }
        else
        {
            startInfo.Environment.Remove("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT");
            startInfo.Environment["LC_ALL"] = locale;
            startInfo.Environment["LANG"] = locale;
        }

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
