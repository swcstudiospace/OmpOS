using Autonogrammer.Contracts.Cowork.Memory;
using Autonogrammer.Contracts.Cowork.Projects;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Scheduling;

namespace Autonogrammer.Web.Demo;

/// <summary>Ready, empty, offline and error fixtures for projects, memory, global instructions and scheduled-task wording.</summary>
public static class DemoCoworkFixtures
{
    /// <summary>A ready project.</summary>
    public static ProjectSyncRecord ReadyProject { get; } = new()
    {
        Id = "project-ready",
        Name = "Ready project",
        Instructions = "Keep answers short.",
    };

    /// <summary>An empty project list.</summary>
    public static IReadOnlyList<ProjectSyncRecord> EmptyProjects { get; } = [];

    /// <summary>The offline sentence.</summary>
    public static string OfflineSentence => CoworkDegradedSentences.Offline;

    /// <summary>The error sentence.</summary>
    public static string ErrorSentence => CoworkDegradedSentences.ApiDisabled;

    /// <summary>Ready global instructions.</summary>
    public static string ReadyInstructions => "Prefer short answers.";

    /// <summary>Empty global instructions.</summary>
    public static string EmptyInstructions => string.Empty;

    /// <summary>A body over the 8 KiB budget. Saving it is refused.</summary>
    public static string OverBudgetInstructions => new string('x', GlobalInstructionsModel.GlobalInstructionsBytes + 1);

    /// <summary>A ready memory entry that names its source session.</summary>
    public static MemoryEntryRecord ReadyMemory { get; } = new()
    {
        Id = "memory-ready",
        Content = "The user prefers short answers.",
        SourceSession = "session-ready",
        CreatedAt = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero),
    };

    /// <summary>An empty memory list.</summary>
    public static IReadOnlyList<MemoryEntryRecord> EmptyMemory { get; } = [];

    /// <summary>A scheduled task the web list describes and does not start.</summary>
    public static SessionTaskModel ScheduledTask { get; } = new()
    {
        Surface = "Cowork",
        ProjectId = "project-ready",
        SchedulePreset = "Daily",
        MissedRunPolicy = "RunOnce",
        NextRun = new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero),
        LastRun = new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
        MissedRuns = 1,
    };
}
