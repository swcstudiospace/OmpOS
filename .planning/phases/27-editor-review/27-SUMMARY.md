# Phase 27: Editor and Review, Summary

Position 2026-10-27: the editor, tree, Diff review view and inline edit view exist in the tree since this record ("the native editor view is not built" no longer holds); Diff review is fed by the Changes bar. The audited position is in the Phase 27 Status line of `.planning/ROADMAP.md` and in `docs/quality/cursor-parity-audit-2026-10-26.md`. The text below is kept as history.

**Status: partially complete.** The logic for the workspace tree, per-hunk diff review and inline edit is done. The native editor view is not built, and tabs, highlighting and find with match count (ADE-09) are not done.

Mac channel: No. All editor and review tests run headless on the Linux gate.

## Success criteria (ROADMAP Phase 27)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Syntax highlighting per language, tabs, find with match count | Not met | ADE-09 NOT DONE. ADR R-2 open (candidate Avalonia.AvaloniaEdit 12.0.0, MIT; find panel unverified) |
| 2 | Open folder sets the workspace root; file tree lists and expands | Partial | ADE-10 logic done: src/OmpOS.Presentation/Editor/WorkspaceTree.cs; tests/OmpOS.Presentation.Tests/Editor/WorkspaceTreeTests.cs. No native tree view |
| 3 | Per-hunk accept and reject writes only that hunk | Partial | ADE-11 logic done: src/OmpOS.Presentation/Editor/DiffReview.cs; tests/OmpOS.Presentation.Tests/Editor/DiffReviewTests.cs. No native review view |
| 4 | Inline edit sends range and instruction; result opens as a reviewable diff | Partial | ADE-12 logic done: src/OmpOS.Presentation/Editor/InlineEdit.cs; tests/OmpOS.Presentation.Tests/Editor/InlineEditTests.cs. Recorded-provider-reply test: not confirmed |

## Open items

- Decide the editor control under ADR R-2, then build the native editor with tabs, highlighting and find. Owner: owner (R-2 decision), then engineering. Route: ROADMAP Phase 27 criterion 1.
- Build the native views for the file tree, the per-hunk review and inline edit. Owner: engineering. Route: ROADMAP Phase 27 criteria 2 to 4.
- Add a file-content test that accepts one hunk and rejects another (criterion 3). Owner: engineering. Route: tests/OmpOS.Presentation.Tests/Editor/DiffReviewTests.cs.
- Add a recorded-provider-reply test for inline edit (criterion 4). Owner: engineering. Route: tests/OmpOS.Presentation.Tests/Editor/InlineEditTests.cs.

## Commits

- 9e5913d: ADE-04 to ADE-20 foundations on the gate-green tree (includes the ADE-10 to ADE-12 logic)
