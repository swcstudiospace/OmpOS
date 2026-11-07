using System.Globalization;
using System.Windows.Input;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Presentation.Computer;

/// <summary>One grant row. Revoke calls the registry. The model cannot create a grant.</summary>
public sealed class ComputerGrantRow
{
    /// <summary>Creates a row.</summary>
    /// <param name="grant">The grant.</param>
    /// <param name="revoke">Revokes it.</param>
    public ComputerGrantRow(ComputerGrantView grant, Action<string> revoke)
    {
        ArgumentNullException.ThrowIfNull(grant);
        GrantId = grant.GrantId;
        Text = ComputerUseCopy.GrantRow(grant.SessionId, grant.AppIdentity, grant.Tier.ToString(), grant.Origin.ToString(), grant.Expiry.ToString("u", CultureInfo.InvariantCulture));
        Revoke = new RowCommand(() => revoke(grant.GrantId));
    }

    /// <summary>Gets the grant id.</summary>
    public string GrantId { get; }

    /// <summary>Gets the row text: session, application, tier, origin, expiry.</summary>
    public string Text { get; }

    /// <summary>Gets the revoke command.</summary>
    public ICommand Revoke { get; }

    private sealed class RowCommand : ICommand
    {
        private readonly Action _execute;

        public RowCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
