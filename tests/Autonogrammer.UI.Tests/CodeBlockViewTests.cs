using System.Collections.Concurrent;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>A code block whose clipboard write is refused logs the failure once and never reports the copy (M-12, D6).</summary>
public sealed class CodeBlockViewTests : UiTestContext
{
    /// <summary>A refused clipboard write logs exactly one entry carrying the JS exception, and the copy callback does not fire.</summary>
    [Fact]
    public void AClipboardFailureLogsOnceAndDoesNotInvokeOnCopy()
    {
        var logger = new RecordingLogger();
        Services.AddSingleton<ILogger<CodeBlockView>>(logger);
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("Clipboard permission denied."));
        var copied = new List<string>();
        var cut = Render<CodeBlockView>(parameters => parameters
            .Add(p => p.Block, new CodeBlock { Id = "code-1", Language = "csharp", Code = "var x = 1;" })
            .Add(p => p.OnCopy, EventCallback.Factory.Create<string>(this, copied.Add)));

        cut.Find("button").Click();

        cut.WaitForAssertion(() => logger.Entries.Count.ShouldBe(1));
        logger.Entries.Single().Exception.ShouldBeOfType<JSException>();
        copied.ShouldBeEmpty();
    }

    /// <summary>A logger that records the level and exception of every entry it receives.</summary>
    private sealed class RecordingLogger : ILogger<CodeBlockView>
    {
        /// <summary>Gets the entries in the order they were logged.</summary>
        public ConcurrentQueue<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue((logLevel, exception));
    }
}
