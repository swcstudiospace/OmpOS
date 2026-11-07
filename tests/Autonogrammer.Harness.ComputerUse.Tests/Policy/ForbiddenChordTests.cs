using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Hard-denied chords, including virtual-terminal switches and the kill chord.</summary>
public sealed class ForbiddenChordTests
{
    [Fact]
    public async Task NineHardDeniedChordsAreRefused()
    {
        string[] chords =
        [
            "Win+R",
            "Win+X",
            "Alt+F2",
            "Ctrl+Alt+T",
            "Cmd+Space",
            "Ctrl+Alt+Del",
            "Ctrl+Alt+Backspace",
            "Ctrl+Alt+F1",
            "Ctrl+Alt+Shift+Escape",
        ];
        foreach (string chord in chords)
        {
            GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
            fixture.BindChord(chord);
            fixture.Request = fixture.Request with { ToolName = "computer_chord" };
            ComputerGateDenied denied = await fixture.DenyAsync().ConfigureAwait(true);
            denied.Code.ShouldBe(DenialCode.ForbiddenChord);
            denied.UserMessageId.ShouldBe("computer.denial.forbidden-chord");
        }
    }
}
