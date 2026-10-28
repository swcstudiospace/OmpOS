using Avalonia.Controls;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Branding;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Branding;

/// <summary>The application icon helper: the brand mark resource is in the views assembly and becomes a window icon.</summary>
public sealed class AppIconTests
{
    /// <summary>The mark resource resolves, and a missing neighbour does not.</summary>
    [Fact]
    public async Task TheMarkResourceResolves()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            AppIcon.Exists().ShouldBeTrue();
            AppIcon.ResourceUri.ShouldBe("avares://Autonogrammer.Native.Views/Assets/Brand/autonogrammer-mark-256.png");
            Avalonia.Platform.AssetLoader.Exists(new Uri("avares://Autonogrammer.Native.Views/Assets/Brand/autonogrammer-missing.png")).ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Create builds an icon, and Apply gives a window a new one each time.</summary>
    [Fact]
    public async Task ApplyGivesAWindowTheIcon()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            AppIcon.Create().ShouldNotBeNull();
            var window = new Window();
            window.Icon.ShouldBeNull();

            AppIcon.Apply(window);

            window.Icon.ShouldNotBeNull();
            Should.Throw<ArgumentNullException>(() => AppIcon.Apply(null!));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Settings window carries the icon too.</summary>
    [Fact]
    public async Task TheSettingsWindowCarriesTheIcon()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);

            fixture.Window.Icon.ShouldNotBeNull();
        }, ct).ConfigureAwait(true);
    }
}
