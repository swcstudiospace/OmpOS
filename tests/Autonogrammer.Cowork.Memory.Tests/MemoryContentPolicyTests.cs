#pragma warning disable IDE0005
using Autonogrammer.Cowork.Memory;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Tests;

/// <summary>One theory row per never-saved and sensitive class, plus near-miss numbers and the named limits.</summary>
public sealed class MemoryContentPolicyTests
{
    /// <summary>Never-saved classes are refused with a sentence, including when include_sensitive is on.</summary>
    /// <param name="className">The class.</param>
    /// <param name="text">The sample.</param>
    [Theory]
    [InlineData(MemoryContentPolicy.GovernmentId, "SSN 219-09-9999")]
    [InlineData(MemoryContentPolicy.CriminalHistory, "noted a felony conviction from 2019")]
    [InlineData(MemoryContentPolicy.FinancialAccount, "card 4111111111111111")]
    [InlineData(MemoryContentPolicy.FinancialAccount, "iban GB82WEST12345698765432")]
    [InlineData(MemoryContentPolicy.ImmigrationStatus, "immigration status is an undocumented immigrant")]
    public void NeverSavedClassesAreRefused(string className, string text)
    {
        var decision = MemoryContentPolicy.Evaluate(topic: null, text, category: null, includeSensitive: true);
        decision.Verdict.ShouldBe(MemoryPolicyVerdict.RefusedNeverSave);
        decision.ClassName.ShouldBe(className);
        decision.Sentence.ShouldNotBeNullOrWhiteSpace();
        decision.Sentence.ShouldNotContain(text);
    }

    /// <summary>Sensitive classes are refused unless include_sensitive is on.</summary>
    /// <param name="className">The class.</param>
    /// <param name="text">The sample.</param>
    [Theory]
    [InlineData(MemoryContentPolicy.Health, "medical diagnosis is diabetes")]
    [InlineData(MemoryContentPolicy.RaceEthnicity, "my ethnicity is Korean")]
    [InlineData(MemoryContentPolicy.ReligiousBelief, "religious belief is Buddhist")]
    [InlineData(MemoryContentPolicy.Politics, "political party is independent")]
    [InlineData(MemoryContentPolicy.GenderIdentity, "gender identity is nonbinary")]
    public void SensitiveClassesNeedAnOptIn(string className, string text)
    {
        var refused = MemoryContentPolicy.Evaluate(null, text, null, includeSensitive: false);
        refused.Verdict.ShouldBe(MemoryPolicyVerdict.RefusedSensitive);
        refused.ClassName.ShouldBe(className);
        refused.Sentence.ShouldNotBeNullOrWhiteSpace();
        refused.Sentence.ShouldContain("include_sensitive");

        var allowed = MemoryContentPolicy.Evaluate(null, text, null, includeSensitive: true);
        allowed.Verdict.ShouldBe(MemoryPolicyVerdict.Allowed);
        allowed.Sentence.ShouldBeNull();
    }

    /// <summary>Near-miss numbers and ordinary words are allowed.</summary>
    /// <param name="text">The sample.</param>
    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("219-09-999")]
    [InlineData("GB00WEST12345698765432")]
    [InlineData("I like independent films")]
    [InlineData("the account number is 12")]
    public void NearMissNumbersAreAllowed(string text)
    {
        var decision = MemoryContentPolicy.Evaluate("note", text, null, includeSensitive: false);
        decision.Verdict.ShouldBe(MemoryPolicyVerdict.Allowed);
        decision.ClassName.ShouldBeNull();
    }

    /// <summary>The caps the plan names are constants, and an over-long field is refused with a sentence.</summary>
    [Fact]
    public void LimitsAreNamedConstants()
    {
        MemoryOptions.MaxContentChars.ShouldBe(1000);
        MemoryOptions.MaxTopicChars.ShouldBe(120);
        MemoryOptions.MaxEntriesPerAccount.ShouldBe(1000);
        MemoryOptions.MaxSavesPerSessionPerHour.ShouldBe(20);
        MemoryOptions.MaxBriefBytes.ShouldBe(4 * 1024);
        MemoryOptions.MaxQueryChars.ShouldBe(200);
        MemoryOptions.SectionName.ShouldBe("Cowork:Memory");
        var sentence = MemoryContentPolicy.LimitSentence(new string('t', 121), new string('c', 1001));
        sentence.ShouldNotBeNull();
        sentence.ShouldContain("1000");
    }
}
