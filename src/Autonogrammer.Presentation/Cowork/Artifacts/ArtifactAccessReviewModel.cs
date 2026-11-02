using System.Globalization;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The Access review for one version (D-26, D-27). Focus starts on Deny. Escape chooses Deny. A timeout chooses Deny and
/// approves nothing, so no review is ever approved by waiting. The first answer is final.
/// </summary>
public sealed class ArtifactAccessReviewModel : ObservableModel
{
    /// <summary>The reason label the dialog shows.</summary>
    public const string ReasonLabel = "Written by the agent";

    /// <summary>The warning the dialog shows above the choices.</summary>
    public const string WarningText = "Anything you type into this artifact can be sent to these hosts. Requests are made from your device.";

    /// <summary>The banner text that opens the review.</summary>
    public const string BannerText = "This artifact asks to contact other hosts.";

    /// <summary>The answer to a storage request. Storage is never granted (D-27).</summary>
    public const string StorageAnswer = "This artifact asked to store data; storage is not available";

    private readonly TimeProvider _clock;
    private readonly DateTimeOffset _deadline;
    private ArtifactAccessDecision? _decision;

    /// <summary>Opens a review for the origins one version declares.</summary>
    /// <param name="version">The version the review binds to.</param>
    /// <param name="origins">The declared origins, at most five.</param>
    /// <param name="clock">The clock that measures the timeout.</param>
    /// <param name="timeout">How long the review waits before it denies.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public ArtifactAccessReviewModel(ArtifactVersionKey version, IReadOnlyList<string> origins, TimeProvider clock, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(clock);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive.");
        }

        Version = version;
        RequestedOrigins = origins;
        Origins = origins.Select(DescribeOrigin).ToList();
        _clock = clock;
        _deadline = clock.GetUtcNow() + timeout;
    }

    /// <summary>Gets the version the review binds to.</summary>
    public ArtifactVersionKey Version { get; }

    /// <summary>Gets the origins in both forms, in declaration order.</summary>
    public IReadOnlyList<ArtifactOriginView> Origins { get; }

    /// <summary>Gets the origins the artifact declared, as text.</summary>
    public IReadOnlyList<string> RequestedOrigins { get; }

    /// <summary>Gets the choice that holds focus when the dialog opens. It is always Deny.</summary>
    public static ArtifactAccessChoice FocusedChoice => ArtifactAccessChoice.Deny;

    /// <summary>Gets the instant after which the review denies.</summary>
    public DateTimeOffset Deadline => _deadline;

    /// <summary>Gets the final decision, or null while the review is open.</summary>
    public ArtifactAccessDecision? Decision => _decision;

    /// <summary>Records a button choice. The first decision is final; later calls return it unchanged.</summary>
    /// <param name="choice">The answer.</param>
    /// <returns>The final decision.</returns>
    public ArtifactAccessDecision Choose(ArtifactAccessChoice choice) =>
        Decide(choice, choice == ArtifactAccessChoice.Deny ? "Denied" : "Approved");

    /// <summary>Handles Escape. Escape chooses Deny.</summary>
    /// <returns>The final decision.</returns>
    public ArtifactAccessDecision Escape() => Decide(ArtifactAccessChoice.Deny, "Escape");

    /// <summary>Denies the review if its deadline has passed. Nothing is approved by timeout.</summary>
    /// <returns>The timeout decision, or null when the deadline has not passed or the review was already decided.</returns>
    public ArtifactAccessDecision? ExpireIfDue()
    {
        if (_decision is not null || _clock.GetUtcNow() < _deadline)
        {
            return null;
        }

        return Decide(ArtifactAccessChoice.Deny, "Timed out");
    }

    /// <summary>Answers a storage request. The answer never grants storage.</summary>
    /// <param name="permission">The requested permission name.</param>
    /// <returns>The storage answer for "storage", or null for other permissions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="permission"/> is null.</exception>
    public static string? AnswerPermission(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return string.Equals(permission, "storage", StringComparison.Ordinal) ? StorageAnswer : null;
    }

    /// <summary>
    /// Describes one origin: its scheme and port are kept, and its host is shown in ASCII and decoded forms with the mixed-script mark.
    /// A value that is not an absolute URI is described as a bare host.
    /// </summary>
    /// <param name="origin">The origin as the artifact declared it.</param>
    /// <returns>The display form.</returns>
    private static ArtifactOriginView DescribeOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.IdnHost))
        {
            return ArtifactHostNames.Describe(origin);
        }

        var host = ArtifactHostNames.Describe(uri.IdnHost);
        var prefix = uri.Scheme + "://";
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return new ArtifactOriginView(prefix + host.Ascii + port, prefix + host.Decoded + port, host.MixedScript);
    }

    private ArtifactAccessDecision Decide(ArtifactAccessChoice choice, string reason)
    {
        if (_decision is not null)
        {
            return _decision;
        }

        IReadOnlyList<string> approved = choice == ArtifactAccessChoice.Deny ? [] : RequestedOrigins;
        _decision = new ArtifactAccessDecision(choice, Version, approved, reason);
        NotifyPropertyChanged(nameof(Decision));
        return _decision;
    }
}
