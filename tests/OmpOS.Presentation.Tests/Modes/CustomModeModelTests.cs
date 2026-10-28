using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Modes;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Modes;

/// <summary>Verifies pinning a skill as a mode: the prompt block, the chip, replacing and clearing, the Alt+Enter rule and every refusal.</summary>
public sealed class CustomModeModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ModeSkill Skill(string name, string? color = null) => new(name, name + " skill", "star", color, SlashScope.Project, ".ompos/skills/" + name + "/SKILL.md");

    private static async Task<(CustomModeModel Model, ScriptedModeSkillSource Source)> CreateAsync()
    {
        var source = new ScriptedModeSkillSource { Skills = [Skill("review", "Purple"), Skill("explain"), Skill("empty"), Skill("huge")] };
        source.Bodies["review"] = "\n  Review for correctness first.\n  Then style.\n";
        source.Bodies["explain"] = "Explain like I am new.";
        source.Bodies["empty"] = "   \n";
        source.Bodies["huge"] = new string('x', CustomModeCopy.MaxBodyChars + 1);
        var model = new CustomModeModel(source);
        await model.RefreshAsync(Token).ConfigureAwait(true);
        return (model, source);
    }

    [Fact]
    public async Task UseAsModePinsTheTrimmedBodyAndEveryPromptGetsItInFront()
    {
        var (model, _) = await CreateAsync().ConfigureAwait(true);

        model.Apply("fix it").ShouldBe("fix it");
        (await model.UseAsModeAsync("review", Token).ConfigureAwait(true)).ShouldBeTrue();

        model.IsActive.ShouldBeTrue();
        model.Apply("fix it").ShouldBe("<mode name=\"review\">\nReview for correctness first.\n  Then style.\n</mode>\n\nfix it");
        model.Apply("and again").ShouldStartWith("<mode name=\"review\">", Case.Sensitive);
        model.ChipText.ShouldBe("Mode: review");
        model.ChipName.ShouldContain("Activate to exit the mode", Case.Sensitive);
        model.ChipColorKey.ShouldBe("purple");
        model.ChipIcon.ShouldBe("star");
        model.StatusText.ShouldContain("until you exit the mode", Case.Sensitive);
    }

    [Fact]
    public async Task AnUnknownChipColourIsTheDefaultAndTheNameMatchIgnoresCase()
    {
        var (model, source) = await CreateAsync().ConfigureAwait(true);

        (await model.UseAsModeAsync(" EXPLAIN ", Token).ConfigureAwait(true)).ShouldBeTrue();

        model.ChipColorKey.ShouldBe("default");
        source.Skills[1] = Skill("explain", "chartreuse");
        await model.RefreshAsync(Token).ConfigureAwait(true);
        (await model.UseAsModeAsync("explain", Token).ConfigureAwait(true)).ShouldBeTrue();
        model.ChipColorKey.ShouldBe("default");
    }

    [Fact]
    public async Task ClearExitsTheModeAndPromptsGoUnchanged()
    {
        var (model, _) = await CreateAsync().ConfigureAwait(true);
        await model.UseAsModeAsync("review", Token).ConfigureAwait(true);

        model.Clear().ShouldBeTrue();

        model.IsActive.ShouldBeFalse();
        model.ChipText.ShouldBeEmpty();
        model.Apply("plain").ShouldBe("plain");
        model.StatusText.ShouldBe(CustomModeCopy.Exited);
        model.Clear().ShouldBeFalse();
    }

    [Fact]
    public async Task APinnedModeIsReplacedByTheNextAndKeptWhenTheNextCannotBePinned()
    {
        var (model, _) = await CreateAsync().ConfigureAwait(true);
        await model.UseAsModeAsync("review", Token).ConfigureAwait(true);

        (await model.UseAsModeAsync("explain", Token).ConfigureAwait(true)).ShouldBeTrue();
        model.Active.ShouldNotBeNull().Skill.Name.ShouldBe("explain");

        (await model.UseAsModeAsync("empty", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldBe(CustomModeCopy.NoBody("empty"));
        model.HasError.ShouldBeTrue();
        (await model.UseAsModeAsync("huge", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldBe(CustomModeCopy.TooLarge("huge"));
        (await model.UseAsModeAsync("nothing", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldBe(CustomModeCopy.Unknown("nothing"));
        model.Active.ShouldNotBeNull().Skill.Name.ShouldBe("explain");
    }

    [Fact]
    public async Task ASkillThatCannotBeReadAnymoreAndASourceFailureAreSentencesNotExceptions()
    {
        var (model, source) = await CreateAsync().ConfigureAwait(true);
        source.Bodies.Remove("explain");

        (await model.UseAsModeAsync("explain", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldBe(CustomModeCopy.Unreadable("explain"));

        source.Failure = new IOException("disk gone");
        (await model.UseAsModeAsync("review", Token).ConfigureAwait(true)).ShouldBeFalse();
        model.StatusText.ShouldContain("disk gone", Case.Sensitive);
        await model.RefreshAsync(Token).ConfigureAwait(true);
        model.HasError.ShouldBeTrue();
        model.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task AltEnterPinsTheSkillAndAPlainAcceptInsertsItsToken()
    {
        var (model, _) = await CreateAsync().ConfigureAwait(true);

        (await model.AcceptFromPaletteAsync("review", altEnter: false, Token).ConfigureAwait(true)).ShouldBe(ModePaletteResult.InsertSkillToken);
        model.IsActive.ShouldBeFalse();
        (await model.AcceptFromPaletteAsync("review", altEnter: true, Token).ConfigureAwait(true)).ShouldBe(ModePaletteResult.ModeActivated);
        model.IsActive.ShouldBeTrue();
        (await model.AcceptFromPaletteAsync("empty", altEnter: true, Token).ConfigureAwait(true)).ShouldBe(ModePaletteResult.Failed);
    }

    [Fact]
    public async Task TheListFollowsTheSourceAndAnEmptyListSaysHowToAddASkill()
    {
        var source = new ScriptedModeSkillSource();
        var model = new CustomModeModel(source);

        await model.RefreshAsync(Token).ConfigureAwait(true);
        model.EmptyText.ShouldBe(CustomModeCopy.Empty);

        source.Skills = [Skill("a")];
        await model.RefreshAsync(Token).ConfigureAwait(true);

        model.Skills.Select(skill => skill.Name).ShouldBe(["a"]);
        model.EmptyText.ShouldBeEmpty();
    }
}
