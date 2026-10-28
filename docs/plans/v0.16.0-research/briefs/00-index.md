# Briefs index and reconciliation: milestone v0.16.0 (Autonogrammer)

Written by the design integrator on 2026-10-28 over ADR-0030 to ADR-0037 and briefs 67 to 76. Names are POST-RENAME (`Autonogrammer.*`); the tree on disk still says `OmpOS.*`. Nothing here was built or run. The scaffold step that creates every new project is in [00-scaffold-manifest.md](00-scaffold-manifest.md).

## 1. Phase table

Requirement ids are `AG-<phase>-<n>`. Work units are the units of the brief. "Cannot be verified here" is taken from each brief's own section (host: Linux x64, uid 0, no Windows or macOS, no Android SDK, Railway deploys paused, no live Supabase, no live omp turn: OD-07 is not approved).

| Phase | Name | Brief | ADR | Depends on | Requirement ids | Units | Cannot be verified on this host |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 66 | Rebrand (OmpOS to Autonogrammer) | [66-rebrand.md](66-rebrand.md); research report [01](../01-rename-inventory.md); tool `scripts/rebrand/rebrand.py`, runbook `docs/runbooks/rebrand-autonogrammer.md` | none (physical names: ADR-0031 D1) | none; applied after the units of 67 to 76 (they write post-rename names and must not run it) | AG-66-* | see the brief | not stated by any brief; the renamed tree has to be built and gated once, which this design pass did not do |
| 67 | Accounts: Supabase passwordless sign-in, GoTrue client, account surface | [67-accounts.md](67-accounts.md) | ADR-0030 | 68 (units 4 and 5 need U3: surface column, repository methods); 69 and 70 (SurfaceHost chrome slot for the account menu) | AG-67-1 to AG-67-24 | 6 | any live Supabase call (publishable key and owner actions O1, O3, O4); Windows DPAPI and macOS Keychain (compile only); web two-tab hand-off and CSP in a real browser; a real deploy; the Linux keyring live test is possible but was not run |
| 68 | Data plane: names, schema, migrations, telemetry sink, resilience, ledger sink | [68-data-plane.md](68-data-plane.md) | ADR-0031 | rename tool applied after its units; consumed by 67 (M0003), 74 (M0005, M0008), 75 (M0009) | AG-68-1 to AG-68-14 | 6 (U1 to U6) | Railway private network, Timescale edition and `add_retention_policy`, Substrate prefix and index location, Greptime per-database authorisation, any hosted deploy (OQ-73), rootless podman in CI (OQ-168), head banners on Windows, macOS, Android |
| 69 | Surface switch (Code or Cowork), Cowork layout, Cowork session model | [69-mode-shell-and-cowork-sessions.md](69-mode-shell-and-cowork-sessions.md) | ADR-0032 | 67 (account state), 68 (`ListByOwnerAsync`, `surface` column) | AG-69-1 to AG-69-12 | 6 | Windows and macOS rendering and Meta shortcuts; Wayland focus; screen readers; whether a browser reserves Ctrl+Shift+1 or 2; live Supabase and Railway routes |
| 70 | Cowork chat core on omp over ACP and the tool host | [70-cowork-chat-core.md](70-cowork-chat-core.md) | ADR-0032 | 69 units 1 to 3; 67 (account gate); 68 (audit writer, a null audit is used until it lands) | AG-70-1 to AG-70-14 | 6 | a live omp turn through the gateway (`--append-system-prompt`, overlay keys, MCP timeout against a long approval, the `mcp__cowork_` name); real providers; model image use; Windows and macOS path rules; screen readers |
| 71 | Cowork Artifacts | [71-artifacts.md](71-artifacts.md) | ADR-0033 | 67, 68, 70 (`Cowork.Abstractions`, `IDockPanel`, `ToolUpdateImage`), ADR-0009 and ADR-0010 | AG-71-1 to AG-71-24 | 6 | Windows WebView2, macOS WKWebView, Android WebView; CSP-by-meta, navigation cancel and bridge option beyond a private Xvfb run on WebKitGTK; Firefox and Safari sandboxes; screen readers; the real Railway API framed from the real Vercel origin; live Supabase and Railway; a 5.2 MB inline script load; legal admission of EPL-2.0 (OQ-400) |
| 72 | Cowork Browser Use | [72-browser-use.md](72-browser-use.md) | ADR-0034 | 70 (and 69); ADR-0036 D13 (no relay frames); 68; 67 | AG-72-1 to AG-72-16 | 6 (WU-1 to WU-6) | Windows and macOS launch, pipe handles, proxy, a downloaded browser's signature; a pinned Chrome for Testing (no manifest rows, terms unread); a live omp turn with gateway, images and overlay; vision accuracy and cost; relay frame rate (no feed exists); fonts on a minimal Linux; sites that block headless Chromium. Verifiable here: the success criteria on `google-chrome` 153 with a private Xvfb |
| 73 | Cowork Computer Use | [73-computer-use.md](73-computer-use.md) | ADR-0035 | 67, 70, 74 (relay refusals, capability flag), 72 optional | AG-73-1 to AG-73-24 | 6 | Windows (SendInput, UIPI, GDI, secure desktop, hotkey); macOS (TCC, CGEvent, `screencapture`, Carbon hotkey, Spaces); Wayland portals; AT-SPI; multi-DPI and multi-monitor; non-US layouts; top-most over full-screen applications; live vision accuracy; omp reaching the tools and honouring `computer.enabled=false` (OD-07). X11 is verifiable under a private Xvfb |
| 74 | Dispatch relay | [74-dispatch.md](74-dispatch.md) | ADR-0036 | 67 (`IAccountSession`, Secrets.Os, `IAccountFeature`), 68 (M0005, M0008, keyspace, `ITenantHasher`, `IAuditLedger`, `accounts.feed_seq`), 69 and 70 (SurfaceHost chrome slot, Cowork host), 72 and 73 (tool classes, `AllowDispatchSessions`) | AG-74-1 to AG-74-18 | 6 (U1 to U6) | the Android build and a real phone; Windows and macOS (compile only); live Supabase tokens; Railway long WebSockets and deploy restarts; Dragonfly multi-replica routing on Railway; screen-reader behaviour of the approval prompt; the Substrate ledger (never called). Owner actions: two P-256 keys, unpause deploys, create the API service, enable `Dispatch__Enabled` |
| 75 | Cowork parity A: projects, instructions, memory, scheduled tasks | [75-cowork-parity-a.md](75-cowork-parity-a.md) | ADR-0037 | 67, 68 (U3 writes M0009), 69 and 70 (project store, host, profile, tool host), ADR-0010 and ADR-0014 | AG-75-1 to AG-75-11 | 6 (75-01 to 75-06) | live Supabase and live Railway Postgres; Windows and macOS; omp keeping a 28 KiB first-prompt block, honouring `memory.backend: off`, a model calling the memory tools (OD-07); a task waking from real sleep; screen readers |
| 76 | Cowork parity B: plugin bundles, skills, connectors, Auto mode, isolation, sub-agents, notifications | [76-cowork-parity-b.md](76-cowork-parity-b.md) | ADR-0037 | 75-06 (`ILaunchContributor`, `LaunchContribution`, `docs/parity/cowork-parity.md`), 69 and 70 | AG-76-1 to AG-76-14 | 6 (76-01 to 76-06) | a real `bwrap` run (user namespaces untried, CI cannot create them, OQ-168); macOS `sandbox-exec` and `osascript`; Windows job objects; OS notification display; omp consuming `skills.customDirectories`, `--skills`, `--approval-mode always-ask`; whether a `task` call names its agent; MCP OAuth; screen readers |
| 77 | Hardening, docs, parity audit and integration | [77-hardening-and-integration.md](77-hardening-and-integration.md); ADR-0037 D14, report 09 sections 2 and 7 | all (ADR-0030 to ADR-0037) | 67 to 76 landed, 66 applied | AG-77-* | see the brief | every live run (OD-07 not approved; Supabase, Railway); the 0.16.0 `VersionPrefix` bump and relock of about 85 lock files (report 09 D2: once, at the end); legal admission of the vendored Mermaid file (OQ-400); Windows and macOS runners |

Order of phases: 68 first with 67 units 1 to 3 in parallel; 69 then 70; 71, 72, 73 after 70 unit 1 (they need `Cowork.Abstractions` only); 74 after 67 and 68; 75 after 69, 70 and 68 U3; 76 after 75-06. The scaffold step (manifest) precedes all of them.

## 2. Naming rules (one rule each; the ADRs already agree unless R-items say otherwise)

| Subject | Rule |
| --- | --- |
| Cowork tool descriptor names | `<ProviderId>_<verb>`: `^[a-z][a-z0-9]*(_[a-z0-9]+)*$`, 3 to 40 characters, starts with the provider id and `_`, never contains `__`, never starts `cowork_` (ADR-0032 D5). ProviderIds: `artifact`, `browser`, `computer`, `memory`. omp shows them as `mcp__cowork_<name>`. |
| Double underscore | Reserved for registry tools of upstream MCP servers (`server__tool`, `McpGatewayToolNames.Separator`). A Cowork name never contains it, which is what keeps the two name sets disjoint. |
| Dotted names | Never tool names. Dispatch operations: `session.list`, `session.steer`, `session.cancel`, `approval.decide`, `code.prompt`, `cowork.task`, `command.cancel`. omp settings: `browser.enabled`, `computer.enabled`, `memory.backend`. Message ids: `computer.unavailable.wayland`. In ADR-0035 and brief 73 `computer.<x>` is only the logical label of descriptor `computer_<x>` (ADR-0035 D8). |
| Dispatch capability ids | `sessions`, `code`, `cowork`, `browser_use`, `computer_use`. `browser_use` and `computer_use` are tool classes (provider ids `browser`, `computer`) named in `cowork.task.payload.toolClasses`; they are never the capability of a command (ADR-0036 D2). |
| Session discriminator | `SessionSurface { Code, Cowork }` in `Autonogrammer.Contracts.Sessions`, created by phase 68 and reused everywhere; column `surface text NOT NULL DEFAULT 'code'`; never `SessionKind`. |
| Repository list | The only list is `ListByOwnerAsync(string ownerId, SessionSurface surface, bool includeClosed, int limit, SessionCursor? cursor, CancellationToken cancellationToken)`; `surface` is required and non-nullable. ADR-0032 and brief 69 were corrected to this signature. |
| Dragonfly keys | `autonogrammer:v1:<domain>:<tenant>:<entity>[:<id>]`, domains `lease`, `idem`, `presence`, `rl`, `auth`, `sig`; built only by `DragonflyKeyspace`. All documents agree. |
| Secret names | `autonogrammer.account.session`; `autonogrammer.dispatch.device-key.<deviceId>`; `autonogrammer.dispatch.device-credential.<deviceId>`. The device key never goes to the file store. |
| Layers | As in `tests/Autonogrammer.ArchitectureTests/LayerMap.cs`; all documents agree on the layer of every new project (see the manifest). |

## 3. Open-question renumbering

Blocks by owner phase (old repo ids below 300, for example OQ-73, OQ-168, are untouched). Every id was replaced in two passes through temporary tokens, per document, from the ids that document defines; no id was replaced twice.

| Block | Owner phase | Old to new |
| --- | --- | --- |
| OQ-300 to OQ-319 | 67 (ADR-0030) | OQ-300 to OQ-308 unchanged |
| OQ-320 to OQ-339 | 69 and 70 (ADR-0032) | OQ-320 to OQ-329 unchanged (they were already coordinated) |
| OQ-340 to OQ-359 | 68 (ADR-0031) | OQ-340 to OQ-350 unchanged |
| OQ-360 to OQ-379 | 72 (ADR-0034) | OQ-360 to OQ-377 unchanged |
| OQ-380 to OQ-399 | 73 (ADR-0035) | OQ-370 to OQ-384 became OQ-380 to OQ-394 (plus 10) |
| OQ-400 to OQ-419 | 71 (ADR-0033) | OQ-371 to OQ-382 became OQ-400 to OQ-411 (plus 29) |
| OQ-420 to OQ-439 | 74 (ADR-0036) | OQ-740 to OQ-751 became OQ-420 to OQ-431 (minus 320) |
| OQ-440 to OQ-479 | 75 and 76 (ADR-0037) | phase 75: OQ-375 to OQ-381 became OQ-440 to OQ-446; phase 76: OQ-385 to OQ-394 became OQ-450 to OQ-459 (plus 65 each) |

Documents changed by the renumbering: ADR-0033, brief 71 (plus 29); ADR-0035, brief 73 (plus 10); ADR-0036, brief 74 (minus 320); ADR-0037, briefs 75 and 76 (plus 65). ADR-0036 also cites OQ-348 of ADR-0031 (cross-citation table). All other documents keep their numbers; their block sentences were rewritten ("reserved", "the orchestrator may renumber" removed).

All 104 rows were appended to `docs/open-questions.md` after OQ-168 (6 columns, owner and status filled; `Why it matters` of the briefs that had none names the ADR section). OQ-343 (ADR-0031) and OQ-402 (ADR-0033) are the same decision with opposite defaults; both rows say so (R4).

### Cross-citations (an id defined by a different document, resolved to that document's new id)

| Citing document | Place | Cites (as written) | Defined by | Final id |
| --- | --- | --- | --- | --- |
| ADR-0034 | Decision D9 item 7 (line 126) | OQ-322 ("ADR-0032 OQ-322") | ADR-0032, brief 70 | OQ-322 (unchanged) |
| ADR-0036 | D12 (line 121) | OQ-348 ("ADR-0031 OQ-348") | ADR-0031, brief 68 | OQ-348 (unchanged) |
| brief 70 | Index rows (line 97) | OQ-320, OQ-324, OQ-325, OQ-326 | brief 69 | unchanged |
| brief 72 | OQ-365 (line 97) and the block sentence (line 90) | OQ-322 | brief 70 | OQ-322 (unchanged) |
| brief 72 | OQ-375 (line 107) | OQ-749 ("ADR-0036 OQ-749") | brief 74: the browser policy for dispatched tasks is old OQ-747; old OQ-749 is the Android controller | OQ-427 (the miscitation was corrected, not carried) |
| brief 67 | Depends on (line 36) | the block "OQ-320 to OQ-329" reserved for ADR-0032 | briefs 69 and 70 | block OQ-320 to OQ-339 |

## 4. Reconciliation

### 4.1 Applied directly (spelling level)

- `ListByOwnerAsync` is written with its full signature everywhere: ADR-0032 (D2 rule 1 and the Candidate section) and brief 69 listed a three-argument form; ADR-0030, ADR-0031 and brief 67 already had the full one (ADR-0031 D4 owns it).
- ADR-0033 Open risks: `from_file` follows `CoworkSessionContext.TrustedFolders` and the Storage folder, not `ICoworkGrantStore` (which holds tool grants). The stale "table names are proposals of report 04" risk now points at R3.
- ADR-0030 D7: the freshness rule is the newest non-refresh `amr` entry (ADR-0036 D2), not `iat` and `session_id` (R18).
- Brief 68: only `Coordination.Dragonfly` (L2) references `Resilience`; `Coordination` (L1) must not (a same-layer edge needs a tuple that ADR-0031 does not grant).
- Brief 75: `CoworkSessionProfile` lives at `Cowork/Chat/` (brief 70), not `Cowork/Sessions/`.
- ADR-0034 and brief 72 cited on-disk spellings of paths (`src/OmpOS...`); they now use the post-rename spelling like the other documents. Brief 76 and ADR-0031 had one stray "OmpOS" each.
- `docs/decisions/README.md` rows for ADR-0030 to ADR-0037 (ADR-0035 row says `computer_*`; the ADR-0036 row no longer asserts the M0009 number, R2) and `docs/SUMMARY.md` entries (the docs gate rejects an ADR page that is not in SUMMARY.md).
- OQ-427 instead of OQ-749 in brief 72 (see the cross-citation table).

### 4.2 Genuine design conflicts (decided 2026-10-28; the owner delegated the decisions, every recommendation accepted)

R1. Computer Use start operation. Decided: there is no `computer.*` Dispatch operation; Computer Use is a tool class in `cowork.task.payload.toolClasses`; a `cowork.task` naming `computer_use` on a desktop with `Cowork:ComputerUse:Enabled` and `Cowork:ComputerUse:AllowDispatchSessions` registers `computer_*`, every other session none, `code.prompt` never; `computer.task` is gone (applied in ADR-0035 D12, ADR-0036, briefs 73 and 74).

R2. Migration M0009. Decided: M0009 stays `cowork_projects_memory`; the Dispatch device columns become M0010 `dispatch_device_state`, file-owned by phase 74 U2 and registered in the runner list by that unit; AG-68-4 says "up to the compiled head"; final list M0001 to M0010 (applied in ADR-0031 D4, ADR-0036 D2, briefs 68, 74 and 75).

R3. Artifact tables, size cap, RLS and migration owner. Decided: flat names, no RLS; M0006 (phase 68 U3) carries `artifacts`, `artifact_versions` (content cap 5,242,880 bytes) and `artifact_grants`; phase 71 U3 writes `PostgresArtifactStore` and no migration; erasure and the family list add `artifact_grants` and `artifacts` (applied in ADR-0031, ADR-0033, briefs 68 and 71, docs/open-questions.md).

R4. Artifact sync default. Decided: one setting, off by default (metadata only until the user enables sync); OQ-343 and OQ-402 merged into OQ-343 (OQ-402 row marked merged, nothing renumbered); the web head shows a "sync is off" state (applied in ADR-0031, ADR-0033, brief 71, docs/open-questions.md).

R5. Accounts table. Decided: ADR-0031 M0004 wins; `AccountResponse.email` is read from the validated token and never stored; ADR-0030 D7 loses its DDL sentence; `AccountEnrollmentMiddleware` upserts `last_seen_at` only (applied in ADR-0030, brief 67).

R6. Approval of artifact writes in Manual mode. Decided: ADR-0032 D6 amended: a stored grant may satisfy `ByPolicy` for any non-`Destructive` call whose preview names a grant; artifact writes return `Preview.Grant = ("artifact.write", "cowork")`, the first write asks once; tainted sessions still ask (applied in ADR-0032, ADR-0033, briefs 70 and 71).

R7. Dock slot cardinality. Decided: `IDockPanel` is a content contract; `CoworkDockHost` hosts a registered list with a tab strip and one visible panel; each provider registers its panel; the sentence is in ADR-0032 D3 and the test in brief 69 (applied in ADR-0032, ADR-0034, briefs 69 and 72).

R8. Account state seam. Decided: `Autonogrammer.Cowork` owns the port `ICoworkAccountGate`; the Native head implements it over `IAccountSession`; no tuple; `IAccountState` is renamed (applied in ADR-0032, briefs 69 and 70).

R9. Contract version. Decided: one bump 1.5.0 to 1.6.0 in the scaffold step; no phase edits `ContractVersion.Current`; checked by reading ADR-0008 section 7 and `src/*.Contracts/Plugins` (five plugin types, none references `Contracts/Ui`): `Contracts.Ui` is not a plugin contract, so 1.6.0 stands, and the scaffold step carries a check for it (applied in 00-scaffold-manifest.md, ADR-0031, ADR-0032, ADR-0033, ADR-0036, briefs 67, 68, 71 and 74).

R10. Artifact routes and the anonymous list. Decided: routes move to `/cowork/artifacts/...` and `/cowork/artifact-render/{ticket}` (module `Api/Cowork/Artifacts`); the OpenAPI transformer lists the render route as `security: []` with its own limiter partition and the connect route as scheme `DispatchDevice` (applied in ADR-0030 D7, ADR-0033, ADR-0036, briefs 67, 71 and 74).

R11. Option sections. Decided: Cowork features live under `Cowork:` (`Artifacts`, `BrowserUse`, `ComputerUse`, `Projects`, `Memory`); platform modules keep `Dispatch`, `Accounts`, `DataPlane` (applied in ADR-0033, ADR-0034, ADR-0035, ADR-0036, briefs 71 to 75).

R12. System-prompt budget. Decided: `CoworkSystemPromptOptions`: base 4 KiB, standing context 2 KiB, browser guidance 1 KiB, computer guidance 1 KiB, total at most 8 KiB; brief 70 tests the 4 KiB base (applied in ADR-0032 D4, ADR-0034 D8, ADR-0037 D4, briefs 70 and 72).

R13. Two 503 problem types for the same outage. Decided: `data_plane_unavailable` (closed `reason`) for every store outage; `coordination_unavailable` removed (applied in ADR-0031 D3 and D8, ADR-0036 D10, brief 68).

R14. Same-layer edges not declared. Decided: no tuple added; Presentation stays on `Contracts/Artifacts` types, the two Postgres stores take an `NpgsqlDataSource` from the host, the Native composition feeds bundle sources into the workspace and MCP catalog, `Dispatch.Desktop` uses `IAccountSession` and `ISecretStore` only (recorded in the manifest as written; no document needed a change) (applied in 00-scaffold-manifest.md).

R15. Cowork tool audit sink and `cowork_events.kind`. Decided: the desktop writes a local owner-only hash-chained JSONL `FileCoworkToolAudit`, nothing goes to the ledger; `cowork_events.kind` is the closed list `artifact.created`, `artifact.updated`, `artifact.grant`, `artifact.revoke`, `schedule.run`, `schedule.late`, `schedule.waiting_for_approval` (fixed in phase 68 U3); browser and computer events stay local (applied in ADR-0031 M0006, ADR-0032, briefs 68 and 70).

R16. Approval vocabulary and timeout names. Decided: "mode" means only `CoworkPermissionMode` (Manual, Auto shown as "Auto (rules)", Skip); renamed `Dispatch:CommandApprovalTimeout` and `Automations` `UnattendedApprovalTimeout`; `BrowserUse:PolicyMode` and `CoworkToolHostOptions.ApprovalTimeout` kept; Skip needing `FilesystemIsolated` or a per-session acknowledgement is written into ADR-0032 D4 (applied in ADR-0032, ADR-0036 D7, ADR-0037, briefs 70, 74 and 76).

R17. Who edits the shared registries. Decided: the scaffold step performs all of 00-scaffold-manifest.md; phase units add code only and edit a registry file only for rows the manifest marks "per phase" (applied in 00-scaffold-manifest.md, ADR-0037 D14, briefs 68 to 74).

R18. Pairing freshness. Decided: newest non-refresh `amr` entry within 10 minutes (ADR-0036 D2); `iat` is not used; OQ-430 stays open (applied in ADR-0030 D7 (see 4.1)).

### 4.3 Items outside the four tasks

- `docs/runbooks/rebrand-autonogrammer.md` is now in `docs/SUMMARY.md` (no orphan).
- `00-scaffold-manifest.md` marks one inference: which projects the API references (ADR-0033 D9, ADR-0037 D5 read as implying `Cowork.Artifacts(.Stores)` and `Cowork.Memory(.Postgres)`).
