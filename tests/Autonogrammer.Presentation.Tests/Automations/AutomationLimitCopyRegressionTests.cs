using System.Globalization;
using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>
/// The editor's limit messages name the limit the editor enforces, because both read the same value: a changed limit changes its message.
/// </summary>
public sealed class AutomationLimitCopyRegressionTests
{
    /// <summary>A name over the editor limit is refused with a message that names that limit.</summary>
    [Fact]
    public void NameMessageNamesTheEditorLimit()
    {
        var model = Local();
        model.Name = new string('a', AutomationEditorModel.NameMaxLength + 1);

        model.Errors["name"].ShouldBe(AutomationsCopy.NameTooLong(AutomationEditorModel.NameMaxLength));
        model.Errors["name"].ShouldContain(Limit(AutomationEditorModel.NameMaxLength));
    }

    /// <summary>A prompt over the editor limit is refused with a message that names that limit.</summary>
    [Fact]
    public void PromptMessageNamesTheEditorLimit()
    {
        var model = Local();
        model.Prompt = new string('p', AutomationEditorModel.PromptMaxLength + 1);

        model.Errors["prompt"].ShouldBe(AutomationsCopy.PromptTooLong(AutomationEditorModel.PromptMaxLength));
        model.Errors["prompt"].ShouldContain(Limit(AutomationEditorModel.PromptMaxLength));
    }

    /// <summary>A cloud target over the repository limit is refused with a message that names that limit.</summary>
    [Fact]
    public void RepositoriesMessageNamesTheEditorLimit()
    {
        var model = Local();
        model.TargetKind = "cloud";
        model.RepositoriesText = string.Join('\n', Enumerable.Range(1, AutomationEditorModel.MaxRepositories + 1).Select(Repository));

        model.Errors["repositories"].ShouldBe(AutomationsCopy.RepositoriesTooMany(AutomationEditorModel.MaxRepositories));
        model.Errors["repositories"].ShouldContain(Limit(AutomationEditorModel.MaxRepositories));
    }

    /// <summary>A secret name over the editor limit is refused with a message that names that limit.</summary>
    [Fact]
    public void SecretNameMessageNamesTheEditorLimit()
    {
        var model = Local();
        model.TargetKind = "cloud";
        model.RepositoriesText = Repository(1);
        model.SecretRefsText = new string('S', AutomationEditorModel.SecretNameMaxLength + 1);

        model.Errors["secrets"].ShouldBe(AutomationsCopy.SecretNameInvalid(AutomationEditorModel.SecretNameMaxLength));
        model.Errors["secrets"].ShouldContain(Limit(AutomationEditorModel.SecretNameMaxLength));
    }

    /// <summary>Each limit message is built from the limit it is given, not from a fixed number.</summary>
    [Fact]
    public void LimitMessagesFollowTheLimitTheyAreGiven()
    {
        AutomationsCopy.NameTooLong(50).ShouldBe("The name is longer than 50 characters.");
        AutomationsCopy.PromptTooLong(500).ShouldBe("The prompt is longer than 500 characters.");
        AutomationsCopy.RepositoriesTooMany(3).ShouldBe("Use at most 3 repositories.");
        AutomationsCopy.SecretNameInvalid(8).ShouldEndWith("up to 8 characters.");
    }

    private static string Limit(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Repository(int number) =>
        "https://github.com/owner/repo" + number.ToString(CultureInfo.InvariantCulture);

    private static AutomationEditorModel Local() =>
        new(new FakeAutomationsManager(), null)
        {
            Name = "Nightly triage",
            Prompt = "Triage new issues.",
            CronText = "0 9 * * 1-5",
        };
}
