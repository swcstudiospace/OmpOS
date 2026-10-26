using Shouldly;
using Xunit;

namespace OmpOS.Harness.Completion.Tests;

/// <summary>Verifies the pure accept and dismiss functions (ADE-13): insertion at the caret and nothing else.</summary>
public sealed class CompletionAcceptanceTests
{
    /// <summary>ADE-13: accepting inserts the suggestion at the caret and moves the caret past it.</summary>
    [Fact]
    public void Accept_InsertsAtCaret_AndMovesCaretPastIt()
    {
        var accepted = CompletionAcceptance.Accept(new CaretBuffer("hello world", 5), CompletionResult.Suggested(", big"));

        accepted.Text.ShouldBe("hello, big world");
        accepted.Caret.ShouldBe(10);
    }

    /// <summary>ADE-13: accepting at the start or the end of the text inserts there.</summary>
    [Theory]
    [InlineData(0, "Hi:hello world", 3)]
    [InlineData(11, "hello worldHi:", 14)]
    public void Accept_AtBoundaries_InsertsThere(int caret, string expectedText, int expectedCaret)
    {
        var accepted = CompletionAcceptance.Accept(new CaretBuffer("hello world", caret), CompletionResult.Suggested("Hi:"));
        accepted.Text.ShouldBe(expectedText);
        accepted.Caret.ShouldBe(expectedCaret);
    }

    /// <summary>ADE-13: accepting does not change the buffer it was given.</summary>
    [Fact]
    public void Accept_DoesNotChangeTheInputBuffer()
    {
        var buffer = new CaretBuffer("hello world", 5);

        _ = CompletionAcceptance.Accept(buffer, CompletionResult.Suggested(", big"));

        buffer.Text.ShouldBe("hello world");
        buffer.Caret.ShouldBe(5);
    }

    /// <summary>ADE-13: a result without a suggestion cannot be accepted.</summary>
    [Theory]
    [InlineData(CompletionStatus.Disabled)]
    [InlineData(CompletionStatus.NoSuggestion)]
    public void Accept_ResultWithoutSuggestion_Throws(CompletionStatus status)
    {
        var result = status == CompletionStatus.Disabled ? CompletionResult.Disabled : CompletionResult.NoSuggestion;

        Should.Throw<InvalidOperationException>(() => CompletionAcceptance.Accept(new CaretBuffer("hello", 5), result));
    }

    /// <summary>ADE-13: dismissing returns the buffer unchanged.</summary>
    [Fact]
    public void Dismiss_ReturnsTheBufferUnchanged()
    {
        var buffer = new CaretBuffer("hello world", 5);

        CompletionAcceptance.Dismiss(buffer).ShouldBe(buffer);
    }

    /// <summary>ADE-13: a null buffer is rejected by both operations.</summary>
    [Fact]
    public void Accept_And_Dismiss_NullBuffer_Throw()
    {
        Should.Throw<ArgumentNullException>(() => CompletionAcceptance.Accept(null!, CompletionResult.Suggested("x")));
        Should.Throw<ArgumentNullException>(() => CompletionAcceptance.Dismiss(null!));
    }
}
