using Avalonia.Controls;

namespace OmpOS.Native.Views.Remote;

/// <summary>
/// The fields of a WSL environment: name, distribution, user, folder, omp path, and the problems of the current values. It draws a
/// <see cref="OmpOS.Presentation.Remote.WslProfileEditor"/>; the section that hosts it owns the Save and Cancel buttons.
/// </summary>
internal partial class WslProfileView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="WslProfileView"/> class.</summary>
    public WslProfileView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the first field, which the section focuses when the form opens.</summary>
    internal TextBox FirstField => DisplayNameBox;

    /// <summary>Gets the distribution field.</summary>
    internal TextBox DistroField => DistroBox;

    /// <summary>Gets the list of problems.</summary>
    internal ItemsControl Issues => IssueRows;
}
