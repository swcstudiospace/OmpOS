using Avalonia.Controls;
using OmpOS.Contracts.Ui;
using OmpOS.Native.Views;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Native.Views.Agents;
using OmpOS.Native.Views.Attachments;
using OmpOS.Native.Views.Blocks;
using OmpOS.Native.Views.Changes;
using OmpOS.Native.Views.Chats;
using OmpOS.Native.Views.Editor;
using OmpOS.Native.Views.Explorer;
using OmpOS.Native.Views.Kanban;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.Search;
using OmpOS.Native.Views.Terminal;
using OmpOS.Native.Views.Workspace;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Agents;
using OmpOS.Presentation.Changes;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.State;

namespace OmpOS.Native;

/// <summary>
/// Maps each presentation view model, and each transcript block, to the native view that renders it. The mapping is an
/// explicit type switch (D-20-11): there is no reflection and no view locator, and a type with no mapping is an error, not a guess.
/// </summary>
internal static class ViewMapping
{
    /// <summary>Creates the view for a presentation view model, with the model as its data context.</summary>
    /// <param name="viewModel">A presentation view model.</param>
    /// <returns>The native view bound to <paramref name="viewModel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="viewModel"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No native view is mapped for the type of <paramref name="viewModel"/>.</exception>
    public static Control Create(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return viewModel switch
        {
            SessionViewModel session => new SessionView { DataContext = session },
            ComposerViewModel composer => CreateComposer(composer),
            AgentsViewModel agents => new AgentsView { DataContext = agents },
            SideChatPanel sideChats => new SideChatListView { DataContext = sideChats },
            TranscriptSearchModel search => new TranscriptSearchView { DataContext = search },
            EditorViewModel editor => new EditorView { DataContext = editor },
            WorkspaceTree tree => new WorkspaceTreeView { DataContext = tree },
            WorkspaceConventionsModel conventions => new WorkspaceConventionsView { DataContext = conventions },
            McpToolsModel mcpTools => new McpToolsView { DataContext = mcpTools },
            WorkspaceSearchModel workspaceSearch => new WorkspaceSearchView { DataContext = workspaceSearch },
            ChangeSetModel changes => new ChangesBarView { DataContext = changes },
            SessionCatalogModel catalog => new AgentRailView { DataContext = catalog },
            AgentPanelViewModel panel => new PanelStateView { DataContext = panel },
            AgentSessionViewModel agentSession => new ThreadHeaderView { DataContext = agentSession },
            StageTimelineModel timeline => new PipelineStripView { DataContext = timeline },
            KanbanBoardModel board => new KanbanBoardView { DataContext = board },
            OmpTerminalViewModel terminal => new OmpTerminalView { DataContext = terminal },
            _ => throw new ArgumentOutOfRangeException(
                nameof(viewModel),
                viewModel.GetType(),
                "No native view is mapped for this view model type."),
        };
    }

    /// <summary>
    /// Creates the composer view with the Attach button's file picker and the clipboard reader of Ctrl+V, both over the window the view is shown in. The button appears only when the session also wired
    /// image input into the composer model (<see cref="ComposerViewModel.Images"/>), so a session without it shows neither.
    /// </summary>
    private static ComposerView CreateComposer(ComposerViewModel composer)
    {
        var view = new ComposerView { DataContext = composer };
        view.FilePicker = new StorageProviderFilePicker(() => TopLevel.GetTopLevel(view));
        view.ClipboardImages = new AvaloniaClipboardImageSource(() => TopLevel.GetTopLevel(view));
        return view;
    }

    /// <summary>Creates the view for one transcript block.</summary>
    /// <param name="block">A folded UI block.</param>
    /// <returns>The native view that draws <paramref name="block"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No native view is mapped for the type of <paramref name="block"/>.</exception>
    public static Control CreateBlock(UiBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return block switch
        {
            UserMessageBlock userMessage => new UserMessageView(userMessage),
            MarkdownBlock markdown => new MarkdownBlockView(markdown),
            CodeBlock code => new CodeBlockView(code),
            ToolCallBlock toolCall => new ToolCallBlockView(toolCall),
            ToolResultBlock result => new ToolResultBlockView(result),
            FileEditBlock edit => new FileEditBlockView(edit),
            PermissionBlock permission => new PermissionBlockView(permission),
            PlanBlock plan => new PlanBlockView(plan),
            DiffPlaceholderBlock diff => new DiffPlaceholderView(diff),
            ErrorBlock error => new ErrorBlockView(error),
            A2uiSurfaceBlock surface => new A2uiSurfaceView(surface),
            UnknownBlock unknown => new UnknownBlockView(unknown),
            _ => throw new ArgumentOutOfRangeException(
                nameof(block),
                block.GetType(),
                "No native view is mapped for this block type."),
        };
    }
}
