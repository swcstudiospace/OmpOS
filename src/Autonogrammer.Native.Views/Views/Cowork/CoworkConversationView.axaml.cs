using Autonogrammer.Presentation.Cowork;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The Cowork conversation: transcript, composer, then approvals. Image blocks go through <see cref="ViewMapping.CreateBlock"/>,
/// which draws them with the image block view.
/// </summary>
public partial class CoworkConversationView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    /// <summary>Creates the view.</summary>
    public CoworkConversationView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as CoworkConversationModel);
    }

    /// <summary>Gets the transcript region.</summary>
    public Border TranscriptRegion => Transcript;

    /// <summary>Gets the composer view.</summary>
    public CoworkComposerView ComposerView => Composer;

    /// <summary>Gets the approvals region.</summary>
    public StackPanel ApprovalsRegion => Approvals;

    private void Bind(CoworkConversationModel? model)
    {
        TranscriptBlocks.Children.Clear();
        Approvals.Children.Clear();
        Classes.Set(ReducedMotionClass, model?.ReducedMotion == true);
        if (model is null)
        {
            Composer.DataContext = null;
            return;
        }

        Composer.DataContext = model.ComposerSurface;
        foreach (var block in model.Session.Tree.Blocks)
        {
            TranscriptBlocks.Children.Add(ViewMapping.CreateBlock(block));
        }

        foreach (var card in model.Approvals.Cards)
        {
            Approvals.Children.Add(new CoworkApprovalCard { DataContext = card });
        }
    }
}
