using Autonogrammer.Presentation.Computer;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Computer;

/// <summary>
/// Top-most indicator. The head places one on every display edge. This window does not take focus.
/// The gate denies when it is absent or minimised.
/// </summary>
public partial class ComputerIndicatorWindow : Window
{
    /// <summary>Creates the indicator for the XAML loader. Bind a model before showing it.</summary>
    public ComputerIndicatorWindow()
    {
        InitializeComponent();
    }

    /// <summary>Creates the indicator.</summary>
    /// <param name="model">The indicator model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public ComputerIndicatorWindow(ComputerIndicatorViewModel model)
        : this()
    {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
    }
}
