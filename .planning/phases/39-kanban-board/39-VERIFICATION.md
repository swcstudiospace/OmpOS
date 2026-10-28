# Phase 39 Verification: Kanban Board

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. Every test below is hermetic: fake sessions, a fake steering service, a fake Substrate details source or a stub Substrate handler, temporary directories. None starts a real omp, sends a prompt or uses a credential.

Results recorded on 2026-10-26 are in `39-SUMMARY.md`. Project totals recorded after the native steering work: the whole-solution run passed 5409 tests with 0 failed and 10 skipped (`Category=Live`) (F7). After the Kanban commits it passed 4237 (F5). After the Settings and Models wave: `Autonogrammer.Presentation.Tests` 975, `Autonogrammer.Native.Tests` 1087 (F6b).

## Gate tests

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1, 3 | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*KanbanBoardModelTests*" --filter-class "*RuntimeStatusMapTests*" --filter-class "*KanbanCardStateTests*" --filter-class "*UnmappedRuntimeSignalExceptionTests*"` | One card per chat with title, model, provider and last activity; status changes only through `RuntimeStatusMap`; an unmapped signal or an unknown chat changes nothing; a catalogue row's own status change replaces a signal; the stale flag changes without changing the status; an unreachable session shows the error text. |
| 2 | the class above plus `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KanbanBoardDropTests*"` | Only Backlog and Archived accept a drop, in the model and in the view; a drop on a runtime column changes nothing. |
| 4 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KanbanBoardViewStateTests*"` | The ten column names and counts are drawn. The decision itself is the table in `39-CONTEXT.md`. |
| 5 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*AgentPanelTabsTests*" --filter-class "*AgentPanelKanbanHostingTests*"` | The tab strip names both tabs and follows the skip link; Ctrl+Shift+K and Ctrl+Shift+A switch tabs; the session and its draft survive a switch; the head's board outlives the view. |
| 6 | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*KanbanBoardStateTests*"`, `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KanbanCardViewTests*" --filter-class "*SessionEngineSetTests*"` | Chats and omp terminal sessions each have one card; ended and closed sessions stay as cards in a terminal column (Completed, Failed, Archived); a closed Paused session goes to Archived; closing records the lifecycle. |
| 7 | `KanbanBoardStateTests` (Substrate fields, activation, announcements), `KanbanCardViewTests` (live updates, Substrate rows) and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KanbanHeadTests*"` | Fields appear only when reported and read unavailable otherwise; a stub Substrate that is reachable but holds no task shows the Substrate as available with empty fields; the lease holder is never reported by the head. |
| 8 | `KanbanBoardViewStateTests`, `KanbanBoardKeyboardTests`, `KanbanMarkupTests`, `KanbanCardViewTests` (all in `tests/Autonogrammer.Native.Tests/Kanban`) | One test per state, named tab stops, arrow-key movement, Enter opens the session, the focus ring, reduced motion for the skeleton and the running glyph, status in words and a glyph. |
| 9 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KanbanSteerFlyoutTests*" --filter-class "*CardSteeringTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*KanbanCardSteeringTests*"` | The card's steer box calls the steering service of its own session; a refusal keeps the text and shows the reason; the head's card steering reports what omp answered (see `49-VERIFICATION.md`). |
| 10 | `KanbanMarkupTests.NoKanbanViewHoldsALiteralFontFamily`, `KanbanCardViewTests.FontsResolveThroughTheTokensAndTextWraps` | No literal family in the Kanban AXAML; the card text resolves its family through the tokens. |
| gate | `./bootstrap.sh`, or `dotnet build Autonogrammer.Linux.slnf -c Release` then `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/ui-parity-2026-10-26/before/04-kanban-cards.png` | The Kanban tab with a Queued chat card and a Running omp terminal card: kind chip, `Model unknown`, status chip with glyph and word, relative time, `Task`, `Graph`, `Lessons` and `Lease` rows reading `unavailable`, a Steer button, the live-region line `omp terminal 1: Running`. Backlog shows `Drop a card here`, the other empty columns `No tasks`. The fifth column is cut at the window edge. |
| `docs/runbooks/evidence/ui-parity-2026-10-26/before/05-kanban-empty.png` | The empty board: `No sessions yet`, `Start a session and it appears here as a card`, and the `Start a session` button. |

Both were made under Xvfb with the real omp as the engine in a sandbox, before the Phase 50 restyle. The body text has the shapes of DM Sans and the `Graph` and `Lease` lines a monospace face (judged by eye). There are no screenshots of the loading, error or disconnected state.

## Known limits of this verification

- Only fakes drive the board. No test lets a real omp terminal end and reach a terminal column.
- The head test for closing asserts the registry lifecycle (`Paused` or `Completed`), and a presentation test asserts the Archived column for a closed Paused session; no single test goes from the close to the column.
- The Substrate fields were never read from a real Substrate; the lease holder has no source.
- Linux only. The screenshots show the white look that Phase 50 replaces with a Dark default.
