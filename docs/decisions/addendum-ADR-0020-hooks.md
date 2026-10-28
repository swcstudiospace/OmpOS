# Addendum to ADR-0020: hooks in cloud workers

- Status: Accepted 2026-10-28
- Date: 2026-10-28
- Owner: ompos-maintainers
- Amends: [ADR-0020](ADR-0020-cloud-agents-on-railway.md) D3 (worker bridge protocol) and D4 (authentication); applies [ADR-0016](ADR-0016-hooks-execution-model.md) to the worker.
- Scope: where hooks run for a cloud agent, how the user's consent reaches the worker, which hooks a worker runs, and the one extension frame the worker adds to the bridge (`_ompos/hook`). Requirements PX-14 (hooks) and PX-17 (cloud agents), milestone v0.15.0, phases 57 and 60. The research is [08-hooks-design.md](../plans/v0.15.0-research/08-hooks-design.md), sections 3.2 (where hooks run), 3.5 (failure policy) and unit U11.
- File name: this record is not named `ADR-0020-…` on purpose. `DecisionRecordTests` require every `ADR-*.md` file to carry a unique number and a row in the index, and a second file with the number 0020 would fail both.

## Context

A cloud agent's omp child runs in a Railway container, next to the `OmpOS.AgentWorker` that bridges its ACP stdio to a WebSocket (ADR-0020 D3). Hooks must run next to omp: the omp extension that blocks a tool call reaches its host over loopback HTTP, and a loopback address on the desktop is not reachable from the container (research risk R11). Cursor runs project hooks in cloud agents, not the user's own hooks, and only fourteen of its events. OmpOS adds one rule: the consent decision stays with the human on the desktop, so a worker never runs a hook the user did not approve.

## Decision

### H1. Hooks run in the worker, once per connection, only when the operator turns them on

`Worker:Hooks:Enabled` is false by default (environment `Worker__Hooks__Enabled`). When true, `Worker:Hooks:PluginsRoot` (absolute; the image's `plugins` directory holding `ompos-hooks-omp`) is required, and `Worker:Hooks:StateRoot` (default `<Worker:WorkspaceRoot>/.ompos-hooks`, a name no session subject can take) holds the per-session hook state. For each connection the worker:

1. checks that the session directory is writable (Cursor does not run hooks in the read-only early turns of a cloud agent); a read-only workspace reports `notWritable`;
2. finds the checkouts: the session directory itself or its non-hidden child directories that hold a `.git`;
3. loads the project hook files (`.ompos/hooks.json`, `.cursor/hooks.json`, `.claude/settings*.json`) of those checkouts with the shared loader of `OmpOS.Harness.Plugins`, with no home directory and as a remote session, so the user's own files are not even looked for, and the machine-wide managed file is dropped;
4. keeps only command hooks (no prompt hooks), no omp-section hooks, hooks whose `runsIn` includes `remote`, and the fourteen events Cursor runs in cloud agents: `beforeShellExecution`, `afterShellExecution`, `beforeReadFile`, `afterFileEdit`, `preToolUse`, `postToolUse`, `postToolUseFailure`, `subagentStart`, `subagentStop`, `beforeSubmitPrompt`, `preCompact`, `afterAgentResponse`, `afterAgentThought`, `stop`. `sessionStart`, `sessionEnd`, `workspaceOpen`, both MCP events and both Tab events are not run (the bridge handler also refuses a request for them);
5. applies the consent snapshot (H2), starts a loopback `HookBridgeServer` for this connection and gives the omp child `-e <plugins>/ompos-hooks-omp` plus `OMPOS_HOOKS_URL`, `OMPOS_HOOKS_TOKEN` and `OMPOS_HOOKS_STARTUP_FILE`. The bridge token is added to the redactor that scrubs the child's stderr, like the connection's own token.

Hook commands are run by the shared `SystemHookCommandRunner` with the scrubbed environment of ADR-0016. The extension is the only producer: the ACP tap of the desktop does not run in the worker, because the worker does not parse the ACP traffic it carries.

### H2. Consent is a snapshot in the connection token

The worker has no outbound channel (ADR-0020 D4: it makes no HTTP calls) and `GET /acp` has no body, so the snapshot travels in the signed bearer token the control plane mints for the connection, as the claim `ompos_hooks_consent`: a string holding one JSON object

```json
{"version":2,"approver":"ada@example.test","approvedAt":"2026-10-28T09:00:00+00:00","hashes":["<64 lowercase hex>", "…"]}
```

`version` is the consent identity version of ADR-0016 (currently 2) and `hashes` are the identities (`CommandHash`) of the project hooks the user approved on the desktop, computed with the relative-working-directory identity so they equal the worker's. The token's signature makes the snapshot as authentic as the connection; its 15 minute lifetime means a revoked approval cannot outlive its token, because the worker rebuilds its consent store from the snapshot on every connection and deletes the store of the previous one.

- A hook runs only if its recomputed identity is in the snapshot. Otherwise ADR-0016 applies unchanged: a hook that guards an action (the permission events: `preToolUse`, `subagentStart`, `beforeShellExecution`, `beforeReadFile`) denies it with an `awaitingConsent` run record, any other hook is skipped.
- No claim, or a claim that is present but invalid (wrong version, a hash that is not 64 lowercase hex, more than `Worker:Hooks:MaxSnapshotHashes` (256) hashes, a bad approver, more than one claim, over 64 KiB), means **no project hook runs**; the worker reports `untrusted` when the checkout has hooks. The claim is refused as a whole and its text never reaches a log or a frame.
- Approver and approval time are carried for audit; they are not logged by the worker.

### H3. Failure policy

Hooks that cannot be enforced are never silently dropped. If the extension is not installed or the bridge cannot start, the session reports `unavailable` and runs without hooks, unless one of the hooks is fail-closed: the worker then refuses the connection with HTTP 503 (the same status as an omp child that cannot start). Within a running bridge ADR-0016 section 3.5 applies as it is: a failing handler denies fail-closed events and lets the others through.

### H4. `_ompos/hook`, the extension frame

The worker sends hook information to the client as a JSON-RPC notification on the existing socket, one text frame, interleaved with the frames of the omp child (both writers take one send gate, because a WebSocket allows one outstanding send):

```json
{"jsonrpc":"2.0","method":"_ompos/hook","params":{"version":1,"run":{ "<HookRunEvent, camelCase>": "…" }}}
{"jsonrpc":"2.0","method":"_ompos/hook","params":{"version":1,"state":"active","hookCount":2,"reason":"1 of 2 hooks are approved by the consent snapshot."}}
```

- `run` is the contract record `OmpOS.Contracts.Hooks.HookRunEvent` (output excerpts redacted, only a digest of the input), one per hook run.
- `state` is `active`, `none`, `untrusted`, `notWritable` or `unavailable`; `hookCount` is how many project hooks the worker found; `reason` is a fixed sentence without paths or secrets. The state frame is sent first on every connection that has hooks enabled.
- The underscore prefix marks an ACP extension method. A client that does not know the method ignores the notification (ACP requires it), so the frame is additive: no `initialize` capability, no version bump of ACP, and an older control plane keeps working. `version` allows a later incompatible shape without breaking a reader of this one.
- Frames wait in a bounded in-memory queue (512, oldest dropped) until the socket is attached and are not replayed after a reconnect; the run log `<StateRoot>/<session>/hooks/runs.jsonl` (0600, 5 MiB bounded) keeps every record.
- Client side: `AcpHookNotificationFrames` (`OmpOS.Harness.Acp`) writes and reads the frame; `AcpAgentSession.HookNotificationReceived` raises it with a `HookRunEvent` and its `hook` stream payload (`HookStreamPayload.Create`). A plain local `omp acp` never sends it.

## Consequences

- The control plane mints the claim; the desktop owns the approval. Until the API carries the snapshot from the desktop to the token minting (a field on the cloud agent create/prompt request and the token issuer), every cloud session reports `untrusted` and runs no project hook. This is the safe default and is recorded as an open item in the wave report.
- The worker image must contain `plugins/ompos-hooks-omp` and set `Worker__Hooks__PluginsRoot`; hooks that need a toolchain (a linter, a formatter) run in the image the user's environment file builds.
- Prompt-type hooks stay off in the cloud (research O5).
- Hooks run with the worker's rights, inside the session container. They can read the container's environment except what the hook environment scrub removes; the isolation of ADR-0020 D5 is the boundary, not the hook runner.

## Verification

`tests/OmpOS.AgentWorker.Tests/Hooks/` runs the loopback worker with a fake omp: a denying project hook stops a tool call (the bridge answers `deny` and a `denied` run record arrives as `_ompos/hook`), a connection without a snapshot runs no hook and reports `untrusted`, a snapshot that lacks a hook's identity denies a guarding hook with `awaitingConsent`, and the filter drops user-scope, prompt-type, omp-section and unsupported-event hooks. Not run in this pass (no .NET build was allowed); a live omp with the extension is UNVERIFIED: `omp acp --no-extensions -e plugins/ompos-hooks-omp` with `OMPOS_HOOKS_URL` and `OMPOS_HOOKS_TOKEN` pointing at a stub.

## Candidate chosen

Reuse the hooks library (`OmpOS.Harness.Plugins.Hooks`), the loopback bridge (`OmpOS.Harness.AgentLoop.Hooks.HookBridgeServer`) and the omp extension (`plugins/ompos-hooks-omp`) of ADR-0016 inside the worker, instead of a worker-specific hook runner. Consent travels in the signed token instead of a new endpoint or an unsigned header, because the worker accepts no unsigned input about trust.

## Licence

No new dependency. Everything reused is part of this repository (AGPL-3.0-or-later, SPDX `AGPL-3.0-or-later`; source offer per [ADR-0018](ADR-0018-licence-agpl-and-source-offer.md)).

## Open risks

- The extension and a real omp were never run together (ADR-0016 UNVERIFIED list).
- A token over the Kestrel header limit (32 KiB) is refused before the worker sees it. Each hash costs about 90 bytes in the encoded token, so the default cap of 256 hashes stays near 23 KiB; the validator allows raising it to 4096 only together with `Kestrel:Limits:MaxRequestHeadersTotalSize`. UNVERIFIED: the control plane's token size with a full snapshot.
- `Worker__Hooks__*` is not yet in `deploy/compose.yaml` or `.env.example` (not owned by this unit).
