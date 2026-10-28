using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Views.Cloud;
using OmpOS.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Cloud;

/// <summary>The new cloud agent form, opened from the page: the cost confirmation, the entries and their messages, secrets as names only, the keyboard order and the accessibility contract.</summary>
public sealed class NewCloudAgentViewTests
{
    private const string Canary = "s3cr3t-canary-value";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] TabOrder(Avalonia.Visual root) =>
        [.. SettingsTree.TabStops(root)
            .Where(static control => control is Button or TextBox or ComboBox or CheckBox)
            .Select(static control => AutomationProperties.GetName(control) ?? string.Empty)];

    private static NewCloudAgentView Open(CloudScreen screen)
    {
        screen.Click(screen.View.NewAgent);
        return screen.View.NewAgentForm.ShouldNotBeNull();
    }

    private static void Fill(CloudScreen screen, NewCloudAgentView form)
    {
        form.NameField.Text = "Fix the build";
        form.RepositoryField.Text = "https://github.com/owner/app";
        screen.Settle();
        screen.Click(form.AddRepository);
    }

    /// <summary>The form replaces the list, starts with focus in the name field, and Create is disabled with the reason beside it.</summary>
    [Fact]
    public async Task TheFormOpensWithFocusInTheNameFieldAndCreateDisabled()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);

            screen.View.Empty.IsVisible.ShouldBeFalse();
            screen.View.NewAgent.IsEffectivelyVisible.ShouldBeFalse("the toolbar waits while the form is open");
            form.IsEffectivelyVisible.ShouldBeTrue();
            form.Create.IsEnabled.ShouldBeFalse();
            form.WhyDisabled.IsEffectivelyVisible.ShouldBeTrue();
            form.WhyDisabled.Text.ShouldBe(CloudCopy.NameRequired);
            screen.FocusedName().ShouldBe(CloudCopy.NameLabel);
            SettingsTree.Texts(form).ShouldContain(CloudCopy.NewAgentTitle, StringComparer.Ordinal);
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Create stays disabled until the entries are valid AND the cost box is ticked; the cost sentence is on screen the whole time and no request is made before.</summary>
    [Fact]
    public async Task CreateNeedsValidEntriesAndTheCostConfirmation()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            SettingsTree.Texts(form).ShouldContain(CloudCopy.CostNotice(15), StringComparer.Ordinal);
            form.CostConfirm.IsChecked.ShouldNotBe(true);

            Fill(screen, form);

            form.Repositories.ItemCount.ShouldBe(1);
            form.RepositoryField.Text.ShouldBeNullOrEmpty();
            form.Create.IsEnabled.ShouldBeFalse("the cost is not confirmed");
            form.WhyDisabled.Text.ShouldBe(CloudCopy.CostNotConfirmed);
            screen.Host.CountOf("Create").ShouldBe(0);

            form.CostConfirm.IsChecked = true;
            screen.Settle();

            form.Create.IsEnabled.ShouldBeTrue();
            form.WhyDisabled.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Changing the idle time updates the cost sentence and withdraws the confirmation, so the user confirms what is shown.</summary>
    [Fact]
    public async Task ChangingTheIdleTimeUpdatesTheCostAndUntickTheBox()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            Fill(screen, form);
            form.CostConfirm.IsChecked = true;
            screen.Settle();
            form.Create.IsEnabled.ShouldBeTrue();

            form.IdleField.Text = "30";
            screen.Settle();

            form.CostNotice.Text.ShouldBe(CloudCopy.CostNotice(30));
            form.CostConfirm.IsChecked.ShouldNotBe(true);
            form.Create.IsEnabled.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Create sends the entries, closes the form, lists the new agent and shows its details with the provisioning status.</summary>
    [Fact]
    public async Task CreateMakesTheAgentAndShowsIt()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            Fill(screen, form);
            form.CostConfirm.IsChecked = true;
            screen.Settle();

            screen.Click(form.Create);

            var request = screen.Host.CreateRequests.ShouldHaveSingleItem();
            request.Name.ShouldBe("Fix the build");
            request.Repositories.Select(repository => repository.Address).ShouldBe(["https://github.com/owner/app"]);
            request.IdleMinutes.ShouldBe(15);
            screen.View.NewAgentForm.ShouldBeNull();
            screen.View.NewAgentArea.IsVisible.ShouldBeFalse();
            screen.View.Agents.ItemCount.ShouldBe(1);
            screen.View.Details.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(screen.View).ShouldContain("Fix the build", StringComparer.Ordinal);
            SettingsTree.Texts(screen.View).ShouldContain(CloudCopy.StatusProvisioning, StringComparer.Ordinal);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A service refusal stays in the open form with its words in an assertive result, and Create can be pressed again.</summary>
    [Fact]
    public async Task ARefusalStaysInTheFormWithItsWords()
    {
        await CloudScreen.RunAsync(
            static host => host.FailNext("Create", new CloudHostException(CloudFailureKind.QuotaExceeded, "The compute quota is used up.")),
            screen =>
            {
                var form = Open(screen);
                Fill(screen, form);
                form.CostConfirm.IsChecked = true;
                screen.Settle();

                screen.Click(form.Create);

                screen.View.NewAgentForm.ShouldBeSameAs(form);
                form.Result.IsEffectivelyVisible.ShouldBeTrue();
                form.Result.Text.ShouldBe("The compute quota is used up.");
                AutomationProperties.GetLiveSetting(form.Result).ShouldBe(AutomationLiveSetting.Assertive);
                form.Create.IsEnabled.ShouldBeTrue();

                screen.Click(form.Create);

                screen.View.NewAgentForm.ShouldBeNull("the second try works");
                screen.Host.CountOf("Create").ShouldBe(2);
                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }

    /// <summary>Cancel closes the form without a request and gives focus back to New cloud agent.</summary>
    [Fact]
    public async Task CancelClosesTheFormAndRestoresFocus()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);

            screen.Click(form.Cancel);

            screen.View.NewAgentForm.ShouldBeNull();
            screen.View.Empty.IsEffectivelyVisible.ShouldBeTrue();
            screen.FocusedName().ShouldBe(CloudCopy.NewAgentLabel);
            screen.Host.CountOf("Create").ShouldBe(0);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// A secret value pasted into the secret name box is refused with a sentence that does not repeat it, is cleared from the box, never
    /// joins the list, and appears nowhere on the form.
    /// </summary>
    [Fact]
    public async Task APastedSecretValueNeverAppearsOnTheForm()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            form.SecretField.Text = "API_KEY=" + Canary;
            screen.Settle();

            screen.Click(form.AddSecret);

            form.SecretField.Text.ShouldBeNullOrEmpty();
            form.Secrets.ItemCount.ShouldBe(0);
            SettingsTree.Texts(form).ShouldContain(CloudCopy.SecretNameInvalid, StringComparer.Ordinal);
            SettingsTree.Everything(screen.View).Any(text => text.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse("no text, name or box of the form holds the value");
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A secret name is added to the list, each entry has a Remove button that names it, and removing it empties the list.</summary>
    [Fact]
    public async Task ASecretNameIsAddedAndRemoved()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            form.SecretField.Text = "API_KEY";
            screen.Settle();

            screen.Click(form.AddSecret);

            form.Secrets.ItemCount.ShouldBe(1);
            SettingsTree.Texts(form).ShouldContain("API_KEY", StringComparer.Ordinal);
            var remove = SettingsTree.Named<Button>(form, CloudCopy.RemoveSecretName("API_KEY"));

            screen.Click(remove);

            form.Secrets.ItemCount.ShouldBe(0);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The known stored secret names are offered as a pick list; picking one adds it as a name.</summary>
    [Fact]
    public async Task AStoredSecretCanBePickedByName()
    {
        await CloudScreen.RunAsync(
            static host => host.Setup = new CloudSetup { IsConfigured = true, KnownSecretNames = ["NPM_TOKEN", "DB_URL"] },
            screen =>
            {
                var form = Open(screen);
                form.KnownSecrets.ItemCount.ShouldBe(2);
                form.KnownSecrets.IsEffectivelyVisible.ShouldBeTrue();

                screen.Click(SettingsTree.Named<Button>(form, CloudCopy.AddKnownSecretLabel));

                form.Secrets.ItemCount.ShouldBe(1);
                form.KnownSecrets.ItemCount.ShouldBe(1);
                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }

    /// <summary>A repository address with a token in it is refused with a sentence that does not repeat the token, and nothing is added.</summary>
    [Fact]
    public async Task ARepositoryWithCredentialsIsRefusedWithoutEchoingThem()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            form.RepositoryField.Text = "https://" + Canary + "@github.com/owner/app";
            screen.Settle();

            screen.Click(form.AddRepository);

            form.Repositories.ItemCount.ShouldBe(0);
            var texts = SettingsTree.Texts(form);
            texts.ShouldContain(CloudCopy.RepositoryHasCredentials, StringComparer.Ordinal);
            texts.Any(text => text.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse("no message repeats the token");
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A repository can be removed again, and Create goes back to needing one.</summary>
    [Fact]
    public async Task ARepositoryCanBeRemoved()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            Fill(screen, form);
            SettingsTree.Texts(form).ShouldContain("owner/app", StringComparer.Ordinal);

            screen.Click(SettingsTree.Named<Button>(form, CloudCopy.RemoveRepositoryName("owner/app")));

            form.Repositories.ItemCount.ShouldBe(0);
            form.WhyDisabled.Text.ShouldBe(CloudCopy.RepositoryNeeded);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>When the host offers environments and models the form shows a pick list for each, preselected, and the choice reaches the request.</summary>
    [Fact]
    public async Task EnvironmentAndModelChoicesAreOfferedAndSent()
    {
        await CloudScreen.RunAsync(
            static host => host.Setup = new CloudSetup
            {
                IsConfigured = true,
                Environments = [new CloudChoice("dev", "Development"), new CloudChoice("prod", "Production")],
                DefaultEnvironmentId = "prod",
                Models = [new CloudChoice("fast", "Fast"), new CloudChoice("deep", "Deep")],
            },
            screen =>
            {
                var form = Open(screen);
                form.Environments.IsEffectivelyVisible.ShouldBeTrue();
                form.Models.IsEffectivelyVisible.ShouldBeTrue();
                form.Environments.ItemCount.ShouldBe(2);
                form.Models.ItemCount.ShouldBe(3, "the service default and two models");
                ((CloudChoice?)form.Environments.SelectedItem).ShouldNotBeNull().Id.ShouldBe("prod");

                form.Environments.SelectedIndex = 0;
                form.Models.SelectedIndex = 2;
                Fill(screen, form);
                form.CostConfirm.IsChecked = true;
                screen.Settle();
                screen.Click(form.Create);

                var request = screen.Host.CreateRequests.ShouldHaveSingleItem();
                request.EnvironmentId.ShouldBe("dev");
                request.Model.ShouldBe("deep");
                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }

    /// <summary>Without environments or models to choose the form shows neither pick list.</summary>
    [Fact]
    public async Task WithoutChoicesThePickListsAreAbsent()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);

            form.Environments.IsEffectivelyVisible.ShouldBeFalse();
            form.Models.IsEffectivelyVisible.ShouldBeFalse();
            form.KnownSecrets.IsEffectivelyVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A bad branch and a bad idle time show their message under the field and keep Create disabled.</summary>
    [Fact]
    public async Task FieldMessagesShowUnderTheirFields()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            Fill(screen, form);
            form.CostConfirm.IsChecked = true;
            form.BranchField.Text = "not a branch";
            form.IdleField.Text = "0";
            screen.Settle();

            var texts = SettingsTree.Texts(form);

            texts.ShouldContain(CloudCopy.BranchInvalid, StringComparer.Ordinal);
            texts.ShouldContain(CloudCopy.IdleInvalid(1, 240), StringComparer.Ordinal);
            form.Create.IsEnabled.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab walks the fields in the order they are drawn; Create and the Add buttons are skipped while they are disabled.</summary>
    [Fact]
    public async Task TabFollowsTheOrderOfTheFields()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);

            TabOrder(form).ShouldBe(
            [
                CloudCopy.NameLabel,
                CloudCopy.RepositoryEntryLabel,
                CloudCopy.BranchLabel,
                CloudCopy.BaseBranchLabel,
                CloudCopy.SecretEntryLabel,
                CloudCopy.IdleLabel,
                CloudCopy.FirstTaskLabel,
                CloudCopy.CostConfirmLabel,
                CloudCopy.CancelLabel,
            ]);

            _ = form.NameField.Focus();
            SettingsTree.PressTab(screen.Window);
            screen.FocusedName().ShouldBe(CloudCopy.RepositoryEntryLabel);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Enter in the repository box adds the repository; Enter in the secret box adds the name.</summary>
    [Fact]
    public async Task EnterAddsTheTypedEntry()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var form = Open(screen);
            form.RepositoryField.Text = "https://github.com/owner/app";
            screen.Settle();
            _ = form.RepositoryField.Focus();
            SettingsTree.Press(screen.Window, Avalonia.Input.Key.Enter, Avalonia.Input.PhysicalKey.Enter);

            form.Repositories.ItemCount.ShouldBe(1);

            form.SecretField.Text = "API_KEY";
            screen.Settle();
            _ = form.SecretField.Focus();
            SettingsTree.Press(screen.Window, Avalonia.Input.Key.Enter, Avalonia.Input.PhysicalKey.Enter);

            form.Secrets.ItemCount.ShouldBe(1);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every control of the form has an accessible name and meets the target size.</summary>
    [Fact]
    public async Task EveryControlOfTheFormIsNamedAndBigEnough()
    {
        await CloudScreen.RunAsync(
            static host => host.Setup = new CloudSetup
            {
                IsConfigured = true,
                Environments = [new CloudChoice("dev", "Development")],
                Models = [new CloudChoice("fast", "Fast")],
                KnownSecretNames = ["NPM_TOKEN"],
            },
            screen =>
            {
                var form = Open(screen);
                Fill(screen, form);
                form.SecretField.Text = "API_KEY";
                screen.Settle();
                screen.Click(form.AddSecret);

                SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
                var controls = SettingsTree.Visible<Button>(form).Cast<Control>()
                    .Concat(SettingsTree.Visible<TextBox>(form))
                    .Concat(SettingsTree.Visible<ComboBox>(form))
                    .Concat(SettingsTree.Visible<CheckBox>(form))
                    .Where(static control => control.TemplatedParent is null)
                    .ToArray();
                controls.Length.ShouldBeGreaterThan(10);
                foreach (var control in controls)
                {
                    var name = AutomationProperties.GetName(control);
                    name.ShouldNotBeNullOrWhiteSpace();
                    control.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too short");
                    control.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too narrow");
                }

                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }
}
