# Phase 69 plan check, round 2 (after the orchestrator's fixes, 2026-10-29)

Re-ran over 69-01..69-06-PLAN.md, 69-CONTEXT.md (D-01..D-45), 69-RESEARCH.md, 69-VALIDATION.md. Nothing was built or run; all facts below come from reading the tree at `/root/src/repos/ompos-autonogrammer`.

## Verdict: PASS WITH WARNINGS

No BLOCKER remains. All four round-1 BLOCKERS are fixed in the plan text.

## BLOCKERS

None.

## Round-1 BLOCKERS: fix confirmed in the plan text

- **B1 (69-03 task order and files):** fixed.
  - Task 1 now creates `SurfaceSwitchView`, `CoworkView`, `CoworkRailView`, `CoworkDockHost`, `CoworkSurfaceServices`, the settings section, `NativeCoworkAccountGate` and `CoworkSessionCatalogAdapter`.
  - Task 2 creates `SurfaceHost`, `MainWindow` and `App.axaml.cs` wiring, the `MainView` slot, `ViewMapping`, `HeadSettings`, `ShellHarness` and the shortcuts.
  - Task 3 does the chrome slot.
  - Checked by script: frontmatter `files_modified` equals the union of task `<files>` in all six plans, with no difference in either direction.
  - No file is owned by two plans.
  - The test classes sit in the task that writes them; each task's `<automated>` runs its own classes. Task 2 also runs `ShortcutCatalogueTests`, `KanbanHeadTests` and ArchitectureTests `HeadMotionTests`.
- **B2 (tunnel handler):** fixed. 69-03 Task 2 (action, `SurfaceHost` bullet) owns the Ctrl+Shift+1/2 window-level tunnel handler. It runs before `MainView.OnKeyDownTunnel`, and `MainView` key code is not edited. `ShortcutCatalogueTests` asserts handled, focus and Meta.
- **B3 (NeedsApproval source):** fixed.
  - 69-01 adds `Contracts/Cowork/CoworkSessionStatus.cs` with `WaitingForDecision` (in frontmatter and Task 1 `<files>`).
  - 69-02:67 names the mapping. The Code side reads `SessionCatalogModel.Rows[*].Status` (`AgentRowStatus`): Running or Queued counts as running, `NeedsApproval` as needs approval, `Failed` as failed. The Cowork side reads the `CoworkSessionStatus` members.
  - Tree check: `AgentRowStatus` has `NeedsApproval`, `Running`, `Queued`, `Failed`, and `AgentRow.Status` is `AgentRowStatus`. `SessionCatalogModel.Rows` is at `SessionCatalogModel.cs:104`.
- **B4 (services-up form):** fixed. 69-05:146,149,176 and VALIDATION:33 now read `env_file="$(bash scripts/test-services.sh up)" && . "$env_file" && ag-dotnet test …` and then `down`. This matches `bootstrap.sh:77`.

Also confirmed: the `SessionKeyHolderTests` change from this round is in the plan. `tests/Autonogrammer.UI.Tests/Security/SessionKeyHolderTests.cs:16,60-72,92` scans `.cs/.razor/.js/.html` under `src/Autonogrammer.UI` and `apps/Autonogrammer.Web` for `localStorage`, `sessionStorage`, `indexedDB` and `document.cookie`. The only exemptions are the `DisplayPreferenceScripts` array `["autonogrammer-theme.js","theme-boot.js"]` (matched by directory `scripts` plus file name), whose contents must also not contain apikey, secret or token. 69-04 Task 3 lists the file in frontmatter (:52) and `<files>` (:169), adds `surface-boot.js` to the allow-list, requires it to contain none of those three words, adds an automated run of `*SessionKeyHolderTests*` (:194) and has `WorkbenchView` call `autonogrammerSurface.read/write`. This is consistent.

## WARNINGS

1. **69-02:106 contradicts 69-02:67.**
   - The acceptance line says no file under `Presentation/Cowork` references "SessionCatalogModel".
   - Line 67 has `SurfaceActivityModel` take "a Func over the Code rows plus a change signal". That works only if the Func is typed over `AgentRow` or `AgentRowStatus`, not over the model.
   - Make 69-02:106 say "references no `SessionCatalogModel` or Kanban type; takes `Func<IReadOnlyList<AgentRow>>`", and say the same in the Task 1 action.
2. **`CoworkSessionSync` is still never constructed, and `ICoworkSessionApi` still has no HTTP implementation.** This is the round-1 W2, unchanged in D-43(a). Neither 69-03 nor 70-04 claims it, so AG-69-11 is library-only. Add a hand-off line to 70-CONTEXT, or an adapter to 69-03 Task 1.
3. **`surface-keys.test.mjs` is not run by any gate.**
   - Nothing in `bootstrap.sh`, `.github` or `scripts/ci` runs `scripts/web-smoke/**/*.test.mjs`. The `a11y` ones are run by hand as their own headers say, and `plugin-node-tests.sh` covers only the plugins.
   - The plan's own `<automated>` does run it. Note it in the SUMMARY, or have `scripts/ci/plugin-node-tests.sh` or the CI workflow call it.
   - The import of `autonogrammer-keys.js` (ES `export` in a `.js` file) relies on Node's module detection. That is fine on the host's node v24.18.0, but would fail on a Node without it.
4. **Stale counts.** 69-03:137,142 and the amendments bullet still say "112 call sites" of `MainViewOf`. The tree has 109 uses in `tests/**/*.cs`, plus the definition. This is harmless because the helper is changed once, but fix the number.
5. **Phase 67 is a hard prerequisite of two tasks.**
   - 69-04 Task 1 halts without `GET /account/sessions` (not in `docs/api/openapi.json` today).
   - 69-03 Task 1 halts the gate file without `IAccountSession`.
   - 69-05 needs phase 68's `SessionSurface` and `ListByOwnerAsync` (the fake was withdrawn, D-38).
   - Keep the order 67 < 68 < 69 < 74 when scheduling.
6. **`RESEARCH.md:50` still shows the bare `scripts/test-services.sh up` then `dotnet test`.** It is informational. VALIDATION and 69-05 carry the correct form.
7. **ROADMAP.md:1392 lists only `69-01-PLAN.md`.** Phases 68 and 70 do the same, so this is consistent and only INFO.

## Checks (c) and (d)

- **Tags and pairing.** `<task>` is balanced (3/2/3/4/3/3 tasks); `<action>`, `<verify>`, `<automated>`, `<fails_when>`, `<files>`, `<read_first>`, `<done>` and `<acceptance_criteria>` all balance. Every `<automated>` has its own `<fails_when>`.
  - 69-04 Task 3 has 6 pairs and 69-03 Task 2 has 4.
  - Every task has all of name, files, read_first, action, acceptance_criteria, verify and done.
- **Commands.** Test commands use `/tmp/ag-bin/ag-dotnet`; `node --test`, `node --check`, `python3` and `bash scripts/ci/*.sh` appear as host binaries.
- **Graph and waves.**
  - Waves: 69-01 wave 1; 69-02 and 69-05 wave 2 on 69-01; 69-03 and 69-04 wave 3 on 02 (03 also on 01); 69-06 wave 4 on 01, 02, 03, 04 and 05.
  - This matches D-44, there is no cycle, and W7 is fixed.
- **Requirements.** AG-69-1..13 are all in a plan's `requirements`: 69-01 {6, 7, 11}; 69-02 {4, 8, 12, 13}; 69-03 {1, 2, 3, 4, 8, 12, 13}; 69-04 {2, 3, 5, 12}; 69-05 {9}; 69-06 {10}.
- **CONTEXT vs plans.**
  - D-34's account-menu resolution is mirrored in 69-04 (`SignOutButton.razor`, `TopBarSignOutTests` in files; `TopBarSignOutTests` run in Task 2 verify).
  - D-45 (status type, dock seam `RegisterPanel`) is mirrored in 69-01, 69-02 (`RegisterPanel` at :68) and 69-03 Task 1 (`CoworkDockHostTests` register through `CoworkShellModel.RegisterPanel`).
  - D-33 and D-36 are mirrored in 69-04 Task 3. The 69-03 and 69-04 acceptance wording matches the actions.
  - No LOCKED decision D-01..D-32 was dropped.
- **Tree facts re-verified this round.**
  - `autonogrammer-keys.js` `registerShortcuts` uses `event.key.toLowerCase()` today, so the planned switch to `event.code` Digit1/Digit2 is a real change; `composing(event)` exists for the IME case.
  - `components/Auth/SignOutButton.razor` exists and renders only when `_session is { HasToken: true }`.
  - `tests/Autonogrammer.UI.Tests/Shell/TopBarSignOutTests.cs` exists.
  - No top-level `scripts/web-smoke/*.test.mjs` exists, so `surface-keys.test.mjs` is new and does not clash; the existing tests are under `scripts/web-smoke/a11y/`.
