using System.Windows.Input;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Views.Workspace;

/// <summary>One legacy notice row. The accessible name carries the severity and the path.</summary>
internal sealed class LegacyConventionNoticeRow
{
    private readonly WorkspaceConventionsView _view;

    /// <summary>Creates a row.</summary>
    /// <param name="notice">The notice.</param>
    /// <param name="view">The view that copies or moves.</param>
    public LegacyConventionNoticeRow(LegacyNotice notice, WorkspaceConventionsView view)
    {
        Notice = notice;
        _view = view;
        var severity = notice.Conflict ? "Warning" : "Note";
        Text = notice.LegacyPath + ": " + notice.MoveCommand;
        Name = severity + ". " + Text;
        ButtonText = string.Equals(notice.Scope, "user", StringComparison.Ordinal) ? "Move" : "Copy command";
        Command = new NoticeCommand(this);
    }

    /// <summary>Gets the notice.</summary>
    public LegacyNotice Notice { get; }

    /// <summary>Gets the sentence.</summary>
    public string Text { get; }

    /// <summary>Gets the accessible name: severity, path and command.</summary>
    public string Name { get; }

    /// <summary>Gets the button label.</summary>
    public string ButtonText { get; }

    /// <summary>Gets the button command. A project scope copies; it never runs git.</summary>
    public ICommand Command { get; }

    private sealed class NoticeCommand(LegacyConventionNoticeRow row) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            if (string.Equals(row.Notice.Scope, "user", StringComparison.Ordinal))
            {
                var home = Directory.GetParent(row.Notice.LegacyPath)?.FullName;
                if (home is not null)
                {
                    row._view.MoveUser(home);
                }

                return;
            }

            _ = row._view.CopyRepoCommandAsync(row.Notice.MoveCommand);
        }
    }
}
