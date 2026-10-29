using Microsoft.AspNetCore.Components;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>A component that fails while it initializes, with a message the user must never see.</summary>
public sealed class ThrowingComponent : ComponentBase
{
    /// <summary>The exception message, standing for text that could hold a secret.</summary>
    public const string Message = "SECRET-DETAIL-do-not-show";

    /// <inheritdoc />
    protected override void OnInitialized() => throw new InvalidOperationException(Message);
}
