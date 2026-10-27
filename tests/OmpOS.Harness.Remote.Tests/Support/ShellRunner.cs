using System.Diagnostics;
using System.Text;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>Runs a local program to completion and captures its output, for tests that execute a generated shell line for real.</summary>
internal static class ShellRunner
{
    /// <summary>Runs <c>sh -c</c> with a script.</summary>
    /// <param name="script">The script.</param>
    /// <param name="stdin">Bytes written to stdin, or <see langword="null"/> for an empty stdin.</param>
    /// <param name="environment">Environment variables set on top of the inherited ones.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The exit code and what the program printed.</returns>
    public static async Task<ShellResult> RunShAsync(
        string script,
        ReadOnlyMemory<byte>? stdin,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sh",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(script);
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                startInfo.Environment[name] = value;
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("sh did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        if (stdin is { } bytes)
        {
            await process.StandardInput.BaseStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(true);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(true);
        }

        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);
        return new ShellResult(process.ExitCode, await output.ConfigureAwait(true), await error.ConfigureAwait(true));
    }
}
