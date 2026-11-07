using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Dispatch.Desktop.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

public sealed class VerifierTests : IDisposable
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_710_000_000);

    private readonly DispatchTestSigner _signer = new();
    private readonly ManualTimeProvider _clock = new(Start);
    private readonly DispatchKeyRing _ring;
    private readonly DispatchVerifier _verifier;

    public VerifierTests()
    {
        _ring = new DispatchKeyRing([_signer.PublicKey]);
        _verifier = new DispatchVerifier(_ring, DispatchTestSigner.AccountId, DispatchTestSigner.DeviceId, _clock);
    }

    public void Dispose()
    {
        _ring.Dispose();
        _signer.Dispose();
    }

    [Fact]
    public void EveryOracleVectorBuildsTheSameBytesAsTheHub()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "dispatch-canonical-vectors.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var vectors = document.RootElement.GetProperty("vectors").EnumerateArray().ToList();
        vectors.Count.ShouldBeGreaterThanOrEqualTo(4);

        foreach (var vector in vectors)
        {
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

            System.Text.Encoding.UTF8.GetString(DispatchCanonical.Build(input))
                .ShouldBe(vector.GetProperty("canonical").GetString(), vector.GetProperty("name").GetString());
        }
    }

    [Fact]
    public void ASignedFrameWithinItsWindowIsAccepted()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300);

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Accept);
    }

    [Fact]
    public void AForgedSignatureIsRefused()
    {
        using var attacker = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, signer: attacker);

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.SignatureInvalid));
    }

    [Fact]
    public void ATamperedPayloadIsRefused()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300);
        var tampered = frame with { Payload = DispatchTestSigner.Base64Url("{\"text\":\"rm -rf\"}"u8.ToArray()) };

        _verifier.Verify(tampered, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.PayloadMismatch));
    }

    [Fact]
    public void AnExpiredFrameIsRefusedByTheHubClockCorrectedForOffset()
    {
        var expired = _signer.Sign(1, Start.ToUnixTimeSeconds() - 400, Start.ToUnixTimeSeconds() - 31);
        _verifier.Verify(expired, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.Expired));

        // Inside the 30 second skew the frame still verifies.
        var withinSkew = _signer.Sign(2, Start.ToUnixTimeSeconds() - 400, Start.ToUnixTimeSeconds() - 29);
        _verifier.Verify(withinSkew, TimeSpan.Zero).ShouldBe(DispatchVerdict.Accept);

        // A hub clock 60 seconds ahead of this device makes the same frame expired.
        _verifier.Verify(withinSkew, TimeSpan.FromSeconds(60)).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.Expired));
    }

    [Fact]
    public void AFrameForAnotherUserIsRefused()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, userId: "acct_other");

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.WrongUser));
    }

    [Fact]
    public void AFrameForAnotherDeviceIsRefused()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, deviceId: "device_other");

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.WrongDevice));
    }

    [Fact]
    public void AKidNotDeliveredAtPairingIsRefused()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, kid: "hub.key-9");

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.UnknownKid));
    }

    [Theory]
    [InlineData("cmd 1")]
    [InlineData("cmd/1")]
    [InlineData("cmd\n1")]
    public void AValueOutsideTheSignedCharsetIsRefusedBeforeAnyBytesAreBuilt(string commandId)
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300) with { CommandId = commandId };

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.Malformed));
    }

    [Theory]
    [InlineData("browser_use")]
    [InlineData("computer_use")]
    [InlineData("computer.click")]
    public void ANonCommandCapabilityOrUnknownOperationIsRefused(string capability)
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, capability: capability);

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.Malformed));
    }

    [Fact]
    public void AnUnknownOperationIsRefusedEvenWhenSigned()
    {
        var frame = _signer.Sign(1, Start.ToUnixTimeSeconds(), Start.ToUnixTimeSeconds() + 300, op: "computer.click");

        _verifier.Verify(frame, TimeSpan.Zero).ShouldBe(DispatchVerdict.Refuse(DispatchRefusal.Malformed));
    }

    [Fact]
    public void AKeyThatIsNotAPublicKeyIsRefusedAtPairing()
    {
        Should.Throw<ArgumentException>(() => new DispatchKeyRing([new DispatchPublicKey("k", Convert.ToBase64String(new byte[] { 1, 2, 3 }))]));
    }
}
