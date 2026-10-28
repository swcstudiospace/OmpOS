using System.Reflection;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>The copy of the Models page: every string exists, every enum value has words, and the required sentences are there.</summary>
public sealed class ModelsCopyTests
{
    /// <summary>Every public constant of the copy class holds a non-blank sentence without a placeholder token.</summary>
    [Fact]
    public void EveryConstantIsWorded()
    {
        var constants = typeof(ModelsCopy)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();

        constants.Length.ShouldBeGreaterThan(50);
        foreach (var constant in constants)
        {
            var text = (string)constant.GetRawConstantValue()!;
            text.ShouldNotBeNullOrWhiteSpace(constant.Name);
            text.ShouldBe(text.Trim(), constant.Name);
            text.ShouldNotContain(string.Concat("TO", "DO"), Case.Insensitive, constant.Name);
            text.ShouldNotContain("{", Case.Sensitive, constant.Name);
        }
    }

    /// <summary>Every value of every enum the page shows has its own non-blank words.</summary>
    [Fact]
    public void EveryEnumValueHasWords()
    {
        Enum.GetValues<ProviderGroup>().Select(ModelsCopy.GroupTitle).Distinct(StringComparer.Ordinal).Count().ShouldBe(5);
        Enum.GetValues<ProviderGroup>().Select(ModelsCopy.GroupDescription).Distinct(StringComparer.Ordinal).Count().ShouldBe(5);
        Enum.GetValues<ProviderAuthKind>().Select(ModelsCopy.AuthLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
        Enum.GetValues<CredentialState>().Select(ModelsCopy.CredentialLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(4);
        Enum.GetValues<CustomProviderProtocol>().Select(ModelsCopy.ProtocolLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        Enum.GetValues<CustomProviderProtocol>().Select(ModelsCopy.EndpointHint).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        Enum.GetValues<CustomProviderAuth>().Select(ModelsCopy.AuthChoiceLabel).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);

        foreach (var status in Enum.GetValues<ProviderTestStatus>())
        {
            foreach (var group in Enum.GetValues<ProviderGroup>())
            {
                ModelsCopy.TestText(status, null, group).ShouldNotBeNullOrWhiteSpace();
            }
        }
    }

    /// <summary>An undefined enum value is a defect and throws.</summary>
    [Fact]
    public void UndefinedEnumValuesThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ModelsCopy.GroupTitle((ProviderGroup)99));
        Should.Throw<ArgumentOutOfRangeException>(() => ModelsCopy.AuthLabel((ProviderAuthKind)99));
        Should.Throw<ArgumentOutOfRangeException>(() => ModelsCopy.CredentialLabel((CredentialState)99));
        Should.Throw<ArgumentOutOfRangeException>(() => ModelsCopy.TestText((ProviderTestStatus)99, null, ProviderGroup.Local));
    }

    /// <summary>The status line joins how the provider authenticates with whether omp holds a credential.</summary>
    [Fact]
    public void StatusLineJoinsAuthAndCredential()
    {
        ModelsCopy.StatusLine(ProviderAuthKind.OAuth, CredentialState.Unknown).ShouldBe("Sign-in through omp, credential state unknown");
        ModelsCopy.StatusLine(ProviderAuthKind.None, CredentialState.NotRequired).ShouldBe("No authentication, no credential needed");
    }

    /// <summary>The not-run sentence is the one the owner asked for, word for word.</summary>
    [Fact]
    public void NotRunSentenceIsExact()
    {
        ModelsCopy.TestNotRun.ShouldBe("Testing needs credentials the owner has not supplied; not run.");
    }
}
