# Phase 62 Summary: Providers, EU US CN and Coding Plans

Status (recorded 2026-10-28): criteria 1 and 3 are met; criterion 2 is Partial. The catalogue holds 103 rows, the writer produces blocks for all 13 omp api values, and the Models page lists, filters and sets up every row on the desktop and web heads without ever rendering a key. The "validated by omp itself" half of criterion 2 rests on a sandbox experiment with a Python replica of the writer layout, recorded only as text in ADR-0019 and the builder report; no evidence file is committed and the C# writer's output was not probed by omp in a recorded run: UNVERIFIED. No key was checked against a vendor (OQ-71, OD-07). PX-19 is Partial. Nothing ran on Windows, macOS or Android.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded test results (recorded 2026-10-28, orchestrator): Api.IntegrationTests 1027/1027 passed on the final tree; Presentation 2303, Native 2012 (2 skipped), Web 262+, Contracts 137, Architecture 46 passed on the final tree before the last wiring. `OmpOS.Harness.Providers.Tests` and `OmpOS.Harness.OmpConfig.Tests` are not in the orchestrator's per-project list; their results are in the full `./bootstrap.sh` gate on the final commit, which was running when this record was written and which the orchestrator records in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`. The web Models screenshots were captured from the live Demo build (`docs/runbooks/evidence/web-app-2026-10-28/M-11-models-cn-coding-plans.png`, `M-12-models-eu.png`; `docs/assets/screens/web-settings-models-light.png`).

## The catalogue count, checked against the tree

The builder report says 103 rows. Counted from the three JSON files in `src/OmpOS.Harness.Providers/Data/` on 2026-10-28:

| Measure | Tree | Report |
|---------|------|--------|
| Rows | 103 (ids unique) | 103 |
| Verification status | 50 verified, 49 partial, 4 unverified (`stepfun-cn`, `baichuan`, `tencent-coding-plan`, `telechat`) | 50, 49, 4 |
| Region | eu 20, us 25, cn 41, global 8, local 9 | not given |
| Kind | api 62, codingPlan 23, aggregator 9, local 9 | 23 coding plans, 9 local runtimes |
| Rows with an omp provider id | 46 | 46 |
| Sign-in-only rows (login command) | 7 | 7 |
| Routes | 150 (openai-completions 101, anthropic-messages 34, openai-responses 11, google-generative-ai, google-vertex, azure-openai-responses, bedrock-converse-stream 1 each) | 151 (102 openai-completions) |

The one-route difference is not explained by any file read (UNVERIFIED which route was removed or merged after the report; `ADR-0019` repeats the 151). The 103 rows are confirmed. All 103 rows have an https source URL; the only non-https routes are the nine local runtimes on loopback. None of GitHub Models, Lambda or Replicate appears in the data files.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Catalogue holds at least the EU, US, CN, aggregator and local rows with base URL, auth, api style, region, residency, omp id or `models.yml` template, docs and key links and plan terms; retired or unconfirmed vendors not offered; catalogue tests (unique ids, https, no placeholders, source URL per row) | Met | 103 rows counted above, including 23 coding plans with terms (`ruleText`, `ruleSourceUrl`, `interactiveOnly`, `cloudDefaultPayAsYouGo`, `separateKey`, `planUrl`, tiers). Tests present: `CatalogueRowsTests` (`IdsAreUniqueAndFindable`, `RemoteRoutesAreHttpsAndOnlyLocalRuntimesUseLoopbackHttp`, `EveryRowCarriesAnHttpsSourceAndADatedStatus`, `EveryCodingPlanCarriesTermsAndDefaultsCloudAgentsToPayAsYouGo`, `UnverifiedRowsAskTheUserToVerifyBeforeUse`, `RetiredAndExcludedVendorsAreNotOffered`, `EveryOmpProviderIdNamedByARowExistsInOmp`, `EveryRowNamesAnEnvironmentVariableNeverAKey`), `CatalogueRowValidatorTests`, `CatalogueUrlTemplateTests`, `ProviderIdMapTests`. Caveats: 49 rows are Partial and 4 Unverified (the user is asked to verify before use); the builders transcribed the rows from the research and a cell-by-cell audit was not done; `docs/reference/providers.md` is generated and `scripts/ci/check-providers-doc.sh` is referenced by no gate |
| 2 | Writer produces valid `models.yml` blocks for all ten omp api styles, validated by omp itself; writer tests and an `omp models` probe in a sandbox | Partial | Writer: `OmpProviderApiNamesTests` (13 names), `OmpManagedBlockWriterApiTests` (`EveryNonAnthropicStyleWritesAKeyedBlockWithTheKeyAsAnEnvName`, golden blocks, Bearer anthropic, keyless local, custom key header, four-space indent, `EveryWrittenBlockIsReadBackToAnEqualProvider`), `OmpManagedProviderDiscoveryTests`, `OmpCatalogueBlockTests` (every route of every row). omp probe: ADR-0019 records an experiment of 2026-10-27 with omp 18.12.6 in a sandbox `HOME` with fake keys and no model turn: a `models.yml` with one block per route of the catalogue, then `omp models --json` printed no validation message and listed every route's models except a deliberate dead-port discovery block; one block per api value (13) was accepted; a wrong `api` value printed `models.yml validation failed`. That experiment used a Python replica of the writer layout (`/tmp/providers-experiment.py`), not the C# writer, and the only trace is text in ADR-0019 and `/tmp/reports/Providers.md`: nothing is committed under `docs/runbooks/evidence/`. UNVERIFIED: run the C# writer into a sandbox `models.yml` (`HOME=<tmp> PI_CODING_AGENT_DIR=<tmp>/.omp/agent`) and `omp models --json`, with stderr empty. The criterion says ten api styles; the writer carries 13 and the catalogue routes use 7 of them |
| 3 | Models page filters by region and kind, shows coding-plan cards with rule text, never renders a key; view model tests and screenshots | Met | `ProviderDirectoryTests` (region then kind grouping, counts, `SelectingAKindKeepsOnlyItsListingsAndCombinesWithTheRegion`, search), `ProviderListingCardTests` (plan terms, rule tick, URL variables, no key after save or failure), `ModelsListingViewTests` (desktop, inside the Native run), `ApiModelsSettingsSourceTests` and `DemoProvidersRouteTests` (web), `ProvidersCredentialEndpointTests` (canary key absent from responses, headers and logs), `ProvidersListEndpointTests` (no key member in the list). Screenshots of the web head: `M-11-models-cn-coding-plans.png`, `M-12-models-eu.png`. No desktop screenshot of the listing exists (the `settings-4-*` images of `ui-parity-2026-10-26` predate it) |

## What the phase delivered

- The catalogue, its validator and the generated reference (`62-01-PLAN.md`, units 1, 2 and 9).
- The writer for all 13 omp api values, key header and Bearer variants, and a discovery block (unit 3).
- The Models page directory on both heads: region and kind filters with counts, search, listing cards with plan terms and the rule tick, URL variables for account-specific hosts, a write-only key (units 4, 5 and 8).
- The desktop adapter and the API endpoints with their operator switches (all writes off by default; custom endpoints refused for unsafe addresses; the live test connects only to public addresses and reads no body) (units 6 and 7).

## Deviations

- The catalogue is JSON, not C#; the legacy `ProviderCatalogue` stays and `ProviderIdMap` maps its 15 ids.
- ADR-0019 is still "Proposed" in the file and in the `docs/decisions/README.md` row (written before the integration); it says 151 routes where the tree has 150.
- The hosted API does not store provider keys: an omp-native row without URL variables is keyed through `omp auth import`; every other row is operator-managed (409 `credential_operator_managed`), a decision beyond the criteria (OQ-104).
- `ContractVersion` is now 1.5.0; the Providers shapes are additive.
- The cell audit of the JSON against the research was not done; `scripts/docs/generate-providers-md.py --validate` checks structure only.
- Cloudflare Workers AI has the row id `cloudflare-workers-ai-compat`, and Meta `meta-llama-api`; `kimi-code` carries a derived environment name `KIMI_CODE_API_KEY` that the vendor does not document.

## Partial

- Criterion 2: the omp validation of the C# writer is not a committed record.
- Rows: 49 Partial and 4 Unverified; 83 of the routes (report figure) have no model list and ask the user for a model id.
- Custom endpoints on the hosted API: the owner ledger is per process; a restart forgets it, although a written block stays in `models.yml`.
- Web: the Razor card and the Demo directory exist; the web head needs the API for real keys and a "key field disabled by the operator" reason (`ApiModelsSettingsSource.CredentialWritesDisabledReason`).

## Blocked (OQ/OD)

- Live key validation per vendor: OQ-71 and OD-07 (no owner credentials; no live provider call). The 49 Partial and 4 Unverified rows stay unchecked against a live vendor.
- Cloud agents on coding-plan keys: "pay-as-you-go only" is a default (OQ-98); Kimi Code and StepFun plan text does not forbid plan keys in cloud agents.
- Hosted key vault: OD-03 (no sign-in authority) and OQ-104.

## Known limits

- UNVERIFIED: `JsonSourceGenerationOptions.RespectRequiredConstructorParameters` and `RespectNullableAnnotations` exist on the .NET 10 SDK used; [INFERENCE] the recorded builds of the final tree compile the catalogue loader, which settles it for that tree.
- UNVERIFIED: `new Uri("http://2130706433")` and octal and hex hosts end in a refusal by design; the exact `uri.Host` was not observed.
- A model list is declared only where the research documents it; no per-vendor refresh exists.
- Windows, macOS and Android were not run; no live model turn.
