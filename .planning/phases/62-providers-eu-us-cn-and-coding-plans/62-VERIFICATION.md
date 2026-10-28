# Phase 62 Verification: Providers, EU US CN and Coding Plans

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `62-SUMMARY.md`. A row marked "no recorded run" has a command but no result. The tests are hermetic: no network, no vendor, no key; headless Avalonia for the views; the API tests use fake backends, environments and probes; the omp backend tests use a fake omp script and need `/bin/sh`.

Recorded totals (recorded 2026-10-28): the `./bootstrap.sh` gate on commit e24e6851 exited 0 (`docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`): `dotnet test OmpOS.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; the log lists `OmpOS.Harness.Providers.Tests` and `OmpOS.Harness.OmpConfig.Tests` as passed and `repo-checks: ok` with "docs/reference/providers.md is up to date". Separate run: Api.IntegrationTests 1027/1027 on the final tree; earlier per-project runs: Presentation 2303, Native 2012 (2 skipped), Web 262+, Contracts 137, Architecture 46. The commands below were not re-run by the author of this record.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (data) | `python3 scripts/docs/generate-providers-md.py --validate` and `bash scripts/ci/check-providers-doc.sh` | The JSON rows are structurally valid and `docs/reference/providers.md` is up to date with them | Run in the e24e6851 gate through `scripts/ci/repo-checks.sh`: "docs/reference/providers.md is up to date", `repo-checks: ok` |
| 1 (count) | A Python count over `src/OmpOS.Harness.Providers/Data/catalogue-*.json` | 103 rows, ids unique, 50 verified, 49 partial, 4 unverified, 23 coding plans, 46 omp ids, 7 sign-in rows, 150 routes, every source URL https | Counted by the author of this record on 2026-10-28 (the builder report says 151 routes) |
| 1 (tests) | `dotnet test tests/OmpOS.Harness.Providers.Tests -c Release -- --filter-class "*CatalogueRowsTests*" --filter-class "*CatalogueRowValidatorTests*" --filter-class "*CatalogueUrlTemplateTests*" --filter-class "*ProviderIdMapTests*"` | Unique ids, https routes (loopback http only for local runtimes), a source URL and dated status per row, coding plan terms with cloud agents defaulting to pay-as-you-go, retired and excluded vendors absent, every omp id the rows name exists in omp (fixture `omp-provider-ids.txt`), env names never keys | `OmpOS.Harness.Providers.Tests` passed (9 s 696 ms) in the e24e6851 gate log; classes not run alone |
| 2 (writer) | `dotnet test tests/OmpOS.Harness.OmpConfig.Tests -c Release -- --filter-class "*OmpProviderApiNamesTests*" --filter-class "*OmpManagedBlockWriterApiTests*" --filter-class "*OmpManagedProviderDiscoveryTests*" --filter-class "*OmpCatalogueBlockTests*"` | The 13 api names; a keyed block per style with the key as an environment name; Bearer and custom key header; keyless local block with discovery; four-space indent; every written block reads back to an equal provider; a block for every route of every row | `OmpOS.Harness.OmpConfig.Tests` passed (55 s) in the e24e6851 gate log; classes not run alone |
| 2 (omp probe) | `export HOME=$(mktemp -d) PI_CODING_AGENT_DIR=$HOME/.omp/agent`, write a `models.yml` with the C# writer for every route, then `omp models --json > out 2> err` and check `err` is empty (omp prints `Warning: models.yml validation failed` otherwise) | omp itself accepts the writer's blocks | UNVERIFIED for the C# writer: UNVERIFIED: the command shown. A Python replica of the layout was accepted by omp 18.12.6 on 2026-10-27 (`ADR-0019`, text only; re-checked 2026-10-28: no file under `docs/runbooks/evidence/` records it for the C# writer) |
| 3 | `dotnet test tests/OmpOS.Presentation.Tests -c Release -- --filter-class "*ProviderDirectoryTests*" --filter-class "*ProviderListingCardTests*"` and `dotnet test tests/OmpOS.Native.Tests -c Release -- --filter-class "*ModelsListingViewTests*" --filter-class "*ModelsSettingsSourceCatalogueTests*"` and `dotnet test tests/OmpOS.Web.Tests -c Release -- --filter-class "*ApiModelsSettingsSourceTests*" --filter-class "*DemoProvidersRouteTests*"` | Region then kind grouping, filter counts, search, plan cards with rule text and tick, URL variables, the key only in a write-only field and in the key store, the desktop adapter's block write and refusals, the web source over scripted responses | Inside the passing Presentation, Native and Web runs |
| 3 (API) | `dotnet test tests/OmpOS.Api.IntegrationTests -c Release -- --filter-namespace "*Providers*"` | Listing with filters and no key member; credential writes off by default (403 before the body is read); canary key absent from responses, headers and logs; custom endpoint rules (10-row unsafe URL table); ownership; the live test endpoint skipped by default and connecting only to public addresses | Inside the passing Api.IntegrationTests run (1027/1027) |
| 3 (screenshots) | `scripts/web-demo/capture.mjs` capture of the Demo Models page; the Demo is at https://ompos-web.vercel.app | The Models page with the CN coding plans and the EU providers in the web head | `docs/runbooks/evidence/web-app-2026-10-28/M-11-models-cn-coding-plans.png`, `M-12-models-eu.png`; `docs/assets/screens/web-settings-models-light.png`. No desktop screenshot of the listing |

## Evidence files

| File | What it shows |
|------|---------------|
| `src/OmpOS.Harness.Providers/Data/catalogue-eu.json`, `catalogue-us.json`, `catalogue-cn.json` | The 103 rows |
| `docs/reference/providers.md` | The generated reference of every row |
| `docs/decisions/ADR-0019-provider-catalogue.md` | Decisions, the not-offered table and the text record of the omp experiment |
| `docs/runbooks/evidence/web-app-2026-10-28/M-11-models-cn-coding-plans.png`, `M-12-models-eu.png` | The web Models page |

## Known limits of this verification

- No live key or vendor check (OQ-71, OD-07); no model turn.
- The omp acceptance of the C# writer's output is UNVERIFIED (command above); the 2026-10-27 experiment used a Python replica.
- The cell-by-cell audit of the rows against the research was not done.
- Linux only; Windows, macOS and Android were not run.
- Per-project run logs are not committed; the `./bootstrap.sh` log of e24e6851 is.
