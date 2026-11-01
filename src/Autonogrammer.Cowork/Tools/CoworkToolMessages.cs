namespace Autonogrammer.Cowork.Tools;

/// <summary>Fixed, secret-free text the model reads. None of these strings include arguments or exception messages.</summary>
public static class CoworkToolMessages
{
    /// <summary>The model reads this when a person did not approve, the ask timed out, or no window could show it.</summary>
    public const string NotApproved = "The tool call was not approved.";

    /// <summary>The model reads this when the tool is not offered.</summary>
    public const string NotOffered = "The tool is not offered.";

    /// <summary>The model reads this when the session lease is gone.</summary>
    public const string SessionMissing = "The Cowork session is not open.";

    /// <summary>The model reads this when arguments are not a JSON object within 256 KiB.</summary>
    public const string ArgumentsInvalid = "The tool arguments are not a JSON object within the size cap.";

    /// <summary>The model reads this when a provider throws. The exception message is never included.</summary>
    public const string ProviderFailed = "The tool call failed.";

    /// <summary>The model reads this when the provider exceeds the call timeout.</summary>
    public const string TimedOut = "The tool call timed out.";

    /// <summary>The model reads this when the caller cancels the call.</summary>
    public const string Cancelled = "The tool call was cancelled.";
}
