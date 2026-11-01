using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Desktop.Tests;

/// <summary>The one-shot loopback listener over real TCP (AG-67-2, D-08, T-67-13).</summary>
public sealed class LoopbackListenerTests
{
    private const string Nonce = "nonce-AbC_123";
    private const string Secret = "SECRETQUERY";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheCorrectNonceWithACodeReturnsTheCodeAndASecondCallbackGetsNothing()
    {
        var time = new ManualTimeProvider();
        var logger = new CapturingLogger();
        LoopbackListener listener = StartListener(time, logger);
        await using (listener.ConfigureAwait(true))
        {
            string? first = await SendAsync(listener.Port, Get(listener.Port, "/cb/" + Nonce + "?code=" + Secret)).ConfigureAwait(true);

            StatusOf(first).ShouldBe(200);
            LoopbackResult result = await listener.Result.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            result.Outcome.ShouldBe(LoopbackOutcome.Code);
            result.Code!.Reveal().ShouldBe(Secret);
            result.IsLinkInvalidOrExpired.ShouldBeFalse();
            result.ToString().ShouldNotContain(Secret);

            string? second = await SendAsync(listener.Port, Get(listener.Port, "/cb/" + Nonce + "?code=another")).ConfigureAwait(true);

            (second is null || StatusOf(second) != 200).ShouldBeTrue();
            (await listener.Result.ConfigureAwait(true)).Code!.Reveal().ShouldBe(Secret);
            logger.Lines.ShouldNotContain(line => line.Contains(Secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task TheResponseCarriesTheThreeHardeningHeadersAndAFixedPageThatEchoesNothing()
    {
        var time = new ManualTimeProvider();
        LoopbackListener listener = StartListener(time, new CapturingLogger());
        await using (listener.ConfigureAwait(true))
        {
            string? response = await SendAsync(listener.Port, Get(listener.Port, "/cb/" + Nonce + "?code=" + Secret)).ConfigureAwait(true);

            response.ShouldNotBeNull();
            response.ShouldContain("Cache-Control: no-store");
            response.ShouldContain("Referrer-Policy: no-referrer");
            response.ShouldContain("Content-Security-Policy: default-src 'none'");
            response.ShouldContain("Connection: close");
            response.ShouldContain("You can close this tab and go back to Autonogrammer.");
            response.ShouldNotContain(Secret);
        }
    }

    [Theory]
    [InlineData("wrong-host", 400)]
    [InlineData("localhost-host", 400)]
    [InlineData("two-hosts", 400)]
    [InlineData("no-host", 400)]
    [InlineData("wrong-nonce", 404)]
    [InlineData("other-path", 404)]
    [InlineData("long-request-line", 414)]
    [InlineData("long-headers", 431)]
    [InlineData("many-headers", 431)]
    [InlineData("post", 405)]
    [InlineData("no-code", 400)]
    public async Task ARequestThatBreaksALimitIsRejectedWithoutQueryTextAndDoesNotEndTheWait(string kind, int status)
    {
        var time = new ManualTimeProvider();
        var logger = new CapturingLogger();
        LoopbackListener listener = StartListener(time, logger);
        await using (listener.ConfigureAwait(true))
        {
            string? response = await SendAsync(listener.Port, Rejected(kind, listener.Port)).ConfigureAwait(true);

            StatusOf(response).ShouldBe(status);
            response!.ShouldNotContain(Secret);
            listener.Result.IsCompleted.ShouldBeFalse();
            listener.RejectedRequests.ShouldBe(1);

            // The wait survives the stray request: the real callback is still accepted afterwards.
            string? valid = await SendAsync(listener.Port, Get(listener.Port, "/cb/" + Nonce + "?code=good")).ConfigureAwait(true);
            StatusOf(valid).ShouldBe(200);
            (await listener.Result.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true)).Code!.Reveal().ShouldBe("good");
            logger.Lines.ShouldNotContain(line => line.Contains(Secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AnErrorQueryOnTheRightNonceIsLinkInvalidOrExpiredAndNeverEchoed()
    {
        var time = new ManualTimeProvider();
        var logger = new CapturingLogger();
        LoopbackListener listener = StartListener(time, logger);
        await using (listener.ConfigureAwait(true))
        {
            string? response = await SendAsync(
                listener.Port,
                Get(listener.Port, "/cb/" + Nonce + "?error=access_denied&error_code=otp_expired&error_description=" + Secret)).ConfigureAwait(true);

            response.ShouldNotBeNull();
            response.ShouldNotContain(Secret);
            LoopbackResult result = await listener.Result.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            result.Outcome.ShouldBe(LoopbackOutcome.LinkError);
            result.IsLinkInvalidOrExpired.ShouldBeTrue();
            result.Code.ShouldBeNull();
            logger.Lines.ShouldNotContain(line => line.Contains(Secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task TheListenerExpiresAfterTenMinutesAsLinkInvalidOrExpired()
    {
        var time = new ManualTimeProvider();
        LoopbackListener listener = StartListener(time, new CapturingLogger());
        await using (listener.ConfigureAwait(true))
        {
            LoopbackListener.Lifetime.ShouldBe(TimeSpan.FromMinutes(10));
            time.Advance(TimeSpan.FromMinutes(9));
            listener.Result.IsCompleted.ShouldBeFalse();

            time.Advance(TimeSpan.FromMinutes(1));

            LoopbackResult result = await listener.Result.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            result.Outcome.ShouldBe(LoopbackOutcome.Expired);
            result.IsLinkInvalidOrExpired.ShouldBeTrue();
            (await SendAsync(listener.Port, Get(listener.Port, "/cb/" + Nonce + "?code=late")).ConfigureAwait(true)).ShouldBeNull();
        }
    }

    [Fact]
    public async Task ARequestThatStallsIsClosedAfterFiveSecondsAndDoesNotEndTheWait()
    {
        var time = new ManualTimeProvider();
        LoopbackListener listener = StartListener(time, new CapturingLogger());
        await using (listener.ConfigureAwait(true))
        {
            LoopbackListener.ReadTimeout.ShouldBe(TimeSpan.FromSeconds(5));
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, listener.Port, Token).ConfigureAwait(true);
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET /cb/"), Token).ConfigureAwait(true);

            Task<int> read = stream.ReadAsync(new byte[16], Token).AsTask();
            for (int i = 0; i < 100 && !read.IsCompleted; i++)
            {
                time.Advance(TimeSpan.FromSeconds(1));
                await Task.Delay(20, Token).ConfigureAwait(true);
            }

            read.IsCompleted.ShouldBeTrue();
            (await read.ConfigureAwait(true)).ShouldBe(0);
            listener.Result.IsCompleted.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task DisposingBeforeACallbackCancelsTheResultAndClosesThePort()
    {
        var time = new ManualTimeProvider();
        LoopbackListener listener = StartListener(time, new CapturingLogger());
        int port = listener.Port;

        await listener.DisposeAsync().ConfigureAwait(true);

        listener.Result.IsCanceled.ShouldBeTrue();
        (await SendAsync(port, Get(port, "/cb/" + Nonce + "?code=late")).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task TheListenerBindsTheLoopbackInterfaceOnlyAndTheRedirectAddressNamesIt()
    {
        var time = new ManualTimeProvider();
        LoopbackListener listener = StartListener(time, new CapturingLogger());
        await using (listener.ConfigureAwait(true))
        {
            listener.RedirectUri.Scheme.ShouldBe("http");
            listener.RedirectUri.Host.ShouldBe("127.0.0.1");
            listener.RedirectUri.Port.ShouldBe(listener.Port);
            listener.RedirectUri.AbsolutePath.ShouldBe("/cb/" + Nonce);
            listener.RedirectUri.Query.ShouldBeEmpty();
            listener.Port.ShouldBeGreaterThan(0);
            listener.ToString().ShouldNotContain(Nonce);
        }
    }

    [Fact]
    public void TheListenerIsHandWrittenOverSocketsNotHttpListener()
    {
        typeof(LoopbackListener).Assembly.GetReferencedAssemblies()
            .Any(assembly => string.Equals(assembly.Name, "System.Net.HttpListener", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    private static LoopbackListener StartListener(TimeProvider time, ILogger logger) =>
        LoopbackListener.TryStart(Nonce, time, logger) ?? throw new InvalidOperationException("No loopback port could be bound.");

    private static string Get(int port, string target) =>
        string.Create(CultureInfo.InvariantCulture, $"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n");

    private static string Rejected(string kind, int port)
    {
        string host = "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        string good = "/cb/" + Nonce + "?x=" + Secret + "&code=c";
        return kind switch
        {
            "wrong-host" => $"GET {good} HTTP/1.1\r\nHost: 127.0.0.1:1\r\n\r\n",
            "localhost-host" => $"GET {good} HTTP/1.1\r\nHost: localhost:{port}\r\n\r\n",
            "two-hosts" => $"GET {good} HTTP/1.1\r\nHost: {host}\r\nHost: {host}\r\n\r\n",
            "no-host" => $"GET {good} HTTP/1.1\r\n\r\n",
            "wrong-nonce" => $"GET /cb/wrong?x={Secret}&code=c HTTP/1.1\r\nHost: {host}\r\n\r\n",
            "other-path" => $"GET /other?x={Secret}&code=c HTTP/1.1\r\nHost: {host}\r\n\r\n",
            "long-request-line" => $"GET /cb/{Nonce}?x={Secret}&code={new string('a', LoopbackListener.MaxRequestLineBytes + 100)} HTTP/1.1\r\nHost: {host}\r\n\r\n",
            "long-headers" => $"GET {good} HTTP/1.1\r\nHost: {host}\r\nX-Pad: {Secret}{new string('a', LoopbackListener.MaxHeaderBytes + 100)}\r\n\r\n",
            "many-headers" => $"GET {good} HTTP/1.1\r\nHost: {host}\r\n" + string.Concat(Enumerable.Range(0, 400).Select(i => $"X-Pad-{i}: {Secret}-0123456789\r\n")) + "\r\n",
            "post" => $"POST {good} HTTP/1.1\r\nHost: {host}\r\nContent-Length: 0\r\n\r\n",
            "no-code" => $"GET /cb/{Nonce}?x={Secret} HTTP/1.1\r\nHost: {host}\r\n\r\n",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown case."),
        };
    }

    private static int StatusOf(string? response)
    {
        response.ShouldNotBeNull();
        response.ShouldStartWith("HTTP/1.1 ");
        return int.Parse(response.AsSpan(9, 3), CultureInfo.InvariantCulture);
    }

    private static async Task<string?> SendAsync(int port, string request)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, Token).ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), Token).ConfigureAwait(false);
            using var received = new MemoryStream();
            byte[] buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, Token).ConfigureAwait(false)) > 0)
            {
                await received.WriteAsync(buffer.AsMemory(0, read), Token).ConfigureAwait(false);
            }

            return Encoding.Latin1.GetString(received.ToArray());
        }
        catch (IOException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly Lock _gate = new();
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_gate)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _lines.Add(formatter(state, exception));
            }
        }
    }
}
