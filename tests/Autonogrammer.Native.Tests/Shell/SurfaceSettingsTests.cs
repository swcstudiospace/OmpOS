using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Tests.Settings.Head;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>shell.surface round-trips through the settings file. An unusable value is an error and is not overwritten.</summary>
public sealed class SurfaceSettingsTests : IDisposable
{
    private readonly HeadTempDirectory _state = new();

    /// <inheritdoc />
    public void Dispose() => _state.Dispose();

    /// <summary>The stored words are the switch model's pair, and a later read returns them.</summary>
    [Fact]
    public void TheSurfaceRoundTripsThroughTheSwitchModel()
    {
        var store = new FileSettingsStore(_state.Root);
        store.SetSurface(SessionSurface.Cowork);
        var text = File.ReadAllText(store.FilePath);
        text.ShouldContain(SettingsShellSection.SurfaceMember);
        text.ShouldContain(SurfaceSwitchModel.Format(SessionSurface.Cowork));
        store.Read().Surface.ShouldBe(SurfaceSwitchModel.CoworkValue);
        SurfaceSwitchModel.Parse(store.Read().Surface).ShouldBe(SessionSurface.Cowork);

        store.SetSurface(SessionSurface.Code);
        store.Read().Surface.ShouldBe(SurfaceSwitchModel.Format(SessionSurface.Code));
    }

    /// <summary>An unusable member keeps Code, shows the settings error, and writes nothing.</summary>
    [Fact]
    public void AnUnusableSurfaceIsAnErrorAndIsNotOverwritten()
    {
        var store = new FileSettingsStore(_state.Root);
        var path = _state.Write("config/settings.json", "{\"schemaVersion\":1,\"shell.surface\":\"desk\"}");
        var before = File.ReadAllBytes(path);
        var error = Should.Throw<SettingsSourceException>(() => store.Read());
        error.Message.ShouldBe(HeadSettingsCopy.SettingsBadMember(path, SettingsShellSection.SurfaceMember));
        Should.Throw<SettingsSourceException>(() => store.SetSurface(SessionSurface.Code));
        File.ReadAllBytes(path).ShouldBe(before);

        var model = new SurfaceSwitchModel();
        model.Restore("desk", available: true);
        model.Selected.ShouldBe(SessionSurface.Code);
    }
}
