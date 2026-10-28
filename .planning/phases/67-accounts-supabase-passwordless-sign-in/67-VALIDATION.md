# Phase 67: Accounts: Supabase passwordless sign-in - Validation

Source: Validation Architecture table of 67-RESEARCH.md plus the brief Success criteria. Each row names the AG id, the test class and file, and the exact command the main agent runs once after all units land. All commands use the serialising wrapper /tmp/ag-bin/ag-dotnet with the MTP filter syntax from report 09 section 6. Live rows start with Assert.Skip when their env gate is missing and never send an email.

## Offline command map

| Requirement | Test project and behaviour | Command |
| --- | --- | --- |
| AG-67-1, 3, 5 | Accounts.Tests GoTrueClientTests plus SignInCoordinatorTests: FakeGoTrueHandler six calls, stripped 6 to 10 digit codes, first-time hint, link-then-new-code path | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-2 | Accounts.Desktop.Tests LoopbackListenerTests: real-TCP wrong Host wrong nonce oversize line second callback timeout error query, S256 PKCE in Accounts.Tests | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-4, 24 | UI.Tests WebAccountStateTests with FakeBroadcastChannel plus origin-mismatch refusal, storage scan green; Web.Tests RenderCspScriptTests valid missing http wildcard | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-6, 7, 8, 9 | Accounts.Tests AccountSessionManagerTests: 50-caller single flight, rotation write order with failing store, 10 s reuse interval on ManualTimeProvider, offline start, sign-out scopes, 60 s cooldown, 5-attempt rule, LogHygieneTests no secret email code in captures; Native.Tests AccountStateTests startup | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-10, 11, 12 | Harness.Secrets.Os.Tests OsSecretStoreContractTests against InMemoryLibsecretFake plus LinuxLibsecretTests; Accounts.Desktop.Tests SecretStoreSelectorTests degrade paths and first-launch zero keyring calls | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-13, 15, 16, 17 | Api.IntegrationTests AuthBoundaryTests ES256 accept plus six rejection rows plus rotated kid, RateLimitPartitionTests per-sub isolation, ApiAuthBoundaryTests empty-owners deny, GuardTests OpenAPI equality | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live" |
| AG-67-14, 18, 19, 20, 21, 22 | Api.IntegrationTests AuthConfigTests validator rows plus anonymous access, AccountEnrollmentTests idempotence plus 10-minute cache, AccountRoutesTests two-owner 404 scoping, BoardCutoverTests board gone plus parity; Web.Tests ClientRouteSurface moved | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Web.Tests -c Release --filter-not-trait "Category=Live" |
| AG-67-23 | Presentation.Tests AccountViewModelTests five states on FakeAccountHost; Native.Tests AccountAccessibilityTests names focus announce throttling reduced motion contrast; UI.Tests AccessibilityTests bUnit plus axe | /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release --filter-not-trait "Category=Live"; /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.UI.Tests -c Release --filter-not-trait "Category=Live" |

## Live rows

| Requirement | Live test and env gate | Command |
| --- | --- | --- |
| AG-67-10 | LiveKeyringTests stores reads lists removes a throwaway item or skips with a stated reason | AUTONOGRAMMER_LIVE_KEYRING=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --no-build --filter-trait "Category=Live" |
| AG-67-1 to 9 live check | Any Supabase Live row, gated by AUTONOGRAMMER_LIVE_SUPABASE=1, never sends an email | AUTONOGRAMMER_LIVE_SUPABASE=1 /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --no-build --filter-trait "Category=Live" |

## Gate commands for the main agent after all units land

- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Tests -c Release --filter-not-trait "Category=Live"
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Secrets.Os.Tests -c Release --filter-not-trait "Category=Live" plus the Live keyring command above
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Accounts.Desktop.Tests -c Release --filter-not-trait "Category=Live"
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release --filter-not-trait "Category=Live", includes GuardTests OpenAPI equality
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Presentation.Tests -c Release plus Native plus UI plus Web equivalents
- /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release
- /tmp/ag-bin/ag-dotnet build Autonogrammer.Linux.slnf -c Release -warnaserror; bash scripts/ci/check-licences.sh; bash scripts/ci/check-changelog.sh for the phase 67 entry
- CI=true /tmp/ag-bin/ag-dotnet restore Autonogrammer.Linux.slnf --locked-mode

## Open points

- Phase 66.1 scaffold had not landed on disk at planning time: src/Autonogrammer.Accounts, src/Autonogrammer.Harness.Secrets.Os, src/Autonogrammer.Accounts.Desktop and their test projects plus Contracts/Accounts are absent, and every plan gates on the scaffold first with STOP-and-report. If the scaffold layout differs, the NEW-file lists in each plan follow the scaffold names.
- Phases 69 and 70 SurfaceHost slot HeaderEndSlot and SurfaceChromeModel do not exist yet: plan 67-06 builds the account views unplaced and stops the menu placement with a report if the slot is absent.
- No csproj needed a ProjectReference the manifest does not list: Accounts.Desktop references Accounts Harness.Secrets.Os Harness.Secrets, Accounts references Contracts Harness.Secrets, and Web references Accounts, all covered by manifest rows 4 5 19 and section 3.
