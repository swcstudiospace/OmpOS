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

1. `authMethods` is never empty for omp, and the OmpOS ACP client refuses to continue when `authMethods` is non-empty and `AcpClientOptions.AuthMethodId` is unset. The head never set it, so a chat session against this omp failed its handshake. The fix sets `AuthMethodId = AcpClientOptions.OmpAuthMethodId` (`"agent"`) in `apps/OmpOS.Native/Engine/EngineComposition.cs` and, because `services/OmpOS.Api/Harness/AcpHarnessSessionFactory.cs` had the same gap in all three of its `AcpClientOptions`, there too. It was found by running the real application (`docs/runbooks/evidence/phase-40-settings-2026-10-26/`, finding 2); the earlier live tests spoke raw frames and never went through the OmpOS client.
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

## After the fix: what the real application showed (run of 2026-10-26)

Release build of `apps/OmpOS.Native` under Xvfb (1440x900), real omp 18.12.6, sandbox `HOME`, `PI_CODING_AGENT_DIR` and `OMPOS_STATE_DIR` under `/tmp/f7shot`, no model prompt, no login. The machine environment holds one provider variable (Anthropic), which the sandbox omp inherits; the sandbox home itself has no credential. Steps: wait for the main window (cold start takes about 20 s under load), click "New agent", click the "Model" chip of the composer.

- `acp-fix-1-new-agent-composer.png`: the session "New session" is Ready with the composer; the only chip is "Model". The mode and thinking chips are not there, because `ComposerViewModel.ModeChip` and `ThinkingChip` exist but no AXAML binds them (`grep ModeChip src --include=*.axaml` finds nothing). That is the composer-view agent's work; the view model side is proven below.
- `acp-fix-2-model-chip-lists-omp-models.png` (20 s and 60 s after the click look the same): the session opened, with no "sign in" error and no `AcpClientOptions` text. The chip label became omp's current model ("anthropic / Claude Opus 5.5") and the flyout lists omp's own catalogue grouped by provider ("anthropic": Claude Sonnet 3.5, Claude Haiku 4.5, ...) with the model id of the highlighted row. Duplicate display names ("Claude Haiku 4.5" twice) are omp's own entries with different ids. The app log stayed empty: no `pump failed` line.
- Before this fix (`phase-40-settings-2026-10-26/chat-b-model-chip-flyout.png`) the same click showed the internal sign-in error.

Real omp, no view involved (`OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"`, class `LiveOmpOptionChipsTests`): `EngineComposition.Compose` with a sandbox plan connects, signs in with `agent`, opens a session without a model turn, and the two `OptionChipBinding`s of the session runtime produce a mode chip with choices `default`/`plan` (labels Default and Plan, current Default, tooltip "Mode: Default. ...") and a thinking chip with omp's thinking levels. It passed.

Wire contract (`OMPOS_LIVE_OMP=1 dotnet test tests/OmpOS.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"`, class `LiveOmpSteeringContractTests`, passed together with the earlier handshake test): `initialize` advertised `_meta.steering.supported: true` and `authMethods` containing `agent`; `authenticate {methodId:"agent"}` and `session/new` succeeded; `_session/steering` on the idle session answered `{"outcome":"promptRequired","queue":"steering"}`, and with `_meta.steering.queue:"followUp"` answered `{"outcome":"promptRequired","queue":"followUp"}`; an unknown session id answered JSON-RPC error `-32602` (invalid params); no `session/prompt` was sent and no omp process of the run remained.

Handshake time: `initialize` of a cold omp (fresh home) took 7.4 s and 8.0 s on this host (two runs). The 10 s `AcpClientOptions.HandshakeTimeout` default failed `LiveOmpOptionChipsTests` once on a loaded host ("The omp acp initialize request timed out"), so the Native head and the API now set `AcpClientOptions.OmpHandshakeTimeout` (45 s) for sessions that start a real omp.
