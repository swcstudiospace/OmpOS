using System.Reflection;
using Autonogrammer.Presentation.Notifications;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Notifications;

/// <summary>The in-app centre: toast, list, live region, privacy and the approval-queue link.</summary>
public sealed class NotificationCentreModelTests
{
    /// <summary>The centre keeps a toast and a newest-first list, with kind, title, session and the privacy flag, and announces the title.</summary>
    [Fact]
    public void TheCentreKeepsAToastAndANewestFirstList()
    {
        var centre = new NotificationCentreModel();
        centre.Add(Item("1", NotificationCentreKind.RunFinished, "First", "notes", "session-a", titleOnly: true, approvalId: null));
        centre.Add(Item("2", NotificationCentreKind.Waiting, "Second", "later", "session-b", titleOnly: false, approvalId: null));

        centre.Toast.ShouldNotBeNull().Id.ShouldBe("2");
        centre.LiveRegion.ShouldBe("Second");
        centre.Items.Select(item => item.Id).ShouldBe(["2", "1"]);
        centre.Items[0].Kind.ShouldBe(NotificationCentreKind.Waiting);
        centre.Items[0].Title.ShouldBe("Second");
        centre.Items[0].SessionId.ShouldBe("session-b");
        centre.Items[0].TitleOnly.ShouldBeFalse();
        centre.Items[1].TitleOnly.ShouldBeTrue();
        centre.Rows[0].VisibleSummary.ShouldBeNull();
        centre.ShowContent = true;
        centre.Rows[0].VisibleSummary.ShouldBe("later");
        centre.Rows[1].VisibleSummary.ShouldBe("notes");
    }

    /// <summary>An approval-needed item links to the queue entry by id. The link target does not carry the title or the summary.</summary>
    [Fact]
    public void ApprovalNeededLinksToTheQueueEntryByIdOnly()
    {
        const string title = "Approve the deploy";
        const string summary = "rm -rf the staging tree";
        var item = Item("n1", NotificationCentreKind.ApprovalNeeded, title, summary, "session-9", titleOnly: true, approvalId: "queue-9");

        item.ApprovalLinkTarget.ShouldBe("queue-9");
        item.ApprovalLinkTarget.ShouldNotContain(title);
        item.ApprovalLinkTarget.ShouldNotContain(summary);

        var centre = new NotificationCentreModel();
        centre.Add(item);
        centre.Rows[0].ApprovalLinkTarget.ShouldBe("queue-9");
        centre.Rows[0].ApprovalLinkName.ShouldBe("queue-9");
        centre.Rows[0].AccessibleName.ShouldNotContain(summary);
        centre.Rows[0].AccessibleName.ShouldNotContain("queue-9");
    }

    /// <summary>Presentation does not reference Harness.Notifications.</summary>
    [Fact]
    public void PresentationDoesNotReferenceHarnessNotifications()
    {
        var names = typeof(NotificationCentreModel).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);
        names.ShouldNotContain("Autonogrammer.Harness.Notifications");
        typeof(NotificationItemModel).Namespace.ShouldNotBeNull().ShouldNotContain("Harness.Notifications");
    }

    private static NotificationItemModel Item(
        string id,
        NotificationCentreKind kind,
        string title,
        string summary,
        string sessionId,
        bool titleOnly,
        string? approvalId) =>
        new(id, kind, title, summary, sessionId, titleOnly, approvalId);
}
