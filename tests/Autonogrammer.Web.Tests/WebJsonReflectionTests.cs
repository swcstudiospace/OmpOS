using System.Text.Json;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Reflection-off JSON behaviour the web head depends on (Q-WEB-REFLECTION).</summary>
public sealed class WebJsonReflectionTests
{
    /// <summary>The head's source-generated options refuse an unregistered type rather than reflecting over it.</summary>
    [Fact]
    public void HeadOptionsRejectUnregisteredPlainType()
    {
        var options = WebJsonContext.Default.Options;

        Should.Throw<NotSupportedException>(() => JsonSerializer.Serialize(new PlainPayload { Name = "x" }, options));
    }

    /// <summary>Options without a type resolver throw when the repo-wide reflection switch is off.</summary>
    [Fact]
    public void UnconfiguredOptionsThrowInvalidOperationWhenReflectionIsOff()
    {
        var options = new JsonSerializerOptions();

        Should.Throw<InvalidOperationException>(() => JsonSerializer.Serialize(new PlainPayload { Name = "x" }, options));
    }

    /// <summary>A plain type that no context registers.</summary>
    private sealed class PlainPayload
    {
        public string Name { get; set; } = string.Empty;
    }
}
