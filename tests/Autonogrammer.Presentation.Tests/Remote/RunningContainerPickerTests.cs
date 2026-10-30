using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The "add from a running container" picker: states, the rows with their markers in words, the pre-filled form, and failures that never throw.</summary>
public sealed class RunningContainerPickerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContainerSummary Running(string name, string? folder = null) => new("id-" + name, name, "registry.example/dev:1", "running", folder);

    private static EnvironmentProfile Saved(string id, string container) => new()
    {
        Id = id,
        DisplayName = container,
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = container, WorkspaceFolder = "/w" },
    };

    /// <summary>The picker is Idle until it is asked; asking lists the running containers only, in order, and an exited one is not offered.</summary>
    [Fact]
    public async Task ItListsTheRunningContainers()
    {
        var lister = new FakeContainerLister { Listing = new ContainerListing([Running("devbox", "/home/me/proj"), new ContainerSummary("id-old", "old", "i", "exited", null), Running("db")], null) };
        var picker = new RunningContainerPicker(lister);
        picker.State.ShouldBe(RunningContainerPickerState.Idle);

        await picker.RefreshAsync([], Token).ConfigureAwait(true);

        picker.State.ShouldBe(RunningContainerPickerState.Ready);
        picker.IsReady.ShouldBeTrue();
        picker.Items.Select(item => item.Name).ShouldBe(["devbox", "db"]);
        picker.Announcement.ShouldBe("2 running container(s).");
        lister.Calls.ShouldBe(1);
    }

    /// <summary>A dev container and an already added container are marked in words, and the row reads out its name and detail.</summary>
    [Fact]
    public async Task MarkersAreWordsNotColours()
    {
        var picker = new RunningContainerPicker(new FakeContainerLister { Listing = new ContainerListing([Running("devbox", "/home/me/proj"), Running("db")], null) });

        await picker.RefreshAsync([Saved("container-devbox", "devbox")], Token).ConfigureAwait(true);

        var devbox = picker.Items[0];
        devbox.IsDevContainer.ShouldBeTrue();
        devbox.AlreadyAdded.ShouldBeTrue();
        devbox.Detail.ShouldBe("registry.example/dev:1, dev container, already added");
        devbox.AccessibleName.ShouldBe("devbox. registry.example/dev:1, dev container, already added");
        picker.Items[1].Detail.ShouldBe("registry.example/dev:1");
        picker.Items[1].IsDevContainer.ShouldBeFalse();
    }

    /// <summary>With no running container the picker is Empty and says that Autonogrammer does not start containers.</summary>
    [Fact]
    public async Task NoRunningContainerIsEmpty()
    {
        var picker = new RunningContainerPicker(new FakeContainerLister());

        await picker.RefreshAsync([], Token).ConfigureAwait(true);

        picker.State.ShouldBe(RunningContainerPickerState.Empty);
        picker.IsEmpty.ShouldBeTrue();
        picker.StateText.ShouldContain("does not start containers", Case.Sensitive);
        picker.Items.ShouldBeEmpty();
    }

    /// <summary>A diagnosis from the lister becomes the Error state with the summary and the fix; a thrown exception does too.</summary>
    [Fact]
    public async Task FailuresBecomeTheErrorState()
    {
        var diagnosis = new RemoteDiagnosis { Kind = RemoteDiagnosisKind.ContainerRuntimeMissing, Severity = DiagnosisSeverity.Error, Summary = "podman is not installed.", FixText = "Install podman." };
        var lister = new FakeContainerLister { Listing = new ContainerListing([], diagnosis) };
        var picker = new RunningContainerPicker(lister);

        await picker.RefreshAsync([], Token).ConfigureAwait(true);

        picker.State.ShouldBe(RunningContainerPickerState.Error);
        picker.IsError.ShouldBeTrue();
        picker.StateText.ShouldBe("podman is not installed.");
        picker.Problem.ShouldNotBeNull().FixText.ShouldBe("Install podman.");

        lister.Failure = new IOException("boom");
        await picker.RefreshAsync([], Token).ConfigureAwait(true);

        picker.State.ShouldBe(RunningContainerPickerState.Error);
        picker.StateText.ShouldContain("boom", Case.Sensitive);
        picker.Items.ShouldBeEmpty();
    }

    /// <summary>A cancelled refresh keeps the previous list.</summary>
    [Fact]
    public async Task ACancelledRefreshKeepsTheList()
    {
        var lister = new FakeContainerLister { Listing = new ContainerListing([Running("devbox")], null) };
        var picker = new RunningContainerPicker(lister);
        await picker.RefreshAsync([], Token).ConfigureAwait(true);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await picker.RefreshAsync([], cancelled.Token).ConfigureAwait(true);

        picker.State.ShouldBe(RunningContainerPickerState.Ready);
        picker.Items.ShouldHaveSingleItem();
    }

    /// <summary>Adding opens a form pre-filled with the container and, for a dev container, the devcontainer default folder; nothing selected gives no form.</summary>
    [Fact]
    public async Task AddingPrefillsTheForm()
    {
        var picker = new RunningContainerPicker(new FakeContainerLister { Listing = new ContainerListing([Running("devbox", "/home/me/my proj"), Running("db")], null) });
        await picker.RefreshAsync([Saved("container-devbox", "other")], Token).ConfigureAwait(true);
        picker.CanAdd.ShouldBeFalse();
        picker.BeginAdd().ShouldBeNull();

        picker.Selected = picker.Items[0];
        var dev = picker.BeginAdd().ShouldNotBeNull();
        picker.Selected = picker.Items[1];
        var plain = picker.BeginAdd().ShouldNotBeNull();

        picker.CanAdd.ShouldBeTrue();
        dev.Container.ShouldBe("devbox");
        dev.DisplayName.ShouldBe("devbox");
        dev.WorkspaceFolder.ShouldBe("/workspaces/my proj");
        dev.IsValid.ShouldBeTrue();
        plain.Container.ShouldBe("db");
        plain.WorkspaceFolder.ShouldBeEmpty();
        plain.IsValid.ShouldBeFalse();
        plain.Issues.ShouldContain(issue => issue.Field == "WorkspaceFolder");
    }

    /// <summary>The new profile gets an identifier that is not taken, and the selection survives a refresh while the container is still listed.</summary>
    [Fact]
    public async Task TheIdentifierIsUniqueAndTheSelectionSurvivesARefresh()
    {
        var picker = new RunningContainerPicker(new FakeContainerLister { Listing = new ContainerListing([Running("devbox", "/home/me/proj")], null) });
        await picker.RefreshAsync([Saved("container-devbox", "elsewhere")], Token).ConfigureAwait(true);
        picker.Selected = picker.Items[0];

        await picker.RefreshAsync([Saved("container-devbox", "elsewhere")], Token).ConfigureAwait(true);
        picker.Selected.ShouldNotBeNull().Name.ShouldBe("devbox");
        var editor = picker.BeginAdd().ShouldNotBeNull();
        editor.TryBuild(out var profile).ShouldBeTrue();

        profile.ShouldNotBeNull().Id.ShouldBe("container-devbox-2");
    }
}
