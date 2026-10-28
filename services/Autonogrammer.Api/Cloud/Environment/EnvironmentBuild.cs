namespace Autonogrammer.Api.Cloud;

/// <summary>The <c>build</c> member of an environment file.</summary>
/// <param name="Dockerfile">The Dockerfile path relative to the repository root, or null.</param>
/// <param name="Context">The build context path relative to the repository root, or null.</param>
internal sealed record EnvironmentBuild(string? Dockerfile, string? Context);
