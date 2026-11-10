using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Autonogrammer.UI.Components.Shell;

/// <summary>
/// The root error boundary of the web head. When a routed component throws it shows <see cref="ErrorFallback"/> (an alert with a Reload button) instead
/// of a blank page, and it logs the failure through <see cref="ILogger"/> with the exception type only. A JavaScript interop failure also logs the
/// frames of its stack, never its message: an interop message can quote what the page passed (a selector, a JSON input), and the first line of a
/// JavaScript stack repeats that message, so only the lines that begin with "at " are kept. Neither the message nor the screen carries that text.
/// </summary>
public sealed class AppErrorBoundary : ErrorBoundary
{
    private const int MaxStackFrames = 40;

    private static readonly Action<ILogger, string, Exception?> ComponentFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, "ComponentFailed"),
            "A component failed while rendering ({ExceptionType}); the error fallback is shown");

    private static readonly Action<ILogger, string, string, Exception?> InteropFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2, "InteropFailed"),
            "A JavaScript interop call failed while rendering ({ExceptionType}); the error fallback is shown. Stack frames: {StackFrames}");

    [Inject]
    private ILogger<AppErrorBoundary> Logger { get; set; } = default!;

    /// <inheritdoc />
    protected override Task OnErrorAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (IsInteropFailure(exception))
        {
            InteropFailed(Logger, exception.GetType().Name, FrameLines(exception.StackTrace), null);
        }
        else
        {
            ComponentFailed(Logger, exception.GetType().Name, null);
        }

        return Task.CompletedTask;
    }

    /// <summary>Tells a JavaScript interop failure from any other exception. Only the former also logs its stack frames.</summary>
    /// <param name="exception">The exception the boundary caught.</param>
    /// <returns>True when the exception is a <see cref="JSException"/>.</returns>
    internal static bool IsInteropFailure(Exception exception) => exception is JSException;

    /// <summary>Keeps the frame lines of a stack trace, at most 40 of them, and joins them into one line.</summary>
    /// <param name="stackTrace">The stack trace, or null.</param>
    /// <returns>The frames separated by " | ", or an empty string.</returns>
    internal static string FrameLines(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
        {
            return string.Empty;
        }

        var frames = stackTrace
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("at ", StringComparison.Ordinal))
            .Take(MaxStackFrames);
        return string.Join(" | ", frames);
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
