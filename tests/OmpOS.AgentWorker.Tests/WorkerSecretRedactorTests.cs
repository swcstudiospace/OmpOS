using System.Collections;
using OmpOS.AgentWorker.Security;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>What the redactor treats as a secret, and for how long.</summary>
public sealed class WorkerSecretRedactorTests
{
    /// <summary>Values of credential-looking variables are replaced; other values and short values are not.</summary>
    [Fact]
    public void EnvironmentSecretsAreRedactedByVariableName()
    {
        var environment = new Hashtable
        {
            ["ANTHROPIC_API_KEY"] = "sk-ant-0123456789",
            ["GITHUB_TOKEN"] = "ghs_abcdefghij",
            ["DB_PASSWORD"] = "hunter2hunter2",
            ["TLS_CERT_PEM"] = "-----BEGIN CERTIFICATE-----",
            ["HOME"] = "/data/home",
            ["SHORT_SECRET"] = "abc",
        };
        var redactor = new WorkerSecretRedactor(environment);

        var scrubbed = redactor.Redact("key=sk-ant-0123456789 tok=ghs_abcdefghij pw=hunter2hunter2 home=/data/home short=abc cert=-----BEGIN CERTIFICATE-----");

        scrubbed.ShouldBe("key=[REDACTED] tok=[REDACTED] pw=[REDACTED] home=/data/home short=abc cert=[REDACTED]");
    }

    /// <summary>A tracked bearer token is redacted until its handle is disposed.</summary>
    [Fact]
    public void TrackedTokenIsRedactedWhileTheHandleLives()
    {
        var redactor = new WorkerSecretRedactor(new Hashtable());
        var handle = redactor.Track("eyJhbGciOiJFUzI1NiJ9.payload.signature");

        redactor.Redact("Authorization: Bearer eyJhbGciOiJFUzI1NiJ9.payload.signature").ShouldBe("Authorization: Bearer [REDACTED]");
        handle.Dispose();
        redactor.Redact("Authorization: Bearer eyJhbGciOiJFUzI1NiJ9.payload.signature").ShouldBe("Authorization: Bearer eyJhbGciOiJFUzI1NiJ9.payload.signature");
    }

    /// <summary>When one secret contains another, the longer one is replaced whole.</summary>
    [Fact]
    public void LongerSecretIsReplacedBeforeItsPrefix()
    {
        var environment = new Hashtable { ["A_KEY"] = "secret-value", ["B_KEY"] = "secret-value-extended" };
        var redactor = new WorkerSecretRedactor(environment);

        redactor.Redact("x secret-value-extended y").ShouldBe("x [REDACTED] y");
    }
}
