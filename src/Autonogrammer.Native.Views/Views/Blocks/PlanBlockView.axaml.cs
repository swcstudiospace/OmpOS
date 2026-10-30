using Autonogrammer.Contracts.Ui;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A plan panel: its steps as a numbered list, in order.</summary>
internal partial class PlanBlockView : UserControl
{
    /// <summary>Builds the panel for one plan.</summary>
    /// <param name="block">The plan block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public PlanBlockView(PlanBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        for (var index = 0; index < block.Steps.Count; index++)
        {
            Steps.Children.Add(BlockControls.Row($"{index + 1}.", BlockControls.Text(block.Steps[index], "plan-step")));
        }
    }
}
