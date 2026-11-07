using System.ComponentModel;
using System.Diagnostics;

namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// <see cref="INotificationProcessRunner"/> over <see cref="Process"/>. Arguments go through <c>ArgumentList</c>.
/// <see cref="ProcessStartInfo.UseShellExecute"/> is false. No shell string is built.
/// </summary>
public sealed class SystemNotificationProcessRunner : INotificationProcessRunner
{
    /// <inheritdoc />
    public async Task<int?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return null;
            }
        }
        catch (Exception exception) when (IsMissing(exception))
        {
            return null;
        }

        if (!standardInput.IsEmpty)
        {
            await process.StandardInput.BaseStream.WriteAsync(standardInput, cancellationToken).ConfigureAwait(false);
        }

        process.StandardInput.Close();
        var drainOutput = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        var drainError = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await drainOutput.ConfigureAwait(false);
            await drainError.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        return process.ExitCode;
    }

    private static bool IsMissing(Exception exception) =>
        exception is FileNotFoundException
        || (exception is Win32Exception win32 && win32.NativeErrorCode == 2);

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
    }
}
