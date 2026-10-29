// Minimal Server-Sent Events reader shared by the hosted end-to-end scripts. It follows the WHATWG
// "Server-sent events" parsing rules for the subset the Autonogrammer API and the fake model server use:
// lines end in LF, CR or CRLF; a line starting with ":" is a comment; the fields are id, event, data and
// retry; a blank line dispatches the event. An event that is still open when the body ends is dropped,
// as the specification requires.

/**
 * @typedef {object} SseEvent
 * @property {string | null} id The `id:` field of this event, or null when the event carried none.
 * @property {string} event The `event:` field, or "message" when absent.
 * @property {string} data The `data:` lines joined with a line feed.
 * @property {number | null} retry The `retry:` field in milliseconds, or null.
 */

/**
 * Parses an SSE byte stream into events.
 * @param {AsyncIterable<Uint8Array>} body The response body (a fetch Response body is async-iterable in Node).
 * @returns {AsyncGenerator<SseEvent>} The dispatched events, in order.
 */
export async function* parseSse(body) {
  const decoder = new TextDecoder("utf-8");
  let buffer = "";
  let current = freshEvent();
  let streamEnded = false;

  const takeLine = () => {
    for (let i = 0; i < buffer.length; i++) {
      const ch = buffer[i];
      if (ch === "\n") {
        const line = buffer.slice(0, i);
        buffer = buffer.slice(i + 1);
        return line;
      }
      if (ch === "\r") {
        // A CR at the very end may be the first half of CRLF: wait for the next chunk unless the stream ended.
        if (i === buffer.length - 1 && !streamEnded) return null;
        const line = buffer.slice(0, i);
        buffer = buffer.slice(buffer[i + 1] === "\n" ? i + 2 : i + 1);
        return line;
      }
    }
    return null;
  };

  function* drain() {
    for (;;) {
      const line = takeLine();
      if (line === null) return;
      if (line === "") {
        if (current.hasData) {
          yield { id: current.id, event: current.eventName ?? "message", data: current.dataLines.join("\n"), retry: current.retry };
        }
        current = freshEvent();
        continue;
      }
      if (line.startsWith(":")) continue;
      const colon = line.indexOf(":");
      const field = colon === -1 ? line : line.slice(0, colon);
      let value = colon === -1 ? "" : line.slice(colon + 1);
      if (value.startsWith(" ")) value = value.slice(1);
      switch (field) {
        case "id":
          // A value containing NUL is ignored by the specification.
          if (!value.includes("\0")) current.id = value;
          break;
        case "event":
          current.eventName = value;
          break;
        case "data":
          current.dataLines.push(value);
          current.hasData = true;
          break;
        case "retry":
          if (/^\d+$/.test(value)) current.retry = Number(value);
          break;
        default:
          break;
      }
    }
  }

  for await (const chunk of body) {
    buffer += decoder.decode(chunk, { stream: true });
    yield* drain();
  }
  buffer += decoder.decode();
  streamEnded = true;
  yield* drain();
}

function freshEvent() {
  return { id: null, eventName: null, dataLines: [], hasData: false, retry: null };
}
