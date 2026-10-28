using Microsoft.JSInterop;

namespace Autonogrammer.Web.Tests;

/// <summary>A JavaScript runtime for tests that never expect a call: any call fails.</summary>
internal sealed class NoJsRuntime : IJSRuntime
{
    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException("The test expects no JavaScript call: " + identifier);

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException("The test expects no JavaScript call: " + identifier);
}
