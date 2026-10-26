using Avalonia.Controls;
using OmpOS.Native.Settings;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>A Settings window that records what the host did with it, in place of the real window.</summary>
internal sealed class FakeSettingsWindow : ISettingsWindowHandle
{
    public FakeSettingsWindow(SettingsWindowModel model)
    {
        Model = model;
    }

    /// <inheritdoc />
    public event EventHandler? Closed;

    /// <summary>Gets the model the window was built over.</summary>
    public SettingsWindowModel Model { get; }

    /// <summary>Gets how many times the window was shown.</summary>
    public int ShowCount { get; private set; }

    /// <summary>Gets how many times the window was brought forward.</summary>
    public int ActivateCount { get; private set; }

    /// <summary>Gets the owner the window was shown over.</summary>
    public Window? Owner { get; private set; }

    /// <summary>Gets a value indicating whether the host closed the window because the application was closing.</summary>
    public bool ClosedForShutdown { get; private set; }

    /// <summary>Gets or sets the failure <see cref="Show"/> throws, or null.</summary>
    public Exception? ShowFailure { get; set; }

    /// <inheritdoc />
    public void Show(Window? owner)
    {
        if (ShowFailure is not null)
        {
            throw ShowFailure;
        }

        ShowCount++;
        Owner = owner;
    }

    /// <inheritdoc />
    public void Activate() => ActivateCount++;

    /// <inheritdoc />
    public void CloseDiscardingChanges()
    {
        ClosedForShutdown = true;
        Close();
    }

    /// <summary>Closes the window as the user does.</summary>
    public void Close() => Closed?.Invoke(this, EventArgs.Empty);
}
