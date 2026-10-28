using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// The Remote environments page: its states (loading, empty, ready, failed), adding from the ssh config and by hand, editing, deleting, the connection test with
/// every finding and its fix, the host trust terminal, and the live connection states of a running session. Nothing contacts a host until a test is asked for.
/// </summary>
public sealed class RemoteEnvironmentsViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Profile(string id, string alias, string root = "/home/dev/work") => new()
    {
        Id = id,
        DisplayName = "Box " + id,
        Kind = EnvironmentKind.Ssh,
        Ssh = new SshProfile { Alias = alias, RemoteWorkspaceRoot = root },
    };

    private static RemotePreflightReport Failure(RemoteDiagnosisKind kind, string summary = "It failed.", string fix = "Fix it this way.") => new()
    {
        Succeeded = false,
        Diagnoses = [new RemoteDiagnosis { Kind = kind, Severity = DiagnosisSeverity.Error, Summary = summary, FixText = fix, Detail = "ssh said no" }],
    };

    /// <summary>Before the first load the page is Loading; with nothing saved it is Empty and offers the config hosts; the host is never contacted.</summary>
    [Fact]
    public async Task LoadingWithNothingSavedIsEmptyAndContactsNoHost()
    {
        var host = new FakeRemoteEnvironmentsHost { ConfigAliases = ["alpha", "beta"], ConfigWarnings = ["~/.ssh/other could not be read"] };
        var page = new RemoteEnvironmentsViewModel(host);
        page.State.ShouldBe(RemoteEnvironmentsState.Loading);
        page.IsLoading.ShouldBeTrue();

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Empty);
        page.IsEmpty.ShouldBeTrue();
        page.Rows.ShouldBeEmpty();
        page.Candidates.ShouldBe(["alpha", "beta"]);
        page.CandidateWarnings.ShouldHaveSingleItem();
        page.HasNoCandidates.ShouldBeFalse();
        host.TestCalls.ShouldBe(0);
    }

    /// <summary>Saved environments become rows with their destination, folder and last-used time in words; a saved alias is no longer offered; non-ssh kinds are not listed.</summary>
    [Fact]
    public async Task SavedEnvironmentsBecomeRows()
    {
        var host = new FakeRemoteEnvironmentsHost { ConfigAliases = ["alpha", "beta"], ProfileWarning = "1 stored environment(s) were ignored because they are not valid." }
            .With(new EnvironmentProfile { Id = "a", DisplayName = "Alpha box", Kind = EnvironmentKind.Ssh, Ssh = new SshProfile { Alias = "alpha", User = "dev", Port = 2222, RemoteWorkspaceRoot = "/srv/app" } }, new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero))
            .With(new EnvironmentProfile { Id = "cloud", DisplayName = "Cloud", Kind = EnvironmentKind.Cloud });
        var page = new RemoteEnvironmentsViewModel(host);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Ready);
        var row = page.Rows.ShouldHaveSingleItem();
        row.DestinationText.ShouldBe("dev@alpha:2222");
        row.RootText.ShouldBe(RemoteCopy.RemoteFolderPrefix + "/srv/app");
        row.LastUsedText.ShouldBe("Last used 2026-10-27 09:30 UTC");
        row.Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
        row.StatusLabel.ShouldBe("Not checked");
        row.AccessibleName.ShouldBe("Alpha box, dev@alpha:2222, Not checked");
        page.Candidates.ShouldBe(["beta"]);
        page.Warning.ShouldNotBeNull().ShouldContain("ignored", Case.Sensitive);
    }

    /// <summary>A profile list that cannot be read shows the Error state with the reason, and Try again recovers.</summary>
    [Fact]
    public async Task AFailedLoadShowsTheErrorAndCanBeRetried()
    {
        var host = new FakeRemoteEnvironmentsHost { LoadFailure = new IOException("The disk is unavailable.") };
        var page = new RemoteEnvironmentsViewModel(host);

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Error);
        page.IsError.ShouldBeTrue();
        page.LoadError.ShouldContain(RemoteCopy.LoadFailed, Case.Sensitive);
        page.LoadError.ShouldContain("The disk is unavailable.", Case.Sensitive);

        host.LoadFailure = null;
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.State.ShouldBe(RemoteEnvironmentsState.Empty);
        page.LoadError.ShouldBeEmpty();
    }

    /// <summary>Adding from an ssh config alias opens a fixed-alias form; saving stores the environment, closes the form, selects the row and announces it; the alias leaves the candidates.</summary>
    [Fact]
    public async Task AddingFromTheSshConfigSavesSelectsAndAnnounces()
    {
        var host = new FakeRemoteEnvironmentsHost { ConfigAliases = ["alpha"] };
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.StartAdd("alpha");
        page.IsEditing.ShouldBeTrue();
        page.Editor!.AliasIsFixed.ShouldBeTrue();
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeFalse();
        page.IsEditing.ShouldBeTrue();
        page.Editor.RemoteWorkspaceRoot = "/srv/app";
        var saved = await page.SaveEditAsync(Token).ConfigureAwait(true);

        saved.ShouldBeTrue();
        page.IsEditing.ShouldBeFalse();
        host.Profiles.ShouldHaveSingleItem().Profile.Ssh!.Alias.ShouldBe("alpha");
        page.Rows.ShouldHaveSingleItem();
        page.Selected.ShouldBeSameAs(page.Rows[0]);
        page.Candidates.ShouldBeEmpty();
        page.Announcement.ShouldContain("Saved", Case.Sensitive);
        page.State.ShouldBe(RemoteEnvironmentsState.Ready);
    }

    /// <summary>A hand-entered environment gets an identifier that does not clash with an existing one, and Cancel closes the form without saving.</summary>
    [Fact]
    public async Task AddingByHandAvoidsAnIdentifierClashAndCancelSavesNothing()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("ssh-dev-box", "dev-box"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);

        page.StartAdd(alias: null);
        page.CancelEdit();
        page.IsEditing.ShouldBeFalse();
        host.Profiles.Count.ShouldBe(1);

        page.StartAdd(alias: null);
        page.Editor!.Alias = "dev-box";
        page.Editor.DisplayName = "Second";
        page.Editor.RemoteWorkspaceRoot = "/w";
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        host.Profiles.Select(stored => stored.Profile.Id).ShouldBe(["ssh-dev-box", "ssh-dev-box-2"]);
    }

    /// <summary>Editing opens the form on the stored values; saving replaces the environment and keeps what the row knew about the host while its settings are unchanged.</summary>
    [Fact]
    public async Task EditingReplacesTheEnvironmentAndKeepsTheTestOutcome()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        page.StartEdit(page.Rows[0]);
        page.Editor!.DisplayName = "Renamed";
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows.ShouldHaveSingleItem().DisplayName.ShouldBe("Renamed");
        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.Ready);
        host.TestCalls.ShouldBe(1);
    }

    /// <summary>Changing where an environment points drops the old test outcome; a save that fails keeps the form open and says why.</summary>
    [Fact]
    public async Task ChangingTheHostDropsTheOutcomeAndAFailedSaveKeepsTheForm()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);
        page.StartEdit(page.Rows[0]);
        page.Editor!.RemoteWorkspaceRoot = "/srv/other";

        host.OperationFailure = new IOException("The disk is full.");
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeFalse();
        page.IsEditing.ShouldBeTrue();
        page.Announcement.ShouldContain("The disk is full.", Case.Sensitive);

        host.OperationFailure = null;
        (await page.SaveEditAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
        page.Rows[0].Diagnoses.ShouldBeEmpty();
    }

    /// <summary>Delete removes the row and announces it; a failure is announced and the row stays.</summary>
    [Fact]
    public async Task DeleteRemovesTheRowOrReportsTheFailure()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha")).With(Profile("b", "beta"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var first = page.Rows[0];

        host.OperationFailure = new IOException("Locked.");
        (await page.DeleteAsync(first, Token).ConfigureAwait(true)).ShouldBeFalse();
        page.Rows.Count.ShouldBe(2);
        page.Announcement.ShouldContain("Locked.", Case.Sensitive);

        host.OperationFailure = null;
        (await page.DeleteAsync(first, Token).ConfigureAwait(true)).ShouldBeTrue();

        page.Rows.Select(row => row.Id).ShouldBe(["b"]);
        page.Announcement.ShouldContain("Deleted", Case.Sensitive);
        (await page.DeleteAsync(first, Token).ConfigureAwait(true)).ShouldBeFalse();
        page.Announcement.ShouldContain("already gone", Case.Sensitive);
    }

    /// <summary>A passing test shows Ready with the omp version and platform; a failing one shows the status of its first error and every finding with its fix, severity in words.</summary>
    [Fact]
    public async Task ATestShowsTheOutcomeAndEveryFindingWithItsFix()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];

        await page.TestConnectionAsync(row, Token).ConfigureAwait(true);
        row.Status.ShouldBe(RemoteEnvironmentStatus.Ready);
        row.IsGood.ShouldBeTrue();
        row.EnvironmentText.ShouldBe("omp 18.12.6, Linux x86_64");
        row.HasDiagnoses.ShouldBeFalse();
        page.Announcement.ShouldContain("Ready", Case.Sensitive);

        host.Report = Failure(RemoteDiagnosisKind.OmpTooOld, "omp is too old.", "Update omp on the host.");
        await page.TestConnectionAsync(row, Token).ConfigureAwait(true);

        row.Status.ShouldBe(RemoteEnvironmentStatus.VersionMismatch);
        row.IsError.ShouldBeTrue();
        row.IsGood.ShouldBeFalse();
        row.StatusLabel.ShouldBe(RemoteStatusDescriber.LabelOf(RemoteEnvironmentStatus.VersionMismatch));
        var finding = row.Diagnoses.ShouldHaveSingleItem();
        finding.SeverityLabel.ShouldBe("Error");
        finding.Summary.ShouldBe("omp is too old.");
        finding.FixText.ShouldBe("Update omp on the host.");
        finding.HasDetail.ShouldBeTrue();
        finding.AccessibleText.ShouldContain("Update omp on the host.", Case.Sensitive);
        row.CanTrustHost.ShouldBeFalse();
    }

    /// <summary>The row says Testing while the test runs, cannot be tested twice at once, and the test can be cancelled, which keeps the previous outcome.</summary>
    [Fact]
    public async Task ATestInProgressShowsTestingAndCanBeCancelled()
    {
        var gate = new TaskCompletionSource();
        var host = new FakeRemoteEnvironmentsHost { TestGate = gate.Task }.With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];

        var run = page.TestConnectionAsync(row, Token);
        row.Status.ShouldBe(RemoteEnvironmentStatus.Testing);
        row.IsTesting.ShouldBeTrue();
        row.CanTest.ShouldBeFalse();
        await page.TestConnectionAsync(row, Token).ConfigureAwait(true);
        host.TestCalls.ShouldBe(1);
        page.CancelTest();
        await run.ConfigureAwait(true);

        row.Status.ShouldBe(RemoteEnvironmentStatus.NotChecked);
        row.CanTest.ShouldBeTrue();
        page.Announcement.ShouldContain("cancelled", Case.Sensitive);
    }

    /// <summary>A test that throws still ends with a finding on the row that says to check ssh by hand, and the model does not throw.</summary>
    [Fact]
    public async Task ATestThatThrowsBecomesAFindingOnTheRow()
    {
        var host = new FakeRemoteEnvironmentsHost { OperationFailure = new InvalidOperationException("ssh is broken") }.With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.Unreachable);
        var finding = page.Rows[0].Diagnoses.ShouldHaveSingleItem();
        finding.Summary.ShouldContain("ssh is broken", Case.Sensitive);
        finding.FixText.ShouldContain("ssh", Case.Sensitive);
    }

    /// <summary>An unknown host key offers the trust terminal; opening it announces what to do next; without a terminal the page says to run ssh by hand; the host sees the right profile.</summary>
    [Fact]
    public async Task AnUnknownHostKeyOffersTheTrustTerminal()
    {
        var host = new FakeRemoteEnvironmentsHost { Report = Failure(RemoteDiagnosisKind.HostKeyUnknown) }.With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];

        await page.TestConnectionAsync(row, Token).ConfigureAwait(true);
        row.Status.ShouldBe(RemoteEnvironmentStatus.HostKeyUnconfirmed);
        row.CanTrustHost.ShouldBeTrue();
        (await page.TrustHostAsync(row, Token).ConfigureAwait(true)).ShouldBeTrue();
        page.Announcement.ShouldBe(RemoteCopy.TrustHostOpened);
        host.HasTerminal = false;
        (await page.TrustHostAsync(row, Token).ConfigureAwait(true)).ShouldBeFalse();

        page.Announcement.ShouldBe(RemoteCopy.TrustHostUnavailable);
        host.TrustRequests.Count.ShouldBe(2);
        host.TrustRequests[0].Alias.ShouldBe("alpha");
    }

    /// <summary>A changed host key offers nothing but the explanation: no trust terminal.</summary>
    [Fact]
    public async Task AChangedHostKeyOffersNoShortcut()
    {
        var host = new FakeRemoteEnvironmentsHost { Report = Failure(RemoteDiagnosisKind.HostKeyChanged) }.With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);

        await page.TestConnectionAsync(page.Rows[0], Token).ConfigureAwait(true);

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.HostKeyChanged);
        page.Rows[0].CanTrustHost.ShouldBeFalse();
    }

    /// <summary>The live state of a session overrides the test outcome: connecting, signing in, connected, reconnecting with the attempt, and a lost connection with an interrupted turn; clearing it shows the test outcome again.</summary>
    [Fact]
    public async Task TheLiveConnectionStateOverridesTheTestOutcome()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var row = page.Rows[0];
        await page.TestConnectionAsync(row, Token).ConfigureAwait(true);

        page.SetConnectionState("a", RemoteConnectionState.Connecting).ShouldBeTrue();
        row.Status.ShouldBe(RemoteEnvironmentStatus.Connecting);
        page.ReportAuthenticating("a").ShouldBeTrue();
        row.Status.ShouldBe(RemoteEnvironmentStatus.Authenticating);
        page.SetConnectionState("a", RemoteConnectionState.Connected);
        row.Status.ShouldBe(RemoteEnvironmentStatus.Connected);
        row.IsGood.ShouldBeTrue();
        page.SetConnectionState("a", RemoteConnectionState.Reconnecting, reconnectAttempt: 2);
        row.Status.ShouldBe(RemoteEnvironmentStatus.Reconnecting);
        row.IsWarning.ShouldBeTrue();
        row.StatusDetail.ShouldContain("attempt 2", Case.Sensitive);
        page.SetConnectionState("a", RemoteConnectionState.Unreachable, turnInterrupted: true);
        row.Status.ShouldBe(RemoteEnvironmentStatus.Interrupted);
        row.StatusDetail.ShouldContain("send the prompt again", Case.Sensitive);
        page.Announcement.ShouldContain("Disconnected, turn interrupted", Case.Sensitive);
        page.ClearConnectionState("a").ShouldBeTrue();

        row.Status.ShouldBe(RemoteEnvironmentStatus.Ready);
        page.SetConnectionState("missing", RemoteConnectionState.Connected).ShouldBeFalse();
        page.ReportAuthenticating("missing").ShouldBeFalse();
        page.ClearConnectionState("missing").ShouldBeFalse();
    }

    /// <summary>A reload keeps the live state and a running test of the rows that are still there.</summary>
    [Fact]
    public async Task AReloadKeepsTheLiveState()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        await page.LoadAsync(Token).ConfigureAwait(true);
        page.SetConnectionState("a", RemoteConnectionState.Connected);
        page.Selected = page.Rows[0];

        await page.LoadAsync(Token).ConfigureAwait(true);

        page.Rows[0].Status.ShouldBe(RemoteEnvironmentStatus.Connected);
        page.Selected.ShouldBeSameAs(page.Rows[0]);
        host.LoadCalls.ShouldBe(2);
    }

    /// <summary>The page announces changes through property notifications the views bind to.</summary>
    [Fact]
    public async Task StateChangesAreAnnounced()
    {
        var host = new FakeRemoteEnvironmentsHost().With(Profile("a", "alpha"));
        var page = new RemoteEnvironmentsViewModel(host);
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await page.LoadAsync(Token).ConfigureAwait(true);
        page.StartAdd(alias: null);

        changed.ShouldContain("State", StringComparer.Ordinal);
        changed.ShouldContain("IsReady", StringComparer.Ordinal);
        changed.ShouldContain("Candidates", StringComparer.Ordinal);
        changed.ShouldContain("Editor", StringComparer.Ordinal);
        changed.ShouldContain("IsEditing", StringComparer.Ordinal);
    }
}
