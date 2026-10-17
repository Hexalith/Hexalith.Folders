using System.Diagnostics;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Runs bounded local infrastructure commands without shell interpolation or credential output.</summary>
internal static class FolderDurabilityProcess
{
    /// <summary>Runs a command and returns its standard output, failing on timeout or a nonzero exit.</summary>
    internal static async Task<string> RunAsync(string executable, params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {executable}.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{executable} exceeded the acceptance infrastructure timeout.");
        }

        _ = await error.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} failed with exit {process.ExitCode}.");
        }

        return (await output.ConfigureAwait(false)).Trim();
    }
}
