using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// The container and WSL section of the Remote environments page: its states, the add and edit forms, saving and deleting, the connection test with every finding and
/// its fix, the start of a stopped container, and the rule that nothing contacts podman or wsl.exe until the user asks.
/// </summary>
public sealed class RuntimeEnvironmentsViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Container(string id = "container-devbox", string name = "devbox") => new()
    {
        Id = id,
        DisplayName = "Box " + name,
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile { Container = name, RemoteUser = "dev", WorkspaceFolder = "/workspaces/app" },
    };

    private static EnvironmentProfile Wsl(string id = "wsl-ubuntu") => new()
    {
        Id = id,
        DisplayName = "Ubuntu",
        Kind = EnvironmentKind.Wsl,
        Wsl = new WslProfile { Distro = "Ubuntu-24.04", WorkspacePath = "/home/dev/app" },
    };

    private static EnvironmentProfile Ssh() => new()
    {
        Id = "ssh-box",
        DisplayName = "Ssh box",
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = "box", RemoteWorkspaceRoot = "/w" },
    };

    private static RemotePreflightReport Failure(RemoteDiagnosisKind kind, string summary = "It failed.", string fix = "Fix it this way.") => new()
    {
        Succeeded = false,
        Diagnoses = [new RemoteDiagnosis { Kind = kind, Severity = DiagnosisSeverity.Error, Summary = summary, FixText = fix, Detail = "podman said no" }],
    };

    private static (RuntimeEnvironmentsViewModel Page, FakeRemoteEnvironmentsHost Store, FakeContainerEnvironmentsHost Runtime, FakeContainerLister Lister) Create()
    {
        var store = new FakeRemoteEnvironmentsHost();
        var runtime = new FakeContainerEnvironmentsHost();
        var lister = new FakeContainerLister();
        return (new RuntimeEnvironmentsViewModel(store, runtime, lister), store, runtime, lister);
    }

    /// <summary>Loading with nothing saved is Empty and contacts neither podman nor wsl.exe; saved ssh environments are not listed here.</summary>
    [Fact]
    public async Task LoadingContactsNothingAndListsOnlyContainerAndWsl()
    {
        var (page, store, runtime, lister) = Create();
        page.State.ShouldBe(RemoteEnvironmentsState.Loading);
        _ = store.With(Ssh());

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Empty);
        page.Rows.ShouldBeEmpty();
        runtime.ContainerTests.ShouldBeEmpty();
        runtime.WslTests.ShouldBeEmpty();
        lister.Calls.ShouldBe(0);
    }

    /// <summary>Saved container and WSL environments become rows with their kind in words, destination, folder and last-used time.</summary>
    [Fact]
    public async Task SavedEnvironmentsBecomeRows()
    {
        var (page, store, _, _) = Create();
        _ = store.With(Container(), new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero)).With(Wsl()).With(Ssh());

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Ready);
        page.Rows.Select(row => row.Id).ShouldBe(["container-devbox", "wsl-ubuntu"]);
        var container = page.Rows[0];
        container.KindLabel.ShouldBe("Container");
        container.DestinationText.ShouldBe("devbox as dev");
        container.RootText.ShouldBe("Folder in the container: /workspaces/app");
        container.LastUsedText.ShouldBe("Last used 2026-10-27 09:30 UTC");
        container.StatusLabel.ShouldBe("Not checked");
        container.AccessibleName.ShouldBe("Box devbox, Container, devbox as dev, Not checked");
        var wsl = page.Rows[1];
        wsl.KindLabel.ShouldBe("WSL");
        wsl.DestinationText.ShouldBe("Ubuntu-24.04");
        wsl.RootText.ShouldBe("Folder in the distribution: /home/dev/app");
        wsl.LastUsedText.ShouldBe("Not used yet");
    }

    /// <summary>A load that fails is the Error state with the reason, and does not throw.</summary>
    [Fact]
    public async Task ALoadFailureIsTheErrorState()
    {
        var (page, store, _, _) = Create();
        store.LoadFailure = new IOException("disk gone");

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Error);
        page.LoadError.ShouldContain("disk gone", Case.Sensitive);
    }

    /// <summary>Adding a container by hand saves it, closes the form and selects the new row.</summary>
    [Fact]
    public async Task AddingAContainerSavesAndSelectsIt()
    {
        var (page, store, _, _) = Create();
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.StartAddContainer();
        page.IsEditing.ShouldBeTrue();
        var editor = page.ContainerEditor.ShouldNotBeNull();
        editor.IsValid.ShouldBeFalse();
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeFalse();
        editor.Container = "devbox";
        editor.DisplayName = "Dev box";
        editor.WorkspaceFolder = "/workspaces/app";
        var saved = await page.SaveEditAsync(Token).ConfigureAwait(true);

        saved.ShouldBeTrue();
        page.IsEditing.ShouldBeFalse();
        store.Profiles.ShouldHaveSingleItem().Profile.Kind.ShouldBe(EnvironmentKind.Container);
        page.Rows.ShouldHaveSingleItem();
        page.Selected.ShouldBeSameAs(page.Rows[0]);
        page.Announcement.ShouldBe("Saved Dev box.");
    }

    /// <summary>Adding from the picker opens the pre-filled form and saves the container.</summary>
    [Fact]
    public async Task AddingFromThePickerOpensThePrefilledForm()
    {
        var (page, store, _, lister) = Create();
        lister.Listing = new ContainerListing([new ContainerSummary("id1", "devbox", "registry.example/dev:1", "running", "/home/me/proj")], null);
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.StartAddFromPicker().ShouldBeFalse();
        await page.OpenPickerAsync(Token).ConfigureAwait(true);
        page.Picker.Selected = page.Picker.Items[0];
        page.StartAddFromPicker().ShouldBeTrue();
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        var profile = store.Profiles.ShouldHaveSingleItem().Profile;
        var details = profile.Container.ShouldNotBeNull();
        details.Container.ShouldBe("devbox");
        details.WorkspaceFolder.ShouldBe("/workspaces/proj");
    }

    /// <summary>A WSL environment can be added only where WSL exists; elsewhere the form does not open and the note says why.</summary>
    [Fact]
    public async Task WslCanBeAddedOnlyWhereItExists()
    {
        var (page, store, runtime, _) = Create();
        await page.LoadAsync(Token).ConfigureAwait(true);

        runtime.WslSupported = false;
        page.SupportsWsl.ShouldBeFalse();
        page.WslNote.ShouldContain("Windows only", Case.Sensitive);
        page.StartAddWsl().ShouldBeFalse();
        page.WslEditor.ShouldBeNull();
        page.Announcement.ShouldContain("Windows only", Case.Sensitive);

        runtime.WslSupported = true;
        page.WslNote.ShouldBeEmpty();
        page.StartAddWsl("Ubuntu-24.04").ShouldBeTrue();
        var editor = page.WslEditor.ShouldNotBeNull();
        editor.WorkspacePath = "/home/dev/app";
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        store.Profiles.ShouldHaveSingleItem().Profile.Wsl.ShouldNotBeNull().Distro.ShouldBe("Ubuntu-24.04");
    }

    /// <summary>Editing a row opens the form of its kind with its values; cancelling closes it without saving.</summary>
    [Fact]
    public async Task EditingOpensTheFormOfTheKind()
    {
        var (page, store, _, _) = Create();
        _ = store.With(Container()).With(Wsl());
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.StartEdit(page.Rows[0]);
        page.ContainerEditor.ShouldNotBeNull().Container.ShouldBe("devbox");
        page.WslEditor.ShouldBeNull();
        page.StartEdit(page.Rows[1]);
        page.WslEditor.ShouldNotBeNull().Distro.ShouldBe("Ubuntu-24.04");
        page.ContainerEditor.ShouldBeNull();
        page.CancelEdit();

        page.IsEditing.ShouldBeFalse();
        store.Profiles.Count.ShouldBe(2);
    }

    /// <summary>Deleting removes the row; a failure is announced and does not throw.</summary>
    [Fact]
    public async Task DeletingRemovesTheRow()
    {
        var (page, store, _, _) = Create();
        _ = store.With(Container());
        await page.LoadAsync(Token).ConfigureAwait(true);

        (await page.DeleteAsync(page.Rows[0], Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows.ShouldBeEmpty();
        page.State.ShouldBe(RemoteEnvironmentsState.Empty);
        page.Announcement.ShouldStartWith("Deleted", Case.Sensitive);

        _ = store.With(Container());
        await page.LoadAsync(Token).ConfigureAwait(true);
        store.OperationFailure = new IOException("read-only");
        (await page.DeleteAsync(page.Rows[0], Token).ConfigureAwait(true)).ShouldBeFalse();
        page.Announcement.ShouldContain("read-only", Case.Sensitive);
    }

    /// <summary>A passing test shows Ready with what was found; the container is tested with its own details.</summary>
    [Fact]
    public async Task ATestShowsReadyWithWhatWasFound()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        var row = page.Rows[0];
        row.Status.ShouldBe(RemoteEnvironmentStatus.Ready);
        row.EnvironmentText.ShouldBe("omp 18.12.6, Linux x86_64");
        row.HasDiagnoses.ShouldBeFalse();
        row.IsBusy.ShouldBeFalse();
        runtime.ContainerTests.ShouldHaveSingleItem().Container.ShouldBe("devbox");
    }

    /// <summary>A WSL row is tested with the WSL test, not the container one.</summary>
    [Fact]
    public async Task AWslRowIsTestedAsWsl()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Wsl());
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        runtime.WslTests.ShouldHaveSingleItem().Distro.ShouldBe("Ubuntu-24.04");
        runtime.ContainerTests.ShouldBeEmpty();
    }

    /// <summary>Each failure kind maps to the status that says what to do, with the finding and its fix on the row; only a stopped container offers Start container.</summary>
    [Theory]
    [InlineData(RemoteDiagnosisKind.ContainerNotRunning, RemoteEnvironmentStatus.NotRunning, true)]
    [InlineData(RemoteDiagnosisKind.ContainerMissing, RemoteEnvironmentStatus.NotFound, false)]
    [InlineData(RemoteDiagnosisKind.ContainerRuntimeMissing, RemoteEnvironmentStatus.RuntimeUnavailable, false)]
    [InlineData(RemoteDiagnosisKind.ContainerRuntimeUnavailable, RemoteEnvironmentStatus.RuntimeUnavailable, false)]
    [InlineData(RemoteDiagnosisKind.OmpMissing, RemoteEnvironmentStatus.VersionMismatch, false)]
    [InlineData(RemoteDiagnosisKind.ContainerUserMissing, RemoteEnvironmentStatus.NeedsSetup, false)]
    public async Task EachFailureHasItsStatus(RemoteDiagnosisKind kind, RemoteEnvironmentStatus expected, bool canStart)
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        runtime.ContainerReport = Failure(kind, "Summary text.", "Fix text.");
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        var row = page.Rows[0];
        row.Status.ShouldBe(expected);
        row.CanStartContainer.ShouldBe(canStart);
        var finding = row.Diagnoses.ShouldHaveSingleItem();
        finding.Summary.ShouldBe("Summary text.");
        finding.FixText.ShouldBe("Fix text.");
        finding.SeverityLabel.ShouldBe("Error");
        page.Announcement.ShouldContain(row.StatusLabel, Case.Sensitive);
    }

    /// <summary>A test that throws leaves a runtime-unavailable finding with the container fix and never throws out of the page.</summary>
    [Fact]
    public async Task AThrowingTestBecomesAFinding()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        runtime.Failure = new IOException("pipe broke");
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        var row = page.Rows[0];
        row.Status.ShouldBe(RemoteEnvironmentStatus.RuntimeUnavailable);
        row.Diagnoses.ShouldHaveSingleItem().Summary.ShouldContain("pipe broke", Case.Sensitive);
        row.Diagnoses[0].FixText.ShouldContain("podman ps", Case.Sensitive);
    }

    /// <summary>A second test of a row that is busy does nothing; cancelling leaves the previous outcome.</summary>
    [Fact]
    public async Task ABusyRowIsNotTestedTwiceAndCancelKeepsTheOutcome()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        var gate = new TaskCompletionSource();
        runtime.Gate = gate.Task;
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];

        var first = page.TestAsync(row, Token);
        row.IsBusy.ShouldBeTrue();
        row.Status.ShouldBe(RemoteEnvironmentStatus.Testing);
        row.CanTest.ShouldBeFalse();
        await page.TestAsync(row, Token).ConfigureAwait(true);
        runtime.ContainerTests.Count.ShouldBe(1);
        page.CancelRun();
        await first.ConfigureAwait(true);

        row.IsBusy.ShouldBeFalse();
        row.Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
        page.Announcement.ShouldContain("cancelled", Case.Sensitive);
    }

    /// <summary>Start container runs only for a stopped container, only when the user asks, and the row then asks for a new test.</summary>
    [Fact]
    public async Task StartContainerRunsOnlyWhenOffered()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        runtime.ContainerReport = Failure(RemoteDiagnosisKind.ContainerNotRunning);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];

        (await page.StartContainerAsync(row, Token).ConfigureAwait(true)).ShouldBeFalse();
        runtime.Starts.ShouldBeEmpty();
        await page.TestAsync(row, Token).ConfigureAwait(true);
        row.CanStartContainer.ShouldBeTrue();
        var started = await page.StartContainerAsync(row, Token).ConfigureAwait(true);

        started.ShouldBeTrue();
        runtime.Starts.ShouldHaveSingleItem().Container.ShouldBe("devbox");
        row.Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
        row.HasDiagnoses.ShouldBeFalse();
        row.CanStartContainer.ShouldBeFalse();
        page.Announcement.ShouldContain("Test the environment", Case.Sensitive);
    }

    /// <summary>A start that podman refuses keeps podman's diagnosis on the row; a start that throws is a finding too.</summary>
    [Fact]
    public async Task AFailedStartIsAFinding()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        runtime.ContainerReport = Failure(RemoteDiagnosisKind.ContainerNotRunning);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];
        await page.TestAsync(row, Token).ConfigureAwait(true);

        runtime.StartProblem = new RemoteDiagnosis { Kind = RemoteDiagnosisKind.ContainerMissing, Severity = DiagnosisSeverity.Error, Summary = "Gone.", FixText = "Create it." };
        (await page.StartContainerAsync(row, Token).ConfigureAwait(true)).ShouldBeFalse();
        row.Status.ShouldBe(RemoteEnvironmentStatus.NotFound);
        row.Diagnoses.ShouldHaveSingleItem().Summary.ShouldBe("Gone.");

        runtime.ContainerReport = Failure(RemoteDiagnosisKind.ContainerNotRunning);
        await page.TestAsync(row, Token).ConfigureAwait(true);
        runtime.StartProblem = null;
        runtime.Failure = new IOException("pipe broke");
        (await page.StartContainerAsync(row, Token).ConfigureAwait(true)).ShouldBeFalse();
        row.Diagnoses.ShouldHaveSingleItem().Summary.ShouldContain("pipe broke", Case.Sensitive);
    }

    /// <summary>After a reload a row keeps its test outcome while its target is unchanged and forgets it when the target changes.</summary>
    [Fact]
    public async Task AReloadKeepsTheOutcomeOnlyForAnUnchangedTarget()
    {
        var (page, store, runtime, _) = Create();
        _ = store.With(Container());
        runtime.ContainerReport = Failure(RemoteDiagnosisKind.OmpMissing);
        await page.LoadAsync(Token).ConfigureAwait(true);
        await page.TestAsync(page.Rows[0], Token).ConfigureAwait(true);

        await page.LoadAsync(Token).ConfigureAwait(true);
        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.VersionMismatch);

        await store.SaveProfileAsync(Container() with { Container = new ContainerProfile { Container = "other", WorkspaceFolder = "/workspaces/app" } }, Token).ConfigureAwait(true);
        await page.LoadAsync(Token).ConfigureAwait(true);
        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
    }
}
