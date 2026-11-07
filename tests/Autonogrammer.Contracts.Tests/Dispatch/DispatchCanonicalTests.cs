using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>Hub and device canonical bytes agree on the shared vector file.</summary>
public sealed class DispatchCanonicalTests
{
    /// <summary>Every vector builds the recorded bytes, and parsing them yields the same fields.</summary>
    [Fact]
    public void VectorsMatchRecordedBytes()
    {
        var document = JsonDocument.Parse(File.ReadAllText(VectorFile()));
        document.RootElement.GetProperty("note").GetString()!.ShouldContain("MUST", Case.Sensitive);
        var count = 0;
        foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            count++;
            var input = new DispatchCanonicalInput(
                vector.GetProperty("v").GetString()!,
                vector.GetProperty("commandId").GetString()!,
                vector.GetProperty("seq").GetString()!,
                vector.GetProperty("userId").GetString()!,
                vector.GetProperty("deviceId").GetString()!,
                vector.GetProperty("capability").GetString()!,
                vector.GetProperty("op").GetString()!,
                vector.GetProperty("issuedAt").GetString()!,
                vector.GetProperty("expiresAt").GetString()!,
                vector.GetProperty("nonce").GetString()!,
                vector.GetProperty("payloadSha256").GetString()!,
                vector.GetProperty("kid").GetString()!);
            var built = DispatchCanonical.Build(input);
            var expected = Encoding.UTF8.GetBytes(vector.GetProperty("canonical").GetString()!);
            built.ShouldBe(expected);
            var parsed = DispatchCanonical.Parse(built);
            parsed.ShouldBe(input);
            var mutated = built.ToArray();
            mutated[0] ^= 0x01;
            mutated.ShouldNotBe(expected);
        }

        count.ShouldBeGreaterThanOrEqualTo(4);
    }

    /// <summary>A signed value outside the charset is refused before bytes are built and before they are parsed.</summary>
    [Fact]
    public void CharsetIsRefusedBothDirections()
    {
        var input = Sample() with { Nonce = "bad nonce" };
        Should.Throw<ArgumentOutOfRangeException>(() => DispatchCanonical.Build(input));
        var bytes = DispatchCanonical.Build(Sample());
        var text = Encoding.UTF8.GetString(bytes).Replace("nonce=abc-DEF_12", "nonce=bad nonce", StringComparison.Ordinal);
        Should.Throw<ArgumentException>(() => DispatchCanonical.Parse(Encoding.UTF8.GetBytes(text)));
        DispatchCanonical.IsSignedValue("a/b").ShouldBeFalse();
        DispatchCanonical.IsSignedValue("a+b").ShouldBeFalse();
        DispatchCanonical.IsSignedValue("").ShouldBeFalse();
    }

    private static DispatchCanonicalInput Sample() => new(
        "1",
        "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        "1",
        "user:acct",
        "dev.one",
        "sessions",
        "session.list",
        "1710000000",
        "1710000300",
        "abc-DEF_12",
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "hub.key-1");

    private static string VectorFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Autonogrammer.Contracts.Tests", "Dispatch", "TestData", "dispatch-canonical-vectors.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "Dispatch", "TestData", "dispatch-canonical-vectors.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("dispatch-canonical-vectors.json");
    }
}
