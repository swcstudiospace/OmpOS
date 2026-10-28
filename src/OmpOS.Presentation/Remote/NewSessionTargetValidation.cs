namespace OmpOS.Presentation.Remote;

/// <summary>Whether the current choice of the New session picker can start a session, and the sentence that says why not or what to expect.</summary>
/// <param name="IsValid">Whether a session can start on the chosen target.</param>
/// <param name="Message">One plain sentence: the reason it cannot start, a caution, or that it is ready.</param>
public sealed record NewSessionTargetValidation(bool IsValid, string Message);
