using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using OmpOS.Harness.Mcp.Templates;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Mcp.Tests.Templates;

/// <summary>The preset list holds the services the MCP page offers, every entry is complete and credential free, and an unverified entry never reaches the default list.</summary>
public sealed class McpTemplateCatalogTests
{
    private static readonly string[] RequiredServices =
    [
        "linear", "notion", "sentry", "github", "railway", "atlassian", "stripe", "supabase", "vercel", "context7", "filesystem", "fetch", "git",
    ];

    /// <summary>Gets the ids of the default presets, one theory case each.</summary>
    public static TheoryData<string> DefaultIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var preset in McpTemplateCatalog.Default)
            {
                data.Add(preset.Id);
            }

            return data;
        }
    }

    /// <summary>Every service the page promises is in the default list.</summary>
    [Fact]
    public void TheDefaultListHoldsTheRequiredServices()
    {
        RequiredServices.Except(McpTemplateCatalog.Default.Select(preset => preset.Id), StringComparer.Ordinal).ShouldBeEmpty();
    }

    /// <summary>An id is a valid server name, lower case, and appears once, so adding a preset under its own id never needs a rename.</summary>
    [Fact]
    public void IdsAreUniqueLowerCaseServerNames()
    {
        var ids = McpTemplateCatalog.All.Select(preset => preset.Id).ToList();

        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
        ids.ShouldAllBe(id => McpServerNames.IsValid(id) && !id.Any(char.IsUpper));
    }

    /// <summary>A remote preset is an https address without user information and without a credential in it, and a stdio preset has a command and arguments.</summary>
    [Fact]
    public void EveryPresetIsCompleteForItsKind()
    {
        foreach (var preset in McpTemplateCatalog.All)
        {
            preset.DisplayName.ShouldNotBeNullOrWhiteSpace(preset.Id);
            preset.Description.ShouldNotBeNullOrWhiteSpace(preset.Id);
            if (preset.Kind == McpTemplateKind.Http)
            {
                var uri = new Uri(preset.Address.ShouldNotBeNull(), UriKind.Absolute);
                uri.Scheme.ShouldBe("https", preset.Id);
                uri.UserInfo.ShouldBeEmpty(preset.Id);
                uri.Query.ShouldBeEmpty(preset.Id);
                preset.Command.ShouldBeNull(preset.Id);
                preset.Args.ShouldBeEmpty(preset.Id);
            }
            else
            {
                preset.Command.ShouldNotBeNullOrWhiteSpace(preset.Id);
                preset.Args.ShouldNotBeEmpty(preset.Id);
                preset.Address.ShouldBeNull(preset.Id);
                preset.Auth.ShouldBe(McpPresetAuth.None, preset.Id);
            }
        }
    }

    /// <summary>A token preset names an environment variable; a preset that signs in another way names none. No preset holds a token.</summary>
    [Fact]
    public void TokenVariablesMatchTheAuthMode()
    {
        foreach (var preset in McpTemplateCatalog.All)
        {
            if (preset.Auth == McpPresetAuth.BearerToken)
            {
                var variable = preset.TokenVariable.ShouldNotBeNull();
                variable.ShouldAllBe(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_', preset.Id);
                char.IsAsciiDigit(variable[0]).ShouldBeFalse(preset.Id);
            }
            else
            {
                preset.TokenVariable.ShouldBeNull(preset.Id);
            }
        }
    }

    /// <summary>Every preset cites the vendor page it was read from and the day it was read.</summary>
    [Fact]
    public void EveryPresetCitesItsSource()
    {
        foreach (var preset in McpTemplateCatalog.All)
        {
            new Uri(preset.DocumentationAddress, UriKind.Absolute).Scheme.ShouldBe("https", preset.Id);
            DateOnly.TryParseExact(preset.VerifiedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _).ShouldBeTrue(preset.Id);
        }
    }

    /// <summary>The default list is the verified part of the full list: an unverified preset is left out and nothing else is.</summary>
    [Fact]
    public void UnverifiedPresetsAreLeftOutOfTheDefaultList()
    {
        var verified = McpTemplateCatalog.All[0];
        var unverified = verified with { Id = "unconfirmed", Status = McpPresetStatus.Unverified };

        var kept = McpTemplateCatalog.Verified([verified, unverified]);

        kept.ShouldBe([verified]);
        McpTemplateCatalog.Default.ShouldAllBe(preset => preset.Status == McpPresetStatus.Verified);
    }

    /// <summary>A preset is found by its ordinal id; an unknown id is no preset.</summary>
    /// <param name="id">The id of a default preset.</param>
    [Theory]
    [MemberData(nameof(DefaultIds))]
    public void AVerifiedPresetIsFoundById(string id)
    {
        McpTemplateCatalog.Find(id).ShouldNotBeNull().Id.ShouldBe(id);
    }

    /// <summary>An id that differs in case or is unknown finds nothing, and a null id is refused.</summary>
    [Fact]
    public void FindIsOrdinalAndRefusesNull()
    {
        McpTemplateCatalog.Find("Linear").ShouldBeNull();
        McpTemplateCatalog.Find("nothing").ShouldBeNull();
        _ = Should.Throw<ArgumentNullException>(() => McpTemplateCatalog.Find(null!));
    }
}
