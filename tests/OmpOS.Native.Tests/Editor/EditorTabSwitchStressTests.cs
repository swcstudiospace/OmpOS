using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Editor;

/// <summary>
/// Closing a tab or opening a file without a grammar while TextMate is still colouring the previous file must not hang the UI thread. TextMate's model and
/// its colouring transformer take their two locks in opposite orders when the grammar is cleared under a running tokenizer, so the editor releases TextMate
/// instead of clearing the grammar. The race window is narrow (the hang was seen about once in seven full runs of this assembly, in
/// <c>ClosingTheLastTabLeavesAnEmptyEditor</c>), so the stress tests repeat the sequences, and a regression shows up as a timeout instead of a hung run;
/// the install and release test pins the cause deterministically.
/// </summary>
public sealed class EditorTabSwitchStressTests
{
    private const int QuickRounds = 60;
    private const int Rounds = 6;
    private const int Switches = 100;
    // The test makes progress on every switch, so a deadlock is the only reason for it to run this long. The limit is generous because the sequence is
    // CPU bound and the suite runs beside other heavy processes: at a load average of 96 on 8 cores 120 seconds were not enough, and the cancelled run left
    // its work on the shared UI thread, which made the next 190 tests of the assembly fail.
    private const int TimeoutMilliseconds = 900_000;

    /// <summary>
    /// Switching between a long C# file and a file without a grammar, then closing both tabs, always completes and leaves the right text. Each switch back to
    /// the C# file starts a tokenizer over a long document, and each switch to the other file clears the grammar of a tokenizer that is still busy.
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task SwitchingAndClosingWhileTheTokenizerIsBusyNeverHangs()
    {
        var longSource = LongSource();
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            for (var round = 0; round < Rounds; round++)
            {
                var model = EditorHarness.NewModel();
                model.Open("src/Big.cs", longSource);
                model.Open("notes/readme.unknown", "plain notes");
                using var window = EditorHarness.Show(model);

                for (var switchIndex = 0; switchIndex < Switches; switchIndex++)
                {
                    model.Select("src/Big.cs");
                    EditorHarness.Code(window).Text.Length.ShouldBe(longSource.Length);
                    model.Select("notes/readme.unknown");
                    EditorHarness.Code(window).Text.ShouldBe("plain notes");
                }

                model.Select("src/Big.cs");
                EditorHarness.Settle(window);
                EditorHarness.Click(window, EditorHarness.TabClose(window, "readme.unknown"));
                EditorHarness.Click(window, EditorHarness.TabClose(window, "Big.cs"));

                model.Items.ShouldBeEmpty();
                EditorHarness.Code(window).Text.ShouldBe(string.Empty);
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A file without a grammar, and no file at all, are drawn plain by releasing TextMate: clearing the grammar of a live TextMate model is the call that
    /// can deadlock the UI thread (see the class summary), so the view never makes it. A file with a grammar is coloured again when it is selected.
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TextMateIsReleasedForAFileWithoutAGrammarAndInstalledAgainForOneWithIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            using var window = EditorHarness.Show(model);
            EditorHarness.View(window).IsHighlighting.ShouldBeFalse("no file is open");

            model.Open("src/Program.cs", "class Program { }");
            EditorHarness.Settle(window);
            EditorHarness.View(window).IsHighlighting.ShouldBeTrue("C# has a grammar");

            model.Open("notes/readme.unknown", "plain notes");
            EditorHarness.Settle(window);
            EditorHarness.View(window).IsHighlighting.ShouldBeFalse("an unknown extension has no grammar");
            EditorHarness.Code(window).Text.ShouldBe("plain notes");

            model.Select("src/Program.cs");
            EditorHarness.Settle(window);
            EditorHarness.View(window).IsHighlighting.ShouldBeTrue("selecting the C# file colours it again");
            EditorHarness.Code(window).Text.ShouldBe("class Program { }");

            model.Close("src/Program.cs");
            model.Close("notes/readme.unknown");
            EditorHarness.Settle(window);
            EditorHarness.View(window).IsHighlighting.ShouldBeFalse("no file is open again");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Closing the only tab right after the window showed it, the sequence a user produces by opening a file and closing it again, always completes. TextMate
    /// is still loading its grammar and starting its tokenizer when the close arrives.
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ClosingATabRightAfterItOpenedNeverHangs()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            for (var round = 0; round < QuickRounds; round++)
            {
                var model = EditorHarness.NewModel();
                model.Open("src/Program.cs", "class Program { }");
                using var window = EditorHarness.Show(model);

                EditorHarness.Click(window, EditorHarness.TabClose(window, "Program.cs"));

                model.Items.ShouldBeEmpty();
                EditorHarness.Code(window).Text.ShouldBe(string.Empty);
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static string LongSource()
    {
        var builder = new StringBuilder();
        builder.AppendLine("namespace Sample;");
        for (var index = 0; index < 4000; index++)
        {
            builder.Append("public sealed class Type").Append(index).AppendLine(" { public int Value => ").Append(index).AppendLine("; }");
        }

        return builder.ToString();
    }
}
