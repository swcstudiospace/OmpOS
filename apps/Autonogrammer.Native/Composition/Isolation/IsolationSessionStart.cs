using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Isolation;
using Autonogrammer.Presentation.Isolation;

namespace Autonogrammer.Native.Composition.Isolation;

/// <summary>What one session start should do with isolation.</summary>
/// <param name="Decision">The planner decision.</param>
/// <param name="Chip">The chip the Isolation page shows.</param>
/// <param name="ProcessWrapper">The wrapper for <c>AcpClientOptions</c>, or null for Code and for an unanswered probe.</param>
/// <param name="FilesystemIsolated">The level handed to <c>CoworkPermissionResolver</c> as the Skip gate.</param>
/// <param name="SessionMayStart">False while the continue-without-isolation question is unanswered.</param>
/// <param name="Grants">The isolation grants. A read-only grant has no write bind.</param>
internal sealed record IsolationSessionStart(
    IsolationDecision Decision,
    IsolationChipModel Chip,
    IProcessLaunchWrapper? ProcessWrapper,
    bool FilesystemIsolated,
    bool SessionMayStart,
    IReadOnlyList<IsolationGrant> Grants);
