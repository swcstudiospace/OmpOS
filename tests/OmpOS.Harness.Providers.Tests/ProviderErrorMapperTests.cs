using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// Checks that each failure maps to a message that names the provider, says what happened and says what to do. The quota
/// figures asserted here are the ones the iFlytek primary page publishes (section 4.1); the 429 texts are its section 8 texts.
/// </summary>
public sealed class ProviderErrorMapperTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri IFlytekPlanPage = new("https://maas.xfyun.cn/packageSubscription");

    private readonly ProviderErrorMapper _mapper = new(new FixedTimeProvider(Today));

    [Fact]
    public void QuotaExhaustionMapsToAnActionableMessage()
    {
        var mapped = _mapper.Map(
            ProviderCatalogue.IFlytekCodingAnthropic,
            new ProviderFailure(429, "429-超出当前配额，请检查计划和计费详情"));

        mapped.Kind.ShouldBe(ProviderErrorKind.QuotaExhausted);
        mapped.ActionUrl.ShouldBe(IFlytekPlanPage);
        mapped.Message.ShouldContain("iFlytek Astron Coding Plan");
        mapped.Message.ShouldContain("quota is used up");
        mapped.Message.ShouldContain("6,000 requests per 5 hours");
        mapped.Message.ShouldContain("45,000 requests per week");
        mapped.Message.ShouldContain("90,000 requests per subscription month");
        mapped.Message.ShouldContain("30,000 requests per subscription month");
        mapped.Message.ShouldContain(IFlytekPlanPage.AbsoluteUri);
        mapped.Message.ShouldContain("switch to another provider");
    }

    [Fact]
    public void AnExhaustedBalanceOnAPayPerUseProviderPointsAtTopUp()
    {
        var mapped = _mapper.Map(ProviderCatalogue.DeepSeekAnthropic, new ProviderFailure(402));

        mapped.Kind.ShouldBe(ProviderErrorKind.QuotaExhausted);
        mapped.ActionUrl.ShouldBeNull();
        mapped.Message.ShouldContain("deepseek-anthropic");
        mapped.Message.ShouldContain("Top up the balance");
    }

    [Fact]
    public void ACodingPlanThatPublishesNoCapsSaysSoInsteadOfInventingOne()
    {
        var mapped = _mapper.Map(ProviderCatalogue.MiniMaxAnthropic, new ProviderFailure(429));

        mapped.Kind.ShouldBe(ProviderErrorKind.QuotaExhausted);
        mapped.Message.ShouldContain("MiniMax M Plan publishes no request limits");
        mapped.Message.ShouldNotContain("per 5 hours");
    }

    [Fact]
    public void TheRateLimitTextIsNotMistakenForQuotaExhaustion()
    {
        var mapped = _mapper.Map(
            ProviderCatalogue.IFlytekCodingOpenAi,
            new ProviderFailure(429, "429-请求速率限制已达上限"));

        mapped.Kind.ShouldBe(ProviderErrorKind.RateLimited);
        mapped.Message.ShouldContain("Wait a moment and retry");
    }

    [Fact]
    public void AnUntextedTooManyRequestsOnAPayPerUseProviderIsRateLimiting()
    {
        _mapper.Map(ProviderCatalogue.HuggingFaceRouter, new ProviderFailure(429)).Kind.ShouldBe(ProviderErrorKind.RateLimited);
        _mapper.Map(ProviderCatalogue.HuggingFaceRouter, new ProviderFailure(429, "You exceeded your current quota")).Kind
            .ShouldBe(ProviderErrorKind.QuotaExhausted);
    }

    [Fact]
    public void PlanExpiryMapsToAnActionableMessage()
    {
        var mapped = _mapper.Map(
            ProviderCatalogue.IFlytekCodingAnthropic,
            new ProviderFailure(401, "401-无效的身份验证", new DateOnly(2026, 10, 20)));

        mapped.Kind.ShouldBe(ProviderErrorKind.PlanExpired);
        mapped.ActionUrl.ShouldBe(IFlytekPlanPage);
        mapped.Message.ShouldContain("iFlytek Astron Coding Plan");
        mapped.Message.ShouldContain("ended on 2026-10-20");
        mapped.Message.ShouldContain("valid for 31 days");
        mapped.Message.ShouldContain("Renew the plan at " + IFlytekPlanPage.AbsoluteUri);
        mapped.Message.ShouldContain("new plan key");
    }

    [Fact]
    public void ThePlanIsStillValidOnItsLastDay()
    {
        var mapped = _mapper.Map(
            ProviderCatalogue.IFlytekCodingAnthropic,
            new ProviderFailure(401, null, new DateOnly(2026, 10, 26)));

        mapped.Kind.ShouldBe(ProviderErrorKind.CredentialRejected);
    }

    [Fact]
    public void ARejectedKeyWithoutAKnownEndDateExplainsTheSeparatePlanKey()
    {
        var mapped = _mapper.Map(ProviderCatalogue.IFlytekCodingOpenAi, new ProviderFailure(401));

        mapped.Kind.ShouldBe(ProviderErrorKind.CredentialRejected);
        mapped.Message.ShouldContain("plan's own key");
        mapped.Message.ShouldContain("31-day paid period");
    }

    [Fact]
    public void APlanEndDateMeansNothingForAPayPerUseProvider()
    {
        var mapped = _mapper.Map(ProviderCatalogue.DeepSeekAnthropic, new ProviderFailure(403, null, new DateOnly(2026, 1, 1)));

        mapped.Kind.ShouldBe(ProviderErrorKind.CredentialRejected);
        mapped.Message.ShouldContain("deepseek-anthropic");
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(599)]
    public void ServerFailuresAreNotBlamedOnTheKeyOrQuota(int status)
    {
        var mapped = _mapper.Map(ProviderCatalogue.IFlytekCodingOpenAi, new ProviderFailure(status));

        mapped.Kind.ShouldBe(ProviderErrorKind.ServiceUnavailable);
        mapped.Message.ShouldContain("Your key and quota are not the cause");
    }

    [Fact]
    public void AnUnmappedStatusIsReportedWithItsCode()
    {
        var mapped = _mapper.Map(ProviderCatalogue.HuggingFaceRouter, new ProviderFailure(418));

        mapped.Kind.ShouldBe(ProviderErrorKind.Unrecognised);
        mapped.Message.ShouldContain("HTTP 418");
    }
}
