using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The project editor view: a typed name is the name renamed, the view follows its models, and the rail shows the attached editor.</summary>
public sealed class ProjectsViewTests
{
    /// <summary>The name the person typed is the name the rename sends, not the name the project opened with.</summary>
    [Fact]
    public async Task ATypedNameIsTheNameThatIsRenamed()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeProjectEditorSource("Alpha");
            var editor = new ProjectEditorModel(source);
            var view = new ProjectsView();
            view.Attach(new ProjectListModel(new EmptyProjectList()), editor);
            await editor.OpenAsync("p1", token).ConfigureAwait(true);

            var nameBox = view.FindControl<TextBox>("NameBox")!;
            nameBox.Text = "Beta";
            view.FindControl<Button>("RenameButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            source.RenamedTo.ShouldBe("Beta");
        }, token).ConfigureAwait(true);
    }

    /// <summary>An editor that changes after the view is attached shows its new name without the view being attached again.</summary>
    [Fact]
    public async Task AnOpenedProjectsNameAppearsInTheBoxAfterAttach()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var editor = new ProjectEditorModel(new FakeProjectEditorSource("Alpha"));
            var view = new ProjectsView();
            view.Attach(new ProjectListModel(new EmptyProjectList()), editor);

            await editor.OpenAsync("p1", token).ConfigureAwait(true);

            view.FindControl<TextBox>("NameBox")!.Text.ShouldBe("Alpha");
        }, token).ConfigureAwait(true);
    }

    /// <summary>The rail's Projects section shows the editor it was attached to, and the same view is shown each time it is opened.</summary>
    [Fact]
    public async Task TheRailsProjectsSectionShowsTheAttachedEditor()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var editor = new ProjectEditorModel(new FakeProjectEditorSource("Alpha"));
            await editor.OpenAsync("p1", token).ConfigureAwait(true);
            var rail = new CoworkRailView();
            rail.AttachProjects(new ProjectListModel(new EmptyProjectList()), editor);

            rail.ProjectsSection.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var first = rail.ProjectEditor.Content.ShouldBeOfType<ProjectsView>();
            first.FindControl<TextBox>("NameBox")!.Text.ShouldBe("Alpha");

            rail.ProjectsSection.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            rail.ProjectEditor.Content.ShouldBeSameAs(first);
        }, token).ConfigureAwait(true);
    }

    private sealed class EmptyProjectList : IProjectListSource
    {
        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, [], null, null));
    }

    private sealed class FakeProjectEditorSource(string name) : IProjectEditorSource
    {
        public string? RenamedTo { get; private set; }

        public Task<CoworkChannelResult<ProjectListItem>> ReadAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<ProjectListItem>(true, false, new ProjectListItem(id, name, false, 0, 0), null, null));

        public Task<CoworkChannelResult<bool>> RenameAsync(string id, string newName, CancellationToken cancellationToken)
        {
            RenamedTo = newName;
            return Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
        }

        public Task<CoworkChannelResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));

        public Task<CoworkChannelResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<bool>(true, false, true, null, null));
    }
}
