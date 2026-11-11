using System.Reflection;
using System.Security.Cryptography;
using Autonogrammer.Accounts.Desktop;
using Autonogrammer.Dispatch.Desktop.Tests.Support;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Harness.Secrets.Os;
using Shouldly;
using Xunit;

namespace Autonogrammer.Dispatch.Desktop.Tests;

public sealed class PairingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dispatch-pairing-" + Guid.NewGuid().ToString("N"));

    private string PairingPath => Path.Combine(_directory, "pairing.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void FileAndWrappedFileStoresAreRejectedBeforeWritingPairingSecrets()
    {
        var file = new OwnerOnlyFileSecretStore(Path.Combine(_directory, "secrets.json"));
        using var wrapper = new AccountSecretStoreSelector(file, file, hasStoredSession: false, allowFileSecretStore: true);

        Should.Throw<DispatchKeyStoreUnavailableException>(() => new DispatchPairing(file, PairingPath));
        Should.Throw<DispatchKeyStoreUnavailableException>(() => new DispatchPairing(wrapper, PairingPath));
        Should.Throw<DispatchKeyStoreUnavailableException>(() => new DispatchPairing(new InMemorySessionSecretStore(), PairingPath));
        Directory.Exists(_directory).ShouldBeFalse();
    }

    [Fact]
    public void PublicPairingApiHasNoTestStoreBypass()
    {
        var constructors = typeof(DispatchPairing).GetConstructors();
        constructors.Length.ShouldBe(1);
        constructors[0].GetParameters().Select(static parameter => parameter.ParameterType)
            .ShouldBe([typeof(ISecretStore), typeof(string)]);
        typeof(DispatchPairing).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.ReturnType == typeof(DispatchPairing))
            .ShouldBeEmpty();
    }

    [Fact]
    public void OperatingSystemStoreCanBeSelectedWithoutContactingTheKeyring()
    {
        var pairing = new DispatchPairing(new OsSecretStore(), PairingPath);

        pairing.LoadPairing().ShouldBeNull();
    }

    [Fact]
    public async Task TheDeviceKeyIsStoredOnlyInTheSecretStore()
    {
        var secrets = new InMemorySessionSecretStore();
        var pairing = DispatchPairing.CreateForTesting(secrets, PairingPath);

        var spki = await pairing.EnrollDeviceKeyAsync("device_2", CancellationToken.None);

        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _);
        (await pairing.HasDeviceKeyAsync("device_2", CancellationToken.None)).ShouldBeTrue();
        var names = await secrets.ListNamesAsync(CancellationToken.None);
        names.Count.ShouldBe(1);
        names[0].ShouldBe(DispatchPairing.DeviceKeyName("device_2"));
        File.Exists(PairingPath).ShouldBeFalse();
    }

    [Fact]
    public async Task AnUnavailableStoreRefusesPairingAndWritesNoRecord()
    {
        var pairing = DispatchPairing.CreateForTesting(new UnavailableSecretStore(), PairingPath);

        await Should.ThrowAsync<DispatchKeyStoreUnavailableException>(() => pairing.EnrollDeviceKeyAsync("device_2", CancellationToken.None));

        pairing.LoadPairing().ShouldBeNull();
    }

    [Fact]
    public async Task ACompletedPairingReloadsItsAccountAndKeys()
    {
        var pairing = DispatchPairing.CreateForTesting(new InMemorySessionSecretStore(), PairingPath);
        using var signer = new DispatchTestSigner();

        await pairing.CompleteAsync(
            new DispatchPairedDevice("device_2", "acct_1", [signer.PublicKey]),
            new SecretString(DispatchTestSigner.Jwt(DateTimeOffset.UtcNow.AddHours(1))),
            CancellationToken.None);

        var loaded = pairing.LoadPairing();
        loaded.ShouldNotBeNull();
        loaded.AccountId.ShouldBe("acct_1");
        loaded.Keys.Single().Kid.ShouldBe(DispatchTestSigner.Kid);
    }

    [Fact]
    public void TheCredentialExpiryIsReadFromItsClaim()
    {
        var expiry = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        DispatchPairing.ReadCredentialExpiry(DispatchTestSigner.Jwt(expiry)).ShouldBe(expiry);
        DispatchPairing.ReadCredentialExpiry("not-a-jwt").ShouldBeNull();
    }
}
