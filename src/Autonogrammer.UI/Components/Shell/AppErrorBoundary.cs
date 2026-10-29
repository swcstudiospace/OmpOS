using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.UI.Components.Shell;

/// <summary>
/// The root error boundary of the web head. When a routed component throws it shows <see cref="ErrorFallback"/> (an alert with a Reload button) instead
/// of a blank page, and it logs the failure through <see cref="ILogger"/> with the exception type only: an exception message or a stack could carry
/// text the user typed, so neither reaches the log or the screen.
/// </summary>
public sealed class AppErrorBoundary : ErrorBoundary
{
    private static readonly Action<ILogger, string, Exception?> ComponentFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "ComponentFailed"),
            "A component failed while rendering ({ExceptionType}); the error fallback is shown");

    [Inject]
    private ILogger<AppErrorBoundary> Logger { get; set; } = default!;

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ComponentFailed(Logger, exception.GetType().Name, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (CurrentException is null)
        {
            builder.AddContent(0, ChildContent);
            return;
        }

        builder.OpenComponent<ErrorFallback>(1);
        builder.CloseComponent();
    }
}
