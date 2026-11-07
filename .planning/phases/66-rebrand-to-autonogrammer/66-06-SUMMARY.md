# 66-06 Summary: models.yml dual marker families (AG-66-9, D-16, D-17)

## Result
models.yml reading, updating and removal now recognize both the new `# autonogrammer:begin/end <id>` family and the legacy family. A legacy block is rewritten under the new markers in the same write. Removal deletes both families. For a same-id twin, new wins and the legacy span is removed in the same splice. Unmanaged bytes are untouched (existing splice and Join path unchanged).

## Changes
- `LegacyBrandNames.cs` (new, OmpConfig product): fragment-built legacy begin/end prefixes, the only legacy spelling in src.
- `OmpModelsLayout.cs`: family-aware `TryMarker`, family-tagged scan markers, family-aware pairing (same-family duplicates and cross-family end markers stay Unsupported), new `LegacyBlocks` for superseded twins, `InsideBlock` covers both lists so legacy bodies never count as foreign keys.
- `OmpModelsEditor.cs`: `Replace` helper (always renders new markers, drops twins in one splice, keeps a missing final newline), dual-family `Remove`, `AllSpans` helper. Unchanged is returned only when no twin exists.
- `OmpManagedBlockSpan.cs`: doc note only.
- Tests: `OmpModelsDualMarkerTests.cs` (20 cases: layout matrix, upsert, twin both orders, removal of each shape, CRLF+BOM+no-final-newline full-text equality, idempotence, ReadManaged, file level with backup and validation call) and test-project `LegacyBrandNames.cs`.

## Messages review (plan resolution B)
The Unsupported templates interpolate ids only and name "autonogrammer" generically; kept unchanged for both families.

## Verification (exercised)
- RED: dual-marker suite run before the implementation: 18 of 20 failed (legacy markers invisible). The two that passed were negative-direction cases. A first RED attempt was initially a compile error in the test, fixed; two layout span indices in my fixtures were also corrected before GREEN.
- GREEN: `ag-dotnet test --project tests/Autonogrammer.Harness.OmpConfig.Tests -c Release -- --filter-not-trait "Category=Live"`: total 718, failed 0 (includes OmpModelsFileTests, OmpModelsEditorTests, OmpProviderIdTests). The build used the repo analyzers as errors (an MA0051 method-length error was fixed along the way).
- Legacy-token grep over the six files: 0 hits. No lock or csproj changes.
- Unrun: the separate `build -warnaserror` command (the test build compiled the product project under the same analyzers); no live omp turn.

## Deviations
Test-first held per plan, with one combined RED run for both tasks. Waited on another phase's temporary Secrets build break (not touched).
