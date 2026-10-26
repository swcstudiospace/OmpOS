using System.Text.Json.Nodes;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>
/// Answers the RPC commands from one recorded capture. A command is answered by the first captured response of the same
/// type, with its id rewritten. Each <c>prompt</c> starts the next recorded turn: its admission, its stream frames and its
/// <c>prompt_result</c>. A held turn withholds its <c>prompt_result</c> until the session aborts it.
/// </summary>
/// <remarks>
/// Synthesised, not observed: the abort reply (<c>command</c> <c>abort</c>, <c>success</c> true) and the <c>aborted</c>
/// status of a held turn's <c>prompt_result</c> are written by repository convention, copied from the RPC session's own
/// tests (RpcPromptStreamTests). No recorded omp output contains an abort, so they are not evidence of omp's behaviour.
/// </remarks>
internal sealed class RecordedRpcCapture
{
    private readonly List<CapturedResponse> _responses;
    private readonly List<RecordedTurn> _turns;
    private int _nextTurn;
    private bool _holdNext;
    private HeldTurn? _held;

    private RecordedRpcCapture(List<string> connectionFrames, List<CapturedResponse> responses, List<RecordedTurn> turns, int discardedFrameCount)
    {
        ConnectionFrames = connectionFrames;
        _responses = responses;
        _turns = turns;
        DiscardedFrameCount = discardedFrameCount;
    }

    /// <summary>Gets the frames the engine sends before any command: <c>ready</c> and the unsolicited frames before the first prompt.</summary>
    public IReadOnlyList<string> ConnectionFrames { get; }

    /// <summary>Gets the number of recorded turns.</summary>
    public int TurnCount => _turns.Count;

    /// <summary>Gets the number of frames that <see cref="Load"/> discarded: unsolicited frames sent outside a turn after the first prompt, such as <c>session_settled</c>.</summary>
    public int DiscardedFrameCount { get; }

    /// <summary>
    /// Loads a capture from the linked transcript folder. Frames before the first prompt that are not responses become
    /// <see cref="ConnectionFrames"/>. A frame sent outside a turn after the first prompt that is neither a response nor a
    /// <c>prompt_result</c> (for example <c>session_settled</c>) is discarded, and counted in <see cref="DiscardedFrameCount"/>.
    /// </summary>
    /// <param name="fileName">The capture file name under <c>TestData/frames</c>.</param>
    /// <returns>The capture.</returns>
    public static RecordedRpcCapture Load(string fileName)
    {
        var reader = new CaptureReader(fileName);
        foreach (string line in TranscriptFiles.ReadLines(fileName))
        {
            reader.Accept(line);
        }

        return reader.Build();
    }

    /// <summary>Holds the next turn open: its <c>prompt_result</c> is released only by the session's abort.</summary>
    public void HoldNextTurn() => _holdNext = true;

    /// <summary>Answers one command line written by the session.</summary>
    /// <param name="commandLine">The command JSON.</param>
    /// <returns>The lines to deliver, in order.</returns>
    public List<string> Respond(string commandLine)
    {
        JsonObject command = JsonNode.Parse(commandLine)!.AsObject();
        string id = Text(command, "id") ?? throw new InvalidOperationException("A command carries no id.");
        string type = Text(command, "type") ?? throw new InvalidOperationException("A command carries no type.");
        if (string.Equals(type, "prompt", StringComparison.Ordinal))
        {
            return StartTurn(id);
        }

        if (string.Equals(type, "abort", StringComparison.Ordinal) && _held is { } held)
        {
            return FinishHeldTurn(held, id);
        }

        return AnswerFromCapture(type, id);
    }

    private List<string> StartTurn(string id)
    {
        if (_nextTurn == _turns.Count)
        {
            throw new InvalidOperationException("The capture has no further recorded turn.");
        }

        RecordedTurn turn = _turns[_nextTurn];
        _nextTurn++;
        var lines = new List<string> { WithId(turn.Admission, id) };
        lines.AddRange(turn.Frames);
        if (_holdNext)
        {
            _held = new HeldTurn(id, turn.Result);
            _holdNext = false;
        }
        else
        {
            lines.Add(WithId(turn.Result, id));
        }

        return lines;
    }

    // Synthesised by repository convention (see the class remarks): the abort reply and the aborted status are not observed omp output.
    private List<string> FinishHeldTurn(HeldTurn held, string abortId)
    {
        _held = null;
        JsonObject result = JsonNode.Parse(held.Result)!.AsObject();
        result["id"] = held.PromptId;
        result["status"] = "aborted";
        return
        [
            new JsonObject
            {
                ["id"] = abortId,
                ["type"] = "response",
                ["command"] = "abort",
                ["success"] = true,
                ["data"] = null,
            }.ToJsonString(),
            result.ToJsonString(),
        ];
    }

    private List<string> AnswerFromCapture(string type, string id)
    {
        CapturedResponse? captured = _responses.Find(response => string.Equals(response.Command, type, StringComparison.Ordinal));
        if (captured is null)
        {
            return [];
        }

        return [WithId(captured.Line, id)];
    }

    private static string? Text(JsonObject frame, string property) => frame[property]?.GetValue<string>();

    private static string WithId(string line, string id)
    {
        JsonObject node = JsonNode.Parse(line)!.AsObject();
        node["id"] = id;
        return node.ToJsonString();
    }

    /// <summary>Sorts a capture's lines, in file order, into connection frames, responses, turns and discarded frames.</summary>
    private sealed class CaptureReader
    {
        private readonly string _fileName;
        private readonly List<string> _connection = new();
        private readonly List<CapturedResponse> _responses = new();
        private readonly List<RecordedTurn> _turns = new();
        private readonly List<string> _frames = new();
        private string? _admission;
        private bool _sawPrompt;
        private int _discarded;

        public CaptureReader(string fileName)
        {
            _fileName = fileName;
        }

        public void Accept(string line)
        {
            JsonObject frame = JsonNode.Parse(line)!.AsObject();
            string? type = Text(frame, "type");
            if (string.Equals(type, "response", StringComparison.Ordinal) && string.Equals(Text(frame, "command"), "prompt", StringComparison.Ordinal))
            {
                OpenTurn(line);
                return;
            }

            if (_admission is not { } admission)
            {
                AcceptOutsideTurn(frame, type, line);
                return;
            }

            if (string.Equals(type, "prompt_result", StringComparison.Ordinal))
            {
                _turns.Add(new RecordedTurn(admission, _frames.ToArray(), line));
                _admission = null;
                return;
            }

            _frames.Add(line);
        }

        public RecordedRpcCapture Build()
        {
            if (_admission is not null)
            {
                throw new InvalidOperationException($"Capture {_fileName} ends inside a turn.");
            }

            return new RecordedRpcCapture(_connection, _responses, _turns, _discarded);
        }

        private void OpenTurn(string line)
        {
            if (_admission is not null)
            {
                throw new InvalidOperationException($"Capture {_fileName} starts a prompt inside an unfinished turn.");
            }

            _admission = line;
            _frames.Clear();
            _sawPrompt = true;
        }

        private void AcceptOutsideTurn(JsonObject frame, string? type, string line)
        {
            if (string.Equals(type, "prompt_result", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Capture {_fileName} has a prompt_result outside a turn.");
            }

            if (string.Equals(type, "response", StringComparison.Ordinal))
            {
                _responses.Add(new CapturedResponse(Text(frame, "command") ?? string.Empty, line));
            }
            else if (!_sawPrompt)
            {
                _connection.Add(line);
            }
            else
            {
                _discarded++;
            }
        }
    }

    private sealed record CapturedResponse(string Command, string Line);

    private sealed record RecordedTurn(string Admission, string[] Frames, string Result);

    private sealed record HeldTurn(string PromptId, string Result);
}
