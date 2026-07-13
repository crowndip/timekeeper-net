using System.Diagnostics;

namespace ParentalControl.Client.Services;

// A wedged external process (loginctl talking to a stuck logind/D-Bus, in particular)
// must never hang the caller forever -- every invocation gets a hard timeout and is
// killed (whole process tree) if it's exceeded, so the worker's tick loop always makes
// forward progress even when the system it's querying is unhealthy.
public static class ProcessRunner
{
    public static async Task<(bool Ok, string StdOut)> RunAsync(ProcessStartInfo psi, TimeSpan timeout, ILogger logger)
    {
        Process? process = null;
        try
        {
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            process = Process.Start(psi);
            if (process == null)
            {
                logger.LogWarning("Failed to start process: {FileName}", psi.FileName);
                return (false, string.Empty);
            }

            // Start reading stdout immediately (not after WaitForExit) so a chatty process
            // that fills the pipe buffer can't deadlock against a parent that isn't reading.
            var readTask = process.StandardOutput.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("{FileName} {Arguments} timed out after {Timeout}s, killing it",
                    psi.FileName, psi.Arguments, timeout.TotalSeconds);
                try { process.Kill(entireProcessTree: true); } catch { }
                return (false, string.Empty);
            }

            var output = await readTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning("{FileName} {Arguments} exited with code {ExitCode}",
                    psi.FileName, psi.Arguments, process.ExitCode);
                return (false, output);
            }

            return (true, output);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running {FileName} {Arguments}", psi.FileName, psi.Arguments);
            try { process?.Kill(entireProcessTree: true); } catch { }
            return (false, string.Empty);
        }
    }
}
