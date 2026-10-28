namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /settings/runtime</c>: the effective limits and options of the API as read-only values. It holds no secret, no connection string and no path.</summary>
public sealed record RuntimeSettingsResponse
{
    /// <summary>Gets the harness adapter name (<c>InMemory</c> or <c>Acp</c>).</summary>
    public required string HarnessAdapter { get; init; }

    /// <summary>Gets the session store name (<c>InMemory</c> or <c>Postgres</c>).</summary>
    public required string SessionStore { get; init; }

    /// <summary>Gets the coordination store name (<c>InMemory</c> or <c>Dragonfly</c>).</summary>
    public required string CoordinationStore { get; init; }

    /// <summary>Gets how long a run lease lasts without renewal, in seconds.</summary>
    public required double RunLeaseSeconds { get; init; }

    /// <summary>Gets how often the owner of a run lease renews it, in seconds.</summary>
    public required double RunLeaseRenewSeconds { get; init; }

    /// <summary>Gets the largest accepted prompt, in characters.</summary>
    public required int MaxPromptLength { get; init; }

    /// <summary>Gets the largest accepted request body, in bytes.</summary>
    public required long MaxRequestBodyBytes { get; init; }

    /// <summary>Gets the prompt submissions allowed per minute.</summary>
    public required int PromptsPerMinute { get; init; }

    /// <summary>Gets the session creations allowed per minute.</summary>
    public required int SessionsPerMinute { get; init; }

    /// <summary>Gets the workspace and settings reads allowed per minute.</summary>
    public required int WorkspaceRequestsPerMinute { get; init; }

    /// <summary>Gets a value indicating whether the API has a workspace folder.</summary>
    public required bool WorkspaceConfigured { get; init; }

    /// <summary>Gets a value indicating whether the user directories of the API process home are read.</summary>
    public required bool IncludesUserScope { get; init; }

    /// <summary>Gets the largest file <c>GET /workspace/file</c> returns, in bytes.</summary>
    public required int MaxWorkspaceFileBytes { get; init; }
}
