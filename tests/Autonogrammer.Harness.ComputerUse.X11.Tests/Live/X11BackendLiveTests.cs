using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.X11.Tests.Live;

/// <summary>
/// Live X11 checks against a private Xvfb. They never read or set the session <c>DISPLAY</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class X11BackendLiveTests : IDisposable
{
    private const string OptIn = "AUTONOGRAMMER_LIVE_X11";

    private readonly string? _sessionDisplay;

    private readonly string? _display;

    private readonly Process? _server;

    private readonly List<Process> _children = [];

    private readonly Dictionary<int, StringBuilder> _logs = [];

    public X11BackendLiveTests()
    {
        _sessionDisplay = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.Equals(Environment.GetEnvironmentVariable(OptIn), "1", StringComparison.Ordinal) || !File.Exists("/usr/bin/Xvfb"))
        {
            return;
        }

        var start = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("exec 3>&1; exec /usr/bin/Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24");
        _server = Process.Start(start) ?? throw new InvalidOperationException("Xvfb did not start.");
        string? line = _server.StandardOutput.ReadLine();
        if (string.IsNullOrEmpty(line))
        {
            throw new InvalidOperationException("Xvfb did not report a display.");
        }

        _display = ":" + line.Trim();
    }

    public void Dispose()
    {
        foreach (Process child in _children)
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }

            child.Dispose();
        }

        if (_server is not null && !_server.HasExited)
        {
            _server.Kill(entireProcessTree: true);
        }

        _server?.Dispose();
        Environment.GetEnvironmentVariable("DISPLAY").ShouldBe(_sessionDisplay);
    }

    [Fact]
    public async Task PointerMoveIsReadBack()
    {
        SkipUnlessLive();
        using X11Backend backend = CreateBackend();
        Process mover = StartHeld("xdotool", "mousemove", "320", "180", "sleep", "3");
        await Task.Delay(200, TestContext.Current.CancellationToken).ConfigureAwait(true);
        PointerState pointer = await backend.GetPointerAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        pointer.X.ShouldBe(320);
        pointer.Y.ShouldBe(180);
        if (!mover.HasExited)
        {
            mover.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task ClickReachesXev()
    {
        SkipUnlessLive();
        Process xev = StartLogged("xev", "-name", "ag-xev", "-geometry", "200x150+40+40", "-event", "button");
        using X11Backend backend = CreateBackend();
        WindowInfo window = await WaitForWindowAsync(backend, "ag-xev").ConfigureAwait(true);
        await backend.InjectAsync(new PointerMove { X = window.Bounds.X + 20, Y = window.Bounds.Y + 20 }, CancellationToken.None).ConfigureAwait(true);
        await backend.InjectAsync(new PointerButton { Button = 1, Down = true }, CancellationToken.None).ConfigureAwait(true);
        await backend.InjectAsync(new PointerButton { Button = 1, Down = false }, CancellationToken.None).ConfigureAwait(true);
        (await WaitForOutputAsync(xev, "ButtonPress").ConfigureAwait(true)).ShouldContain("ButtonPress");
    }

    [Fact]
    public async Task TypingReachesXevAsTheSameKeysyms()
    {
        SkipUnlessLive();
        const string text = "Hello, ünï";
        Process xev = StartLogged("xev", "-name", "ag-type", "-geometry", "200x150+40+40", "-event", "keyboard");
        using X11Backend backend = CreateBackend();
        WindowInfo window = await WaitForWindowAsync(backend, "ag-type").ConfigureAwait(true);
        Run("xdotool", "windowfocus", "--sync", "0x" + window.Id);
        await backend.InjectAsync(new TypeText { Text = text }, CancellationToken.None).ConfigureAwait(true);
        string log = await WaitForOutputAsync(xev, "KeyPress", text.Length).ConfigureAwait(true);
        ParseKeysyms(log).ShouldBe(text.Select(KeysymMapper.ToKeysym).ToArray());
    }

    [Fact]
    public async Task CaptureAgreesWithScrot()
    {
        SkipUnlessLive();
        _ = StartLogged("xterm", "-geometry", "20x5+80+60", "-bg", "red", "-fg", "white", "-title", "ag-term");
        using X11Backend backend = CreateBackend();
        WindowInfo window = await WaitForWindowAsync(backend, "ag-term").ConfigureAwait(true);
        string path = Path.Combine(Path.GetTempPath(), "ag-scrot-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".png");
        try
        {
            Run("scrot", "-o", "-a", window.Bounds.X.ToString(CultureInfo.InvariantCulture) + "," + window.Bounds.Y.ToString(CultureInfo.InvariantCulture) + "," + window.Bounds.Width.ToString(CultureInfo.InvariantCulture) + "," + window.Bounds.Height.ToString(CultureInfo.InvariantCulture), path);
            RawFrame frame = await backend.CaptureAsync(new CaptureRegion { Bounds = window.Bounds }, CancellationToken.None).ConfigureAwait(true);
            PngImage png = PngImage.Read(path);
            int width = Math.Min(frame.Width, png.Width);
            int height = Math.Min(frame.Height, png.Height);
            int compared = 0;
            int within = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int frameOffset = (y * frame.Stride) + (x * 4);
                    int pngOffset = ((y * png.Width) + x) * png.Channels;
                    int red = Math.Abs(frame.Pixels.Span[frameOffset + 2] - png.Pixels[pngOffset]);
                    int green = Math.Abs(frame.Pixels.Span[frameOffset + 1] - png.Pixels[pngOffset + 1]);
                    int blue = Math.Abs(frame.Pixels.Span[frameOffset] - png.Pixels[pngOffset + 2]);
                    compared++;
                    if (red <= 12 && green <= 12 && blue <= 12)
                    {
                        within++;
                    }
                }
            }

            within.ShouldBeGreaterThan(compared * 9 / 10);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task HotkeyFiresOnTheChord()
    {
        SkipUnlessLive();
        using var registrar = new X11HotkeyRegistrar(Options());
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registrar.Pressed += (_, _) => pressed.TrySetResult();
        (await registrar.RegisterAsync(X11HotkeyRegistrar.DefaultChord, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        Run("xdotool", "key", "ctrl+alt+shift+Escape");
        await pressed.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task SecondGrabReportsKillSwitchUnavailable()
    {
        SkipUnlessLive();
        using var first = new X11HotkeyRegistrar(Options());
        using var second = new X11HotkeyRegistrar(Options());
        (await first.RegisterAsync(X11HotkeyRegistrar.DefaultChord, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        bool grabbed = await second.RegisterAsync(X11HotkeyRegistrar.DefaultChord, CancellationToken.None).ConfigureAwait(true);
        grabbed.ShouldBeFalse();
        second.RegistrationFailure.ShouldBe(ComputerAvailability.KillSwitchUnavailable);
        ComputerAvailabilityRemediation.For(second.RegistrationFailure!.Value).ShouldBe(ComputerAvailabilityRemediation.KillSwitchUnavailable);
    }

    [Fact]
    public async Task BareXvfbListsWindowsThroughXQueryTree()
    {
        SkipUnlessLive();
        using X11Backend backend = CreateBackend();
        _ = await backend.ListWindowsAsync(CancellationToken.None).ConfigureAwait(true);
        backend.StackingSource.ShouldBe("XQueryTree");
    }

    private void SkipUnlessLive()
    {
        Environment.GetEnvironmentVariable("DISPLAY").ShouldBe(_sessionDisplay);
        if (!string.Equals(Environment.GetEnvironmentVariable(OptIn), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set " + OptIn + "=1 to run the private Xvfb checks.");
        }

        if (!File.Exists("/usr/bin/Xvfb"))
        {
            Assert.Skip("Xvfb not found");
        }

        _display.ShouldNotBeNull();
    }

    private X11Backend CreateBackend() => new(Options());

    private X11BackendOptions Options() => new()
    {
        DisplayName = _display,
        Environment = name => string.Equals(name, "DISPLAY", StringComparison.Ordinal) ? _display : null,
    };

    private Process StartLogged(params string[] args)
    {
        var start = Child("/usr/bin/stdbuf");
        start.ArgumentList.Add("-oL");
        start.ArgumentList.Add("-eL");
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        Process process = Process.Start(start) ?? throw new InvalidOperationException(args[0] + " did not start.");
        var log = new StringBuilder();
        _logs[process.Id] = log;
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                lock (log)
                {
                    _ = log.AppendLine(eventArgs.Data);
                }
            }
        };
        process.BeginOutputReadLine();
        _children.Add(process);
        return process;
    }

    private Process StartHeld(params string[] args)
    {
        var start = Child(args[0]);
        for (int i = 1; i < args.Length; i++)
        {
            start.ArgumentList.Add(args[i]);
        }

        Process process = Process.Start(start) ?? throw new InvalidOperationException(args[0] + " did not start.");
        _children.Add(process);
        return process;
    }

    private void Run(params string[] args)
    {
        var start = Child(args[0]);
        for (int i = 1; i < args.Length; i++)
        {
            start.ArgumentList.Add(args[i]);
        }

        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using Process process = Process.Start(start) ?? throw new InvalidOperationException(args[0] + " did not start.");
        process.WaitForExit(5_000).ShouldBeTrue();
        process.ExitCode.ShouldBe(0, process.StandardError.ReadToEnd());
    }


    private ProcessStartInfo Child(string file)
    {
        var start = new ProcessStartInfo
        {
            FileName = file,
            UseShellExecute = false,
        };
        start.Environment.Remove("DISPLAY");
        start.Environment["DISPLAY"] = _display ?? string.Empty;
        return start;
    }

    private static async Task<WindowInfo> WaitForWindowAsync(X11Backend backend, string title)
    {
        for (int i = 0; i < 50; i++)
        {
            IReadOnlyList<WindowInfo> windows = await backend.ListWindowsAsync(CancellationToken.None).ConfigureAwait(false);
            WindowInfo? match = windows.FirstOrDefault(item => item.Title.Contains(title, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Window was not listed.");
    }

    private async Task<string> WaitForOutputAsync(Process process, string token, int count = 1)
    {
        for (int i = 0; i < 60; i++)
        {
            string text;
            lock (_logs[process.Id])
            {
                text = _logs[process.Id].ToString();
            }

            if (Count(text, token) >= count)
            {
                return text;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        lock (_logs[process.Id])
        {
            return _logs[process.Id].ToString();
        }
    }

    private static int Count(string text, string token)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static uint[] ParseKeysyms(string log)
    {
        var found = new List<uint>();
        foreach (string block in log.Split("KeyPress", StringSplitOptions.RemoveEmptyEntries))
        {
            int marker = block.IndexOf("keysym 0x", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            int start = marker + "keysym 0x".Length;
            int end = start;
            while (end < block.Length && Uri.IsHexDigit(block[end]))
            {
                end++;
            }

            if (uint.TryParse(block.AsSpan(start, end - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint keysym)
                && (keysym < 0xff00 || keysym >= KeysymMapper.UnicodePlane))
            {
                found.Add(keysym);
            }
        }

        return [.. found];
    }

    private sealed record PngImage(int Width, int Height, int Channels, byte[] Pixels)
    {
        internal static PngImage Read(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            int offset = 8;
            int width = 0;
            int height = 0;
            int channels = 3;
            var compressed = new MemoryStream();
            while (offset + 12 <= file.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset, 4));
                string type = Encoding.ASCII.GetString(file, offset + 4, 4);
                if (string.Equals(type, "IHDR", StringComparison.Ordinal))
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset + 8, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset + 12, 4));
                    channels = file[offset + 17] == 6 ? 4 : 3;
                }
                else if (string.Equals(type, "IDAT", StringComparison.Ordinal))
                {
                    compressed.Write(file, offset + 8, length);
                }
                else if (string.Equals(type, "IEND", StringComparison.Ordinal))
                {
                    break;
                }

                offset += 12 + length;
            }

            compressed.Position = 0;
            return new PngImage(width, height, channels, Unfilter(compressed, width, height, channels));
        }

        private static byte[] Unfilter(Stream compressed, int width, int height, int channels)
        {
            using var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);
            byte[] scanlines = raw.ToArray();
            int rowBytes = width * channels;
            var pixels = new byte[rowBytes * height];
            var previous = new byte[rowBytes];
            int stride = rowBytes + 1;
            for (int y = 0; y < height; y++)
            {
                DecodeRow(scanlines, previous, pixels, y, stride, rowBytes, channels);
            }

            return pixels;
        }

        private static void DecodeRow(byte[] scanlines, byte[] previous, byte[] pixels, int y, int stride, int rowBytes, int channels)
        {
            int row = y * stride;
            byte filter = scanlines[row];
            var current = new byte[rowBytes];
            for (int i = 0; i < rowBytes; i++)
            {
                byte value = scanlines[row + 1 + i];
                byte left = i >= channels ? current[i - channels] : (byte)0;
                byte up = previous[i];
                byte upLeft = i >= channels ? previous[i - channels] : (byte)0;
                current[i] = (byte)(value + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (byte)((left + up) / 2),
                    _ => Paeth(left, up, upLeft),
                });
            }

            current.CopyTo(pixels.AsSpan(y * rowBytes));
            current.CopyTo(previous, 0);
        }

        private static byte Paeth(byte left, byte up, byte upLeft)
        {
            int estimate = left + up - upLeft;
            int distanceLeft = Math.Abs(estimate - left);
            int distanceUp = Math.Abs(estimate - up);
            int distanceUpLeft = Math.Abs(estimate - upLeft);
            if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
            {
                return left;
            }

            return distanceUp <= distanceUpLeft ? up : upLeft;
        }
    }
}
