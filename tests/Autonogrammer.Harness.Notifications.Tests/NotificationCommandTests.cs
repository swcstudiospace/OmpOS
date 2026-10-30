using System.Text;
using Autonogrammer.Harness.Notifications.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Notifications.Tests;

/// <summary>Argument-vector command builders and the named operating-system unavailability states. No test displays a notification.</summary>
public sealed class NotificationCommandTests
{
    /// <summary><c>notify-send</c> is an argument vector. Control characters are stripped and each argument is capped at 200 characters. No shell string is built.</summary>
    [Fact]
    public void NotifySendIsAnArgumentVectorWithStrippedAndCappedArguments()
    {
        var title = "Hello\n$(rm -rf /);`id`\r" + new string('A', 250);
        var summary = "Body\u0007" + new string('B', 250);
        var command = NotifySendCommand.Create(title, summary);

        NotifySendCommand.FileName.ShouldBe("notify-send");
        command.Arguments.Count.ShouldBe(2);
        command.Arguments[0].Length.ShouldBe(200);
        command.Arguments[1].Length.ShouldBe(200);
        command.Arguments[0].ShouldNotContain('\n');
        command.Arguments[0].ShouldNotContain('\r');
        command.Arguments[1].ShouldNotContain('\u0007');
        command.Arguments.ShouldAllBe(argument => argument.All(character => !char.IsControl(character)));
        command.Arguments[0].ShouldContain("$(rm -rf /);`id`", Case.Sensitive);
        string.Join(" ", [NotifySendCommand.FileName, .. command.Arguments]).Split(' ', 2)[0].ShouldBe("notify-send");
        command.Arguments.ShouldNotContain(argument => argument.Contains("notify-send ", StringComparison.Ordinal));
    }

    /// <summary>Notification text is an <c>on run argv</c> parameter. The AppleScript source is a fixed constant, byte for byte, and does not contain the text.</summary>
    [Fact]
    public void OsascriptPassesTextOnlyAsArgvAndKeepsTheSourceByteExact()
    {
        const string expected = "on run argv\ndisplay notification (item 2 of argv) with title (item 1 of argv)\nend run\n";
        var title = "Need approval\nnow";
        var summary = "quote \" ' $(x)";
        var command = OsascriptCommand.Create(title, summary);

        OsascriptCommand.Source.ShouldBe(expected);
        Encoding.UTF8.GetBytes(OsascriptCommand.Source).ShouldBe(Encoding.UTF8.GetBytes(expected));
        OsascriptCommand.FileName.ShouldBe("osascript");
        command.Arguments.ShouldBe(["-", "Need approvalnow", summary]);
        OsascriptCommand.Source.ShouldNotContain(title);
        OsascriptCommand.Source.ShouldNotContain(summary);
        Encoding.UTF8.GetString(OsascriptCommand.StandardInput.Span).ShouldBe(expected);
        Encoding.UTF8.GetString(OsascriptCommand.StandardInput.Span).ShouldNotContain("Need approval");
    }

    /// <summary>A missing <c>notify-send</c>, a failed <c>osascript</c> and Windows each return their named state. Windows does not start a process.</summary>
    [Fact]
    public async Task MissingAndFailedToolsAndWindowsReturnNamedStates()
    {
        var missing = new FakeNotificationProcessRunner { ExitCode = null };
        var linux = new OsNotificationSink(missing, NotificationDesktop.Linux);
        var linuxState = await linux.DeliverAsync("Title\n", "Summary", CancellationToken.None).ConfigureAwait(true);
        linuxState.ShouldBe("NotificationOsUnavailable(notify_send_missing)");
        missing.Calls.Count.ShouldBe(1);
        missing.Calls[0].FileName.ShouldBe("notify-send");
        missing.Calls[0].Arguments.ShouldBe(["Title", "Summary"]);
        missing.Calls[0].StandardInput.ShouldBeEmpty();

        var failed = new FakeNotificationProcessRunner { ExitCode = 1 };
        var mac = new OsNotificationSink(failed, NotificationDesktop.MacOs);
        var macState = await mac.DeliverAsync("Title", "Body $(x)", CancellationToken.None).ConfigureAwait(true);
        macState.ShouldBe("NotificationOsUnavailable(osascript_failed)");
        failed.Calls[0].FileName.ShouldBe("osascript");
        failed.Calls[0].Arguments.ShouldBe(["-", "Title", "Body $(x)"]);
        failed.Calls[0].StandardInput.ShouldBe(Encoding.UTF8.GetBytes(OsascriptCommand.Source));
        Encoding.UTF8.GetString([.. failed.Calls[0].StandardInput]).ShouldNotContain("Body");

        var windowsRunner = new FakeNotificationProcessRunner { ExitCode = 0 };
        var windows = new OsNotificationSink(windowsRunner, NotificationDesktop.Windows);
        var windowsState = await windows.DeliverAsync("Title", "Body", CancellationToken.None).ConfigureAwait(true);
        windowsState.ShouldBe("NotificationOsUnavailable(windows_not_supported)");
        windowsRunner.Calls.ShouldBeEmpty();
    }
}
