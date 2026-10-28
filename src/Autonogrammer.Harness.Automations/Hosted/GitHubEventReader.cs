using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Reads the facts a trigger needs out of a GitHub <c>push</c> or <c>pull_request</c> payload. It reads only the members it names and
/// never throws on a payload of another shape: such a payload is reported as unreadable.
/// </summary>
internal static class GitHubEventReader
{
    private const string BranchPrefix = "refs/heads/";
    private const int MaxTextLength = 200;
    private const int MaxPayloadLength = 4_000;
    private const int MaxCommits = 10;

    /// <summary>Reads an event.</summary>
    /// <param name="eventName">The <c>X-GitHub-Event</c> value: <c>push</c> or <c>pull_request</c>.</param>
    /// <param name="body">The raw JSON body.</param>
    /// <param name="info">The facts when the call succeeds.</param>
    /// <param name="reason">The stable reason code when the call fails: <c>bad_payload</c>, <c>not_a_branch</c> or <c>branch_deleted</c>.</param>
    /// <returns>True when the payload is readable and concerns a branch.</returns>
    public static bool TryRead(string eventName, ReadOnlyMemory<byte> body, [NotNullWhen(true)] out GitHubEventInfo? info, out string reason)
    {
        info = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                reason = "bad_payload";
                return false;
            }

            return string.Equals(eventName, "push", StringComparison.Ordinal)
                ? TryReadPush(root, out info, out reason)
                : TryReadPullRequest(root, out info, out reason);
        }
        catch (JsonException)
        {
            reason = "bad_payload";
            return false;
        }
    }

    private static bool TryReadPush(JsonElement root, [NotNullWhen(true)] out GitHubEventInfo? info, out string reason)
    {
        info = null;
        var reference = Text(root, "ref");
        if (!reference.StartsWith(BranchPrefix, StringComparison.Ordinal))
        {
            reason = "not_a_branch";
            return false;
        }

        if (root.TryGetProperty("deleted", out var deleted) && deleted.ValueKind == JsonValueKind.True)
        {
            reason = "branch_deleted";
            return false;
        }

        var branch = reference[BranchPrefix.Length..];
        var repository = Text(Object(root, "repository"), "full_name");
        var pusher = Text(Object(root, "pusher"), "name");
        var after = Text(root, "after");
        var commits = root.TryGetProperty("commits", out var list) && list.ValueKind == JsonValueKind.Array ? list : default;
        var count = commits.ValueKind == JsonValueKind.Array ? commits.GetArrayLength() : 0;

        var payload = new StringBuilder();
        _ = payload.Append("event: push\nrepository: ").Append(repository).Append("\nbranch: ").Append(branch)
            .Append("\nafter: ").Append(after).Append("\npusher: ").Append(pusher).Append("\ncommits: ").Append(count.ToString(CultureInfo.InvariantCulture));
        if (commits.ValueKind == JsonValueKind.Array)
        {
            var shown = 0;
            foreach (var commit in commits.EnumerateArray())
            {
                if (shown++ >= MaxCommits)
                {
                    break;
                }

                _ = payload.Append("\n- ").Append(Text(commit, "id", 12)).Append(' ').Append(Text(commit, "message"));
            }
        }

        info = new GitHubEventInfo(
            "push",
            string.Empty,
            branch,
            false,
            Bound($"push to {branch} by {pusher} ({count.ToString(CultureInfo.InvariantCulture)} commits)", MaxTextLength),
            Bound(payload.ToString(), MaxPayloadLength));
        reason = string.Empty;
        return true;
    }

    private static bool TryReadPullRequest(JsonElement root, [NotNullWhen(true)] out GitHubEventInfo? info, out string reason)
    {
        info = null;
        var pull = Object(root, "pull_request");
        if (pull.ValueKind != JsonValueKind.Object)
        {
            reason = "bad_payload";
            return false;
        }

        var action = Text(root, "action");
        if (string.Equals(action, "closed", StringComparison.Ordinal) && pull.TryGetProperty("merged", out var merged) && merged.ValueKind == JsonValueKind.True)
        {
            action = "merged";
        }

        var baseBranch = Text(Object(pull, "base"), "ref");
        var headBranch = Text(Object(pull, "head"), "ref");
        var baseRepository = Text(Object(Object(pull, "base"), "repo"), "full_name");
        var headRepository = Text(Object(Object(pull, "head"), "repo"), "full_name");
        var isFork = !string.Equals(baseRepository, headRepository, StringComparison.Ordinal);
        var number = pull.TryGetProperty("number", out var numberElement) && numberElement.TryGetInt32(out var parsed) ? parsed : 0;
        var title = Text(pull, "title");
        var author = Text(Object(pull, "user"), "login");

        var payload = new StringBuilder();
        _ = payload.Append("event: pull_request\naction: ").Append(action).Append("\nrepository: ").Append(baseRepository)
            .Append("\nnumber: ").Append(number.ToString(CultureInfo.InvariantCulture)).Append("\ntitle: ").Append(title).Append("\nauthor: ").Append(author)
            .Append("\nbase: ").Append(baseBranch).Append("\nhead: ").Append(headBranch)
            .Append("\nfork: ").Append(isFork ? "yes" : "no").Append("\nurl: ").Append(Text(pull, "html_url"));

        info = new GitHubEventInfo(
            "pull_request",
            action,
            baseBranch,
            isFork,
            Bound($"pull request #{number.ToString(CultureInfo.InvariantCulture)} {action} on {baseBranch} by {author}: {title}", MaxTextLength),
            Bound(payload.ToString(), MaxPayloadLength));
        reason = string.Empty;
        return true;
    }

    private static JsonElement Object(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string Text(JsonElement parent, string name, int maxLength = MaxTextLength)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return Line(value.GetString() ?? string.Empty, maxLength);
    }

    /// <summary>Replaces every control character with a space and cuts the text to a length, so the result is one line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The most characters kept.</param>
    /// <returns>The cleaned text.</returns>
    internal static string Line(string text, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var character in text)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            _ = builder.Append(char.IsControl(character) ? ' ' : character);
        }

        return builder.ToString();
    }

    /// <summary>Cuts text to a length. Callers pass text whose parts are already single lines.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The most characters kept.</param>
    /// <returns>The cut text.</returns>
    private static string Bound(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
