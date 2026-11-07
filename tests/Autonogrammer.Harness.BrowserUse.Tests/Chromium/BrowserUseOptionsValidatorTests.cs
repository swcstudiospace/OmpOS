using System.Reflection;
using Autonogrammer.Harness.BrowserUse.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Chromium;

/// <summary>Validator messages name keys and never values. Sandbox disable needs the allow switch.</summary>
public sealed class BrowserUseOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreTheDocumentedLimits()
    {
        var options = new BrowserUseOptions();
        options.Enabled.ShouldBeFalse();
        options.MinimumMajorVersion.ShouldBe(149);
        options.ViewportWidth.ShouldBe(1280);
        options.ViewportHeight.ShouldBe(800);
        options.PolicyMode.ShouldBe(PolicyMode.PromptOnNewOrigin);
        options.DownloadMode.ShouldBe(DownloadMode.Deny);
        options.MaxConcurrentBrowsers.ShouldBe(2);
        options.MaxTabs.ShouldBe(5);
        options.MaxBrowserMemoryMiB.ShouldBe(1536);
        options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(10));
        options.MaxSessionLifetime.ShouldBe(TimeSpan.FromHours(2));
        Enum.GetNames<PolicyMode>().ShouldBe(["PromptOnNewOrigin", "Strict", "Open"]);
    }

    [Fact]
    public void SandboxDisabled_IsRefusedWithoutTheAllowSwitch()
    {
        var validator = new BrowserUseOptionsValidator(static _ => null);
        var options = new BrowserUseOptions { SandboxMode = SandboxMode.Disabled, ExecutablePath = "secret-path-value" };
        var result = validator.Validate(null, options);
        result.Failed.ShouldBeTrue();
        var text = string.Join(' ', result.Failures ?? []);
        text.ShouldContain("Cowork:BrowserUse:SandboxMode");
        text.ShouldNotContain("secret-path-value");
        text.ShouldNotContain("Disabled");
    }

    [Fact]
    public void SandboxDisabled_IsAcceptedWhenTheAllowSwitchIsSet()
    {
        var validator = new BrowserUseOptionsValidator(static name => string.Equals(name, BrowserUseOptionsValidator.AllowNoSandboxVariable, StringComparison.Ordinal) ? "1" : null);
        validator.Validate(null, new BrowserUseOptions { SandboxMode = SandboxMode.Disabled }).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void DownloadMode_AcceptsOnlyDenyAndQuarantine()
    {
        var validator = new BrowserUseOptionsValidator(static _ => null);
        validator.Validate(null, new BrowserUseOptions { DownloadMode = DownloadMode.Quarantine }).Succeeded.ShouldBeTrue();
        var invalid = validator.Validate(null, new BrowserUseOptions { DownloadMode = (DownloadMode)99 });
        invalid.Failed.ShouldBeTrue();
        var text = string.Join(' ', invalid.Failures ?? []);
        text.ShouldContain("Cowork:BrowserUse:DownloadMode");
        text.ShouldNotContain("99");
    }

    [Fact]
    public void Failure_NamesTheKeyAndNotTheValue()
    {
        var validator = new BrowserUseOptionsValidator(static _ => null);
        var result = validator.Validate(null, new BrowserUseOptions { MaxTabs = -7, IdleTimeout = TimeSpan.Zero });
        var text = string.Join(' ', result.Failures ?? []);
        text.ShouldContain("Cowork:BrowserUse:MaxTabs");
        text.ShouldContain("Cowork:BrowserUse:IdleTimeout");
        text.ShouldNotContain("-7");
    }

    [Fact]
    public void OptionKeys_DoNotUseForbiddenSectionPrefixes()
    {
        foreach (var property in typeof(BrowserUseOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var key = BrowserUseOptionsValidator.Key(property.Name);
            key.StartsWith("Harness:", StringComparison.Ordinal).ShouldBeFalse();
            key.StartsWith("Api:", StringComparison.Ordinal).ShouldBeFalse();
            key.StartsWith("Hosting:", StringComparison.Ordinal).ShouldBeFalse();
            key.StartsWith("Dispatch:", StringComparison.Ordinal).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task AddBrowserUse_ResolvesTheSessionHost()
    {
        (await BrowserUseServiceExtensions.CanResolveSessionHostAsync().ConfigureAwait(true)).ShouldBeTrue();
    }
}
