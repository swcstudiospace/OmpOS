# Plan 71-01 SUMMARY: Artifact contracts and pure versioned core

Status: library and contract tests green. The official artifacts test command still fails to compile while `src/Autonogrammer.Cowork.Abstractions/Tools/ICoworkToolHost.cs` has CA1003 and MA0046 (Exec70Plan01). Those files were not edited.

## Results

| Command | Result |
| --- | --- |
| `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class "*ArtifactBlockContractTests*"` | Passed: total 3, failed 0 |
| `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` | Not green on a clean rebuild: Abstractions Tools analyzer errors owned by 70-01. The same filter passed total 12, failed 0 when Abstractions was already built (`-p:BuildProjectReferences=false`). |
| `ContractVersion.cs` | Not edited. |

## What landed

- Additive `ArtifactKind`, `ArtifactAction`, and `ArtifactBlock` (kind `artifact`). Old readers still degrade an unmapped kind to `UnknownBlock`.
- Pure `ArtifactService`: version-7 ids, write-once SHA-256 versions, stale-base refusal with the latest number, same-hash idempotent return, integrity refusal with no bytes, copy `DerivedFrom`, user edits as `Author.User`.
- `ArtifactOptions` / `ArtifactOptionsValidator` name `Cowork:Artifacts:*` and never the value. `AddArtifacts` binds that section by named keys (trim-safe; not `BindConfiguration`, which is `RequiresUnreferencedCode`).
- Deterministic zip export and capped DiffPlex diff.

## Deviations

1. `IValidateOptions` and `AddArtifacts` need `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options.ConfigurationExtensions`. The plan forbade a csproj and lock edit; without the references the library does not compile. Lock files updated: this project, its tests, and `Autonogrammer.Cowork.Artifacts.Stores` (transitive).
2. `ArtifactFailure` and `ArtifactResults` are separate files so MA0048 holds. The plan named `ArtifactErrors.cs`.
3. Content is `ReadOnlyMemory<byte>` so CA1819 does not fire.
4. Official artifacts test command is blocked on 70-01 analyzer errors in Cowork.Abstractions Tools.

## Gaps

- `AddArtifacts` does not call `BindConfiguration`. It copies the same section keys without reflection.
- No live store. File and Postgres stores are 71-03.
