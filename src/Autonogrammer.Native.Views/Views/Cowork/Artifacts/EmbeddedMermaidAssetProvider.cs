using System.Reflection;
using System.Text;
using Autonogrammer.Cowork.Artifacts.Rendering;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// Supplies the vendored Mermaid bundle from a manifest resource of an assembly (D-25). The resource is found by a fixed logical
/// name. When the resource is absent, <see cref="GetScript"/> returns null and the Mermaid document reports
/// <see cref="Autonogrammer.Presentation.Cowork.Artifacts.ArtifactSurfaceReason.MermaidAssetMissing"/>.
/// </summary>
public sealed class EmbeddedMermaidAssetProvider : IMermaidAssetProvider
{
    /// <summary>The logical name of the manifest resource that carries <c>mermaid.min.js</c>.</summary>
    public const string ResourceName = "mermaid.min.js";

    private readonly Assembly _assembly;

    /// <summary>Creates the provider over the Native.Views assembly.</summary>
    public EmbeddedMermaidAssetProvider()
        : this(typeof(EmbeddedMermaidAssetProvider).Assembly)
    {
    }

    /// <summary>Creates the provider over an assembly. Tests use an assembly that has no such resource.</summary>
    /// <param name="assembly">The assembly whose manifest is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
    public EmbeddedMermaidAssetProvider(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
    }

    /// <inheritdoc />
    public string? GetScript()
    {
        using var stream = _assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        return reader.ReadToEnd();
    }
}
