using Shouldly;
using Xunit;

namespace OmpOS.Harness.Terminal.Tests;

/// <summary>
/// Verifies how the interactive Linux backend builds the command's environment: the explicit variables reach the command,
/// they win over the host's, the two omp variables pass from the host, every other host variable stays out, and an invalid
/// name or value is refused without ever printing a value. The command is <c>/bin/sh</c>, and every test uses real pseudo-terminals.
/// </summary>
public sealed class LinuxLocalPtyBackendEnvironmentTests
{
    private const string Shell = "/bin/sh";
    private const string SecretValue = "s3cret-value-1234";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExplicitVariablesReachTheCommandAndWinOverTheHostValue()
    {
        const string hostOnly = "OMPOS_TERMINAL_ENV_TEST_ONE";
        const string overridden = "OMPOS_TERMINAL_ENV_TEST_TWO";
        Environment.SetEnvironmentVariable(overridden, "host-value");
        try
        {
            var output = await RunAsync(
                "printf 'ONE<%s> TWO<%s>\\n' \"${OMPOS_TERMINAL_ENV_TEST_ONE:-absent}\" \"${OMPOS_TERMINAL_ENV_TEST_TWO:-absent}\"",
                new Dictionary<string, string>(StringComparer.Ordinal) { [hostOnly] = "from options", [overridden] = "from options too" }).ConfigureAwait(true);

            output.ShouldContain("ONE<from options>", Case.Sensitive);
            output.ShouldContain("TWO<from options too>", Case.Sensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable(overridden, null);
        }
    }

    [Fact]
    public async Task ExplicitVariablesWinOverAnAllowlistedHostVariable()
    {
        var output = await RunAsync(
            "printf 'LANG<%s>\\n' \"$LANG\"",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["LANG"] = "xx_XX.test" }).ConfigureAwait(true);

        output.ShouldContain("LANG<xx_XX.test>", Case.Sensitive);
    }

    [Fact]
    public async Task TheOmpSessionDirectoryAndProfileVariablesPassFromTheHost()
    {
        var sessionDirectory = Environment.GetEnvironmentVariable("PI_CODING_AGENT_DIR");
        var profile = Environment.GetEnvironmentVariable("OMP_PROFILE");
        Environment.SetEnvironmentVariable("PI_CODING_AGENT_DIR", "/tmp/ompos-agent-dir-test");
        Environment.SetEnvironmentVariable("OMP_PROFILE", "ompos-profile-test");
        try
        {
            var output = await RunAsync(
                "printf 'DIR<%s> PROFILE<%s>\\n' \"${PI_CODING_AGENT_DIR:-absent}\" \"${OMP_PROFILE:-absent}\"",
                environment: null).ConfigureAwait(true);

            output.ShouldContain("DIR</tmp/ompos-agent-dir-test>", Case.Sensitive);
            output.ShouldContain("PROFILE<ompos-profile-test>", Case.Sensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PI_CODING_AGENT_DIR", sessionDirectory);
            Environment.SetEnvironmentVariable("OMP_PROFILE", profile);
        }
    }

    [Fact]
    public async Task AHostVariableOutsideTheAllowlistStaysOutEvenWhenOtherVariablesArePassedExplicitly()
    {
        const string sentinel = "OMPOS_TERMINAL_ENV_TEST_LEAK";
        Environment.SetEnvironmentVariable(sentinel, "leaked");
        try
        {
            var output = await RunAsync(
                "printf 'LEAK<%s> KEPT<%s>\\n' \"${OMPOS_TERMINAL_ENV_TEST_LEAK:-absent}\" \"${OMPOS_TERMINAL_ENV_TEST_KEPT:-absent}\"",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["OMPOS_TERMINAL_ENV_TEST_KEPT"] = "yes" }).ConfigureAwait(true);

            output.ShouldContain("LEAK<absent>", Case.Sensitive);
            output.ShouldContain("KEPT<yes>", Case.Sensitive);
            output.ShouldNotContain("leaked", Case.Sensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable(sentinel, null);
        }
    }

    /// <summary>
    /// A variable the test process really inherited from its parent, outside the allowlist, is absent in the command. A variable set with
    /// Environment.SetEnvironmentVariable would prove nothing: .NET keeps it in a managed copy that a child never inherits.
    /// </summary>
    [Fact]
    public async Task AVariableTheHostProcessInheritedStaysOutOfTheCommand()
    {
        var allowed = new HashSet<string>(["PATH", "HOME", "LANG", "TERM", "PI_CODING_AGENT_DIR", "OMP_PROFILE", "PWD", "OLDPWD", "SHLVL", "_"], StringComparer.Ordinal);
        var inherited = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(name => !allowed.Contains(name) && name.Length > 0 && name.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_') && !char.IsAsciiDigit(name[0]))
            .Order(StringComparer.Ordinal)
            .ToList();
        inherited.Count.ShouldBeGreaterThan(0, "the test process inherits at least one variable outside the allowlist");

        var output = await RunAsync(
            "for name in " + string.Join(' ', inherited) + "; do eval \"value=\\${$name+set}\"; if [ -n \"$value\" ]; then echo \"LEAK:$name\"; fi; done; echo DONE",
            environment: null).ConfigureAwait(true);

        output.ShouldContain("DONE", Case.Sensitive);
        output.ShouldNotContain("LEAK:", Case.Sensitive);
    }

    [Theory]
    [InlineData("", "an empty name")]
    [InlineData("A=B", "A=B")]
    public async Task AnInvalidNameIsRefusedBeforeAnythingStartsAndTheValueIsNeverInTheMessage(string name, string because)
    {
        using var workspace = new TempWorkspace();
        var options = new TerminalSessionOptions(Shell, ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink())
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = SecretValue },
        };

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => new LinuxLocalPtyBackend().StartAsync(options, Token)).ConfigureAwait(true);

        exception.ParamName.ShouldBe("options", because);
        exception.Message.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public async Task ANameWithANulCharacterIsRefused()
    {
        using var workspace = new TempWorkspace();
        var options = new TerminalSessionOptions(Shell, ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink())
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["BAD\0NAME"] = SecretValue },
        };

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => new LinuxLocalPtyBackend().StartAsync(options, Token)).ConfigureAwait(true);

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public async Task AValueWithANulCharacterIsRefusedAndNeverPrinted()
    {
        using var workspace = new TempWorkspace();
        var options = new TerminalSessionOptions(Shell, ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink())
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN"] = SecretValue + "\0" },
        };

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => new LinuxLocalPtyBackend().StartAsync(options, Token)).ConfigureAwait(true);

        exception.Message.ShouldContain("TOKEN", Case.Sensitive);
        exception.Message.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public async Task ANullEnvironmentIsRefused()
    {
        using var workspace = new TempWorkspace();
        var options = new TerminalSessionOptions(Shell, ["-c", "exit 0"], workspace.Path, 80, 24, new RecordingSink())
        {
            Environment = null!,
        };

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => new LinuxLocalPtyBackend().StartAsync(options, Token)).ConfigureAwait(true);

        exception.ParamName.ShouldBe("options");
    }

    /// <summary>Runs the script in a session started with the environment, feeds it one line, and returns its output.</summary>
    private static async Task<string> RunAsync(string script, IReadOnlyDictionary<string, string>? environment)
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var options = new TerminalSessionOptions(Shell, ["-c", "read line; " + script + "; exit 0"], workspace.Path, 80, 24, sink);
        if (environment is not null)
        {
            options = options with { Environment = environment };
        }

        var session = await new LinuxLocalPtyBackend().StartAsync(options, Token).ConfigureAwait(true);
        try
        {
            await session.WriteInputAsync("\n", Token).ConfigureAwait(true);
            (await session.WaitForExitAsync(Token).ConfigureAwait(true)).ShouldBe(0);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }

        return sink.Text;
    }
}
