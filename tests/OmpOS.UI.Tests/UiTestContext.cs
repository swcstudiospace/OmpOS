using System.Diagnostics.CodeAnalysis;
using Bunit;

namespace OmpOS.UI.Tests;

/// <summary>
/// Base context for component tests. Components make their own JS calls (the session scroll module),
/// which return defaults in loose mode instead of failing tests that do not assert on them.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public test classes derive from this base; the xUnit test discovery and the derived classes require consistent accessibility.")]
public abstract class UiTestContext : BunitContext
{
    /// <summary>Creates the context in loose JS interop mode.</summary>
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Records a send and reports that the host accepted it, as the Composer's OnSend contract requires.</summary>
    /// <param name="sent">The messages the host has received.</param>
    /// <param name="text">The submitted draft.</param>
    /// <returns>A completed task whose result is true.</returns>
    protected static Task<bool> AcceptAsync(ICollection<string> sent, string text)
    {
        sent.Add(text);
        return Task.FromResult(true);
    }

    /// <summary>Records a send and reports that the host refused it, so the Composer keeps the draft.</summary>
    /// <param name="sent">The messages the host has received.</param>
    /// <param name="text">The submitted draft.</param>
    /// <returns>A completed task whose result is false.</returns>
    protected static Task<bool> RefuseAsync(ICollection<string> sent, string text)
    {
        sent.Add(text);
        return Task.FromResult(false);
    }
}
