using System.Reflection;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Network and Keyboard copy and the two new sections: every text exists, and the sections keep their place in the navigation.</summary>
public sealed class NetworkAndKeyboardCopyTests
{
    private static IEnumerable<(string Name, string? Text)> Texts(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string?)field.GetRawConstantValue()));

    /// <summary>Every text constant of the Network and Keyboard copy is non-empty.</summary>
    [Theory]
    [InlineData(typeof(NetworkCopy))]
    [InlineData(typeof(KeyboardCopy))]
    public void NoTextIsEmpty(Type copy)
    {
        var texts = Texts(copy).ToArray();

        texts.Length.ShouldBeGreaterThan(8);
        foreach (var (name, text) in texts)
        {
            string.IsNullOrWhiteSpace(text).ShouldBeFalse($"{name} must not be empty");
        }
    }

    /// <summary>Network and Keyboard come after Plugins and keep their numbers; every earlier member keeps its own.</summary>
    [Fact]
    public void TheNewSectionsFollowThePluginsSection()
    {
        ((int)SettingsSection.Plugins).ShouldBe(5);
        ((int)SettingsSection.Commands).ShouldBe(8);
        ((int)SettingsSection.Network).ShouldBe(9);
        ((int)SettingsSection.Keyboard).ShouldBe(10);
        ((int)SettingsSection.Hooks).ShouldBe(11);
        ((int)SettingsSection.Ignore).ShouldBe(20);
        ((int)SettingsSection.Automations).ShouldBe(21);
        Enum.GetValues<SettingsSection>().Distinct().Count().ShouldBe(22);
    }

    /// <summary>The navigation titles of the new pages differ from every other title.</summary>
    [Fact]
    public void TheNavigationTitlesAreDistinct()
    {
        string[] titles =
        [
            SettingsCopy.RuntimeTitle,
            SettingsCopy.GeneralTitle,
            SettingsCopy.AgentsTitle,
            SettingsCopy.TabTitle,
            SettingsCopy.PluginsTitle,
            NetworkCopy.Title,
            KeyboardCopy.Title,
        ];

        titles.Distinct(StringComparer.Ordinal).Count().ShouldBe(titles.Length);
    }

    /// <summary>The window model keeps the order it is given, with Network then Keyboard after Plugins, and rejects a repeated section.</summary>
    [Fact]
    public void TheWindowKeepsNetworkThenKeyboardAfterPlugins()
    {
        var window = new SettingsWindowModel(
        [
            new PluginsPage(new ScriptedPluginsSource()),
            new NetworkSettingsPage(new ScriptedNetworkSource()),
            new KeyboardSettingsPage(new ScriptedKeyboardSource()),
        ]);

        window.Pages.Select(page => page.Section).ToArray().ShouldBe([SettingsSection.Plugins, SettingsSection.Network, SettingsSection.Keyboard]);
        window.HasUnsavedChanges.ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new SettingsWindowModel(
        [
            new NetworkSettingsPage(new ScriptedNetworkSource()),
            new NetworkSettingsPage(new ScriptedNetworkSource()),
        ]));
    }

    /// <summary>One load makes the two new pages ready and, for the Network page, sends nothing.</summary>
    [Fact]
    public async Task OneLoadMakesTheNewPagesReadyWithoutARun()
    {
        var network = new ScriptedNetworkSource();
        var window = new SettingsWindowModel([new NetworkSettingsPage(network), new KeyboardSettingsPage(new ScriptedKeyboardSource())]);

        await window.LoadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        window.Pages.OfType<SettingsPageBase>().ShouldAllBe(page => page.State == SettingsPageState.Ready);
        network.RunCount.ShouldBe(0);
    }

    /// <summary>Each status has its own word, and the messages hold no address.</summary>
    [Fact]
    public void EveryStatusHasItsOwnWordAndAMessage()
    {
        var statuses = Enum.GetValues<EndpointStatus>();

        statuses.Select(NetworkCopy.StatusLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(statuses.Length);
        foreach (var status in statuses)
        {
            var message = NetworkCopy.CheckMessage(status, status == EndpointStatus.Reachable ? 204 : null);
            string.IsNullOrWhiteSpace(message).ShouldBeFalse(status.ToString());
            message.ShouldNotContain("://", Case.Sensitive);
        }

        NetworkCopy.CheckMessage(EndpointStatus.Reachable, 503).ShouldBe("The endpoint answered with HTTP 503.");
        NetworkCopy.Explanation.ShouldContain("one unauthenticated request", Case.Sensitive);
        NetworkCopy.LatencyText(TimeSpan.FromMilliseconds(1234.6)).ShouldBe("1235 ms");
    }
}
