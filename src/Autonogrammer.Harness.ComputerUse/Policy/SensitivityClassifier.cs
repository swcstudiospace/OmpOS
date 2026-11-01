using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Pure classifier over host-observed facts. No focused-element traits.</summary>
public static class SensitivityClassifier
{
    private static readonly string[] CredentialPatterns =
    [
        "sign in",
        "log in",
        "login",
        "password",
        "passcode",
        "one-time code",
        "token",
        "secret",
        "unlock",
    ];

    private static readonly string[] PurchasePatterns =
    [
        "checkout",
        "payment",
        "pay now",
        "order",
        "subscribe",
        "billing",
    ];

    private static readonly string[] DeletionPatterns =
    [
        "delete file",
        "move to trash",
        "recycle bin",
        "permanently delete",
    ];

    /// <summary>Classifies one host-observed action.</summary>
    /// <param name="title">Window title, or <see langword="null"/>.</param>
    /// <param name="raisedCategory">Mail or messaging.</param>
    /// <param name="actionClass">Action class.</param>
    /// <param name="autoApproveTyping">Grant flag. Ignored for credentials, Dispatch and non-typing.</param>
    /// <param name="firstActionForApp">First action in this application.</param>
    /// <param name="origin">Local or Dispatch.</param>
    /// <param name="isFileDeletion">Host flag for file deletion when the title does not say so.</param>
    /// <returns>The first matching reason, or <see cref="SensitivityReason.None"/>.</returns>
    public static SensitivityReason Classify(
        string? title,
        bool raisedCategory,
        ComputerActionClass actionClass,
        bool autoApproveTyping,
        bool firstActionForApp,
        ComputerActionOrigin origin,
        bool isFileDeletion)
    {
        if (ContainsAny(title, CredentialPatterns))
        {
            return SensitivityReason.CredentialTitle;
        }

        if (isFileDeletion || ContainsAny(title, DeletionPatterns))
        {
            return SensitivityReason.FileDeletion;
        }

        if (ContainsAny(title, PurchasePatterns))
        {
            return SensitivityReason.Purchase;
        }

        if (raisedCategory && !ComputerGrantPolicy.Allows(ComputerGrantTier.ViewOnly, actionClass))
        {
            return SensitivityReason.RaisedCategory;
        }

        if (actionClass == ComputerActionClass.Clipboard)
        {
            return SensitivityReason.Clipboard;
        }

        if (origin == ComputerActionOrigin.Dispatch)
        {
            return SensitivityReason.DispatchOrigin;
        }

        if (firstActionForApp)
        {
            return SensitivityReason.FirstAction;
        }

        if (actionClass is ComputerActionClass.Type or ComputerActionClass.Chord && autoApproveTyping)
        {
            return SensitivityReason.None;
        }

        if (actionClass is ComputerActionClass.Type or ComputerActionClass.Chord)
        {
            return SensitivityReason.FirstAction;
        }

        return SensitivityReason.None;
    }

    private static bool ContainsAny(string? title, string[] patterns)
    {
        if (string.IsNullOrEmpty(title))
        {
            return false;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            if (title.Contains(patterns[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
