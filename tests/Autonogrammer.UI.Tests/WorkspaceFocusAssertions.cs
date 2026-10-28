using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;

namespace Autonogrammer.UI.Tests;

/// <summary>Reads the focus calls a component made through <c>ElementReference.FocusAsync</c>, in order, so a test can say where the focus went.</summary>
internal static class WorkspaceFocusAssertions
{
    /// <summary>The JavaScript identifier Blazor uses for <c>ElementReference.FocusAsync</c>.</summary>
    internal const string FocusInterop = "Blazor._internal.domWrapper.focus";

    /// <summary>
    /// Reads the element reference id bUnit printed for an element rendered with <c>@ref</c>. The id is only in the markup until the owning component renders again
    /// (Blazor does not copy it to the frames of a re-render), so read it straight after the first render and compare it with the argument of a later focus call.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The reference id.</returns>
    internal static string InitialReferenceId(this IElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var id = element.GetAttribute("blazor:elementReference");
        id.ShouldNotBeNullOrEmpty("the reference id is only in the markup right after the first render");
        return id;
    }

    /// <summary>Returns the element reference id a JS interop call carried as its first argument.</summary>
    /// <param name="call">The recorded call.</param>
    /// <returns>The reference id.</returns>
    internal static string ReferenceIdOf(this JSRuntimeInvocation call)
    {
        return call.Arguments[0].ShouldBeOfType<ElementReference>().Id;
    }

    /// <summary>Returns every focus call so far, oldest first.</summary>
    /// <param name="interop">The JS interop of the test context.</param>
    /// <returns>The calls; the first argument of each is the element reference that took the focus.</returns>
    internal static List<JSRuntimeInvocation> FocusCalls(this BunitJSInterop interop) =>
        interop.Invocations.Where(static call => string.Equals(call.Identifier, FocusInterop, StringComparison.Ordinal)).ToList();
}
