using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Cowork.Sessions;

/// <summary>One Cowork task as the local registry stores it. It is not a wire summary.</summary>
/// <param name="Id">The task identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Status">The stored lifecycle. Waiting for a decision is not a value of this file.</param>
public sealed record CoworkRegistryRow(string Id, string Title, CoworkSessionStatus Status);
