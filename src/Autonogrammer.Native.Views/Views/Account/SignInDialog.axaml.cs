using Autonogrammer.Presentation.Accounts;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Account;

/// <summary>
/// The sign-in window. It hosts <see cref="AccountView"/> and is not placed in the window chrome. Phase 69 owns the slot that will show the account menu.
/// </summary>
internal partial class SignInDialog : Window
{
    /// <summary>Creates the dialog over a model.</summary>
    /// <param name="model">The account model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public SignInDialog(AccountViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        Account.Attach(model);
    }

    /// <summary>Gets the hosted account panel.</summary>
    internal AccountView Panel => Account;
}
