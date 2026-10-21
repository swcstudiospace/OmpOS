using Bunit;

namespace OmpOS.UI.Tests;

/// <summary>
/// Base context for component tests. Components make their own JS calls (the session scroll module),
/// which return defaults in loose mode instead of failing tests that do not assert on them.
/// </summary>
public abstract class UiTestContext : BunitContext
{
    /// <summary>Creates the context in loose JS interop mode.</summary>
    protected UiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }
}
