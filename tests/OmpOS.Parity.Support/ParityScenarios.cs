using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.Parity.Support;

/// <summary>
/// The shared session-chat scenarios. Every head renders the same events and must produce the same normalized
/// blocks; the expectations are written by hand, never derived from one head's output.
/// </summary>
public static class ParityScenarios
{
    /// <summary>A prompt, then a streamed answer that is flushed into one markdown block at completion.</summary>
    public static ParityScenario UserTurnWithMarkdownAnswer() => Scenario(
        "UserTurnWithMarkdownAnswer",
        [
            ParityEvents.Prompt("Explain **Avalonia**"),
            ParityEvents.Delta("Avalonia is a "),
            ParityEvents.Delta("**UI** toolkit."),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.User, "Explain **Avalonia**"),
        Block(ParityKinds.Markdown, "Avalonia is a UI toolkit."));

    /// <summary>Streamed text is flushed into markdown before a code block, and the code keeps its language label.</summary>
    public static ParityScenario CodeBlockFlushesStreamedText() => Scenario(
        "CodeBlockFlushesStreamedText",
        [
            ParityEvents.Delta("Here is the sum:"),
            ParityEvents.Block(new CodeBlock { Id = "code-1", Language = "csharp", Code = "int total = a + b;" }),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.Markdown, "Here is the sum:"),
        Block(ParityKinds.Code, "csharp int total = a + b;"));

    /// <summary>A tool call appears running and its card follows the later status update in place.</summary>
    public static ParityScenario ToolCallCardTracksStatus() => Scenario(
        "ToolCallCardTracksStatus",
        [
            ParityEvents.Unknown("tool_call", """{"callId":"call-1","name":"read_file","payloadJson":"{\"path\":\"notes.txt\"}"}"""),
            ParityEvents.Unknown("tool_call_update", """{"callId":"call-1","status":"completed"}"""),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.ToolCall, "read_file completed"));

    /// <summary>A permission request shows its prompt, its options in order, and the read-only policy note.</summary>
    public static ParityScenario PermissionRequestListsOptionsInOrder() => Scenario(
        "PermissionRequestListsOptionsInOrder",
        [
            ParityEvents.Unknown("permission_request", """{"callId":"perm-1","name":"Run shell command","payloadJson":"{\"options\":[\"Allow once\",\"Deny\"]}"}"""),
        ],
        Block(ParityKinds.Permission, "Run shell command Allow once Deny The decision is made by policy."));

    /// <summary>Plan steps render in order, one per non-empty line.</summary>
    public static ParityScenario PlanStepsRenderInOrder() => Scenario(
        "PlanStepsRenderInOrder",
        [
            ParityEvents.Unknown("plan", """{"markdown":"Read the spec\nWrite the parser\n\nRun the tests"}"""),
        ],
        Block(ParityKinds.Plan, "Read the spec Write the parser Run the tests"));

    /// <summary>Thoughts stay in the log and never render as a block.</summary>
    public static ParityScenario ThoughtsStayOutOfTheView() => Scenario(
        "ThoughtsStayOutOfTheView",
        [
            ParityEvents.Unknown("thought", """{"text":"Weighing the options"}"""),
            ParityEvents.Delta("Answer"),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.Markdown, "Answer"));

    /// <summary>A retryable error keeps the partial output above it and still shows its message.</summary>
    public static ParityScenario RetryableErrorKeepsPartialOutput() => Scenario(
        "RetryableErrorKeepsPartialOutput",
        [
            ParityEvents.Delta("Partial answer"),
            ParityEvents.Error("Connection dropped", retryable: true),
        ],
        Block(ParityKinds.Markdown, "Partial answer"),
        Block(ParityKinds.Error, "Connection dropped"));

    /// <summary>A fatal error shows its message as an error block.</summary>
    public static ParityScenario FatalErrorShowsMessage() => Scenario(
        "FatalErrorShowsMessage",
        [
            ParityEvents.Error("Model unavailable", retryable: false),
        ],
        Block(ParityKinds.Error, "Model unavailable"));

    /// <summary>An unrecognised payload kind renders as a collapsed disclosure that shows its kind and raw JSON.</summary>
    public static ParityScenario UnknownKindStaysCollapsed() => Scenario(
        "UnknownKindStaysCollapsed",
        [
            ParityEvents.Unknown("mystery_kind", """{"value":42}"""),
        ],
        Block(ParityKinds.Unknown, "Unsupported content (mystery_kind) {\"value\":42}"));

    /// <summary>A diff placeholder shows its path and its reason.</summary>
    public static ParityScenario DiffPlaceholderShowsPath() => Scenario(
        "DiffPlaceholderShowsPath",
        [
            ParityEvents.Block(new DiffPlaceholderBlock { Id = "diff-1", Path = "src/Program.cs", Reason = "Diff rendering is not available yet" }),
        ],
        Block(ParityKinds.Diff, "src/Program.cs Diff rendering is not available yet"));

    /// <summary>
    /// An A2UI surface renders its heading, text, remote image as a link, button and list in payload order, and an
    /// unsupported component as a disclosure that carries its raw JSON.
    /// </summary>
    public static ParityScenario A2uiSurfaceRendersSupportedAndDisclosesUnsupported() => Scenario(
        "A2uiSurfaceRendersSupportedAndDisclosesUnsupported",
        [
            ParityEvents.Block(new A2uiSurfaceBlock
            {
                Id = "a2ui-1",
                SurfaceId = "surface-1",
                Payload = ParityEvents.Json("""{"components":[{"type":"heading","text":"Summary"},{"type":"text","text":"Three checks passed."},{"type":"image","url":"https://example.com/chart.png","alt":"Quarterly chart"},{"type":"button","label":"Open report"},{"type":"list","items":["unit","bunit"]},{"type":"chart"}]}"""),
            }),
        ],
        Block(ParityKinds.A2ui, "Summary Three checks passed. Quarterly chart Open report unit bunit Unsupported content (chart) {\"type\":\"chart\"}"));

    /// <summary>Streamed text that has not reached a block boundary renders after the flushed blocks.</summary>
    public static ParityScenario StreamingTailAfterFlushedBlocks() => Scenario(
        "StreamingTailAfterFlushedBlocks",
        [
            ParityEvents.Block(new CodeBlock { Id = "code-1", Language = "csharp", Code = "Console.WriteLine(1);" }),
            ParityEvents.Delta("Still writing"),
        ],
        Block(ParityKinds.Code, "csharp Console.WriteLine(1);"),
        Block(ParityKinds.Streaming, "Still writing"));

    /// <summary>Two turns keep their event order: each user turn precedes its own answer.</summary>
    public static ParityScenario TwoTurnsKeepEventOrder() => Scenario(
        "TwoTurnsKeepEventOrder",
        [
            ParityEvents.Prompt("First question"),
            ParityEvents.Delta("First answer"),
            ParityEvents.Completed(),
            ParityEvents.Prompt("Second question"),
            ParityEvents.Delta("Second answer"),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.User, "First question"),
        Block(ParityKinds.Markdown, "First answer"),
        Block(ParityKinds.User, "Second question"),
        Block(ParityKinds.Markdown, "Second answer"));

    /// <summary>A diagnostic is dropped and does not split the streamed text around it.</summary>
    public static ParityScenario DiagnosticsNeverRender() => Scenario(
        "DiagnosticsNeverRender",
        [
            ParityEvents.Delta("Hello"),
            ParityEvents.Diagnostic("rate limit warning"),
            ParityEvents.Delta(" there"),
            ParityEvents.Completed(),
        ],
        Block(ParityKinds.Markdown, "Hello there"));

    /// <summary>Every shared scenario, in a stable order.</summary>
    public static IReadOnlyList<ParityScenario> All { get; } =
    [
        UserTurnWithMarkdownAnswer(),
        CodeBlockFlushesStreamedText(),
        ToolCallCardTracksStatus(),
        PermissionRequestListsOptionsInOrder(),
        PlanStepsRenderInOrder(),
        ThoughtsStayOutOfTheView(),
        RetryableErrorKeepsPartialOutput(),
        FatalErrorShowsMessage(),
        UnknownKindStaysCollapsed(),
        DiffPlaceholderShowsPath(),
        A2uiSurfaceRendersSupportedAndDisclosesUnsupported(),
        StreamingTailAfterFlushedBlocks(),
        TwoTurnsKeepEventOrder(),
        DiagnosticsNeverRender(),
    ];

    private static ParityScenario Scenario(string name, StreamEventPayload[] payloads, params ParityBlock[] expected) =>
        new(name, ParityEvents.Sequence(payloads), expected);

    private static ParityBlock Block(string kind, string text) => new(kind, text);
}
