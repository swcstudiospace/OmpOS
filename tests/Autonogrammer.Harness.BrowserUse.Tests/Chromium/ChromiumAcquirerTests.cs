using System.Runtime.Versioning;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

/// <summary>Acquisition against a local server: good archive and the five refusals. No silent download.</summary>
public sealed class ChromiumAcquirerTests
{
    [Fact]
    public void EmbeddedManifest_HasNoRows_AndHidesAcquisition()
    {
        var manifest = ChromiumManifest.LoadEmbedded();
        manifest.Rows.Count.ShouldBe(0);
        ChromiumAcquirer.IsAcquisitionVisible(manifest, "linux-x64").ShouldBeFalse();
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task Acquire_InstallsAGoodArchive_AndRefusesTheFiveBadCases()
    {
        var outside = Path.Combine(Path.GetTempPath(), "ag-acquire-outside-" + Guid.NewGuid().ToString("N"));
        var state = Path.Combine(Path.GetTempPath(), "ag-acquire-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state);
        var good = Gzip(Tar(("chrome", "#!/bin/sh\n")));
        var traversal = Gzip(Tar(("chrome", "ok"), ("../" + Path.GetFileName(outside), "escaped")));
        using var server = LocalArchiveServer.Start(good, traversal);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var acquirer = new ChromiumAcquirer(handler, "127.0.0.1", requireTls: false);
        try
        {
            var installed = await acquirer.AcquireAsync(Row(server.GoodUrl, good), "linux-x64", state, userConfirmed: true, TestContext.Current.CancellationToken).ConfigureAwait(true);
            installed.Outcome.ShouldBe(AcquireOutcome.Installed);
            var published = installed.PublishedDirectory!;
            Directory.Exists(published).ShouldBeTrue();
            Directory.Exists(Path.Combine(state, "browser", "chromium", ".incoming-149.0")).ShouldBeFalse();
            var binary = Path.Combine(published, "chrome");
            File.Exists(binary).ShouldBeTrue();
            (File.GetUnixFileMode(binary) & UnixFileMode.UserExecute).ShouldBe(UnixFileMode.UserExecute);

            (await acquirer.AcquireAsync(Row(server.GoodUrl, good, sha256: new string('a', 64)), "linux-x64", state, true, TestContext.Current.CancellationToken).ConfigureAwait(true)).Outcome.ShouldBe(AcquireOutcome.RefusedHash);
            (await acquirer.AcquireAsync(Row(server.GoodUrl, good, size: good.Length + 1), "linux-x64", state, true, TestContext.Current.CancellationToken).ConfigureAwait(true)).Outcome.ShouldBe(AcquireOutcome.RefusedSize);
            (await acquirer.AcquireAsync(Row(server.RedirectUrl, good), "linux-x64", state, true, TestContext.Current.CancellationToken).ConfigureAwait(true)).Outcome.ShouldBe(AcquireOutcome.RefusedHost);
            (await acquirer.AcquireAsync(Row(server.TraversalUrl, traversal), "linux-x64", state, true, TestContext.Current.CancellationToken).ConfigureAwait(true)).Outcome.ShouldBe(AcquireOutcome.RefusedTraversal);
            File.Exists(outside).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(state))
            {
                Directory.Delete(state, recursive: true);
            }

            if (File.Exists(outside))
            {
                File.Delete(outside);
            }
        }
    }

    private static ChromiumManifest Row(string source, byte[] body, string? sha256 = null, long? size = null)
    {
        var hash = sha256 ?? Convert.ToHexString(SHA256.HashData(body));
        return new ChromiumManifest([new ChromiumManifestRow("149.0", "linux-x64", source, hash, size ?? body.Length)]);
    }

    private static byte[] Tar(params (string Name, string Text)[] files)
    {
        using var tar = new MemoryStream();
        using (var writer = new TarWriter(tar, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, file.Name);
                entry.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(file.Text));
                writer.WriteEntry(entry);
            }
        }

        return tar.ToArray();
    }

    private static byte[] Gzip(byte[] tar)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(tar);
        }

        return output.ToArray();
    }

    private sealed class LocalArchiveServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Thread _thread;
        private volatile bool _stop;

        private LocalArchiveServer(byte[] good, byte[] traversal, int port)
        {
            var prefix = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
            GoodUrl = prefix + "good.tar.gz";
            TraversalUrl = prefix + "traversal.tar.gz";
            RedirectUrl = prefix + "redirect";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _thread = new Thread(() => Serve(good, traversal))
            {
                IsBackground = true,
            };
            _thread.Start();
        }

        public string GoodUrl { get; }

        public string TraversalUrl { get; }

        public string RedirectUrl { get; }

        public static LocalArchiveServer Start(byte[] good, byte[] traversal)
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return new LocalArchiveServer(good, traversal, port);
        }

        public void Dispose()
        {
            _stop = true;
            _listener.Stop();
            _thread.Join(TimeSpan.FromSeconds(2));
            _listener.Close();
        }

        private void Serve(byte[] good, byte[] traversal)
        {
            while (!_stop)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var path = context.Request.Url?.AbsolutePath ?? string.Empty;
                if (path.EndsWith("/redirect", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Redirect;
                    context.Response.RedirectLocation = "http://evil.example/chrome.tar.gz";
                    context.Response.Close();
                    continue;
                }

                var body = path.Contains("traversal", StringComparison.Ordinal) ? traversal : good;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentLength64 = body.Length;
                context.Response.OutputStream.Write(body);
                context.Response.Close();
            }
        }
    }
}
