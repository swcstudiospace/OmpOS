using System.Security.Cryptography;
using Autonogrammer.Native.Views.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The desktop Mermaid renderer reads the vendored bundle from the Native.Views manifest (D-25, OQ-400). The bytes must be the pinned
/// Mermaid 12.2.0 file, read raw so no text decoding can change them, and an assembly without the resource reports its absence.
/// </summary>
public sealed class EmbeddedMermaidAssetProviderTests
{
    /// <summary>The SHA-256 of the unmodified <c>dist/mermaid.min.js</c> of mermaid 12.2.0 (lib/mermaid/VERSION.txt).</summary>
    private const string PinnedSha256 = "a8a6a6f805b074a8261a2825c86f99c8054acb3b35f50bb7e24a4f9f2c3c3b9f";

    /// <summary>The embedded resource is present and is exactly the pinned file.</summary>
    [Fact]
    public void TheEmbeddedBundleIsThePinnedFile()
    {
        using var stream = typeof(EmbeddedMermaidAssetProvider).Assembly.GetManifestResourceStream(EmbeddedMermaidAssetProvider.ResourceName);

        stream.ShouldNotBeNull("mermaid.min.js must be an EmbeddedResource of Autonogrammer.Native.Views");
        Convert.ToHexStringLower(SHA256.HashData(stream)).ShouldBe(PinnedSha256);
    }

    /// <summary>The provider returns the script text of the embedded bundle.</summary>
    [Fact]
    public void TheProviderReturnsTheBundleText()
    {
        var script = new EmbeddedMermaidAssetProvider().GetScript();

        script.ShouldNotBeNullOrEmpty();
        script.ShouldContain("mermaid", Case.Insensitive);
    }

    /// <summary>An assembly without the resource yields no script, which makes the Mermaid document the source-view fallback.</summary>
    [Fact]
    public void AnAssemblyWithoutTheResourceYieldsNoScript()
    {
        new EmbeddedMermaidAssetProvider(typeof(string).Assembly).GetScript().ShouldBeNull();
    }
}
