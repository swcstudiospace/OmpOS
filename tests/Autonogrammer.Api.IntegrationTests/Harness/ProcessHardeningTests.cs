using System.Runtime.Versioning;
using Autonogrammer.Api.Harness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// <see cref="ProcessHardening"/>: the option gating on every platform, the Linux call that clears the dumpable flag, and the non-Linux no-op.
/// The Linux test changes the flag of the shared test process for an instant and sets it back; it does not prove that a second process of another
/// user is refused, which needs a second account and is not exercised here.
/// </summary>
public sealed class ProcessHardeningTests
{
    /// <summary>With the key unset, the restriction is on outside Development and off in Development.</summary>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void UnsetKeyDefaultsFromTheEnvironment(string environment, bool expected)
    {
        ProcessHardening.IsEnabled(Configuration(null), new TestHostEnvironment(environment)).ShouldBe(expected);
    }

    /// <summary>A boolean value of Hosting:RestrictProcessInspection wins over the environment default, in either direction.</summary>
    [Theory]
    [InlineData("Production", "false", false)]
    [InlineData("Production", "true", true)]
    [InlineData("Development", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Production", "not-a-bool", true)]
    [InlineData("Development", "not-a-bool", false)]
    public void ConfiguredValueWinsOverTheEnvironmentDefault(string environment, string value, bool expected)
    {
        ProcessHardening.IsEnabled(Configuration(value), new TestHostEnvironment(environment)).ShouldBe(expected);
    }

    /// <summary>When the restriction is configured off, the call logs a warning that names the key, and the dumpable flag is left alone.</summary>
    [Fact]
    public void DisabledRestrictionLogsAWarningAndChangesNothing()
    {
        using var logs = new CapturingLoggerProvider();
        var logger = logs.CreateLogger("ProcessHardening");
        var before = ProcessHardening.IsDumpable();

        ProcessHardening.ApplyIfEnabled(logger, Configuration("false"), TestHostEnvironment.Production);

        ProcessHardening.IsDumpable().ShouldBe(before);
        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain(ProcessHardening.ConfigurationKey);
    }

    /// <summary>On Linux the call clears the dumpable flag and logs the outcome; the test sets the flag back so other tests keep their process as it was.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public void ApplyClearsTheDumpableFlagOnLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The dumpable flag exists only on Linux.");
        }

        using var logs = new CapturingLoggerProvider();
        var logger = logs.CreateLogger("ProcessHardening");
        var before = ProcessHardening.IsDumpable();
        before.ShouldNotBeNull();
        try
        {
            ProcessHardening.ApplyIfEnabled(logger, Configuration(null), TestHostEnvironment.Production);

            ProcessHardening.IsDumpable().ShouldBe(false);
            var entry = logs.Entries.ShouldHaveSingleItem();
            entry.Level.ShouldBe(LogLevel.Information);
            entry.Message.ShouldContain("no longer dumpable");
        }
        finally
        {
            _ = ProcessHardening.SetDumpable(before == true);
        }
    }

    /// <summary>Off Linux the call does nothing, logs at Debug only, and reports no flag.</summary>
    [Fact]
    public void ApplyDoesNothingOffLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            // Covered by the Linux test above: on Linux the call does act, so the no-op branch cannot run in this process.
            return;
        }

        using var logs = new CapturingLoggerProvider();

        ProcessHardening.Apply(logs.CreateLogger("ProcessHardening"));

        ProcessHardening.IsDumpable().ShouldBeNull();
        logs.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
    }

    private static IConfiguration Configuration(string? value)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (value is not null)
        {
            values[ProcessHardening.ConfigurationKey] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
