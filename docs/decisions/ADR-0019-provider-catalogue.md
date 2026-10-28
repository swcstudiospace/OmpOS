# ADR-0019: Provider catalogue: every EU, US and CN provider and coding plan as data

- Status: Accepted 2026-10-28. The catalogue (103 rows), the writer for all 13 omp api values, the Models page model and its desktop and web adapters are implemented; the tests of `Autonogrammer.Harness.Providers.Tests`, `Autonogrammer.Presentation.Tests`, `Autonogrammer.Native.Tests` and `Autonogrammer.UI.Tests` pass in the integrated tree. No provider credential was used, so no row is live-validated (OQ-71).
- Date: 2026-10-27
- Owner: autonogrammer-maintainers
- Deciders: autonogrammer-maintainer
- Requirement: PX-19 (every EU, US and CN model provider and coding plan in Settings). Research: `docs/plans/v0.15.0-research/05-providers-eu.md`, `06-providers-cn.md`, `07-providers-us-plans.md`. Open questions: OQ-71, OQ-92, OQ-98.

## Context

`ProviderCatalogue` held 15 typed entries and two coding plans, written as C# with one citation each. The three research files of v0.15.0 list about 120 providers, aggregators, coding plans and local runtimes with country, residency statements, several protocols per vendor, URL templates (`{product_id}`, `{zone}`, `{workspace_id}`), plan rules that differ per vendor, and a status per row. The writer for omp's `models.yml` knew three of omp's thirteen `api` values and one block shape (no discovery, no headers).

## Candidate chosen

Data, not code, and no new dependency.

1. **Rows are JSON.** `src/Autonogrammer.Harness.Providers/Data/catalogue-eu.json`, `-us.json`, `-cn.json` are embedded resources, read once by `CatalogueRows` through a source-generated `System.Text.Json` context (camelCase, string enums, unmapped members rejected, constructor members required). A row (`CatalogueRow`) holds region (`eu`, `us`, `cn`, `global`, `local`), ISO country, the vendor's residency statement, kind (`api`, `aggregator`, `codingPlan`, `local`), the omp provider id or null, the key's environment variable NAME, one or more routes, URL variables, docs, key page and source URLs, status (`verified`, `partial`, `unverified`) with its date, a note, an omp sign-in command and, for plans, the terms. JSON was chosen over C# because the rows are data, the diff of a vendor change is one line, and `scripts/docs/generate-providers-md.py` reads the same files without a compiler.
2. **A route is one way to reach a vendor**: an omp `api` value (the ten of omp's documentation are the only ones a route may use), a base URL or template, how the key is sent (`bearer`, `xApiKey`, `customHeader` with its name, `none`), whether the vendor documents a models list (`discovery`) and the documented models. The first route is the default. A route with neither discovery nor a documented model makes the form ask the user for a model id; Autonogrammer never guesses one.
3. **The typed `ProviderCatalogue` stays** for the probes, the registry and the Network diagnostics. `ProviderIdMap` maps each of its 15 ids to a row, a route and the omp id (`ollama-local` to row `ollama`, route `openai`, omp id `ollama`; `deepseek-anthropic` to row `deepseek`, route `anthropic`; `minimax-m-plan-anthropic` to `minimax-m-plan`/`anthropic`; and so on). A test proves each mapped route reaches the legacy entry's endpoint, with one documented exception: llama.cpp's port (research 07: omp 8080, upstream README 9931).
4. **The writer maps all thirteen `api` values** of omp 18.12.6's models schema (`OmpProviderApi`), writes `discovery.type: openai-models-list` for OpenAI-style routes whose list the vendor documents, `authHeader: true` for Bearer anthropic routes, one `headers` entry whose value is the key's environment variable NAME for a custom key header, and `auth: none` for local runtimes. `OmpCatalogueBlock` turns a route into an `OmpManagedProvider`, resolves URL variables (a value is one token of letters, digits, dots and hyphens and not a placeholder, so it cannot add a path, host or credential), and refuses http for a non-local host, placeholder hosts, unresolved variables and a literal key where an environment name belongs.
5. **The Models page model** (`ProviderDirectoryModel`, `ProviderListingCardModel`) lives in Presentation, which references no harness assembly: the head maps catalogue rows to `ProviderListing` records and fills `ModelsSnapshot.Listings`. `IModelsSettingsSource.AddCatalogueProviderAsync` takes the request and the key as separate arguments; its default implementation refuses, so an older head says so on the card.

### Mapping and block rules

- A row with an omp provider id, no URL variable and the default route chosen sends the key to omp (`SaveApiKeyAsync(ompProviderId, key)`); omp keeps it. Every other row gets an Autonogrammer managed block with the provider id `row` (default route) or `row-route`; the key is kept by Autonogrammer and reaches omp as an environment variable, as for custom providers.
- A block id that equals an omp provider id overrides that provider. It is used only where the row IS that provider (native rows with account placeholders: Azure, Bedrock, Vertex, Cloudflare AI Gateway, Snowflake) and for three named cases (`minimax-cn`, `xiaomi-token-plan-sgp`, `xiaomi-token-plan-ams`). Cloudflare's OpenAI-compatible route has its own id (`cloudflare-workers-ai-compat`) because a block under omp's `cloudflare-workers-ai` id lists as a judge model (found by running omp, see Verification route).

### Coding-plan terms policy

A coding plan is a bring-your-own-key preset and a row of its own, with the plan-specific host (mixing the plan host with the pay-as-you-go host silently bills pay-as-you-go on Alibaba, Volcengine, Z.ai and Baidu). The card shows the vendor's rule text beside the key field, the tiers (a price appears only where the research verified it; otherwise `price unverified`), an "interactive use only" tag when the vendor limits the plan to interactive coding tools, a "separate key" tag, and the sentence "Cloud agents use pay-as-you-go keys": Autonogrammer cloud agents and headless automation use pay-as-you-go keys, never plan keys (research 06 section 2). `CloudDefaultPayAsYouGo` is true on every plan and a test keeps it so. Sign-in plans (Claude Pro and Max, ChatGPT Codex, Copilot, Antigravity, Gemini Code Assist, SuperGrok) are rows with no key field: the credential is an omp sign-in that Autonogrammer never stores.

### Not offered, and why

| Vendor | Reason |
|---|---|
| GitHub Models | Retired 2026-07-30 (research 07) |
| Lambda Inference | Retired (research 07) |
| Replicate | Compatibility page unconfirmed, 404 on 2026-10-27 (research 07) |
| 01.AI Yi | New sign-ups and API calls ended (research 06) |
| Qwen Portal OAuth | Free tier discontinued 2026-04-15 (research 06) |
| DeepL, Black Forest Labs | Not chat or coding APIs (research 05) |
| Teuken, Poro, Kyutai, Fraunhofer and Telekom | No public inference API (research 05) |
| IBM watsonx.ai | IAM token refresh does not fit a plain API key (research 07) |
| Cursor | Autonogrammer does not resell Cursor inference (research 07) |
| Reseller sites for DeepSeek and Atlas Cloud | Not the model vendors (research 06) |
| Cerebras Code and Ollama plans | Same key and host as the API row: their terms are in that row's note |
| Hyperbolic's old host `api.hyperbolic.xyz` | Decommissioned; the row uses `https://api.hyperbolic.ai/inference/v1` |

Mistral is three rows because it has three key kinds that must not be mixed (research 05 finding 5): the pay-as-you-go console key (`mistral`), the legacy personal Codestral key for `codestral.mistral.ai` (`mistral-codestral`) and the Vibe plan key (`mistral-vibe-plan`).

## Licence

Autonogrammer code and the catalogue data: AGPL-3.0-or-later (https://www.gnu.org/licenses/agpl-3.0.html). The catalogue states facts (hosts, header names, prices, vendor rule text kept as short quotes or paraphrases) with the page each was read on; it copies no vendor text beyond those quotes and adds no third-party component, so no NuGet package or Python package is added. The generator script uses the Python standard library only.

## Verification route

Run by the orchestrator after the merge; the .NET tests were written and not run when this record was written.

- `dotnet test tests/Autonogrammer.Harness.Providers.Tests/Autonogrammer.Harness.Providers.Tests.csproj`: `CatalogueRowsTests` (invariants: unique ids, https routes, loopback http only for local runtimes, source URL and status on every row, rule text on every plan, retired vendors absent, omp ids exist in `tests/Autonogrammer.Harness.Providers.Tests/Fixtures/omp-provider-ids.txt`), `CatalogueRowValidatorTests`, `CatalogueUrlTemplateTests`, `ProviderIdMapTests`.
- `dotnet test tests/Autonogrammer.Harness.OmpConfig.Tests/Autonogrammer.Harness.OmpConfig.Tests.csproj`: `OmpProviderApiNamesTests` (13 spellings), `OmpManagedBlockWriterApiTests` (golden blocks per style, read back equal), `OmpCatalogueBlockTests` (every route of every row yields a block omp's reader accepts).
- `dotnet test tests/Autonogrammer.Presentation.Tests/Autonogrammer.Presentation.Tests.csproj`: `ProviderDirectoryTests`, `ProviderListingCardTests` (filters, search, setup flow, no key on any model).
- `dotnet test tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj`: `ModelsListingViewTests`.
- `scripts/ci/check-providers-doc.sh`: `docs/reference/providers.md` is generated from the data and must not be stale.
- omp experiment (2026-10-27, omp 18.12.6, sandbox `HOME=/tmp/providers-home`, fake keys, no model turn): `models.yml` blocks in the writer's exact layout for all 151 routes of the 103 rows, then `omp models --json`. omp printed no validation message and listed the models of every route except one synthetic discovery-only block pointing at a dead loopback port. A second file with one block per `api` value (13) was also accepted without a message; `typesafe`, `openrouter-decisions`, `openai-decisions` and `workers-ai-systemone` are runner kinds and do not list as chat models. A deliberately wrong `api` value printed `models.yml validation failed`, so the check can fail. Details are in `/tmp/reports/Providers.md`.

## Open risks

- Many rows are Partial (49 of 103) or Unverified (4): the research could not open every vendor page. The card says so; nothing here proves a vendor still serves the base URL. T-Systems announced an API transition (research 05 finding 7).
- Model lists: discovery is declared only where the research documents the list route. Many rows therefore ask for a model id; a vendor's own list would be better and needs a live check with a real key.
- A route that is Anthropic-style cannot use omp's `openai-models-list` discovery, so it also asks for model ids.
- Residency text is what the vendor states, shown as such; Autonogrammer does not verify it and Nebius shared endpoints, Together serverless and Cortecs have conditions the one-line note cannot carry.
- Coding-plan rules are a short quote or paraphrase of pages that change; the rule text and tiers age quickly. Z.ai, Alibaba, Baidu, iFlytek and Volcengine list closed tool sets that do not name Autonogrammer.
- Env var names for rows the research gives none for are derived (`<ID>_API_KEY`) and flagged in the data where the vendor documents a different name; omp-native rows use omp's documented names.
- `IModelsSettingsSource` gains a default interface member; the head adapter must implement it before the listing can add anything (next wave).
- `Autonogrammer.Harness.OmpConfig` now references `Autonogrammer.Harness.Providers` (both layer 2): the architecture allow-list needs the entry.
