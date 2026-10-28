using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Herdr.Tests;

public sealed class NativeSessionReferenceTests
{
    private const string Secret = "omp-secret-session-reference";

    [Fact]
    public void ToStringDoesNotWriteTheReferenceValue()
    {
        var reference = new NativeSessionReference(Secret);
        var stored = new HerdrStoredSession(new HerdrSessionId("alpha"), "local-omp", reference, HerdrSessionState.Running, 1);
        var snapshot = new HerdrSessionSnapshot(new HerdrSessionId("alpha"), HerdrSessionState.Running, true, reference, 1);

        reference.ToString().ShouldNotContain(Secret);
        stored.ToString().ShouldNotContain(Secret);
        snapshot.ToString().ShouldNotContain(Secret);
        reference.Value.ShouldBe(Secret);
    }
}
