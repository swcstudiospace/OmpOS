using OmpOS.Contracts.Ui;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Abstractions.Tests;

/// <summary>Shape of the harness exception tree and event union.</summary>
public sealed class HarnessContractShapeTests
{
    /// <summary>Every typed exception preserves its message, context and inner exception.</summary>
    [Fact]
    public void DerivedExceptionsCarryContextAndInnerException()
    {
        var inner = new InvalidOperationException("root");
        HarnessException[] errors =
        [
            new ProcessStartHarnessException("a", "ctx", inner),
            new HandshakeTimeoutHarnessException("b", "ctx", inner),
            new MalformedFrameHarnessException("c", "ctx", inner),
            new DisconnectedHarnessException("d", "ctx", inner),
            new BackpressureHarnessException("e", "ctx", inner),
            new DuplicateCompletionHarnessException("f", "ctx", inner),
        ];

        foreach (var error in errors)
        {
            error.Context.ShouldBe("ctx");
            error.InnerException.ShouldBeSameAs(inner);
        }

        errors.Select(error => error.Message).ShouldBe(["a", "b", "c", "d", "e", "f"]);
    }

    /// <summary>Optional context and inner exception default to null.</summary>
    [Fact]
    public void ContextAndInnerExceptionAreOptional()
    {
        var error = new DisconnectedHarnessException("gone");

        error.Context.ShouldBeNull();
        error.InnerException.ShouldBeNull();
    }

    /// <summary>The invalid-transition exception exposes both endpoints.</summary>
    [Fact]
    public void InvalidTransitionExceptionExposesEndpoints()
    {
        var error = new InvalidHarnessTransitionException(HarnessState.Disposed, HarnessState.Ready);

        error.From.ShouldBe(HarnessState.Disposed);
        error.To.ShouldBe(HarnessState.Ready);
        error.Message.ShouldContain("Disposed");
    }

    /// <summary>The event union is closed at exactly eleven derived records (the eleventh, <see cref="ContextUsageEvent"/>, carries the context ring's token counts since Phase 63).</summary>
    [Fact]
    public void HarnessEventUnionHasElevenClosedCases()
    {
        var cases = typeof(HarnessEvent).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(HarnessEvent)))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        cases.ShouldBe(
        [
            "CompletedEvent",
            "ContextUsageEvent",
            "DiagnosticEvent",
            "ErrorEvent",
            "PermissionRequestEvent",
            "PlanEvent",
            "TextDeltaEvent",
            "ThoughtEvent",
            "ToolCallEvent",
            "ToolCallUpdateEvent",
            "UiBlockEvent",
        ]);
    }

    /// <summary>A UI block event carries the Contracts block unchanged.</summary>
    [Fact]
    public void UiBlockEventCarriesContractsBlock()
    {
        var block = new MarkdownBlock { Id = "b", Markdown = "# x" };

        new UiBlockEvent(block).Block.ShouldBeSameAs(block);
    }
}
