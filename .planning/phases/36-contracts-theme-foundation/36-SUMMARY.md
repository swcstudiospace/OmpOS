# Phase 36 Summary: Contracts and Theme Foundation

Status: all six success criteria met in code and test. Requirements VX-01, VX-02, VX-04, VX-05, VX-06, VX-07 and VX-10 have their contracts in place. Three items stay open and are listed under Open items. They are external or owner decisions, not code gaps in this phase.

Verification (`36-CONTEXT.md` holds the decisions and the criteria table): `36-01-PLAN.md`.

## Criteria

| # | Criterion | Status | Evidence (file; test) |
|---|-----------|--------|-----------------------|
| 1 | Token dictionary with White and Purple roles in Light and Dark; Greptile families as OFL resources with notice; one spacing and one radius scale; no view hard-codes a colour; 4.5:1 text and 3:1 non-text in both themes | Met (owner look sign-off open) | `apps/OmpOS.Native/Themes/OmpOSTokens.axaml`; `apps/OmpOS.Native/Assets/Fonts/{DMSans,Anybody,SpaceMono}/OFL.txt` (SIL OFL 1.1 verified on disk); `tests/OmpOS.Native.Tests/Theme/FontManifest.txt`. Tests: `ThemeContrastTests.EveryTextPairMeetsTheTextMinimum`, `ThemeContrastTests.EveryNonTextPairMeetsTheNonTextMinimum`, `ThemeContrastTests.EachVariantDefinesExactlyTheClassifiedRoles`, `ColourLiteralTests` (views hold no colour literal), `FontResourceTests.EachFontFileHashesToItsManifestRow`, `FontResourceTests.EachFamilyCarriesItsOflNotice` |
| 2 | Command record and registry with an empty catalogue; no name without a verified reference; empty query, unknown command and disabled reason tested | Met | `src/OmpOS.Presentation/Commands/*`; catalogue `SlashCommandCatalogue.Shipped` (empty). Tests: `SlashCommandRegistryTests.ShippedCatalogueIsEmpty` (empty query, unknown name), `SlashCommandRegistryTests.MatchByPrefixOrdersByNameAndIsCaseSensitive` (empty prefix lists all), `SlashCommandRegistryTests.UnknownNameSuggestsTheCloseRegisteredName`, `SlashCommandRegistryTests.DisabledCommandReportsItsStoredReason`. Reference record (G1, below): `SlashCommandReferenceTests.ShippedCatalogueRecordsTheCursorReferenceAndItsReadDate` |
| 3 | Kanban runtime status enumeration with independent stale flag; mapping recorded in CONTEXT; each value mapped; stale flag changes without changing status | Met | `src/OmpOS.Presentation/Kanban/*` (`RuntimeStatus`, `RuntimeSignalKind`, `RuntimeStatusMap`, `KanbanCardState`). Mapping: `36-CONTEXT.md` ("Kanban runtime status mapping"). Tests: `RuntimeStatusMapTests` (each signal kind to its row, table complete, undefined kind refused), `KanbanCardStateTests.StatusChangesWithoutChangingTheStaleFlag`, `KanbanCardStateTests.AnUnmappedSignalIsRefusedAndTheCardIsUnchanged`, `KanbanCardStateTests.AnUnmappedSignalNamesTheSignalParameter` |
| 4 | Provider record and URL validator: https accepted; http only to localhost; remote plain http refused unless explicitly allowed | Met | `src/OmpOS.Harness.Providers/*` (`ProviderEntry`, `ProviderEndpointValidator`, `ProviderEntryValidator`, `ProviderKind` with separate `CodingPlan`). Tests: `ProviderEndpointValidatorTests.HttpsEndpointIsAccepted`, `LocalhostHttpIsAccepted`, `IPv4LoopbackHttpIsAccepted`, `IPv6LoopbackHttpIsAccepted`, `RemotePlainHttpIsRefusedByDefault`, `RemotePlainHttpIsAcceptedOnlyWithExplicitAllow`; `ProviderEntryValidatorTests` |
| 5 | `SecretString` never renders its value in ToString or serialisation; canary harness fails when a registered secret reaches a stub log, transcript, crash or telemetry sink | Met | `src/OmpOS.Harness.Secrets/*`. Tests: `SecretStringRedactionTests.ToStringReturnsTheRedactionMarkerAndNeverTheValue`, `SecretStringJsonTests.SerialisesAsTheRedactionMarkerNeverTheValue`, `CanaryHarnessTests` (each of `Log`, `Transcript`, `CrashReport`, `Telemetry` detects a registered canary on its channel), `CanaryHarnessTests.CanaryWrittenToEachOfTheFourSinksIsDetectedOnEachChannel` |
| 6 | herdr session client and omp adapter contracts, versioned, with recorded-transcript fakes; fixture replayed twice yields the same typed events; detach leaves the fake session alive | Met (real endpoints open) | `src/OmpOS.Harness.Herdr/IHerdrSessionClient.cs` (`HerdrContract.ContractVersion = 1`); `src/OmpOS.Harness.Agent/IOmpAgentAdapter.cs` (`Version = 1`). Fakes: `tests/OmpOS.Harness.Herdr.Tests/FakeHerdrSessionClient.cs` (records every call in `Transcript`), `tests/OmpOS.Harness.Agent.Tests/FakeOmpAgentAdapter.cs`. Fixtures: `tests/OmpOS.Harness.Rpc.Tests/TestData/frames/` (linked into the Agent tests). Tests: `FakeHerdrSessionClientTests.DetachLeavesTheSessionAliveAndFindable`, `FakeHerdrSessionClientTests.LostSessionResumesFromItsNativeReference`, `HerdrContractTests.ContractVersionIsOne`, `RecordedTranscriptReplayTests.ReplayingTheLiveCaptureTwiceYieldsIdenticalEventSequences`, `RecordedTranscriptReplayTests.CancelTurnLeavesTheSessionRunningAndAcceptsTheNextTurn`, `OmpAgentContractTests.EnvelopeRefusesAnotherContractVersion`, `HerdrHeartbeatMonitorTests.TheStaleWindowIsThreeMissedBeatsOfFifteenSeconds` |

Requirement-level notes for the VX items that the criteria do not restate:
- VX-04 (runtime statuses read-only, drag, stale indicator, live updates): the status and stale model exists (criterion 3). Views and drag are Phase 39.
- VX-05 (settings floors and ceilings, custom provider, coding plan, remote http refusal): the provider record and the remote-http refusal exist (criterion 4). Settings UI and floor and ceiling validation are Phase 40.
- VX-06 (persistence, 15-second heartbeat, stale after three beats, restart resumes from the native reference): `HerdrHeartbeatMonitor` (15 s, 45 s stale) and `HerdrSessionService`/`HerdrSessionStore` tests, plus the `Lost` state and `LostSessionResumesFromItsNativeReference`, exist in the contract layer. The real herdr endpoint is unverified (open).
- VX-07 (versioned adapter emits typed events; cancel keeps session running): `IOmpAgentAdapter` and `OmpEvent` (criterion 6). omp launch-time provider configuration is unverified (open).
- VX-10 (keys never reach sinks): criterion 5 covers the stub sinks. Real sink wiring is later phases.

## Gap closed this phase

| Gap | Change | Test |
|-----|--------|------|
| G1: VX-02 requires the catalogue to record the Cursor reference version and date; `SlashCommandCatalogue` recorded neither. Cursor publishes no single version; the research record carries per-row versions and a read date of 2026-10-22. | New record `SlashCommandReference(string Source, DateOnly ReadOn)` in `src/OmpOS.Presentation/Commands/SlashCommandReference.cs`. New property `SlashCommandCatalogue.Reference` (`docs/plans/vision-research/cursor-slash-commands.md`, 2026-10-22). Catalogue remains empty. | `SlashCommandReferenceTests.ShippedCatalogueRecordsTheCursorReferenceAndItsReadDate` (new) |

No other code changed. No command added. No stub or shim added. The Greptile OFL notices were already on disk, so none was added. Every test named in this summary is pre-existing except `SlashCommandReferenceTests.ShippedCatalogueRecordsTheCursorReferenceAndItsReadDate`. No existing test was changed.

Mutation note: `ShippedCatalogueRecordsTheCursorReferenceAndItsReadDate` was not run against a removed property. Removing `SlashCommandCatalogue.Reference` breaks compilation of the test project, which fails the build step. Changing the source or date fails its equality assertions. Neither mutation was executed.

## Verification

Environment: `DOTNET_ROOT=/root/.dotnet DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1`, `PATH=/root/.dotnet:$PATH`, .NET SDK 10.0.402. Commands run from the repo root. Test runs used `--filter-not-trait "Category=Live"`.

Pre-gap-closure runs (commit-free working tree, before G1):

| Project | Build (exit, warnings, errors) | Test (exit; total, failed, succeeded, skipped) |
|---------|--------------------------------|-----------------------------------------------|
| `tests/OmpOS.Harness.Secrets.Tests` | 0; 0 W; 0 E | 0; 29, 0, 29, 0 |
| `tests/OmpOS.Harness.Providers.Tests` | 0; 0 W; 0 E | 0; 51, 0, 51, 0 |
| `tests/OmpOS.Harness.Herdr.Tests` | 0; 0 W; 0 E | 0; 78, 0, 78, 0 |
| `tests/OmpOS.Harness.Agent.Tests` | 0; 0 W; 0 E | 0; 23, 0, 23, 0 |
| `tests/OmpOS.Presentation.Tests` | 0; 0 W; 0 E | 0; 287, 0, 287, 0 |
| `tests/OmpOS.Native.Tests` | 0; 0 W; 0 E | 0; 334, 0, 334, 0 |

Final runs (after G1, Presentation and Native rebuilt and re-run, since Presentation changed):

| Project | Build (exit, warnings, errors) | Test (exit; total, failed, succeeded, skipped) |
|---------|--------------------------------|-----------------------------------------------|
| `tests/OmpOS.Presentation.Tests` | 0; 0 W; 0 E | 0; 288, 0, 288, 0 |
| `tests/OmpOS.Native.Tests` | 0; 0 W; 0 E | 0; 334, 0, 334, 0 |

The four harness projects (Secrets, Providers, Herdr, Agent) were not changed after their runs, so their pre-gap results stand.

Commands: `dotnet build <project> -nologo -v q`, then `dotnet test <project> --no-build --filter-not-trait "Category=Live"`.

## Files

Created:
- `src/OmpOS.Presentation/Commands/SlashCommandReference.cs`
- `tests/OmpOS.Presentation.Tests/Commands/SlashCommandReferenceTests.cs`
- `.planning/phases/36-contracts-theme-foundation/36-CONTEXT.md`
- `.planning/phases/36-contracts-theme-foundation/36-01-PLAN.md`
- `.planning/phases/36-contracts-theme-foundation/36-SUMMARY.md`

Changed:
- `src/OmpOS.Presentation/Commands/SlashCommandCatalogue.cs` (added `Reference`)

## Open items

1. VX-01: the owner's visual sign-off of the rendered look is pending (validation item, VX-01).
2. VX-06: the real herdr endpoint is unverified.
3. VX-07: omp launch-time provider configuration is unverified.

Not Phase 36 work, recorded for Phase 39: the Kanban stale flag is set by `KanbanCardState.WithStale`, and nothing yet connects it to `HerdrHeartbeatMonitor`. Phase 36 supplies the flag and the monitor only.
