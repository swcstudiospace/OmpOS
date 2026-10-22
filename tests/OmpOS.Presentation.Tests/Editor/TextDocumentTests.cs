using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The line index of <see cref="TextDocument"/> and its range replacement (ADE-09).</summary>
public sealed class TextDocumentTests
{
    /// <summary>Lines, columns and offsets agree across an empty line and a final line without a break.</summary>
    [Fact]
    public void LineIndex_MapsLinesAndOffsetsIncludingAnEmptyLine()
    {
        var document = new TextDocument("alpha\nbeta\n\ngamma");

        document.LineCount.ShouldBe(4);
        document.GetLine(2).ShouldBe(string.Empty);
        document.GetLine(3).ShouldBe("gamma");
        document.GetPosition(8).ShouldBe(new TextPosition(1, 2));
        document.GetPosition(11).ShouldBe(new TextPosition(2, 0));
        document.GetOffset(3, 2).ShouldBe(14);
    }

    /// <summary>Every offset maps to a position and back to the same offset.</summary>
    [Fact]
    public void PositionAndOffset_RoundTripAtEveryOffset()
    {
        var document = new TextDocument("ab\n\ncd\nef");

        for (var offset = 0; offset <= document.Length; offset++)
        {
            var position = document.GetPosition(offset);
            document.GetOffset(position.Line, position.Column).ShouldBe(offset);
        }
    }

    /// <summary>The empty text has one empty line, and a trailing break adds a final empty line.</summary>
    [Fact]
    public void EmptyTextAndTrailingBreak_HaveTheExpectedLines()
    {
        new TextDocument(string.Empty).LineCount.ShouldBe(1);
        new TextDocument("a\n").LineCount.ShouldBe(2);
        new TextDocument("a\n").GetLine(1).ShouldBe(string.Empty);
    }

    /// <summary>A column past the end of its line is refused; the position before the break is accepted.</summary>
    [Fact]
    public void GetOffset_PastTheLineEnd_Throws()
    {
        var document = new TextDocument("ab\ncd");

        document.GetOffset(0, 2).ShouldBe(2);
        Should.Throw<ArgumentOutOfRangeException>(() => document.GetOffset(0, 3));
    }

    /// <summary>A replacement rebuilds the line index and announces the new text and line count.</summary>
    [Fact]
    public void Replace_RebuildsTheLineIndexAndAnnouncesTheChange()
    {
        var document = new TextDocument("one\ntwo\n");
        var announced = PropertyChangeRecorder.Record(document);

        document.Replace(4, 3, "2\n2b");

        document.Text.ShouldBe("one\n2\n2b\n");
        document.LineCount.ShouldBe(4);
        document.GetLine(2).ShouldBe("2b");
        announced.ShouldAnnounce(nameof(TextDocument.Text));
        announced.ShouldAnnounce(nameof(TextDocument.LineCount));
    }

    /// <summary>A replacement that leaves the text unchanged announces nothing.</summary>
    [Fact]
    public void Replace_WithTheSameText_AnnouncesNothing()
    {
        var document = new TextDocument("same");
        var announced = PropertyChangeRecorder.Record(document);

        document.Replace(1, 2, "am");

        announced.ShouldBeEmpty();
    }

    /// <summary>A range outside the text is refused and the text is unchanged.</summary>
    [Fact]
    public void Replace_OutsideTheText_ThrowsAndKeepsTheText()
    {
        var document = new TextDocument("abc");

        Should.Throw<ArgumentOutOfRangeException>(() => document.Replace(2, 5, "x"));
        document.Text.ShouldBe("abc");
    }
}
