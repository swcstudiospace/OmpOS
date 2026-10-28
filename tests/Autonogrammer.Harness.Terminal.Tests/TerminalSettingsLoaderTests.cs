using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Verifies how the terminal settings file turns auto-run on: only the explicit <c>"autoRun": true</c> opt-in does,
/// and the loaded settings drive <see cref="TerminalGate"/> the same way.
/// </summary>
public sealed class TerminalSettingsLoaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AutoRunStaysOffWithoutTheSetting()
    {
        using var workspace = new TempWorkspace();
        var missingFile = Path.Combine(workspace.Path, "terminal.json");
        var runner = new FakeProcessRunner();

        var loaded = TerminalSettingsLoader.Load(missingFile);
        var gate = new TerminalGate(workspace.Path, loaded.Settings, runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["build"]), Token);

        loaded.HasProblem.ShouldBeFalse();
        loaded.Settings.AutoRunEnabled.ShouldBeFalse();
        proposed.Status.ShouldBe(CommandStatus.Pending);
        runner.SpawnCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("""{ "maxOutputCharacters": 1024 }""")]
    [InlineData("""{ "autoRun": false }""")]
    [InlineData("""{ "autoRun": null }""")]
    public void AutoRunStaysOffForEverySettingThatIsNotTheOptIn(string json)
    {
        var loaded = TerminalSettingsLoader.Parse(json);

        loaded.HasProblem.ShouldBeFalse();
        loaded.Settings.AutoRunEnabled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "autoRun": "true" }""")]
    [InlineData("""{ "autoRun": 1 }""")]
    [InlineData("""{ "autorun": true }""")]
    [InlineData("""{ "autoRun": true, "unknown": 1 }""")]
    [InlineData("""{ "autoRun": true, """)]
    [InlineData("""{ "autoRun": true, "maxOutputCharacters": 0 }""")]
    [InlineData("[true]")]
    [InlineData("null")]
    public void AnUnusableFileNeverTurnsAutoRunOnAndSaysWhy(string json)
    {
        var loaded = TerminalSettingsLoader.Parse(json);

        loaded.HasProblem.ShouldBeTrue();
        loaded.Problem.ShouldNotBeNullOrWhiteSpace();
        loaded.Settings.AutoRunEnabled.ShouldBeFalse();
        loaded.Settings.MaxOutputCharacters.ShouldBe(new TerminalSettings().MaxOutputCharacters);
    }

    [Fact]
    public async Task AutoRunTurnsOnOnlyForTheExplicitOptIn()
    {
        using var workspace = new TempWorkspace();
        var file = Path.Combine(workspace.Path, "terminal.json");
        await File.WriteAllTextAsync(file, """{ "autoRun": true, "maxOutputCharacters": 2048 }""", Token);
        var runner = new FakeProcessRunner();

        var loaded = TerminalSettingsLoader.Load(file);
        var gate = new TerminalGate(workspace.Path, loaded.Settings, runner);
        var ran = await gate.ProposeAsync(new CommandProposal("dotnet", ["build"]), Token);

        loaded.HasProblem.ShouldBeFalse();
        loaded.Settings.AutoRunEnabled.ShouldBeTrue();
        loaded.Settings.MaxOutputCharacters.ShouldBe(2048);
        ran.Status.ShouldBe(CommandStatus.Completed);
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task AnUnparseableFileOnDiskKeepsAutoRunOffAndNamesTheFile()
    {
        using var workspace = new TempWorkspace();
        var file = Path.Combine(workspace.Path, "terminal.json");
        await File.WriteAllTextAsync(file, """{ "autoRun": "yes" }""", Token);
        var runner = new FakeProcessRunner();

        var loaded = TerminalSettingsLoader.Load(file);
        var gate = new TerminalGate(workspace.Path, loaded.Settings, runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["build"]), Token);

        loaded.HasProblem.ShouldBeTrue();
        loaded.Problem.ShouldContain(file, Case.Sensitive);
        proposed.Status.ShouldBe(CommandStatus.Pending);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public void ABlankPathIsRejected()
    {
        Should.Throw<ArgumentException>(() => TerminalSettingsLoader.Load(" "));
    }

    [Fact]
    public void NullTextIsRejected()
    {
        Should.Throw<ArgumentNullException>(() => TerminalSettingsLoader.Parse(null!));
    }
}
