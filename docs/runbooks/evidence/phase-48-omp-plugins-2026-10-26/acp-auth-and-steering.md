# omp ACP: authentication method and native steering (probe of 2026-10-26)

Real omp 18.12.6 (`omp --version` printed `omp/18.12.6`). Two throwaway Bun scripts spoke raw ACP frames to `omp acp --no-extensions` over stdio: one with the real home directory, one with a sandbox home (`HOME` and `PI_CODING_AGENT_DIR` under `/tmp`). No `session/prompt` was ever sent, so no model turn ran. The real-home run stopped after `authenticate`; the sandbox run also created one session.

## What omp answered

`initialize` (identical for the real home and the sandbox home):

```json
{"protocolVersion":1,"agentInfo":{"name":"omp","title":"omp","version":"18.12.6"},
 "authMethods":[{"id":"agent","name":"Use existing local credentials",
                 "description":"Authenticate via the provider keys/OAuth state already configured under ~/.omp."}],
 "agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":true,"sse":true},
                      "promptCapabilities":{"embeddedContext":true,"image":true},
                      "sessionCapabilities":{"list":{},"fork":{},"resume":{},"close":{}}},
 "_meta":{"steering":{"supported":true}}}
```

`authenticate` with `{"methodId":"agent"}` answered `{}` in both homes. `session/new` after it answered a session id and `configOptions` with three kinds of entry: `mode` (`default`, `plan`: "Read-only planning mode that drafts a plan to a markdown file before any code changes"), `model` (a list of `provider/id` values) and `thinking`.

## Consequences for OmpOS

1. `authMethods` is never empty for omp, and the OmpOS ACP client refuses to continue when `authMethods` is non-empty and `AcpClientOptions.AuthMethodId` is unset. The head never set it, so a chat session against this omp failed its handshake. The fix sets `AuthMethodId = "agent"` in `apps/OmpOS.Native/Engine/EngineComposition.cs`. It was found by running the real application (`docs/runbooks/evidence/phase-40-settings-2026-10-26/`, finding 2); the earlier live tests spoke raw frames and never went through the OmpOS client.
2. `_meta.steering.supported` is omp's native live steering for ACP sessions. The source (`src/modes/acp/acp-agent.ts` of the installed package, lines 105 to 135, 316 to 336 and 1496 to 1560, and `CHANGELOG.md` line 9) defines the extension request `_session/steering`:
   - params: `{ sessionId, prompt: ContentBlock[] }`, optional `_meta: { steering: { queue: "followUp" } }` to use the follow-up queue;
   - `{"outcome":"injected","queue":...}`: a live run takes the message at its next tool-batch or model-call boundary;
   - `{"outcome":"promptRequired","queue":...}`: the session is idle, or the run settled while the steer was being admitted, or it is in the abort window. Nothing was consumed and no turn was started; the host sends the text through `session/prompt`;
   - `{"outcome":"failed","reason":...}`, and JSON-RPC invalid-params errors for an unknown or closed session or an empty prompt.
   omp never starts a detached turn from this method.
   OmpOS therefore steers chat (ACP) sessions through `_session/steering` and keeps its own inbox plugin only for omp terminals (no ACP) and as the fallback for an omp whose `initialize` lacks the capability (OQ-81).
3. `configOptions` also carries a `mode` option (Default and Plan) and a `thinking` option, so the composer can show Cursor-style Plan and thinking chips driven by what omp offers (UI parity specification, owner question O7).

## Not verified

An `injected` outcome (a steer into a running turn) needs a model turn, which needs the owner's approval (OD-07). The no-model-turn live test asserts the idle `promptRequired` outcome and the unknown-session error only.
