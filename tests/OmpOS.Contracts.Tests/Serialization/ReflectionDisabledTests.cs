using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Serialization;

/// <summary>
/// Proves reflection-based serialization is disabled, so any source-generation
/// gap fails loudly in tests instead of silently working on CoreCLR and
/// breaking under trimming (WASM) or AOT.
/// </summary>
public sealed class ReflectionDisabledTests
{
    [Fact]
    public void PlainObjectSerializationThrowsWhenReflectionDisabled()
    {
        Should.Throw<InvalidOperationException>(() => JsonSerializer.Serialize(new { Value = 42 }));
    }
}
