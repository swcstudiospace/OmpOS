# ADR-0007: Code editor, TextMate highlighting and find for ADE-09

- Status: Accepted
- Date: 2026-10-22
- Deciders: autonogrammer-maintainers
- Scope: the code editor for ADE-09 (`docs/plans/ade-harness-plan.md`): tabs over open files, syntax highlighting for C#, TypeScript, JSON, YAML and Markdown, and find with a match count and moves between matches. Covers the NuGet pins for the editor. Does not cover the workspace tree (ADE-10), diff review (ADE-11), inline edit (ADE-12), saving to disk, or the shell wiring, which the orchestrator does.

## Context

ADE-09 needs an editor the native head can show. The presentation models already exist in `src/Autonogrammer.Presentation/Editor`: `TextDocument`, `EditorTabs`, `EditorTab`, `DocumentFindModel` and `TextMatch`. The native head binds them and holds no copy. Central package management is on with transitive pinning (`CentralPackageTransitivePinningEnabled`), so every package in the editor's graph needs a pin in `Directory.Packages.props`.

The plan names "AvaloniaEdit 12.0.0". On NuGet the 12.x line is published as `Avalonia.AvaloniaEdit`. The id `AvaloniaEdit` stops at 0.10.12, so the pin uses `Avalonia.AvaloniaEdit` at 12.0.0.

## Decision

1. The editor control is `Avalonia.AvaloniaEdit` 12.0.0. Highlighting uses `AvaloniaEdit.TextMate` 12.0.0 on top of TextMate grammars.
2. The grammars come from `TextMateSharp.Grammars` 2.0.3, which runs on `TextMateSharp` 2.0.3. One package supplies all five languages (table below).
3. The view lives in `apps/Autonogrammer.Native/Views/Editor`:
   - `EditorView` (XAML and code-behind) draws the tab strip, the find bar and the editor.
   - `EditorViewModel` wraps `EditorTabs` and the selected tab's `DocumentFindModel`. It copies neither.
   - `EditorTabItem` is the strip item for one tab; `EditorCommand` is the command it uses; `EditorLanguages` maps a file extension to its grammar scope.
4. Keys: Enter and F3 move to the next match; Shift+Enter and Shift+F3 move to the previous one; Escape moves focus from the find box to the editor. Tab order is tab strip, tab close buttons, find box, Previous, Next, Match case, then the editor. F3 is handled on the tunnelling route so the focused editor cannot swallow it.
5. Every control has an accessible name: "Open files", the file name for each tab, "Close {file}", "Find in file", "Previous match", "Next match", "Match case", "Find result" (a polite live region), and "Code editor".
6. Token colours use TextMate's `ThemeName.DarkPlus`. The view includes `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml` itself, so `App.axaml` is not changed.
7. The editor shows the selected tab's text. Edits go into that tab's `TextDocument` as offset-based `Replace` calls, so the model always holds the current text. Switching tabs rebuilds the editor buffer.

## Licences

| Package | Version | Licence | Source |
| --- | --- | --- | --- |
| Avalonia.AvaloniaEdit | 12.0.0 | MIT (nuspec expression; https://licenses.nuget.org/MIT) | https://github.com/AvaloniaUI/AvaloniaEdit |
| AvaloniaEdit.TextMate | 12.0.0 | MIT (nuspec expression; https://licenses.nuget.org/MIT) | https://github.com/AvaloniaUI/AvaloniaEdit |
| TextMateSharp | 2.0.3 | MIT (nuspec expression; GitHub licence `mit`) | https://github.com/danipen/TextMateSharp |
| TextMateSharp.Grammars | 2.0.3 | MIT (nuspec expression); each grammar keeps its upstream licence (below) | https://github.com/danipen/TextMateSharp |
| Onigwrap | 1.0.10 | MIT (nuspec expression); the package also ships Oniguruma (BSD-style, K. Kosako, 2002-2021) in `THIRD-PARTY-NOTICES.TXT` | https://github.com/aikawayataro/Onigwrap |

`Avalonia` 12.x is already pinned by the repository and resolved to 12.1.4. It is MIT and is not changed by this decision.

## Grammar sources

`TextMateSharp.Grammars` 2.0.3 embeds grammars as `syntaxes/*.tmLanguage.json` resources. The scope names below were found in the 2.0.3 package. The folder and upstream columns come from the grammar `cgmanifest.json` files in the TextMateSharp repository, read on its `master` branch. The 2.0.3 tag itself was not read.

| Language | Scope | Folder in TextMateSharp | Upstream and commit | Upstream licence |
| --- | --- | --- | --- | --- |
| C# | `source.cs` | `Grammars/csharp` | dotnet/csharp-tmLanguage @ 16612717ccd557383c0c821d7b6ae6662492ffde | MIT |
| TypeScript | `source.ts` | `Grammars/typescriptbasics` | microsoft/TypeScript-TmLanguage @ 634a12f4fa2b2843c83353c0a395852c3a0c04c4 | MIT |
| JSON | `source.json` | `Grammars/json` | microsoft/vscode-JSON.tmLanguage @ 9bd83f1c252b375e957203f21793316203f61f70 | MIT |
| YAML | `source.yaml` | `Grammars/yaml` | textmate/yaml.tmbundle @ e54ceae3b719506dba7e481a77cea4a8b576ae46 | TextMate Bundle License (permissive; README.mdown) |
| Markdown | `text.html.markdown` | `Grammars/markdownbasics` | microsoft/vscode-markdown-tm-grammar @ a612b96d62aa1ce305c4a55dc9d577316fab39da (MIT) and textmate/markdown.tmbundle @ 11cf764606cb2cde54badb5d0e5a0758a8871c4b (TextMate Bundle License) | MIT and TextMate Bundle License |

Compatibility: `AvaloniaEdit.TextMate` 12.0.0 declares `TextMateSharp` 2.0.3 and `TextMateSharp.Grammars` 2.0.3 as dependencies for net8.0 and net10.0. It declares `Avalonia.AvaloniaEdit` and `Avalonia` 12.0.0. The restore resolved the editor packages with the pins above. The editor tests exercise all five grammars on net10.0 with Avalonia.Headless 12.1.4.

## Pins

`Directory.Packages.props` gains a `Code editor and TextMate highlighting (ADE-09, ADR-0007)` group with `Avalonia.AvaloniaEdit` 12.0.0, `AvaloniaEdit.TextMate` 12.0.0, `TextMateSharp` 2.0.3, `TextMateSharp.Grammars` 2.0.3 and `Onigwrap` 1.0.10. The last three are transitive and are pinned because of central transitive pinning. `apps/Autonogrammer.Native/Autonogrammer.Native.csproj` adds `Avalonia.AvaloniaEdit`, `AvaloniaEdit.TextMate` and `TextMateSharp.Grammars` references without versions. The test project needed no change.

## Evidence

- Restore: `dotnet restore apps/Autonogrammer.Native/Autonogrammer.Native.csproj --force-evaluate` succeeded, and the lock file resolves the pinned versions.
- Native build: `dotnet build apps/Autonogrammer.Native/Autonogrammer.Native.csproj --no-restore`, warnings as errors, reported 0 warnings and 0 errors.
- Editor tests: `dotnet test tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj --no-restore -- --filter-namespace "Autonogrammer.Native.Tests.Editor*"` reported 31 total, 31 passed, 0 failed, 0 skipped.
- Full native test project: `dotnet test tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj --no-restore`, the same day, reported 147 total, 147 passed, 0 failed, 0 skipped.
- Coverage of the editor tests, in `tests/Autonogrammer.Native.Tests/Editor`:
  - `EditorTabsTests` (8): two files open as two tabs with the second selected; a tab opened while shown; clicking a tab selects it; clicking the selected tab keeps it checked; closing the selected tab selects its successor; closing the last tab empties the editor; Enter on a focused tab selects it; edits reach the tab's buffer and survive a switch.
  - `FindBarTests` (6): on a 10000-line fixture the count is 2000 case-insensitive and 1000 with match case; Enter and Shift+Enter move and wrap; F3 and Shift+F3 move, and the Next and Previous buttons move; match case visits only lowercase matches; a query with no matches shows "No matches" and disables the buttons; timing.
  - `FindBarKeyboardTests` (3): Tab reaches the find bar in order and then the editor; Escape returns focus to the editor; every editor control has its accessible name.
  - `HighlightingTests` (14): each of the five keyword fixtures shows at least one text run whose foreground differs from the editor's default; highlighting follows a tab switch; the extension map covers the seven workspace extensions; unknown files have no grammar.
- Find timing: `FindOnTenThousandLinesIsInteractive` writes `editor-find-timing.txt` to the test output directory. The figures below measure the time from setting the query on `EditorViewModel` until the match count and the selected match are up to date, including the reveal in the editor.

  | Run | `needle` (2000 matches) | `row` (10000 matches) | `Needle` (2000 matches) |
  | --- | --- | --- | --- |
  | Full native project, warm process, 2026-10-22 | 1.685 ms | 4.004 ms | 1.278 ms |
  | Editor test alone, cold process (verification copy with the same sources) | 401.437 ms | 283.342 ms | 1.808 ms |

  The cold figures include first-use JIT work, so they are not steady-state. The 5-second bound in the test only catches a find that stops being interactive.

## Consequences

- Each tab's buffer is an AvaloniaEdit document rebuilt on every tab switch. Undo history and caret position do not survive a switch; the model's text does.
- The find searches the whole selected document on each query change. Only the 10000-line fixture was measured; larger files are not measured.
- The view does not dispose `EditorViewModel`. Whoever creates the view model owns it and disposes it.
- Opening a file is the caller's job: `EditorViewModel.Open(relativePath, text)` takes the text, and the workspace layer reads it.
- The token colours are DarkPlus colours, which are designed for a dark background. A development probe found the `TextEditor` background still unset after the TextMate installation, so the editor takes its surface from the Fluent theme. Contrast of the token colours on that surface is not measured. WCAG contrast for code tokens is an open item.
- `apps/Autonogrammer.Native/packages.lock.json` and `tests/Autonogrammer.Native.Tests/packages.lock.json` change because of the restore. Both lock files are required for the new packages.
