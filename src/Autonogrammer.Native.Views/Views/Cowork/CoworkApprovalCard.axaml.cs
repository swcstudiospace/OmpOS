using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>An approval card. It renders the host-observed target and details, never model text.</summary>
public partial class CoworkApprovalCard : UserControl
{
    /// <summary>Creates the card.</summary>
    public CoworkApprovalCard()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as CoworkApprovalCardModel);
    }

    private void Bind(CoworkApprovalCardModel? card)
    {
        if (card is null)
        {
            TargetText.Text = string.Empty;
            DetailsText.Text = string.Empty;
            return;
        }

        TargetText.Text = card.Target;
        DetailsText.Text = card.Details;
        AutomationProperties.SetName(this, card.AccessibleName);
    }
}
