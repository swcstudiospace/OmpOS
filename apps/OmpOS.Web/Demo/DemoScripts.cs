using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Ui;

namespace OmpOS.Web.Demo;

/// <summary>The scripted content of the Demo: what the agents "say" and "do". Every answer starts with an honest Demo label.</summary>
internal static class DemoScripts
{
    /// <summary>The path of the first file the running session edits.</summary>
    internal const string TotalsPath = "src/Checkout/OrderTotals.cs";

    /// <summary>The path of the test the running session edits.</summary>
    internal const string TotalsTestPath = "tests/Checkout.Tests/CheckoutTotalTests.cs";

    private const string DemoLabel = "_Demo: this answer is scripted. No model was called and no file was changed._\n\n";

    /// <summary>Splits text into streamed chunks of about three words, keeping every character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>One typed step per chunk.</returns>
    internal static IEnumerable<DemoStep> Typed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split(' ');
        for (var index = 0; index < words.Length; index += 3)
        {
            var chunk = string.Join(' ', words.Skip(index).Take(3));
            yield return new DemoStep(DemoEvents.Delta(index + 3 < words.Length ? chunk + " " : chunk), Typed: true);
        }
    }

    private static DemoStep EditStep(string id, string callId, string path, string oldText, string newText) => new(
        DemoEvents.Block(new FileEditBlock
        {
            Id = id,
            CallId = callId,
            Path = path,
            OldText = oldText,
            NewText = newText,
        }),
        Typed: false,
        session => session.Changes.Add(new SessionChange
        {
            Path = path,
            Kind = ChangeKind.Modified,
            Status = ChangeReviewStatus.Pending,
            OldText = oldText,
            NewText = newText,
        }));

    /// <summary>The scripted turn of the running session: reproduce a failing test, explain the cause with code, edit two files, run the tests.</summary>
    /// <returns>The steps.</returns>
    internal static IReadOnlyList<DemoStep> CheckoutFix()
    {
        var steps = new List<DemoStep>();
        steps.AddRange(Typed(DemoLabel + "I'll start by reproducing the failing test, then look at how the total is rounded.\n\n"));
        steps.Add(new DemoStep(DemoEvents.Block(new ToolCallBlock
        {
            Id = "call-1",
            CallId = "call-1",
            ToolName = "bash",
            Status = "completed",
            Arguments = DemoEvents.Json("""{"command":"dotnet test tests/Checkout.Tests --filter CheckoutTotal"}"""),
        }), Typed: false));
        steps.Add(new DemoStep(DemoEvents.Block(new ToolResultBlock
        {
            Id = "result-1",
            CallId = "call-1",
            ToolName = "bash",
            Status = "completed",
            Output = "Failed CheckoutTotalTests.RoundsHalfUpToTheCent [12 ms]\n  Expected: 19.99\n  Actual:   19.98\nPassed: 41, Failed: 1",
        }), Typed: false));
        steps.AddRange(Typed("The test is right. The total is **truncated** to two decimals where the shop's rule is to **round half up**:\n\n"));
        steps.Add(new DemoStep(DemoEvents.Block(new CodeBlock
        {
            Id = "code-1",
            Language = "csharp",
            Code = "public decimal Total => decimal.Truncate(Subtotal * 100m) / 100m;",
        }), Typed: false));
        steps.AddRange(Typed("I'll round away from zero and add the case from the failing test as a regression test.\n\n"));
        steps.Add(EditStep(
            "edit-1",
            "call-2",
            TotalsPath,
            "public decimal Total => decimal.Truncate(Subtotal * 100m) / 100m;\n",
            "public decimal Total => decimal.Round(Subtotal, 2, MidpointRounding.AwayFromZero);\n"));
        steps.Add(EditStep(
            "edit-2",
            "call-3",
            TotalsTestPath,
            "[Fact]\npublic void TotalOfEmptyOrderIsZero() => Assert.Equal(0m, new Order().Total);\n",
            "[Fact]\npublic void TotalOfEmptyOrderIsZero() => Assert.Equal(0m, new Order().Total);\n\n[Fact]\npublic void RoundsHalfUpToTheCent() => Assert.Equal(19.99m, Order.WithSubtotal(19.985m).Total);\n"));
        steps.Add(new DemoStep(DemoEvents.Block(new ToolResultBlock
        {
            Id = "result-2",
            CallId = "call-4",
            ToolName = "bash",
            Status = "completed",
            Output = "Passed: 43, Failed: 0",
        }), Typed: false));
        steps.AddRange(Typed("Done. The total now rounds half up, and the regression test pins it. Review the two changed files in the Changes bar; accepting or rejecting them is saved in this demo only."));
        steps.Add(new DemoStep(DemoEvents.Completed(), Typed: false));
        return steps;
    }

    /// <summary>The scripted answer to a prompt the visitor sends in a Demo session.</summary>
    /// <returns>The steps.</returns>
    internal static IReadOnlyList<DemoStep> GenericReply()
    {
        var steps = new List<DemoStep>();
        steps.AddRange(Typed(DemoLabel + "Your message reached the Demo handler running in your browser. In the live app, omp would now work on it. A few things you can try here:\n\n"));
        steps.AddRange(Typed("- Open the **running**, **approval** and **completed** agents in the rail.\n- Open the Kanban board and the Settings dialog.\n- Send a steering message while an agent is running.\n\n"));
        steps.Add(new DemoStep(DemoEvents.Block(new CodeBlock
        {
            Id = "code-reply",
            Language = "bash",
            Code = "dotnet run --project services/OmpOS.Api   # the real API, then reload this page without the Demo build",
        }), Typed: false));
        steps.AddRange(Typed("Nothing you type here leaves this page."));
        steps.Add(new DemoStep(DemoEvents.Completed(), Typed: false));
        return steps;
    }

    /// <summary>The steps that finish the approval session after the visitor allowed the tool.</summary>
    /// <returns>The steps.</returns>
    internal static IReadOnlyList<DemoStep> AfterApproval()
    {
        var steps = new List<DemoStep>();
        steps.Add(new DemoStep(DemoEvents.Block(new ToolResultBlock
        {
            Id = "result-approved",
            CallId = "call-approval",
            ToolName = "bash",
            Status = "completed",
            Output = "payments-sdk 5.0.2 restored\nIntegration suite: 18 passed",
        }), Typed: false));
        steps.AddRange(Typed("The SDK is on v5 and the integration suite passes (scripted result)."));
        steps.Add(new DemoStep(DemoEvents.Completed(), Typed: false));
        return steps;
    }

    /// <summary>The steps that finish the approval session after the visitor denied the tool.</summary>
    /// <returns>The steps.</returns>
    internal static IReadOnlyList<DemoStep> AfterDenial()
    {
        var steps = new List<DemoStep>();
        steps.AddRange(Typed("Understood: I will not run the command. I stopped before changing anything."));
        steps.Add(new DemoStep(DemoEvents.Completed(), Typed: false));
        return steps;
    }

    /// <summary>The sentence the running turn adds when the visitor steered it.</summary>
    /// <returns>The steps.</returns>
    internal static IEnumerable<DemoStep> SteerAcknowledgement() =>
        Typed("\n\n_Noted your message. Adjusting course (scripted)._\n\n");
}
