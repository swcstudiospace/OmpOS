using OmpOS.Api.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Automations;

/// <summary>Verifies the <c>Automations</c> section validation: nothing is checked while automations are off, and each failure names its key.</summary>
public sealed class AutomationOptionsValidatorTests
{
    private static readonly AutomationOptionsValidator Validator = new();

    [Fact]
    public void A_default_section_is_valid_and_automations_are_off()
    {
        var options = new AutomationOptions();

        options.Enabled.ShouldBeFalse();
        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Nothing_is_checked_while_automations_are_off()
    {
        var options = new AutomationOptions { Enabled = false, StorePath = "relative/path", WebhookRequestsPerMinute = 0, WebhookReplayWindowSeconds = 1 };

        Validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void An_enabled_section_with_the_defaults_is_valid()
    {
        Validator.Validate(null, new AutomationOptions { Enabled = true }).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("StorePath", "relative/store.json", "Automations:StorePath")]
    [InlineData("SecretStorePath", "secrets.json", "Automations:SecretStorePath")]
    public void A_relative_path_is_refused(string property, string value, string key)
    {
        var options = new AutomationOptions { Enabled = true };
        if (string.Equals(property, "StorePath", StringComparison.Ordinal))
        {
            options.StorePath = value;
        }
        else
        {
            options.SecretStorePath = value;
        }

        var result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        var failure = result.Failures!.ShouldHaveSingleItem();
        failure.ShouldContain(key, Case.Sensitive);
        failure.ShouldNotContain(value, Case.Sensitive);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100_000, true)]
    [InlineData(100_001, false)]
    public void The_rate_limits_are_checked_at_their_boundaries(int perMinute, bool valid)
    {
        Validator.Validate(null, new AutomationOptions { Enabled = true, WebhookRequestsPerMinute = perMinute }).Succeeded.ShouldBe(valid);
        Validator.Validate(null, new AutomationOptions { Enabled = true, RequestsPerMinute = perMinute }).Succeeded.ShouldBe(valid);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(3600, true)]
    [InlineData(3601, false)]
    public void The_replay_window_is_thirty_seconds_to_one_hour(int seconds, bool valid)
    {
        Validator.Validate(null, new AutomationOptions { Enabled = true, WebhookReplayWindowSeconds = seconds }).Succeeded.ShouldBe(valid);
    }

    [Fact]
    public void Every_failure_is_reported_at_once()
    {
        var options = new AutomationOptions { Enabled = true, StorePath = "a", SecretStorePath = "b", WebhookRequestsPerMinute = 0, RequestsPerMinute = 0, WebhookReplayWindowSeconds = 0 };

        Validator.Validate(null, options).Failures!.Count().ShouldBe(5);
    }
}
