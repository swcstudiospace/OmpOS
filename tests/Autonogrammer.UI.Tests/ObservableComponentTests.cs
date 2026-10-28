using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>ObservableComponent re-renders on model change events and unsubscribes when disposed (research unit U0).</summary>
public sealed class ObservableComponentTests : UiTestContext
{
    /// <summary>A property change re-renders the component with the model's new value.</summary>
    [Fact]
    public async Task PropertyChangeRerendersTheComponent()
    {
        var model = new ObservableProbeModel();
        var cut = Render<ObservableProbe>(parameters => parameters.Add(p => p.Model, model));
        cut.Markup.ShouldContain("initial");

        await cut.InvokeAsync(() => model.Change("changed")).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Markup.ShouldContain("changed")).ConfigureAwait(true);
    }

    /// <summary>A collection change re-renders the component.</summary>
    [Fact]
    public async Task CollectionChangeRerendersTheComponent()
    {
        var model = new ObservableProbeModel();
        var cut = Render<ObservableProbe>(parameters => parameters.Add(p => p.Model, model));
        var before = cut.Instance.RenderCount;

        await cut.InvokeAsync(model.ResetCollection).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Instance.RenderCount.ShouldBe(before + 1)).ConfigureAwait(true);
    }

    /// <summary>Observing the same model on every parameter update subscribes once: one change renders once.</summary>
    [Fact]
    public async Task ObservingTheSameModelAgainDoesNotSubscribeTwice()
    {
        var model = new ObservableProbeModel();
        var cut = Render<ObservableProbe>(parameters => parameters.Add(p => p.Model, model));
        cut.Render(parameters => parameters.Add(p => p.Model, model));
        var before = cut.Instance.RenderCount;

        await cut.InvokeAsync(() => model.Change("once")).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Instance.RenderCount.ShouldBe(before + 1)).ConfigureAwait(true);
    }

    /// <summary>Replacing the model releases the old one and follows the new one.</summary>
    [Fact]
    public async Task ReplacingTheModelStopsObservingTheOldOne()
    {
        var first = new ObservableProbeModel();
        var second = new ObservableProbeModel();
        var cut = Render<ObservableProbe>(parameters => parameters.Add(p => p.Model, first));

        cut.Render(parameters => parameters.Add(p => p.Model, second));

        first.HasPropertyChangedSubscribers.ShouldBeFalse();
        first.HasCollectionChangedSubscribers.ShouldBeFalse();
        second.HasPropertyChangedSubscribers.ShouldBeTrue();
        var before = cut.Instance.RenderCount;
        await cut.InvokeAsync(() => first.Change("stale")).ConfigureAwait(true);
        cut.Instance.RenderCount.ShouldBe(before);
        await cut.InvokeAsync(() => second.Change("fresh")).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Markup.ShouldContain("fresh")).ConfigureAwait(true);
    }

    /// <summary>Disposing the component unsubscribes from both events, so a later change renders nothing.</summary>
    [Fact]
    public async Task DisposingUnsubscribesFromTheModel()
    {
        var model = new ObservableProbeModel();
        var cut = Render<ObservableProbe>(parameters => parameters.Add(p => p.Model, model));
        model.HasPropertyChangedSubscribers.ShouldBeTrue();
        model.HasCollectionChangedSubscribers.ShouldBeTrue();
        var instance = cut.Instance;

        await DisposeComponentsAsync().ConfigureAwait(true);

        model.HasPropertyChangedSubscribers.ShouldBeFalse();
        model.HasCollectionChangedSubscribers.ShouldBeFalse();
        var before = instance.RenderCount;
        model.Change("after dispose");
        instance.RenderCount.ShouldBe(before);
    }
}
