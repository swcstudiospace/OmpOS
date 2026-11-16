using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Views.Account;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Accounts;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Supplies the conversation delegate and the sign-in seam to the phase-69 builder. It does not build a second <see cref="CoworkSurfaceServices"/>.
/// </summary>
internal static class CoworkSurfaceServicesFactory
{
    /// <summary>
    /// Builds the window services and adds <see cref="CoworkSurfaceServices.OpenConversation"/>. A signed-out head gets the signed-out services.
    /// </summary>
    /// <param name="head">The composed head.</param>
    /// <param name="openSignIn">
    /// Opens the sign-in window over a model. Null opens the real <c>SignInDialog</c> modally over the main window. Tests pass a fake that records the call.
    /// </param>
    /// <returns>The services, or null when the head has no Cowork services.</returns>
    public static CoworkSurfaceServices? TryBuild(AgentHead head, Action<AccountViewModel>? openSignIn = null)
    {
        var built = CoworkSessionCatalogAdapter.TryBuild(head, openSignIn ?? new Action<AccountViewModel>(ShowSignInWindow));
        if (built is null)
        {
            return null;
        }

        return built with
        {
            OpenConversation = sessionId => head.Cowork?.Sessions.ConversationOf(sessionId),
        };
    }

    /// <summary>Shows the sign-in window modally over the main window. Without a main window it shows the window unowned.</summary>
    /// <param name="model">The account model the window hosts.</param>
    internal static void ShowSignInWindow(AccountViewModel model)
    {
        var dialog = new SignInDialog(model);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            _ = dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
    }
}
