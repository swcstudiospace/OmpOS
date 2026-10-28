namespace Autonogrammer.Sandbox;

/// <summary>
/// Verification state of one Railway GraphQL operation used by <see cref="RailwayCloudEnvironmentClient"/>.
/// </summary>
/// <param name="Operation">The operation name, for example <c>serviceCreate</c>.</param>
/// <param name="Verified"><see langword="true"/> when a documented example shows the operation and the fields used.</param>
/// <param name="Source">The documentation URL, or <c>UNVERIFIED: introspection needed</c> with the unverified detail.</param>
public sealed record RailwayOperationStatus(string Operation, bool Verified, string Source);
