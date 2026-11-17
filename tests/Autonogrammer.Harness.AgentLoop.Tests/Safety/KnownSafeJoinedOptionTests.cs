using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>An option whose value is joined to it with '=' or attached to a short letter is checked as an argument, like a separate word.</summary>
public sealed class KnownSafeJoinedOptionTests
{
    [Fact]
    public void AGrepLongOptionWithAnOutsidePathIsNotKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("grep --file=/outside/patterns.txt README.md", static argument => string.Equals(argument, "README.md", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public void AWcLongOptionWithAnOutsidePathIsNotKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("wc --files0-from=.env.local", static argument => string.Equals(argument, "README.md", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public void AGrepShortOptionAttachedPathWithAnOutsidePathIsNotKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("grep -nf/outside/patterns.txt README.md", static argument => string.Equals(argument, "README.md", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public void AGrepBareFlagWithAnAllowedArgumentIsStillKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("grep -n README.md", static argument => string.Equals(argument, "README.md", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void AWcBareFlagWithAnAllowedArgumentIsStillKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("wc -l README.md", static argument => string.Equals(argument, "README.md", StringComparison.Ordinal)).ShouldBeTrue();
    }
}
