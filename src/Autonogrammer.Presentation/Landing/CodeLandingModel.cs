using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.Presentation.Observable;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Presentation.Landing;

/// <summary>
/// The Code landing: four environment cards, in the order <see cref="CodeLandingCardKind"/> lists them, shown when no session is selected. Each card
/// is drawn from the window's <see cref="CodeLandingInputs"/> and from the environments of the <see cref="NewSessionTargetModel"/> the picker uses, so a
/// card and the picker never disagree about an environment. A button raises <see cref="ActionRequested"/>; the window does the work.
/// </summary>
public sealed class CodeLandingModel : ObservableModel
{
    private readonly NewSessionTargetModel _targets;
    private CodeLandingInputs _inputs;
    private IReadOnlyList<CodeLandingCard> _cards;

    /// <summary>Initializes a landing over the picker's environments. Nothing is checked yet: the local status is pending and the remote list is loading.</summary>
    /// <param name="targets">The environment picker model the window keeps in step with the saved environments.</param>
    /// <exception cref="ArgumentNullException"><paramref name="targets"/> is null.</exception>
    public CodeLandingModel(NewSessionTargetModel targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _targets = targets;
        _inputs = new CodeLandingInputs(LocalOmpStatus.Pending, RemoteLoadState.Loading, null, null);
        _cards = Build(_inputs, targets.Options);
        ((INotifyCollectionChanged)targets.Options).CollectionChanged += OnEnvironmentsChanged;
        targets.PropertyChanged += OnTargetPropertyChanged;
    }

    /// <summary>Occurs when a card button asks the window to act.</summary>
    public event EventHandler<CodeLandingActionEventArgs>? ActionRequested;

    /// <summary>Gets the cards in display and tab order: this computer, a linked machine, cloud, remote.</summary>
    public IReadOnlyList<CodeLandingCard> Cards => _cards;

    /// <summary>Sets the status of omp on this computer.</summary>
    /// <param name="status">The status.</param>
    /// <exception cref="ArgumentNullException"><paramref name="status"/> is null.</exception>
    public void SetLocal(LocalOmpStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Update(_inputs with { Local = status });
    }

    /// <summary>Sets whether the remote environments have been read.</summary>
    /// <param name="state">The state.</param>
    public void SetRemote(RemoteLoadState state) => Update(_inputs with { Remote = state });

    /// <summary>Sets the account state, or null when the window has no account host.</summary>
    /// <param name="account">The state.</param>
    public void SetAccount(AccountHostState? account) => Update(_inputs with { Account = account });

    /// <summary>Sets the machines linked to the account, or null when that list is not available in this window.</summary>
    /// <param name="names">The names of the machines.</param>
    public void SetLinkedMachines(IReadOnlyList<string>? names) => Update(_inputs with { LinkedMachines = names });

    /// <summary>Raises <see cref="ActionRequested"/>.</summary>
    /// <param name="action">What to do.</param>
    /// <param name="optionId">The environment the action starts on, or null.</param>
    public void Request(CodeLandingAction action, string? optionId = null) =>
        ActionRequested?.Invoke(this, new CodeLandingActionEventArgs(action, optionId));

    /// <summary>Maps the inputs and the environments to the four cards. It is pure: the same inputs always give the same cards.</summary>
    /// <param name="inputs">What the landing draws from.</param>
    /// <param name="options">The picker's options, in display order.</param>
    /// <returns>The cards, in <see cref="CodeLandingCardKind"/> order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inputs"/> or <paramref name="options"/> is null.</exception>
    public static IReadOnlyList<CodeLandingCard> Build(CodeLandingInputs inputs, IReadOnlyList<NewSessionTargetOption> options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);
        var environments = options.Where(static option => option.Kind is SessionTargetKind.Ssh or SessionTargetKind.Container or SessionTargetKind.Wsl).ToList();
        return
        [
            BuildLocal(inputs.Local),
            BuildLinked(inputs.Account, inputs.LinkedMachines),
            BuildCloud(inputs.Account),
            BuildRemote(inputs.Remote, environments),
        ];
    }

    private static CodeLandingCard BuildLocal(LocalOmpStatus local)
    {
        var (state, message, omp, actions) = LocalState(local);
        var card = Card(CodeLandingCardKind.LocalMachine, state, CodeLandingCopy.LocalTitle, message, omp, actions, rows: []);
        return WithPrimary(card, enabled: true, CodeLandingAction.StartOnEnvironment, NewSessionTargetModel.LocalId);
    }

    private static (CodeLandingCardState State, string Message, string Omp, List<CodeLandingButton> Actions) LocalState(LocalOmpStatus local)
    {
        const string title = CodeLandingCopy.LocalTitle;
        switch (local.State)
        {
            case LocalOmpState.Installed:
                return (CodeLandingCardState.Ready, CodeLandingCopy.LocalReady, OmpInstalled(local.Version, local.Source ?? LocalOmpSource.Path), []);
            case LocalOmpState.NotInstalled:
                return (CodeLandingCardState.Unavailable, CodeLandingCopy.LocalNeedsOmp, Format(CodeLandingCopy.OmpNotInstalledFormat, local.Detail ?? string.Empty), []);
            case LocalOmpState.Failed:
                return (
                    CodeLandingCardState.Failed,
                    CodeLandingCopy.LocalFailed,
                    Format(CodeLandingCopy.OmpCheckFailedFormat, local.Detail ?? string.Empty),
                    [NextAction(CodeLandingAction.RetryOmpCheck, CodeLandingCopy.Retry, title)]);
            case LocalOmpState.NotChecked:
                return (CodeLandingCardState.Unavailable, CodeLandingCopy.LocalNotChecked, CodeLandingCopy.OmpNotChecked, []);
            default:
                return (CodeLandingCardState.Checking, CodeLandingCopy.LocalChecking, CodeLandingCopy.OmpChecking, []);
        }
    }

    private static CodeLandingCard BuildLinked(AccountHostState? account, IReadOnlyList<string>? names)
    {
        const string title = CodeLandingCopy.LinkedTitle;
        const CodeLandingCardKind kind = CodeLandingCardKind.LinkedMachine;
        if (account is not { Configured: true } configured)
        {
            return Disabled(kind, title, CodeLandingCardState.NotConfigured, AccountCopy.NotConfigured, []);
        }

        if (configured.Phase != AuthPhase.SignedIn)
        {
            return Disabled(kind, title, CodeLandingCardState.SignedOut, CodeLandingCopy.LinkedSignedOut, [NextAction(CodeLandingAction.SignIn, CodeLandingCopy.SignIn, title)]);
        }

        if (configured.Offline)
        {
            return Disabled(kind, title, CodeLandingCardState.Offline, AccountCopy.OfflineNotice, []);
        }

        if (names is null)
        {
            return Disabled(kind, title, CodeLandingCardState.Unavailable, DispatchCopy.NotConfigured, []);
        }

        if (names.Count == 0)
        {
            return Disabled(kind, title, CodeLandingCardState.Empty, CodeLandingCopy.LinkedEmpty, []);
        }

        var rows = names.Select(static name => new CodeLandingButton(name, name, IsEnabled: false, Action: null)).ToList();
        return Disabled(kind, title, CodeLandingCardState.Ready, CodeLandingCopy.LinkedListed, [], rows);
    }

    private static CodeLandingCard BuildCloud(AccountHostState? account)
    {
        const string title = CodeLandingCopy.CloudTitle;
        const CodeLandingCardKind kind = CodeLandingCardKind.Cloud;
        if (account is not { Configured: true } configured)
        {
            return Disabled(kind, title, CodeLandingCardState.NotConfigured, AccountCopy.NotConfigured, []);
        }

        if (configured.Phase != AuthPhase.SignedIn)
        {
            return Disabled(kind, title, CodeLandingCardState.SignedOut, CodeLandingCopy.CloudSignedOut, [NextAction(CodeLandingAction.SignIn, CodeLandingCopy.SignIn, title)]);
        }

        if (configured.Offline)
        {
            return Disabled(kind, title, CodeLandingCardState.Offline, AccountCopy.OfflineNotice, []);
        }

        var card = Card(kind, CodeLandingCardState.Ready, title, CodeLandingCopy.CloudReady, CodeLandingCopy.OmpNotChecked, [], rows: []);
        return WithPrimary(card, enabled: true, CodeLandingAction.OpenCloud, optionId: null);
    }

    private static CodeLandingCard BuildRemote(RemoteLoadState remote, List<NewSessionTargetOption> environments)
    {
        const string title = CodeLandingCopy.RemoteTitle;
        const CodeLandingCardKind kind = CodeLandingCardKind.Remote;
        var manage = NextAction(CodeLandingAction.ManageRemote, CodeLandingCopy.ManageRemote, title);
        switch (remote)
        {
            case RemoteLoadState.Failed:
                return Card(kind, CodeLandingCardState.Failed, title, RemoteCopy.LoadFailed, CodeLandingCopy.OmpNotChecked, [NextAction(CodeLandingAction.RetryRemoteLoad, CodeLandingCopy.Retry, title)], rows: []);
            case RemoteLoadState.Loaded when environments.Count == 0:
                return Card(kind, CodeLandingCardState.Empty, title, RemoteCopy.EmptyHeading, CodeLandingCopy.OmpNotChecked, [manage], rows: []);
            case RemoteLoadState.Loaded:
                return Card(kind, CodeLandingCardState.Ready, title, CodeLandingCopy.RemoteReady, CodeLandingCopy.OmpNotChecked, [manage], environments.Select(EnvironmentButton).ToList());
            default:
                return Card(kind, CodeLandingCardState.Checking, title, RemoteCopy.Loading, CodeLandingCopy.OmpNotChecked, [], rows: []);
        }
    }

    private static CodeLandingButton EnvironmentButton(NewSessionTargetOption option)
    {
        var blocked = option.Status is { } status && RemoteStatusDescriber.BlocksStart(status);
        return new CodeLandingButton(option.Label, option.AccessibleName, option.IsAvailable && !blocked, CodeLandingAction.StartOnEnvironment, option.Id);
    }

    private static CodeLandingCard Disabled(
        CodeLandingCardKind kind,
        string title,
        CodeLandingCardState state,
        string message,
        List<CodeLandingButton> actions,
        List<CodeLandingButton>? rows = null)
    {
        var card = Card(kind, state, title, message, CodeLandingCopy.OmpNotChecked, actions, rows ?? []);
        return WithPrimary(card, enabled: false, action: null, optionId: null);
    }

    private static CodeLandingCard Card(
        CodeLandingCardKind kind,
        CodeLandingCardState state,
        string title,
        string message,
        string omp,
        List<CodeLandingButton> actions,
        List<CodeLandingButton> rows) =>
        new(kind, state, title, message, omp, null, actions, rows);

    private static CodeLandingCard WithPrimary(CodeLandingCard card, bool enabled, CodeLandingAction? action, string? optionId) =>
        card with { Primary = new CodeLandingButton(card.Title, card.AccessibleName, enabled, action, optionId) };

    private static CodeLandingButton NextAction(CodeLandingAction action, string label, string title) =>
        new(label, Format(CodeLandingCopy.ActionNameFormat, label, title), IsEnabled: true, action);

    private static string OmpInstalled(string? version, LocalOmpSource source)
    {
        var from = source switch
        {
            LocalOmpSource.Managed => CodeLandingCopy.SourceManaged,
            LocalOmpSource.Configured => CodeLandingCopy.SourceConfigured,
            _ => CodeLandingCopy.SourcePath,
        };
        return version is null
            ? Format(CodeLandingCopy.OmpFoundFormat, from)
            : Format(CodeLandingCopy.OmpInstalledFormat, version, from);
    }

    private static string Format(string format, params string[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, format, arguments);

    private void Update(CodeLandingInputs next)
    {
        _inputs = next;
        Rebuild();
    }

    private void OnEnvironmentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _cards = Build(_inputs, _targets.Options);
        NotifyPropertyChanged(nameof(Cards));
    }
}
