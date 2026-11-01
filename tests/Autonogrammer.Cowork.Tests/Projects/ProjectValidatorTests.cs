using System.Text;
using Autonogrammer.Cowork.Projects;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Projects;

/// <summary>D-03 limits: name 1-100, instructions 16 KiB, 20 https links, 200 projects, useAccountMemory default false.</summary>
public sealed class ProjectValidatorTests
{
    [Fact]
    public void NameIsOneToOneHundredCharacters()
    {
        ProjectValidator.Validate(Sample(name: "")).ShouldNotBeEmpty();
        ProjectValidator.Validate(Sample(name: new string('a', 100))).ShouldBeEmpty();
        ProjectValidator.Validate(Sample(name: new string('a', 101))).ShouldNotBeEmpty();
        ProjectValidator.NameMaxChars.ShouldBe(100);
    }

    [Fact]
    public void DescriptionAndInstructionsRespectSixteenKibibyte()
    {
        var over = Encoding.UTF8.GetString(new byte[ProjectValidator.InstructionsMaxBytes + 1]);
        ProjectValidator.InstructionsMaxBytes.ShouldBe(16 * 1024);
        ProjectValidator.Validate(Sample(instructions: new string('x', 16 * 1024))).ShouldBeEmpty();
        ProjectValidator.Validate(Sample(instructions: over)).ShouldNotBeEmpty();
        ProjectValidator.Validate(Sample(description: over)).ShouldNotBeEmpty();
    }

    [Fact]
    public void ContextUrlsAreAtMostTwentyHttpsLinks()
    {
        ProjectValidator.ContextUrlMaxCount.ShouldBe(20);
        var urls = Enumerable.Range(0, 20).Select(static index => "https://example.test/" + index).ToArray();
        ProjectValidator.Validate(Sample(urls: urls)).ShouldBeEmpty();
        ProjectValidator.Validate(Sample(urls: [.. urls, "https://example.test/extra"])).ShouldNotBeEmpty();
        ProjectValidator.Validate(Sample(urls: ["http://example.test/no"])).ShouldNotBeEmpty();
    }

    [Fact]
    public void AccountHoldsAtMostTwoHundredProjects()
    {
        ProjectValidator.MaxProjectsPerAccount.ShouldBe(200);
        ProjectValidator.ValidateCount(200).ShouldBeEmpty();
        ProjectValidator.ValidateCount(201).ShouldNotBeEmpty();
    }

    [Fact]
    public void UseAccountMemoryDefaultsToFalse()
    {
        var project = new CoworkProject { Id = "p", Name = "Desk" };
        project.UseAccountMemory.ShouldBeFalse();
        ProjectValidator.Validate(project).ShouldBeEmpty();
    }

    private static CoworkProject Sample(string name = "Desk", string? description = null, string? instructions = null, string[]? urls = null) =>
        new()
        {
            Id = "p-1",
            Name = name,
            Description = description,
            Instructions = instructions,
            ContextUrls = urls ?? [],
        };
}
