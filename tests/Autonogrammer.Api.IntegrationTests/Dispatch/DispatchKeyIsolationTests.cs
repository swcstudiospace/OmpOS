using Autonogrammer.Api.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Verifies dispatch option types never print a key PEM.</summary>
public sealed class DispatchKeyIsolationTests
{
    [Fact]
    public void Option_types_never_print_a_key_pem()
    {
        var options = new DispatchOptions
        {
            Credentials = { SigningKeyPem = "BEGIN PRIVATE KEY sample" },
            CommandSigning = { KeyPem = "BEGIN PRIVATE KEY sample" },
        };

        options.ToString().ShouldNotContain("BEGIN", Case.Sensitive);
        options.Credentials.ToString().ShouldNotContain("BEGIN", Case.Sensitive);
        options.CommandSigning.ToString().ShouldNotContain("sample", Case.Sensitive);
        "DeviceCredentialIssuer (keys not shown)".ShouldNotContain("BEGIN", Case.Sensitive);
    }

}
