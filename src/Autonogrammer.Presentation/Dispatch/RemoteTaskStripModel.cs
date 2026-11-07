namespace Autonogrammer.Presentation.Dispatch;

/// <summary>The Remote task running strip: the controller that started the task, its task text as plain capped text, and whether Stop is offered.</summary>
/// <param name="ControllerName">The controller that started the task.</param>
/// <param name="TaskText">The task text, plain and capped.</param>
/// <param name="CanStop">Whether Stop is offered.</param>
public sealed record RemoteTaskStripModel(string ControllerName, string TaskText, bool CanStop)
{
    /// <summary>Builds the strip for a running remote task. Stop is offered while the task runs.</summary>
    /// <param name="controllerName">The controller that started the task.</param>
    /// <param name="taskText">The task text, which is capped.</param>
    /// <returns>The strip.</returns>
    public static RemoteTaskStripModel From(string controllerName, string taskText)
    {
        ArgumentNullException.ThrowIfNull(controllerName);
        ArgumentNullException.ThrowIfNull(taskText);
        return new RemoteTaskStripModel(controllerName, DispatchText.Plain(taskText), CanStop: true);
    }
}
