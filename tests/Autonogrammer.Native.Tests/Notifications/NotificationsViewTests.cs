using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Notifications;
using Autonogrammer.Presentation.Notifications;
using Avalonia.Automation;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Notifications;

/// <summary>The notification centre view: accessible names, the live region and title-only privacy.</summary>
public sealed class NotificationsViewTests
{
    /// <summary>The centre renders with accessible names and a polite live region. A title-only item shows no summary until Show content is on.</summary>
    [Fact]
    public async Task TheCentreRendersNamedAndHidesTheSummaryUntilShowContent()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var centre = new NotificationCentreModel();
            centre.Add(new NotificationItemModel(
                "1",
                NotificationCentreKind.ApprovalNeeded,
                "Approval needed",
                "Secret summary",
                "session-1",
                TitleOnly: true,
                ApprovalQueueEntryId: "queue-1"));
            var view = new NotificationsView(centre);
            var window = new Window { Content = view, Width = 480, Height = 640 };
            window.Show();
            SettingsTree.SettleSafe(window);

            SettingsTree.Unnamed(view).ShouldBeEmpty();
            AutomationProperties.GetLiveSetting(view.Toast).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(view.Toast).ShouldBe(NotificationCentreModel.LiveRegionName);
            view.Toast.Text.ShouldBe("Approval needed");
            SettingsTree.Texts(view).ShouldContain("Approval needed", StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldNotContain("Secret summary", StringComparer.Ordinal);
            SettingsTree.Named<Button>(view, "queue-1").Content.ShouldBe("queue-1");

            view.ShowContent.IsChecked = true;
            SettingsTree.SettleSafe(window);
            SettingsTree.Texts(view).ShouldContain("Secret summary", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
