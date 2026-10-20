# Feature Research

**Domain:** Chat-style agent workspace / ACP client harness (engine = omp 18.12.3) with Generative UI, shared across Blazor WASM web, MAUI Blazor Hybrid desktop and mobile
**Researched:** 2026-10-20
**Confidence:** HIGH for protocol-level features (ACP v1 spec pages and omp's bundled docs read today; `omp acp` probed live), HIGH for Zed/Cursor/Claude Code Desktop/OpenCode feature inventories (vendor docs read today), LOW for Grok Chat (no primary doc fetched) and for mobile push/background behaviour (not exercised)

## How to read this file

Every feature row names the protocol surface that carries it, because in this product the UI is a projection of two wire protocols: **ACP v1** (`omp acp`, JSON-RPC over stdio) and **omp RPC** (`omp --mode rpc`, JSONL). Where the two differ, the row says which one OmpOS should bind to. "Complexity" is UI + harness cost for OmpOS, not the protocol's own cost.

Live probe of the installed engine (`omp acp`, 2026-10-20, `initialize` + `session/new` in a scratch cwd) — these are facts, not inferences:

| Observed from `omp acp` 18.12.3 | Value | Implication |
|---|---|---|
| `agentCapabilities.loadSession` | `true` | Session resume with history replay is available |
| `sessionCapabilities` | `list {}`, `fork {}`, `resume {}`, `close {}` — **no `delete`** | Session list, replay-less resume, fork and close are supported; "delete session" must be a local-index operation, not an ACP call |
| `promptCapabilities` | `embeddedContext: true`, `image: true` — **no `audio`** | @-mentions as embedded resources and image attachments are legal; audio attachments are not |
| `authMethods` | one method `agent` ("Use existing local credentials … under ~/.omp") | Auth is omp's, not OmpOS's — reinforces the auth-provider anti-feature |
| `session/new` result | `configOptions` = `mode` (2 options), `model` (**739** options), `thinking` (category `thought_level`, 7 options) **plus** legacy `modes` (`default`, `plan`) | Build one generic config-option selector; model list needs search, not a dropdown |
| First `session/update` | `available_commands_update` with **701** commands, 690 carrying an `input.hint` | Slash-command UI must be a filterable palette, never a flat menu |
| Also pushed | `session_info_update` with `updatedAt` | Session list rows can be kept fresh without polling |

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete. Every competitor surveyed (Zed Agent Panel, Cursor Agent, Claude Code Desktop, OpenCode TUI) ships all of these.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Conversation list + open/resume | Zed threads sidebar, Claude Desktop session sidebar, Cursor Agents Window, OpenCode `/sessions` all lead with it | MEDIUM | ACP: gate on `sessionCapabilities.list`, call `session/list` (cursor-paginated, filter by `cwd`), then `session/load` (history replayed as `session/update`) or `session/resume` (no replay). omp RPC: `switch_session`/`open_session`, `get_messages_page` (cursor, 1–256 per page, `session_busy`/`stale_cursor` error codes). Titles arrive via `session_info_update`; note omp's CLI RPC modes disable auto-titles (`PI_NO_TITLE=1`) so OmpOS must fall back to first-prompt excerpt. Depends on the append-only event log already in PROJECT.md. |
| Streaming assistant text with Markdown + code blocks (copy button) | Universal | LOW–MEDIUM | ACP `agent_message_chunk` with optional `messageId` grouping; omp RPC `message_update` (`set_event_filter messageUpdates: "delta"` to cut bandwidth). Need a Markdown renderer safe against injection (agent output is untrusted). |
| Stop / cancel with partial output retained | Esc stops in Claude Desktop; stop button in Zed/Cursor | MEDIUM | ACP: send `session/cancel`, immediately mark unfinished tool calls `cancelled`, answer every pending `session/request_permission` with `cancelled`, keep accepting late `session/update`s until `session/prompt` returns `stopReason: cancelled`. omp RPC: `abort` (requeues stranded steers) vs `abort_and_restore_queue` (returns withdrawn queue text/images to the composer — the TUI's Esc). Partial text stays in the transcript; never discard it. |
| Retry / error card | Provider errors are routine | MEDIUM | omp `prompt_result.status: "error"` carries `{message, provider?, model?, httpStatus?, retryable}`; `auto_retry_start/end`, `retry_fallback_*` events should render as a transient status, not an error. ACP has no retry channel — expose "resend last prompt". |
| Tool-call cards with live status | Zed "indicators showing which tools"; Claude Desktop collapses tool calls into summaries | MEDIUM | ACP `tool_call` → `tool_call_update`; status `pending → in_progress → completed/failed`; `kind` (read/edit/delete/move/search/execute/think/fetch/switch_mode/other) picks the icon; `title` is the label; `rawInput`/`rawOutput` behind a disclosure; `locations[]` for file chips. omp RPC: `tool_execution_start/update/end` + `tool_stream_update`. Default collapsed, expandable (Claude Desktop Normal/Verbose view modes; OpenCode `/details`). |
| Permission prompt (approval card) | Every client has one; it is the safety UI | MEDIUM–HIGH | ACP `session/request_permission` delivers `toolCall` + `options[]` each with `kind ∈ {allow_once, allow_always, reject_once, reject_always}`; reply `selected{optionId}` or `cancelled`. Render options exactly as sent (names/kinds come from the agent — omp sends e.g. `Approve File` / `Always Approve File` for cached mode), never synthesise an "always" the agent did not offer. OmpOS adds the policy hook from PROJECT.md (deny-by-policy default) *before* the card. Must be answerable by keyboard and survive a reconnect (card re-rendered from the event log, answer idempotent). omp side: modes `always-ask`, `always-ask-cached`, `write`, `yolo`; ACP cannot set approval mode per session — launch flags/`--config` overlay per process. |
| Plan / todo panel | ACP first-class; Cursor/Claude show todo lists | LOW–MEDIUM | ACP `plan` update: entries `{content, priority high/medium/low, status pending/in_progress/completed}`; **each update replaces the whole list** — do not diff-merge. omp RPC: `set_todos` (phases → tasks with `blocker?`), `todo_reminder`/`todo_auto_clear` events. Sticky summary chip ("3/7 done") + expandable list. |
| Agent thought display (collapsed by default, toggleable) | OpenCode `/thinking`, Claude Desktop "Thinking" view mode | LOW | ACP `agent_thought_chunk`; omp RPC thinking deltas inside `message_update`. Render as a dimmed, collapsed block; respect `thinking` config option. |
| File diff cards | Cursor diffs view, Claude Desktop diff pane, Zed inline diffs | MEDIUM | ACP `ToolCallContent{type:"diff", path, oldText (null = new file), newText}` — the client computes the hunks; needs a diff library in C# (see STACK.md) and a side-by-side/stacked toggle (OpenCode `diff_style: auto/stacked`). PROJECT.md already scopes a "file-diff placeholder" for the vertical slice; the real diff renderer is v1.x. |
| Terminal / command output embed | Agents run commands constantly | HIGH (desktop), N/A (web/mobile) | ACP `ToolCallContent{type:"terminal", terminalId}` only exists if the **client** advertised `clientCapabilities.terminal: true` and implements `terminal/create|output|wait_for_exit|kill|release` (process spawn, `outputByteLimit` truncation at character boundary, keep showing output after release). Desktop head can; web/mobile must advertise `terminal: false`, in which case omp runs commands itself and output arrives as plain `content`. Two code paths, both needed. |
| Slash commands with autocomplete | ACP first-class; `/` menus in every client | MEDIUM | ACP `available_commands_update` (dynamic, replace-all; omp pushes **701** entries) → a filterable palette with `input.hint` ghost text; commands are sent as ordinary prompt text. omp RPC: `get_available_commands` → `RpcSlashCommand[]`; local-only builtins return `agentInvoked:false` with `command_output` frames and no `agent_end`. Feeds the IntentEnvelope recogniser in PROJECT.md. |
| @-mentions and attachments | `@file` in Claude Desktop/OpenCode/Zed/Cursor; drag-drop images | MEDIUM | ACP: mention → `ContentBlock{type:"resource", resource:{uri, text|blob, mimeType}}` (requires `embeddedContext`, which omp advertises) or `resource_link` (uri only, agent reads it); images as `{type:"image", data, mimeType}` (omp advertises `image`); **no audio**. Desktop: fuzzy file picker over the session `cwd` (omp RPC rejects `@file` CLI args — attach via content, not argv). Web: upload only, path-less. Mobile: photo/library picker. |
| Current model + thinking level display, with a selector | Visible in Claude Desktop row under prompt, Zed model selector, OpenCode `/models` | LOW (display) / MEDIUM (selector) | ACP: **use `configOptions` and treat `modes` as legacy** — the spec says modes will be removed; omp returns both. `session/set_config_option` returns the full option list; `config_option_update` when the agent changes it. omp RPC: `set_model`/`get_available_models`, `set_thinking_level`/`get_available_thinking_levels` (`off|minimal|low|medium|high|xhigh|max`), `model_changed`/`thinking_level_changed` events. Scaffold boundary (PROJECT.md): a **generic** option selector driven by agent data is in; curated model catalogues, routing, favourites are out. 739 model options ⇒ searchable list required. |
| Session mode switch | ACP `current_mode_update`; Claude Desktop permission-mode selector; omp exposes `default`/`plan` | LOW | Rendered through the same config-option selector (`category: mode`). Agent-initiated `current_mode_update`/`config_option_update` must update the UI without a round trip. `switch_mode` tool kind gets a permission card ("exit plan mode"). |
| Keyboard-first operation | Claude Desktop publishes a shortcut table (Cmd+N new, Esc stop, Ctrl+Tab next session, Cmd+/ cheat sheet); Zed keybinds everywhere; OpenCode leader key | MEDIUM | Enter send / Shift+Enter newline, Esc stop, Cmd/Ctrl+N new session, Ctrl+Tab cycle sessions, `/` and `@` triggers, arrow/PageUp/Home navigation of the thread (Zed), a discoverable shortcut sheet. Must work identically in WASM and MAUI WebView — test both key-event paths. |
| Accessibility baseline | Legal/table stakes; already a PROJECT.md requirement | MEDIUM | Streaming text in an `aria-live="polite"` region with throttled announcements (announce per message/tool-call boundary, never per token); focus management when a permission card appears; visible focus rings; touch targets ≥ 44 px; `prefers-reduced-motion`; contrast. |
| Scroll-lock / jump-to-latest | Zed scroll arrows; universal | LOW | Auto-follow while at bottom, detach on user scroll, "↓ new output" pill. |
| Reconnect and resume mid-stream (web/mobile) | Mobile connections drop; desktop sleeps | HIGH | Backend SSE with monotonic sequence numbers (PROJECT.md); client resumes from last seq, de-dups. On resume re-derive UI state (open permission cards, running tool calls, current plan) by replaying the event log — the UI must be a pure fold over events. ACP side: `session/resume` (no replay) when the process survived, `session/load` when it did not. |
| Usage / context-window indicator | Zed "token usage display", Cursor/Claude show context | LOW–MEDIUM | ACP `usage_update {used, size, cost?}`; omp RPC `get_session_stats`, `auto_compaction_start/end`. Show a small meter; compaction as a divider row (ACP `compaction_update` requires client capability `session.compaction`). |
| Transient notices | ACP `notice` (title, description, severity hint) requires `session.notices` capability; omp RPC `notice`, `config_warnings_changed` | LOW | Toast/banner, dismiss locally, never part of history, never required for correctness. |
| Session rename, export | Zed/OpenCode `/export`; Claude Desktop transcript | LOW | omp RPC `set_session_name`, `export_html`; ACP `session_info_update.title` is agent-pushed only. Markdown export can be generated from the event log without the agent. |

### Differentiators (Competitive Advantage)

Features that set the product apart. Aligned with the Core Value: "a prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, inside an isolation boundary the operator controls."

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Generative UI blocks (A2UI surfaces) | No surveyed ACP client renders agent-generated interactive UI; chat stays text + cards. A2UI v0.9.1 is the open standard (declarative, catalog-bound, no code execution) | HIGH | Own `UiBlock` contract with an `a2ui` block carrying `application/a2ui+json` messages (`createSurface`/`updateComponents`/`updateDataModel`/`deleteSurface`); Blazor renderer bound to a fixed component catalog; unknown components fall back to a visible placeholder. User actions back to the agent land in v1.0 (`actionResponse`, candidate) — design the action channel behind an interface now, ship read-only surfaces first. Transport: A2UI payloads arrive as omp text/tool output today (no native ACP block type) ⇒ a recogniser on the harness side. |
| Queued steering and follow-up chips | Zed docs: steering "is only available for the Zed Agent, since Zed can't detect turn boundaries for external agents". OmpOS *can*, via omp RPC | MEDIUM | omp RPC `steer` (next tool boundary), `follow_up` (after turn), `prompt{streamingBehavior}`; `queue_update {steering[], followUp[]}` renders chips; `remove_queued_message`, `promote_queued_message` (follow-up → steer), `abort_and_restore_queue` restores drafts (text + images); `set_steering_mode`/`set_follow_up_mode`/`set_interrupt_mode`. Cursor ships the same UX ("Send now" vs Tab to queue) — parity with the leader, ahead of ACP-only clients. ACP path has none of this: send while busy = reject (one-active-prompt rule) or local queue. |
| Background subagents panel | Cursor cloud subagents, Claude "work across sessions"; omp exposes subagent telemetry on the wire | HIGH | omp RPC `set_subagent_subscription off|progress|events`, `subagent_lifecycle`/`subagent_progress`/`subagent_event` frames, `get_subagents`, `get_subagent_messages{fromByte}`, `cancel_subagent`, `steer_subagent`. UI: tree under the parent turn (omp ids are dotted `Parent.Child`), progress + cancel + steer. Needs the event log to store child streams separately. Not available over ACP. |
| Cross-surface continuity | Start on desktop, watch/approve on phone — Cursor sells this for cloud agents; nobody does it for a local ACP agent | HIGH | Requires the backend event log + SSE resume + desktop-hosted harness publishing to the backend. Permission answers must be idempotent and first-writer-wins across surfaces. |
| Isolation-boundary badge and controls | Unique to OmpOS layering (local podman → agentOS → OpenShell → attested enclave) | MEDIUM | Session header shows the `SandboxDescriptor` (provider, network policy, mounts, attestation status) and sandbox logs as a side pane; provisioning/teardown as status rows in the transcript. Differentiates on trust, not on chat. |
| Follow-the-agent file chips | Zed "Following the Agent"; ACP `locations[]` | MEDIUM (desktop) | Clickable `{path, line}` chips open a read-only viewer in the desktop head; web shows the path only. |
| Session fork / branch tree | omp has it natively; Zed/Cursor only have "new from summary" | MEDIUM | ACP `sessionCapabilities.fork {}` (advertised by omp); omp RPC `fork{entryId}`, `branch`, `get_tree`, `get_branch_messages`. UI: "Fork from here" on any message; tree view later. |
| Side questions (`/btw`) | Cursor "side chats" and Claude Desktop "side question" are headline features; omp has `btw` | LOW–MEDIUM | omp RPC `btw{question}` → `btw_delta`/`btw_record`, `get_btw_history`, `btw_cancel`. Popover anchored to the composer; does not enter the main transcript. |
| Checkpoints / restore | Zed "Restore Checkpoint", Cursor checkpoints, OpenCode `/undo` (git-backed) | HIGH | omp ships `checkpoint` and `rewind` tools (`omp://tools/checkpoint.md`, `omp://tools/rewind.md`); exposing them in UI needs workspace snapshotting inside the sandbox provider — defer until the sandbox layer is real. |
| Attention notifications (desktop) | OpenCode `attention` (sounds + desktop notifications for question/permission/error/done) | LOW (desktop) | Fire a native notification when a permission card or error appears while the window is unfocused; MAUI local notifications on desktop. Mobile push is a separate, deferred feature (see anti-features). |
| Composer ghost text | omp RPC `predict_word`/`predict_word_feedback` | LOW–MEDIUM | Cheap win for the omp RPC transport only; treat failures as "no ghost text". v2. |
| Conversation search | Cursor builds a local index; Zed sidebar search | MEDIUM | Full-text over the local event log; no agent involvement. v1.x. |
| Goal mode | Cursor `/goal`, omp RPC `goal{op}` + `goal_updated` | MEDIUM | Long-lived objective chip with pause/resume/drop. v2. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems — specifically for this scaffold milestone. PROJECT.md already lists several as Out of Scope; this table records *why* and what to do instead.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Billing / usage metering & plans | Every hosted agent product has it | omp owns provider credentials and cost (`usageLimit`, `advisor_cost_changed`); OmpOS metering would double-count and need payments infra | Show agent-reported `usage_update`/cost read-only; no plans, no invoices |
| Authentication providers (OAuth, SSO, multi-tenant) | "Real product" expectation | `omp acp` advertises a single `agent` auth method using `~/.omp` credentials; building identity now forks the data model before the core loop is validated | Single dev principal with reserved `OwnerId` + no-op auth middleware (PROJECT.md decision); ACP `authenticate` passthrough only |
| Model routing / curated model picker / favourites | Zed favourites, Cursor auto-mode | omp returns **739** model options; curating, ranking or routing them is a product in itself and drifts from the engine's truth | Generic searchable `configOptions` selector rendered verbatim from the agent; no OmpOS-owned model catalogue |
| Plugin / extension / agent marketplace | Zed ACP Registry, Cursor plugins, omp `marketplace.md` | OmpOS is a client; omp already manages extensions, skills and MCP. A marketplace needs trust, signing and update UX | Surface omp's `available_commands_update` and MCP config as read-only; configure plugins in omp |
| Client-side tool execution engine (own tools, own MCP host) | "Make the client smart" | Duplicates omp; breaks the approval model (omp's policy resolver runs before `session/request_permission`) | Implement only the ACP **client** capabilities (`fs/*`, `terminal/*`) on desktop; omp RPC `set_host_tools` only for host-native affordances (clipboard, open file) and only later |
| Full code editor / IDE panes | Claude Desktop and Cursor have editors | Chat-first product; an editor in Blazor WebView is a multi-quarter effort and off-core | Read-only diff and file viewers; "open in external editor" on desktop |
| Mobile push notifications for approvals | Cursor/Claude phone flows | Needs APNs/FCM, a device registry, and background wake semantics on iOS; approval-by-push also weakens the trust boundary | In-app reconnect + resume with pending-approval badge; push deferred to v2 behind the backend |
| Fully offline mode on web/mobile | "Works on a plane" | Agent runs elsewhere; "offline" can only mean read-only history | Cache the event log locally for read-only browsing and queue a draft; do not promise offline execution |
| Real-time multi-user collaboration | omp has `collab.md` (guests) | Presence, conflict and permission attribution are unsolved in the scaffold data model (single `OwnerId`) | Keep `OwnerId` on Session; revisit after multi-tenant auth |
| Voice / live audio | omp has live-voice RPC frames | `omp acp` does not advertise `audio`; UI/permissions for mic on three platforms | None in this milestone |
| Response rating / feedback upload | Zed thumbs up/down | Sends transcripts to a server; privacy posture undefined | Local-only "copy transcript / open issue" action |
| Synthesising permission options the agent did not offer (e.g. an "always allow" shortcut) | Convenience | Breaks the ACP contract — only agent-provided `optionId`s are valid; omp's grant scoping (file/folder/session/persisted) is the agent's business | Render agent options verbatim; OmpOS policy hook can only *deny* earlier, never *grant* more |
| Yolo-by-default approvals | omp's schema default is `yolo` | Scaffold security posture is deny-by-policy; a web surface auto-approving `exec` is unacceptable | Launch `omp acp` with explicit `--approval-mode` per PROJECT.md policy; card for everything else |

## Feature Dependencies

```
[Contracts: Session, StreamEvent(seq), UiBlock]
    └──required by──> [Append-only event log]
                          ├──required by──> [Session list + resume]
                          ├──required by──> [SSE resume / reconnect]  ──required by──> [Cross-surface continuity]
                          ├──required by──> [Conversation search]  (v1.x)
                          └──required by──> [Transcript export]

[ACP/omp harness (state machine)]
    ├──required by──> [Streaming text]  ──required by──> [Stop/cancel + partial retention]
    ├──required by──> [Tool-call cards] ──required by──> [Permission cards]
    │                                    ├──required by──> [Diff cards]
    │                                    ├──required by──> [Terminal embeds]   (needs desktop terminal/* impl)
    │                                    └──required by──> [Follow-the-agent chips] (desktop)
    ├──required by──> [Plan/todo panel]
    ├──required by──> [Thought display]
    ├──required by──> [Config-option selector] ──provides──> [Model/thinking display], [Session mode switch]
    └──required by──> [Slash-command palette] ──feeds──> [Prompt parser / IntentEnvelope]

[Prompt parser / IntentEnvelope]
    ├──required by──> [@-mentions & attachments]  (needs fs picker on desktop, upload on web)
    └──required by──> [Slash-command dispatch]

[omp RPC transport specifically]
    ├──required by──> [Queued steering / follow-up chips]
    ├──required by──> [Subagents panel]
    ├──required by──> [Side questions (/btw)]
    ├──required by──> [Composer ghost text]
    └──required by──> [Goal mode]

[Block renderer (markdown, code, tool-call, error, unknown)]
    └──required by──> [A2UI surface block] ──enhanced by──> [A2UI v1.0 actionResponse] (candidate spec)

[Sandbox provider abstraction]
    ├──required by──> [Isolation badge / sandbox log pane]
    └──required by──> [Checkpoints / restore]  (v2)

[Permission cards] ──conflicts──> [Yolo-by-default]
[Client terminal capability] ──conflicts──> [Web/mobile surfaces]  (advertise terminal:false there)
[Steering chips] ──conflicts──> [One-active-prompt rejection]  (steer/follow_up must bypass the rejection on the omp RPC path)
[Session modes (legacy)] ──superseded by──> [Config options]  (render both, prefer configOptions)
```

### Dependency Notes

- **Session list/resume requires the event log:** ACP `session/load` replays history as `session/update`s; OmpOS must fold that replay into the same log it uses for live streams, or the two views drift. omp CLI RPC disables auto-titles, so titles need a local fallback.
- **Stop/cancel requires tool-call and permission state:** the ACP cancel contract obliges the client to mark tool calls `cancelled` and answer pending permissions with `cancelled` *before* the agent confirms — this is client-side state, hence the ordering.
- **Terminal embeds require the client terminal implementation:** `ToolCallContent.terminal` only ever appears if OmpOS advertised `terminal: true`; the desktop head must implement five `terminal/*` methods with process ownership and output-limit truncation. Web/mobile must advertise `false` and render plain `content` instead — two render paths for "command ran".
- **Permission cards require the policy hook first:** PROJECT.md's deny-by-policy hook runs before a card is shown; the card then shows *only* the agent's options.
- **Steering/follow-up and subagents require the omp RPC transport:** none of `steer`, `follow_up`, `queue_update`, `subagent_*`, `btw`, `predict_word`, `goal` exist in ACP. The harness abstraction must expose capability flags so the RCL hides these affordances on the ACP path instead of failing.
- **Config-option selector supersedes mode selector:** ACP marks `modes` for removal and omp already returns `configOptions` (mode, model, thinking). One generic component, three categories, zero bespoke pickers — this is also how "model selection UI" stays out of scope while the user still sees and can change the model.
- **A2UI requires the block renderer and an unknown-block fallback:** every A2UI component outside the catalog, and every unknown `UiBlock` kind, must render visibly as "unsupported block" (never silently dropped) so streaming is debuggable.
- **Steering chips conflict with one-active-prompt rejection:** the session orchestration rule "one active prompt per session" is right for `prompt`, but `steer`/`follow_up` are *designed* to be sent during an active prompt. Model them as a distinct intent kind that the rejection does not apply to.

## MVP Definition

### Launch With (v1)

Minimum viable product — matches the PROJECT.md vertical slice (web surface → parser → session → in-memory omp adapter → streamed events → blocks) and the shared RCL requirement.

- [ ] Conversation list + open session (local index; ACP `session/list`/`load` behind the harness; in-memory adapter for the slice) — the product is unusable without history
- [ ] Composer with Enter/Shift+Enter, `/` palette (filterable, 700+ entries) and `@` picker stub — feeds the IntentEnvelope parser, which is a hard requirement
- [ ] Streaming Markdown/code blocks with copy, scroll-lock, jump-to-latest — the core loop
- [ ] Stop/cancel with partial output retained + retry on error — ACP cancel contract implemented fully (tool calls → cancelled, permissions → cancelled)
- [ ] Tool-call cards with status, kind icon, collapsible raw I/O; **diff placeholder** and unknown-block fallback — as scoped in PROJECT.md
- [ ] Permission card bound to `session/request_permission` option kinds, keyboard-answerable, behind the deny-by-policy hook — the safety UI must exist from day one
- [ ] Plan/todo panel (replace-all semantics) and collapsed thought blocks — both are single-event renderers, cheap and expected
- [ ] Generic config-option selector (mode/model/thinking) rendered verbatim from `configOptions`, with search — table stakes *and* keeps model routing out of scope
- [ ] A2UI surface block (read-only render, catalog-bound, placeholder for unknown components) — the Core Value; actions deferred
- [ ] Accessibility baseline and keyboard shortcut sheet — required by PROJECT.md, far cheaper now than retrofitted
- [ ] SSE resume with sequence de-dup (web) — required by PROJECT.md; mobile reconnect reuses it

### Add After Validation (v1.x)

- [ ] Real diff renderer (hunks, side-by-side/stacked) — trigger: first real omp session produces `diff` content in testing
- [ ] Desktop terminal capability (`terminal/*`) and terminal embeds — trigger: desktop head spawns `omp acp` in-process
- [ ] Queued steering/follow-up chips (omp RPC path) — trigger: omp RPC adapter lands; this is the first visible "better than Zed for external agents" moment
- [ ] Usage meter + compaction dividers (`usage_update`, `compaction_update` capabilities) — trigger: long sessions in dogfooding
- [ ] Notices (`session.notices` capability) and desktop attention notifications — trigger: permission cards being missed while unfocused
- [ ] Session rename/export, conversation search — trigger: >20 sessions in the local index
- [ ] Fork from message (`sessionCapabilities.fork`) — trigger: users asking for "try again from here"
- [ ] Follow-the-agent file chips (desktop) — trigger: `locations[]` observed in real tool calls
- [ ] Side questions (`/btw`) popover — trigger: omp RPC path stable

### Future Consideration (v2+)

- [ ] Subagents panel (lifecycle/progress/steer/cancel) — defer: needs child-stream storage in the event log and omp RPC subscription plumbing; validate single-agent UX first
- [ ] Cross-surface continuity (desktop-hosted harness publishing to backend, approve from phone) — defer: needs Railway backend live and idempotent permission answers across surfaces
- [ ] A2UI v1.0 `actionResponse` (interactive surfaces) — defer: spec is a candidate, not current
- [ ] Checkpoints/restore via omp `checkpoint`/`rewind` — defer: requires workspace snapshots inside the sandbox provider
- [ ] Isolation badge with live attestation state — defer: sandbox providers beyond podman-compose are stubs this milestone
- [ ] Goal mode, composer ghost text — defer: polish on a stable omp RPC path
- [ ] Mobile push for approvals — defer: anti-feature for the scaffold; revisit with backend + device registry

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Streaming Markdown/code + scroll-lock | HIGH | LOW | P1 |
| Stop/cancel + partial retention + retry | HIGH | MEDIUM | P1 |
| Conversation list + resume | HIGH | MEDIUM | P1 |
| Tool-call cards (status, kind, raw I/O) | HIGH | MEDIUM | P1 |
| Permission card (ACP option kinds) + policy hook | HIGH | MEDIUM–HIGH | P1 |
| Slash-command palette (filterable) | HIGH | MEDIUM | P1 |
| Plan/todo panel | MEDIUM | LOW | P1 |
| Thought display (collapsed) | MEDIUM | LOW | P1 |
| Config-option selector (mode/model/thinking) | HIGH | MEDIUM | P1 |
| A2UI surface block (read-only) | HIGH (core value) | HIGH | P1 |
| Accessibility baseline + shortcuts | HIGH | MEDIUM | P1 |
| SSE resume / reconnect | HIGH | HIGH | P1 |
| @-mentions / image attachments | HIGH | MEDIUM | P1 (stub) → P2 (full) |
| Diff renderer | HIGH | MEDIUM | P2 |
| Terminal embeds (desktop `terminal/*`) | MEDIUM | HIGH | P2 |
| Steering / follow-up chips (omp RPC) | HIGH | MEDIUM | P2 |
| Usage meter + compaction dividers | MEDIUM | LOW | P2 |
| Notices + desktop attention | MEDIUM | LOW | P2 |
| Rename / export / search | MEDIUM | LOW–MEDIUM | P2 |
| Fork from message | MEDIUM | MEDIUM | P2 |
| Follow-the-agent chips | MEDIUM | MEDIUM | P2 |
| Side questions (/btw) | MEDIUM | LOW–MEDIUM | P2 |
| Subagents panel | HIGH | HIGH | P3 |
| Cross-surface continuity | HIGH | HIGH | P3 |
| A2UI actions (v1.0) | HIGH | HIGH | P3 |
| Checkpoints / restore | MEDIUM | HIGH | P3 |
| Isolation badge / sandbox pane | MEDIUM | MEDIUM | P3 |
| Goal mode, ghost text | LOW | LOW–MEDIUM | P3 |
| Mobile push | MEDIUM | HIGH | P3 (anti-feature now) |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Platform-Specific Expectations

| Concern | Desktop (MAUI Blazor Hybrid, harness in-process) | Web (Blazor WASM on Vercel) | Mobile (MAUI, backend-only) |
|---|---|---|---|
| Agent transport | Spawn `omp acp` / `omp --mode rpc` over stdio; own the process lifetime; restart on crash with `session/load` | Backend SSE only | Backend SSE only; reconnect on `Window.Resumed` |
| ACP client capabilities to advertise | `fs.readTextFile/writeTextFile: true`, `terminal: true`, `session.notices/compaction` | n/a (backend is the ACP client) | n/a |
| @-mention source | Fuzzy picker over `cwd` (file system) | Upload (resource with text/blob, no path) | Photo/document picker |
| Window state | Persist size/position, open session, pane layout per window (Claude Desktop/Zed restore these) | URL = session id; browser back/forward | Single-column; session id in nav state |
| Background/foreground | Keep streaming while hidden; attention notification when unfocused | Tab throttling: tolerate paused SSE, resume by seq | MAUI `Window.Deactivated/Stopped/Resumed`: drop SSE on Stopped, resume by seq on Resumed; iOS gives seconds of background, do not promise live streams |
| Permission cards | Modal-ish, keyboard-first | Same | Large touch targets; re-render pending card on resume |
| Terminal/diff | Full terminal embed + diff viewer | Diff viewer; command output as text | Diff viewer (stacked), text output |

## Competitor Feature Analysis

| Feature | Zed Agent Panel (ACP host) | Cursor Agent / Agents Window | Claude Code Desktop | OpenCode TUI | Rivet agentOS | Grok Chat | Our Approach |
|---------|---------------------------|------------------------------|---------------------|--------------|---------------|-----------|--------------|
| Session list/resume | Threads sidebar grouped by project, Ctrl+Tab switcher, archive/history, import threads from external agents over ACP | Sidebar + conversation search (local index) | Sidebar of sessions, Ctrl+Tab cycling, Cmd+W close | `/sessions` (`/resume`) | n/a — runtime, not a chat UI | Not verified (LOW) | Local index fed by `session/list` + `session_info_update`; replay via `session/load` |
| Streaming + stop | Stop button; "Send Now" interrupts | Stop; Enter steers, Enter again interrupts (CLI) | Esc stops; typing a correction steers without stopping | ctrl-based | n/a | Not verified | ACP cancel contract; omp `abort_and_restore_queue` |
| Steering / queued messages | Queue by default; steer toggle **only for Zed Agent** (cannot detect external agent turn boundaries) | "Send now" (next tool call) vs Tab to queue after turn | Correction delivered after current action | — | n/a | Not verified | omp RPC `steer`/`follow_up` + `queue_update` chips — ACP-only hosts cannot do this |
| Tool calls / permissions | Tool indicators; Tool Permissions allow/deny/confirm; ACP permission options | Checkpoints + approvals | Permission mode selector (Manual…), remembered per folder | `/details`; `attention` on permission | Permissions are sandbox policy (network default deny, fs mounts) | Not verified | ACP option kinds verbatim + deny-by-policy hook |
| Plans / thinking | Thread view | Todo lists | Transcript view modes Normal/Thinking/Verbose (Ctrl+O) | `/thinking` toggle | n/a | Not verified | `plan` panel + collapsed thought blocks, view-mode toggle |
| Diffs / terminal | Inline diffs, Restore Checkpoint; Terminal Threads | Diffs view with commit/PR; terminal profile | Diff pane (Cmd+Shift+D), terminal pane (Ctrl+`), browser pane | `diff_style` auto/stacked; `/undo` git-backed | n/a | Not verified | Diff card (v1 placeholder → v1.x renderer); terminal embed desktop-only |
| Slash commands / mentions | `/` commands from agent; `@` files, threads, rules | `/goal`, `/side`, `/in-cloud`; `@` | `@` files, `+` attachments, skills, connectors | `/` + leader keybinds; `@` fuzzy; `!` shell | n/a | Not verified | Palette over 701 agent commands; `@` → embedded resource |
| Model / mode selectors | Model selector, favourites, cycle keybind; profiles | Model picker, custom modes | Model + permission-mode row under prompt | `/models` | n/a | Not verified | Generic `configOptions` selector (mode/model/thinking) |
| Multi-session / background | Parallel threads, worktree isolation | Parallel cloud agents, cloud subagents, worktrees | Parallel sessions with Git isolation; sessions message each other | — | Runs ACP agents (Pi, Claude Code, OpenCode) inside a V8/Wasm VM | Not verified | v1 single active prompt per session, many sessions; omp subagents panel v2 |
| Generative UI | None (Markdown + cards) | None | App preview pane (runs your app, not agent-generated UI) | None | None | Not verified | A2UI surfaces — the differentiator |
| Keyboard / a11y | Extensive keybinds, thread navigation keys | Cmd+K palette, Cmd+F find | Published shortcut table, Cmd+/ sheet | Leader key `ctrl+x`, command palette `ctrl+p` | n/a | Not verified | Shortcut sheet from day one; live regions |
| Mobile | None | Phone/web/Slack for cloud agents | "Dispatch" sessions from phone | None | None | Mobile app exists (LOW) | MAUI mobile = backend viewer/approver first |

## Sources

Primary protocol sources (HIGH — official spec pages and the engine's bundled docs, read 2026-10-20; the GSD confidence seam classifies the `webfetch` provider as LOW generically, so the tier here is asserted on source authority, not provider):

- ACP Tool Calls — statuses, kinds, `ToolCallContent` (`content`/`diff`/`terminal`), `locations`, `session/request_permission` option kinds: https://agentclientprotocol.com/protocol/v1/tool-calls
- ACP Agent Plan — replace-all semantics, priorities, statuses: https://agentclientprotocol.com/protocol/v1/agent-plan
- ACP Session Modes — `current_mode_update`, `session/set_mode`, deprecation note: https://agentclientprotocol.com/protocol/v1/session-modes
- ACP Session Config Options — preferred over modes, `session/set_config_option`, `config_option_update`, categories: https://agentclientprotocol.com/protocol/v1/session-config-options
- ACP Slash Commands — `available_commands_update`, `input.hint`: https://agentclientprotocol.com/protocol/v1/slash-commands
- ACP Prompt Turn — full `sessionUpdate` variant list, stop reasons, cancellation obligations, notices, compaction, `usage_update`: https://agentclientprotocol.com/protocol/v1/prompt-turn
- ACP Content — text/image/audio/resource/resource_link blocks and prompt capabilities: https://agentclientprotocol.com/protocol/v1/content
- ACP Session Setup — `session/new`, `session/load` (replay), `session/resume` (no replay), `session/close`: https://agentclientprotocol.com/protocol/v1/session-setup
- ACP Session List — `session/list`, pagination, `session_info_update`: https://agentclientprotocol.com/protocol/v1/session-list
- ACP Initialization — client/agent capabilities (`fs`, `terminal`, prompt capabilities, session capabilities): https://agentclientprotocol.com/protocol/v1/initialization
- ACP Terminals — `terminal/create|output|wait_for_exit|kill|release`, `outputByteLimit`: https://agentclientprotocol.com/protocol/v1/terminals
- omp RPC Protocol Reference (bundled with omp 18.12.3): `omp://rpc.md` — commands (`prompt`/`steer`/`follow_up`/`abort`/`abort_and_restore_queue`/`remove_queued_message`/`promote_queued_message`, `set_model`/`set_thinking_level`, `get_messages_page`, `fork`/`branch`, `btw`, `predict_word`, `set_subagent_subscription`, `goal`), event stream, `queue_update`
- omp Tool approval mode (bundled): `omp://approval-mode.md` — modes, `always-ask-cached` grants, ACP permission mapping, subagent yolo
- omp CLI reference (bundled): `omp://cli-reference.md` — `--mode acp|rpc|rpc-ui`, `acp` subcommand
- omp `task` tool (bundled): `omp://tools/task.md` — subagent ids, async jobs, `agent://` outputs
- Live probe of `omp acp` 18.12.3 (`initialize`, `session/new` in `/tmp/ompos-acp-probe`) — capabilities, `configOptions` (mode/model/thinking), 701 available commands

Competitor documentation (HIGH for the facts quoted; read 2026-10-20):

- Zed Agent Panel: https://zed.dev/docs/ai/agent-panel — queueing/steering limitation for external agents, checkpoints, thread navigation, model selector
- Zed External Agents (ACP): https://zed.dev/docs/ai/external-agents — ACP registry, thread import over ACP, feature availability varies per agent
- Zed Parallel Agents: https://zed.dev/docs/ai/parallel-agents — threads sidebar, worktree isolation
- Cursor Agent overview: https://cursor.com/docs/agent/overview — follow-ups ("Send now"/Tab), side chats, conversation search, `/goal`, checkpoints
- Cursor Agents Window: https://cursor.com/docs/agent/agents-window — parallel/cloud agents, diffs view, worktrees
- Claude Code Desktop: https://code.claude.com/docs/en/desktop — sessions sidebar, permission mode/model row, @mentions/attachments, view modes, shortcut table, panes
- OpenCode TUI: https://opencode.ai/docs/tui/ — `/sessions`, `/thinking`, `/undo`/`/redo`, `diff_style`, `attention` notifications, `@` fuzzy refs
- Rivet agentOS README: https://github.com/rivet-dev/agentos — V8/Wasm VM, permissions/mounts (runtime, not a chat UI; built-in agents speak ACP per PROJECT.md)
- A2UI: https://a2ui.org/ — v0.9.1 current, v1.0 candidate adds `actionResponse`
- .NET MAUI app lifecycle (Window `Deactivated/Stopped/Resumed`): https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/app-lifecycle

Not verified (LOW): Grok Chat feature set (no primary documentation fetched; listed only as "not verified"); exact iOS background-execution budget for streaming (platform knowledge, not measured); whether `omp acp` emits `usage_update`/`notice`/`compaction_update` in practice (capabilities were advertised by the client in the probe, but no prompt turn was run).

Open questions for phase research:
1. Which `sessionUpdate` variants `omp acp` actually emits during a turn (`plan`, `agent_thought_chunk`, `usage_update`, diff/terminal content) — run a scripted turn against a scratch repo and record the frames as fixtures for the deterministic in-memory adapter.
2. How A2UI payloads arrive from omp today (tool output? fenced block? MCP resource?) — determines the recogniser the harness needs before any A2UI block renders.
3. Whether `steer`/`follow_up` should be exposed on the ACP transport via a local queue (send on `end_turn`) or hidden entirely until the omp RPC adapter exists.
4. Terminal embed on desktop: own PTY implementation in C# vs delegating to omp's bash tool (advertise `terminal: false` on desktop too for v1, revisit).

---
*Feature research for: OmpOS — chat-style agent workspace / ACP harness with Generative UI*
*Researched: 2026-10-20*
*Tracking: SPE-17751 (ultrathink node n3 "Compare surface technology and streaming options") — https://linear.app/swcstudio/issue/SPE-17751 · Notion https://app.notion.com/p/3ffbc1a0c7ae8160bdbadce533321a2a*
