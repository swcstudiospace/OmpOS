using OmpOS.Harness.Automations.Cron;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>Verifies which cron expressions parse and that rejected ones say why without repeating the text.</summary>
public sealed class CronExpressionParseTests
{
    [Theory]
    [InlineData("* * * * *")]
    [InlineData("*/15 * * * *")]
    [InlineData("0 9-17 * * mon-fri")]
    [InlineData("0 0 1,15 * *")]
    [InlineData("5/20 * * * *")]
    [InlineData("0-30/10 * * * *")]
    [InlineData("0 0 * JAN,Mar *")]
    [InlineData("0 0 * * 7")]
    [InlineData("0 0 * * 0-7")]
    [InlineData("  0   12  *  *  sun  ")]
    [InlineData("@yearly")]
    [InlineData("@annually")]
    [InlineData("@monthly")]
    [InlineData("@weekly")]
    [InlineData("@daily")]
    [InlineData("@midnight")]
    [InlineData("@hourly")]
    public void Valid_expressions_parse(string text)
    {
        CronExpression.TryParse(text, out var expression, out var error).ShouldBeTrue(error);

        expression.ShouldNotBeNull();
        expression.Source.ShouldBe(text.Trim());
        error.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 0 *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 8")]
    [InlineData("*/0 * * * *")]
    [InlineData("*/-1 * * * *")]
    [InlineData("1- * * * *")]
    [InlineData("-5 * * * *")]
    [InlineData("30-10 * * * *")]
    [InlineData("a * * * *")]
    [InlineData("* * * foo *")]
    [InlineData("* * * * funday")]
    [InlineData("1,,2 * * * *")]
    [InlineData(", * * * *")]
    [InlineData("? * * * *")]
    [InlineData("L * * * *")]
    [InlineData("* * * * 1#2")]
    [InlineData("0 0 15W * *")]
    [InlineData("jan * * * *")]
    [InlineData("* mon * * *")]
    [InlineData("@reboot")]
    [InlineData("@every 5m")]
    public void Invalid_expressions_are_rejected(string? text)
    {
        CronExpression.TryParse(text, out var expression, out var error).ShouldBeFalse();

        expression.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_six_field_expression_is_told_that_seconds_are_not_supported()
    {
        CronExpression.TryParse("0 */5 * * * *", out _, out var error).ShouldBeFalse();

        error.ShouldContain("Seconds are not supported", Case.Sensitive);
    }

    [Fact]
    public void The_error_never_repeats_the_expression_text()
    {
        CronExpression.TryParse("hunter2 * * * *", out _, out var error).ShouldBeFalse();

        error.ShouldNotContain("hunter2", Case.Insensitive);
    }

    [Fact]
    public void An_expression_longer_than_the_limit_is_rejected()
    {
        var text = "0 0 * * *" + new string(' ', CronExpression.MaxLength);

        CronExpression.TryParse(text + "x", out _, out var error).ShouldBeFalse();

        error.ShouldContain("longer than", Case.Sensitive);
    }

    [Fact]
    public void Parse_throws_FormatException_for_an_invalid_expression()
    {
        Should.Throw<FormatException>(() => CronExpression.Parse("nonsense"));
        CronExpression.Parse("0 0 * * *").Source.ShouldBe("0 0 * * *");
    }
}
