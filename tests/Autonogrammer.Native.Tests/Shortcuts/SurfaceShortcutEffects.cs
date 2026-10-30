using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.Shell;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Controls;
using Avalonia.Interactivity;

using Avalonia.Input;
using Shouldly;

namespace Autonogrammer.Native.Tests.Shortcuts;

/// <summary>The observed effect of the Code and Cowork shortcuts.</summary>
internal static class SurfaceShortcutEffects
{
    /// <summary>Raises the gesture and checks the surface, the handled flag and the selected toggle.</summary>
    /// <param name="gesture">The catalogue gesture.</param>
    /// <param name="surface">The surface it must show.</param>
    internal static void Switches(KeyGesture gesture, SessionSurface surface)
    {
        var catalog = new MemoryCoworkCatalog();
        using var shell = new CoworkShellModel(catalog);
        var services = new CoworkSurfaceServices { Catalog = catalog, Shell = shell };
        var panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System);
        var window = new MainWindow(panel, MotionPreference.System, cowork: services) { Width = 1280, Height = 800 };
        try
        {
            window.Show();
            ShellHarness.Settle(window);
            var args = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Route = RoutingStrategies.Tunnel,
                Key = gesture.Key,
                KeyModifiers = gesture.KeyModifiers,
                Source = window,
            };
            window.RaiseEvent(args);
            ShellHarness.Settle(window);
            var host = window.Content.ShouldBeOfType<SurfaceHost>();
            host.Switch.Selected.ShouldBe(surface);
            var view = surface == SessionSurface.Cowork
                ? host.Cowork!.SwitchSlot.Content.ShouldBeOfType<SurfaceSwitchView>()
                : host.Code.FindControl<ContentControl>("SurfaceSwitchSlot")!.Content.ShouldBeOfType<SurfaceSwitchView>();
            var toggle = surface == SessionSurface.Cowork ? view.CoworkButton : view.CodeButton;
            toggle.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
            panel.Dispose();
        }
    }
}
