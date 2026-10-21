namespace OmpOS.Workflows.Worker;

/// Marker type for the OmpOS.Workflows.Worker assembly (layer L3).
/// Internal: the worker host is an executable, so its API is not referenced
/// from outside the assembly (public Temporal workflow/activity types are the
/// deliberate exception — the SDK instantiates them).
internal static class AssemblyMarker
{
}
