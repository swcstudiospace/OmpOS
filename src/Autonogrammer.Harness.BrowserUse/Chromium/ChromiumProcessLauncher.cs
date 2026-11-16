using System.Diagnostics;
using System.Globalization;
using PuppeteerSharp;
using PuppeteerSharp.Transport;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>
/// Starts Chromium itself. Unix uses <c>/bin/sh -c</c> only to dup the pipe descriptors, then execs the binary with an argument list.
/// Windows uses <c>--remote-debugging-io-pipes</c>. The debugging port is never opened.
/// </summary>
public sealed class ChromiumProcessLauncher : IChromiumLauncher
{
    /// <inheritdoc />
    public async Task<IChromiumSession> LaunchAsync(ChromiumLaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var process = new Process();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                ConfigureWindows(process, request);
            }
            else
            {
                ConfigureUnix(process, request);
            }

            ApplyEnvironment(process, request.Environment);
            if (!process.Start())
            {
                throw new InvalidOperationException("Chromium did not start.");
            }

            var transport = CreatePipe(process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
            try
            {
                var browser = await Puppeteer.ConnectAsync(new ConnectOptions
                {
                    BrowserWSEndpoint = "ws://127.0.0.1/devtools/browser/pipe",
                    TransportFactory = (_, _, _) => Task.FromResult<IConnectionTransport>(transport),
                }).WaitAsync(cancellationToken).ConfigureAwait(false);
                transport = null;
                return new LaunchedChromiumSession(process, browser, request.ProfileDirectory);
            }
            finally
            {
                transport?.Dispose();
            }
        }
        catch
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
                // The process was not started or has already exited.
            }
            finally
            {
                process.Dispose();
            }
            throw;
        }
    }

    private static PipeTransport CreatePipe(Stream write, Stream read)
    {
        var transport = new PipeTransport(write, read);
        transport.Start();
        return transport;
    }

    private static void ConfigureUnix(Process process, ChromiumLaunchRequest request)
    {
        process.StartInfo.FileName = "/bin/sh";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("exec 3<&0 4>&1 </dev/null >/dev/null; exec \"$0\" \"$@\"");
        process.StartInfo.ArgumentList.Add(request.ExecutablePath);
        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
    }

    private static void ConfigureWindows(Process process, ChromiumLaunchRequest request)
    {
        var id = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        process.StartInfo.FileName = request.ExecutablePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        foreach (var argument in request.Arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.ArgumentList.Add("--remote-debugging-io-pipes=\\\\.\\pipe\\ag-browser-" + id + "-in,\\\\.\\pipe\\ag-browser-" + id + "-out");
    }

    private static void ApplyEnvironment(Process process, IReadOnlyDictionary<string, string> environment)
    {
        process.StartInfo.Environment.Clear();
        foreach (var pair in environment)
        {
            process.StartInfo.Environment[pair.Key] = pair.Value;
        }
    }
}
