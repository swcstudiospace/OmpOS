using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>Fresh key pairs for one test host: the ES256 key the control plane signs worker tokens with (its public half is what a worker holds), and the RSA key of a GitHub App.</summary>
internal sealed class CloudTestKeys : IDisposable
{
    private readonly ECDsa _signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _signingPublic = ECDsa.Create();
    private readonly RSA _app = RSA.Create(2048);

    /// <summary>Initializes a new instance of the <see cref="CloudTestKeys"/> class.</summary>
    public CloudTestKeys()
    {
        _signingPublic.ImportSubjectPublicKeyInfo(_signing.ExportSubjectPublicKeyInfo(), out _);
    }

    /// <summary>Gets the worker token signing key as the PKCS#8 PEM the control plane is configured with.</summary>
    public string SigningKeyPem => _signing.ExportPkcs8PrivateKeyPem();

    /// <summary>Gets the public half, as the PEM a worker is configured with.</summary>
    public string WorkerPublicKeyPem => _signing.ExportSubjectPublicKeyInfoPem();

    /// <summary>Gets a security key that verifies worker tokens, as a worker does.</summary>
    public SecurityKey WorkerVerificationKey => new ECDsaSecurityKey(_signingPublic);

    /// <summary>Gets the GitHub App private key PEM.</summary>
    public string AppKeyPem => _app.ExportPkcs8PrivateKeyPem();

    /// <summary>Gets a security key that verifies App JWTs, as GitHub does.</summary>
    public SecurityKey AppVerificationKey => new RsaSecurityKey(_app.ExportParameters(includePrivateParameters: false));

    /// <inheritdoc />
    public void Dispose()
    {
        _signing.Dispose();
        _signingPublic.Dispose();
        _app.Dispose();
    }
}
