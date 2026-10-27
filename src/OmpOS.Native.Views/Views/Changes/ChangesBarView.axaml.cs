using Avalonia.Controls;
using OmpOS.Presentation.Changes;

namespace OmpOS.Native.Views.Changes;

/// <summary>
/// The Changes bar of a chat session: the files the agent changed, with their added and removed lines, and Review, Keep and Undo for each
/// file and for all of them. It binds a <see cref="ChangeSetModel"/>, which does every decision and every write; the view only draws the
/// model and hides itself while the session changed no file.
/// </summary>
internal partial class ChangesBarView : UserControl
{
    /// <summary>Creates the bar with no model bound.</summary>
    public ChangesBarView()
    {
        InitializeComponent();
    }
}
