using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Memory;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The memory settings checkboxes save through the settings model when the person changes them, and not when the view is bound.</summary>
public sealed class MemoryViewTests
{
    /// <summary>Changing a checkbox saves all three settings with the new value, and binding the view saves nothing.</summary>
    [Fact]
    public async Task ChangingACheckboxSavesTheSettingsAndBindingSavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var source = new FakeMemorySettingsSource();
            var settings = new MemorySettingsModel(source);
            var view = new MemoryView();

            view.Attach(new MemoryListModel(new EmptyMemoryList()), settings);
            source.Saves.ShouldBe(0);

            view.FindControl<CheckBox>("PausedBox")!.IsChecked = true;

            source.Saves.ShouldBe(1);
            source.Last.ShouldBe(new MemorySettingsSnapshot(false, true, false));
            return Task.CompletedTask;
        }, token).ConfigureAwait(true);
    }

    private sealed class EmptyMemoryList : IMemoryListSource
    {
        public Task<CoworkChannelResult<IReadOnlyList<MemoryListItem>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<IReadOnlyList<MemoryListItem>>(true, false, [], null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }

    private sealed class FakeMemorySettingsSource : IMemorySettingsSource
    {
        public int Saves { get; private set; }

        public MemorySettingsSnapshot? Last { get; private set; }

        public Task<CoworkChannelResult<MemorySettingsSnapshot>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<MemorySettingsSnapshot>(true, false, new MemorySettingsSnapshot(false, false, false), null, null));

        public Task<CoworkChannelResult<bool>> SaveAsync(MemorySettingsSnapshot snapshot, CancellationToken cancellationToken)
        {
            Saves++;
            Last = snapshot;
            return Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
        }
    }
}
