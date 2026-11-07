namespace Autonogrammer.Harness.ComputerUse.Tools;

/// <summary>Facts for <see cref="ComputerToolProvider.ShouldRegisterFor"/>. The model does not supply these.</summary>
public sealed record ComputerRegistrationQuery
{
    /// <summary>True for a Cowork-surface session.</summary>
    public required bool CoworkSurface { get; init; }

    /// <summary>True for Code mode.</summary>
    public bool CodeMode { get; init; }

    /// <summary>True for a code.prompt session. Always refuses registration.</summary>
    public bool CodePrompt { get; init; }

    /// <summary>True for an API session.</summary>
    public bool Api { get; init; }

    /// <summary>True for a web session.</summary>
    public bool Web { get; init; }

    /// <summary>True for a cloud worker.</summary>
    public bool CloudWorker { get; init; }

    /// <summary>Tool classes from cowork.task.payload.toolClasses.</summary>
    public required IReadOnlyList<string> ToolClasses { get; init; }

    /// <summary>Key <c>Cowork:ComputerUse:Enabled</c>.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Key <c>Cowork:ComputerUse:AllowDispatchSessions</c>.</summary>
    public required bool AllowDispatchSessions { get; init; }
}
