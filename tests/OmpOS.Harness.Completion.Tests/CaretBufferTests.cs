using Shouldly;
using Xunit;

namespace OmpOS.Harness.Completion.Tests;

/// <summary>Verifies the caret boundaries of the buffer a completion is shown in (ADE-13).</summary>
public sealed class CaretBufferTests
{
    /// <summary>ADE-13: the caret may sit at either end of the text.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Caret_AtEitherEnd_IsAccepted(int caret)
    {
        new CaretBuffer("hello", caret).Caret.ShouldBe(caret);
    }

    /// <summary>ADE-13: a caret outside the text is rejected.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Caret_OutsideText_Throws(int caret)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CaretBuffer("hello", caret));
    }

    /// <summary>ADE-13: a caret between the two halves of a surrogate pair is rejected, so no insertion can split it.</summary>
    [Fact]
    public void Caret_InsideSurrogatePair_Throws()
    {
        const string text = "a\uD83D\uDE00b";

        Should.Throw<ArgumentException>(() => new CaretBuffer(text, 2));
        new CaretBuffer(text, 1).Caret.ShouldBe(1);
        new CaretBuffer(text, 3).Caret.ShouldBe(3);
    }

    /// <summary>ADE-13: a null text is rejected.</summary>
    [Fact]
    public void Text_Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new CaretBuffer(null!, 0));
    }
}
