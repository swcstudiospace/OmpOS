#pragma warning disable MA0002
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Presentation.Computer;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>The Computer Use page exposes disclosure, grants, denylist, kill chord, security phrase and audit.</summary>
public sealed class ComputerUseSettingsPageTests
{
    [Fact]
    public async Task ThePageExposesAllSixAreas()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var view = fixture.Show(Autonogrammer.Presentation.Settings.SettingsSection.ComputerUse);
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain(ComputerUseCopy.ProviderDisclosureHeading);
            texts.ShouldContain(ComputerUseCopy.GrantsHeading);
            texts.ShouldContain(ComputerUseCopy.DenylistHeading);
            texts.ShouldContain(ComputerUseCopy.KillChordHeading);
            texts.ShouldContain(ComputerUseCopy.SecurityPhraseHeading);
            texts.ShouldContain(ComputerUseCopy.AuditHeading);
            texts.ShouldContain(ComputerUseCopy.ProviderDisclosure);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}

#pragma warning restore MA0002
