# Phase 29: Browser Pane, Completion and Export, Summary

Position 2026-10-27: the audited position is in the Phase 29 Status line of `.planning/ROADMAP.md` and in `docs/quality/cursor-parity-audit-2026-10-26.md`. The text below was written earlier and is kept as history where it conflicts.

**Status: partially complete.** Export and usage history are done (ADE-20). The completion client is done, but its wire format needs the provider owner's confirmation (ADE-13). The browser pane is not started (ADE-15).

Mac channel: No. Verification runs on the Linux gate with headless Chromium and fixture pages. No claim is made about the browser pane on the macOS and mobile heads.

## Success criteria (ROADMAP Phase 29)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Browser pane loads a URL; design mode sends the selected element with selector and box | Not started | ADE-15 NOT STARTED. ADR R-3 open (candidate Avalonia.Controls.WebView 12.1.1, MIT) |
| 2 | Inline completion calls only the configured provider; none configured means no call | Partial | ADE-13 client done: src/Autonogrammer.Harness.Completion/CompletionClient.cs; tests/Autonogrammer.Harness.Completion.Tests/CompletionClientTests.cs (25 tests). Custom wire format awaits provider owner confirmation. No-configuration test: not confirmed |
| 3 | Chats and runs export to Markdown; usage history lists each run's usage | Met | ADE-20 done: src/Autonogrammer.Presentation/Export; golden file tests/Autonogrammer.Presentation.Tests/Export/Golden/transcript.golden.md with TranscriptMarkdownExporterTests.cs; tests/Autonogrammer.Presentation.Tests/Export/UsageHistoryTests.cs |

## Open items

- Provider owner confirms the completion wire format. Owner: provider owner (external). Route: ROADMAP Phase 29 criterion 2; src/Autonogrammer.Harness.Completion.
- Decide ADR R-3 and build the browser pane with design mode. Owner: owner (R-3 decision), then engineering. Route: docs/decisions/ADR-0004-cross-platform-ui.md (decision 4 and R-3).
- Confirm the no-configuration case: no configured provider means no provider call (criterion 2). Owner: engineering. Route: tests/Autonogrammer.Harness.Completion.Tests/CompletionAcceptanceTests.cs.
- Confirm the Markdown export includes tool calls and approvals (criterion 3). Owner: engineering. Route: tests/Autonogrammer.Presentation.Tests/Export/TranscriptMarkdownExporterTests.cs.
- Add a design-mode test on a fixture page in headless Chromium (criterion 1). Owner: engineering. Route: ROADMAP Phase 29 criterion 1.

## Commits

- 9e5913d: ADE-04 to ADE-20 foundations on the gate-green tree (includes ADE-13 and ADE-20)
