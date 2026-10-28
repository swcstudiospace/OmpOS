using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace OmpOS.UI.Components.Composing;

/// <summary>
/// The base of the composer's popups (the option chips and the model picker): it keeps the open state, and while a popup is open it asks
/// <c>ompos-keys.js</c> to report a pointer or focus change outside the popup, which closes it. Closing by Escape, by choosing and by
/// the toggle button stays with the derived component, which also moves the focus.
/// </summary>
public abstract class PopupComponent : ObservableComponent, IAsyncDisposable
{
    private const string ModulePath = "./_content/OmpOS.UI/scripts/ompos-keys.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<PopupComponent>? _self;
    private int _token;
    private int _guardToken;
    private bool _watching;
    private bool _guarded;
    private bool _disposed;

    /// <summary>Gets or sets the JavaScript runtime. The framework injects it.</summary>
    [Inject]
    public IJSRuntime Js { get; set; } = null!;

    /// <summary>Gets whether the popup is open.</summary>
    protected bool IsOpen { get; private set; }

    /// <summary>Gets or sets the element that holds the toggle button and the popup. Pointer and focus changes outside it close the popup.</summary>
    protected ElementReference Root { get; set; }

    /// <summary>Gets or sets the list the popup shows while it is open. Arrow, Home and End keys pressed in it never scroll the page.</summary>
    protected ElementReference ListElement { get; set; }

    /// <summary>Gets the loaded keyboard module, or null before the first render.</summary>
    protected IJSObjectReference? Module => _module;

    /// <summary>Opens the popup.</summary>
    protected void OpenPopup() => IsOpen = true;

    /// <summary>Closes the popup.</summary>
    protected void ClosePopup() => IsOpen = false;

    /// <summary>Called by ompos-keys.js when a pointer goes down or the focus lands outside the popup.</summary>
    /// <returns>A task that completes when the component has re-rendered.</returns>
    [JSInvokable("OnOutside")]
    public Task OnOutsideAsync()
    {
        IsOpen = false;
        return InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
        {
            return;
        }

        if (IsOpen && !_watching)
        {
            _module ??= await Js.InvokeAsync<IJSObjectReference>("import", ModulePath).ConfigureAwait(true);
            _self ??= DotNetObjectReference.Create(this);
            _token = await _module.InvokeAsync<int>("watchOutside", Root, _self).ConfigureAwait(true);
            _watching = true;
        }
        else if (!IsOpen && _watching && _module is not null)
        {
            await _module.InvokeVoidAsync("release", _token).ConfigureAwait(true);
            _watching = false;
        }

        if (IsOpen && !_guarded && _module is not null)
        {
            _guardToken = await _module.InvokeAsync<int>("guardList", ListElement).ConfigureAwait(true);
            _guarded = true;
        }
        else if (!IsOpen && _guarded && _module is not null)
        {
            await _module.InvokeVoidAsync("release", _guardToken).ConfigureAwait(true);
            _guarded = false;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        try
        {
            if (_module is not null)
            {
                if (_watching)
                {
                    await _module.InvokeVoidAsync("release", _token).ConfigureAwait(false);
                }

                if (_guarded)
                {
                    await _module.InvokeVoidAsync("release", _guardToken).ConfigureAwait(false);
                }

                await _module.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the listeners went with it.
        }
        finally
        {
            _self?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
