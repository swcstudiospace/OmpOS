using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Composer;

/// <summary>Model selection: the composer shows the active model and provider, and a model switch the provider cannot take sets a notice (VX-02).</summary>
public sealed class ModelSelectionModelTests
{
    private sealed class FakeCapabilities(params ProviderCapability[] providers) : IProviderCapabilitySource
    {
        public ProviderCapability? Find(string providerId) =>
            providers.FirstOrDefault(provider => string.Equals(provider.ProviderId, providerId, StringComparison.Ordinal));
    }

    private static FakeCapabilities Capabilities() => new(
        new ProviderCapability("anthropic", "Anthropic", CanSwitchModel: true, ["claude-sonnet", "claude-opus"]),
        new ProviderCapability("pinned", "Pinned Provider", CanSwitchModel: false, ["only-model"]));

    /// <summary>The composer label names the provider by its display name and the active model.</summary>
    [Fact]
    public void ComposerShowsActiveModelAndProvider()
    {
        var selection = new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet");

        selection.ActiveProviderName.ShouldBe("Anthropic");
        selection.ActiveModelId.ShouldBe("claude-sonnet");
        selection.Label.ShouldBe("Anthropic / claude-sonnet");
        selection.AvailableModels.ShouldBe(["claude-sonnet", "claude-opus"]);
        selection.Notice.ShouldBeNull();
    }

    /// <summary>A provider the source does not know is shown by its identifier, with no models to pick.</summary>
    [Fact]
    public void UnknownProviderIsShownByItsIdentifier()
    {
        var selection = new ModelSelectionModel(Capabilities(), "mystery", "m1");

        selection.Label.ShouldBe("mystery / m1");
        selection.AvailableModels.ShouldBeEmpty();
    }

    /// <summary>A switch to a model the provider offers changes the active model, updates the label and raises the change.</summary>
    [Fact]
    public void SupportedModelSwitchChangesTheActiveModel()
    {
        var selection = new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet");
        var changed = new List<string?>();
        selection.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        selection.TrySwitchModel("claude-opus").ShouldBeTrue();

        selection.ActiveModelId.ShouldBe("claude-opus");
        selection.Label.ShouldBe("Anthropic / claude-opus");
        selection.Notice.ShouldBeNull();
        changed.ShouldContain(nameof(ModelSelectionModel.ActiveModelId), StringComparer.Ordinal);
        changed.ShouldContain(nameof(ModelSelectionModel.Label), StringComparer.Ordinal);
    }

    /// <summary>A switch to a model the provider does not offer is refused: the model stays, and a notice says why.</summary>
    [Fact]
    public void UnsupportedModelSwitchSetsANotice()
    {
        var selection = new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet");

        selection.TrySwitchModel("gpt-x").ShouldBeFalse();

        selection.ActiveModelId.ShouldBe("claude-sonnet");
        selection.Notice.ShouldBe("Anthropic does not offer the model 'gpt-x'.");
    }

    /// <summary>A provider that cannot switch model refuses even a model it lists, and says the provider cannot switch.</summary>
    [Fact]
    public void ProviderThatCannotSwitchSetsANotice()
    {
        var selection = new ModelSelectionModel(Capabilities(), "pinned", "only-model");

        selection.TrySwitchModel("only-model").ShouldBeFalse();

        selection.ActiveModelId.ShouldBe("only-model");
        selection.Notice.ShouldBe("Pinned Provider does not support switching the model in a session.");
    }

    /// <summary>A switch on a provider the source does not know is refused with a notice that names the provider.</summary>
    [Fact]
    public void SwitchOnAnUnknownProviderSetsANotice()
    {
        var selection = new ModelSelectionModel(Capabilities(), "mystery", "m1");

        selection.TrySwitchModel("m2").ShouldBeFalse();

        selection.ActiveModelId.ShouldBe("m1");
        selection.Notice.ShouldBe("The model cannot be changed: provider 'mystery' is not known.");
    }

    /// <summary>A later accepted switch clears the notice left by an earlier refused one.</summary>
    [Fact]
    public void AcceptedSwitchClearsThePreviousNotice()
    {
        var selection = new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet");
        selection.TrySwitchModel("gpt-x").ShouldBeFalse();
        selection.Notice.ShouldNotBeNull();

        selection.TrySwitchModel("claude-opus").ShouldBeTrue();

        selection.Notice.ShouldBeNull();
    }

    /// <summary>Model matching is ordinal: a different case is a different model.</summary>
    [Fact]
    public void ModelMatchingIsCaseSensitive()
    {
        var selection = new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet");

        selection.TrySwitchModel("Claude-Opus").ShouldBeFalse();

        selection.ActiveModelId.ShouldBe("claude-sonnet");
    }

    /// <summary>Null and blank arguments are refused.</summary>
    [Fact]
    public void InvalidArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => new ModelSelectionModel(null!, "anthropic", "claude-sonnet"));
        Should.Throw<ArgumentException>(() => new ModelSelectionModel(Capabilities(), " ", "claude-sonnet"));
        Should.Throw<ArgumentException>(() => new ModelSelectionModel(Capabilities(), "anthropic", string.Empty));
        Should.Throw<ArgumentException>(() => new ModelSelectionModel(Capabilities(), "anthropic", "claude-sonnet").TrySwitchModel(" "));
    }
}
