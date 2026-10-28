using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The rules that turn a failed ssh run into a diagnosis, and the bounded excerpt that goes with it.</summary>
public sealed class RemoteDiagnosisCatalogTests
{
    /// <summary>Messages ssh prints map to the kind the user can act on.</summary>
    [Theory]
    [InlineData(255, "Host key verification failed.", RemoteDiagnosisKind.HostKeyUnknown)]
    [InlineData(255, "@@@ WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED! @@@\nHost key verification failed.", RemoteDiagnosisKind.HostKeyChanged)]
    [InlineData(255, "fake@host: Permission denied (publickey,password).", RemoteDiagnosisKind.AuthFailed)]
    [InlineData(255, "Received disconnect: Too many authentication failures", RemoteDiagnosisKind.AuthFailed)]
    [InlineData(255, "ssh: Could not resolve hostname nope: Name or service not known", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(255, "ssh: connect to host h port 22: Connection refused", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(255, "ssh: connect to host h port 22: Connection timed out", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(255, "ssh: connect to host h port 22: No route to host", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(255, "Connection closed by 192.0.2.1 port 22", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(255, "kex_exchange_identification: read: Connection reset by peer", RemoteDiagnosisKind.HostUnreachable)]
    [InlineData(71, "cd: can't cd to /nope", RemoteDiagnosisKind.DirectoryMissing)]
    [InlineData(70, "mktemp: failed", RemoteDiagnosisKind.DirectoryNotWritable)]
    [InlineData(127, "sh: 1: exec: omp: not found", RemoteDiagnosisKind.OmpMissing)]
    public void FailuresAreClassified(int exitCode, string standardError, RemoteDiagnosisKind expected)
    {
        var diagnosis = RemoteDiagnosisCatalog.FromFailure(exitCode, standardError);

        diagnosis.ShouldNotBeNull().Kind.ShouldBe(expected);
        diagnosis.Severity.ShouldBe(DiagnosisSeverity.Error);
        diagnosis.Summary.ShouldNotBeNullOrWhiteSpace();
        diagnosis.FixText.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A failure that is not recognised stays unclassified, so the caller shows the raw text instead of a wrong guess; a clean exit is never a failure.</summary>
    [Theory]
    [InlineData(255, "something nobody has seen before")]
    [InlineData(0, "")]
    [InlineData(3, "some remote message")]
    public void UnknownFailuresAreNotGuessed(int exitCode, string standardError)
    {
        RemoteDiagnosisCatalog.FromFailure(exitCode, standardError).ShouldBeNull();
    }

    /// <summary>Every kind has its own summary and fix text, and the subject appears where the text names one.</summary>
    [Fact]
    public void EveryKindHasText()
    {
        var summaries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in Enum.GetValues<RemoteDiagnosisKind>())
        {
            var diagnosis = RemoteDiagnosisCatalog.Create(kind, subject: "SUBJECT");

            diagnosis.Kind.ShouldBe(kind);
            diagnosis.FixText.ShouldNotBeNullOrWhiteSpace();
            summaries.Add(diagnosis.Summary).ShouldBeTrue($"{kind} repeats a summary");
        }

        RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryMissing, subject: "/srv/x").Summary.ShouldContain("/srv/x", Case.Sensitive);
        RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.RequiredToolMissing, subject: "mktemp").Summary.ShouldContain("mktemp", Case.Sensitive);
    }

    /// <summary>The first-time host key text says that Autonogrammer never trusts a key for the user.</summary>
    [Fact]
    public void TheHostKeyFixPointsToTheTrustTerminal()
    {
        var diagnosis = RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.HostKeyUnknown);

        diagnosis.FixText.ShouldContain("never accepts a host key for you", Case.Sensitive);
        diagnosis.FixText.ShouldContain("trust", Case.Insensitive);
    }

    /// <summary>The excerpt keeps at most three visible lines and a bounded number of characters.</summary>
    [Fact]
    public void TheExcerptIsBounded()
    {
        RemoteDiagnosisCatalog.Excerpt("\n  \nfirst\n\nsecond\nthird\nfourth\n").ShouldBe("first\nsecond\nthird");
        RemoteDiagnosisCatalog.Excerpt("   \n\n").ShouldBeNull();
        RemoteDiagnosisCatalog.Excerpt(new string('x', 5000))!.Length.ShouldBe(400);
    }
}
