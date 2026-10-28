using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Verifies the extra-environment member of the session options: its default, the name and value rules, and that no value
/// is ever printed.
/// </summary>
public sealed class TerminalSessionOptionsTests
{
    private const string SecretValue = "s3cret-value-5678";

    [Fact]
    public void TheEnvironmentIsEmptyByDefault()
    {
        var options = new TerminalSessionOptions("/bin/sh", [], "/tmp", 80, 24, new RecordingSink());

        options.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void AcceptableNamesAndValuesHaveNoProblem()
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PI_CODING_AGENT_DIR"] = "/home/user/.omp/agent",
            ["EMPTY_IS_ALLOWED"] = string.Empty,
            ["with space"] = "a=b",
        };

        TerminalSessionOptions.FindEnvironmentProblem(environment).ShouldBeNull();
    }

    [Fact]
    public void ANameWithAnEqualsSignIsAProblemThatNamesTheNameButNotTheValue()
    {
        var problem = TerminalSessionOptions.FindEnvironmentProblem(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["A=B"] = SecretValue }).ShouldNotBeNull();

        problem.ShouldContain("A=B", Case.Sensitive);
        problem.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public void AnEmptyNameAndANulNameAreProblems()
    {
        TerminalSessionOptions.FindEnvironmentProblem(
            new Dictionary<string, string>(StringComparer.Ordinal) { [string.Empty] = "x" }).ShouldNotBeNull();
        TerminalSessionOptions.FindEnvironmentProblem(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["A\0B"] = "x" }).ShouldNotBeNull();
    }

    [Fact]
    public void ANullValueAndANulValueAreProblemsThatNeverPrintTheValue()
    {
        var nullValue = TerminalSessionOptions.FindEnvironmentProblem(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN"] = null! }).ShouldNotBeNull();
        var nulValue = TerminalSessionOptions.FindEnvironmentProblem(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN"] = SecretValue + "\0" }).ShouldNotBeNull();

        nullValue.ShouldContain("TOKEN", Case.Sensitive);
        nulValue.ShouldContain("TOKEN", Case.Sensitive);
        nulValue.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public void ToStringListsTheVariableNamesAndNeverTheValuesOrTheArguments()
    {
        var options = new TerminalSessionOptions("/bin/sh", ["--token", SecretValue], "/tmp", 80, 24, new RecordingSink())
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["API_TOKEN"] = SecretValue },
        };

        var text = options.ToString();

        text.ShouldContain("API_TOKEN", Case.Sensitive);
        text.ShouldContain("/bin/sh", Case.Sensitive);
        text.ShouldNotContain(SecretValue, Case.Sensitive);
    }

    [Fact]
    public void ANullEnvironmentIsRejectedByTheCheck()
    {
        Should.Throw<ArgumentNullException>(() => TerminalSessionOptions.FindEnvironmentProblem(null!));
    }
}
