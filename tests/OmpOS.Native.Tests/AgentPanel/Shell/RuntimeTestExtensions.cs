using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Composer;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Small helpers the runtime tests use to drive a composer and watch a runtime's feedback.</summary>
internal static class RuntimeTestExtensions
{
    /// <summary>Sets the draft, sends it, and reports whether the host accepted it (the draft cleared).</summary>
    /// <param name="composer">The composer.</param>
    /// <param name="text">The text to send.</param>
    /// <returns>True when the send was accepted.</returns>
    public static async Task<bool> SendFromAsync(this ComposerViewModel composer, string text)
    {
        composer.Draft = text;
        await composer.SendAsync().ConfigureAwait(true);
        return composer.Draft.Length == 0;
    }

    /// <summary>Calls <paramref name="onChanged"/> every time the runtime's status line text or engine error changes.</summary>
    /// <param name="runtime">The runtime.</param>
    /// <param name="onChanged">The action to call.</param>
    public static void FeedbackChangedForTests(this AgentSessionRuntime runtime, Action onChanged) =>
        runtime.FeedbackChanged += (_, _) => onChanged();
}
