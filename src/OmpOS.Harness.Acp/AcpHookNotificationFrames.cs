using System.Buffers;
using System.Text.Json;
using OmpOS.Contracts.Hooks;

namespace OmpOS.Harness.Acp;

/// <summary>
/// The wire form of <see cref="AcpHookNotification"/>: one JSON-RPC notification, method <see cref="Method"/>, whose params are
/// <c>{"version":1,"run":{…}}</c> for a hook run (the object is a <see cref="HookRunEvent"/> in the contract's camel-case form) or
/// <c>{"version":1,"state":"active","hookCount":2,"reason":"…"}</c> for the state of the session's hooks. The frame is additive: an
/// ACP client that does not know the method ignores a notification, so an older client keeps working. A frame with another version is not
/// read, so a later incompatible form can use a new version without breaking this reader.
/// </summary>
public static class AcpHookNotificationFrames
{
    /// <summary>Gets the JSON-RPC method of the notification. An underscore prefix marks an ACP extension method.</summary>
    public const string Method = "_ompos/hook";

    /// <summary>Gets the version of the params this code writes and reads.</summary>
    public const int Version = 1;

    /// <summary>Writes a notification as one JSON-RPC frame without a line break.</summary>
    /// <param name="notification">The notification. Exactly one of its run and state is set.</param>
    /// <returns>The UTF-8 bytes of the frame.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is null.</exception>
    /// <exception cref="ArgumentException">Both or neither of the run and the state are set.</exception>
    public static byte[] Serialize(AcpHookNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if ((notification.Run is null) == (notification.State is null))
        {
            throw new ArgumentException("A hook notification carries either a run or a state.", nameof(notification));
        }

        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteString("method", Method);
            writer.WriteStartObject("params");
            writer.WriteNumber("version", Version);
            if (notification.Run is { } run)
            {
                writer.WritePropertyName("run");
                writer.WriteRawValue(JsonSerializer.SerializeToUtf8Bytes(run, HooksContractsJsonContext.Default.HookRunEvent));
            }
            else
            {
                writer.WriteString("state", notification.State);
                writer.WriteNumber("hookCount", notification.HookCount);
                if (notification.Reason is not null)
                {
                    writer.WriteString("reason", notification.Reason);
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Reads the params of a <see cref="Method"/> notification.</summary>
    /// <param name="parameters">The <c>params</c> member of the frame, or null when it has none.</param>
    /// <param name="notification">The notification when the params are understood.</param>
    /// <returns>True when the params have a known version and a well-formed run or state; false otherwise, and the caller ignores the frame.</returns>
    public static bool TryParse(JsonElement? parameters, out AcpHookNotification? notification)
    {
        notification = null;
        if (parameters is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("version", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var number)
            || number != Version)
        {
            return false;
        }

        try
        {
            if (root.TryGetProperty("run", out var run) && run.ValueKind == JsonValueKind.Object)
            {
                var parsed = run.Deserialize(HooksContractsJsonContext.Default.HookRunEvent);
                notification = parsed is null ? null : new AcpHookNotification { Run = parsed };
                return notification is not null;
            }

            if (root.TryGetProperty("state", out var state) && state.ValueKind == JsonValueKind.String && state.GetString() is { Length: > 0 } text)
            {
                notification = new AcpHookNotification
                {
                    State = text,
                    HookCount = root.TryGetProperty("hookCount", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var hooks) ? hooks : 0,
                    Reason = root.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null,
                };
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
