namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>How a <c>devcontainer.json</c> says its container comes into being. Autonogrammer never builds or starts a dev container; this only tells the user what the definition does.</summary>
public enum DevContainerBuildKind
{
    /// <summary>The definition names no image, build or compose file.</summary>
    None,

    /// <summary>The container is created from an image (<c>image</c>).</summary>
    Image,

    /// <summary>The container is built from a build file (<c>build</c>, or the legacy build-file property).</summary>
    Build,

    /// <summary>The container is a service of a compose project (the compose-file property).</summary>
    Compose,
}
