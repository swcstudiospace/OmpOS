---
description: "How to choose and configure a model provider in OmpOS: region and kind filters, coding plans and their rules, bring-your-own-key, and where keys are stored."
---

# Choose a model provider

Status: Partial (the catalogue, the writer, the desktop Models page, the web Models page and the API routes are built and unit tested; omp accepts a `models.yml` with every catalogue route in a read-only experiment; no provider was called with a real key and no live model turn has run)

Last verified: 2026-10-28

Applies to: v0.15.0

OmpOS has no model of its own. The engine, omp, talks to the provider you configure; OmpOS helps you pick one, writes omp's configuration and keeps the key out of logs. The full list is generated from the catalogue data and lives in [Model providers and coding plans](../reference/providers.md).

## What the catalogue holds

The Settings > Models page lists **103 rows**: model APIs, aggregators and gateways, coding plans and local runtimes.

| Region | Model APIs | Aggregators and gateways | Coding plans |
|---|---|---|---|
| Europe | 17 | 1 | 2 |
| United States and Canada | 18 | 1 | 6 |
| China | 21 | 5 | 15 |
| Global | 6 | 2 | 0 |
| Local runtimes (Ollama and similar) | 9 | | |

Each row names the vendor page it was read on, the base URL, the protocol (`openai-completions`, `anthropic-messages` and others that omp supports), how the key is sent, the **name** of the environment variable that holds the key, and a verification status. The generated page reports **50 rows Verified** (a vendor page showing base URL and auth scheme was opened), **49 Partial** (some fact comes from a search excerpt, a third-party page or an inference) and **4 Unverified**. Treat Partial and Unverified rows as "check the vendor page before you rely on it". The catalogue is data, not code: a provider is a JSON row ([ADR-0019](../decisions/ADR-0019-provider-catalogue.md)).

## Pick by region and kind

The Models page groups rows by region and kind and has filters for both. Use the **region** filter when residency matters. A row's residency note is what the vendor states (for example "Data processed in Germany", or "customer-installed, residency is your deployment"); OmpOS does not verify it. Use the **kind** filter to separate:

- **Model APIs**: pay-as-you-go keys from the vendor (Mistral, Scaleway, OVHcloud, IONOS, STACKIT, OpenAI, Anthropic, DeepSeek, and more: see the reference page).
- **Aggregators and gateways**: one key for several vendors' models.
- **Coding plans**: a subscription that is bring-your-own-key for coding tools.
- **Local runtimes**: models on your machine; these need no key.

## Coding plans and their rules

A coding plan is a row of its own with the plan's own host. Do not mix the plan host with the vendor's pay-as-you-go host: on several vendors that silently bills pay-as-you-go instead of the plan. Plans carry usage rules (which tools may use the key, limits, what counts as abuse). The card shows the vendor's rule text beside the key field and the add action asks you to acknowledge it (`planRuleAccepted`). Read the vendor's current terms before you subscribe; OmpOS reproduces what it read on the date shown and cannot promise a plan accepts third-party tools.

## Bring your own key

1. Open Settings (Ctrl+,) > Models, choose a row and enter the key. The key is never shown again.
2. OmpOS saves it in one of two places, depending on the row:
   - A provider omp supports natively: the key is imported with `omp auth import -` over standard input and kept by omp in its own credential database (`agent.db`, owner-only file, unencrypted).
   - A custom or catalogue-only provider: OmpOS writes its own marked block into omp's `models.yml` (comments kept, a timestamped backup, atomic write, validated by omp) and keeps the key in `secrets/secrets.json` under the state directory, owner-only. It reaches omp as an environment variable at launch.
3. Pick the default model on the same page or with the composer's model chip.
4. **Test connection** checks the provider's endpoint only when you press it. The Network page's diagnostics send one unauthenticated GET per known endpoint, again only on request.

OmpOS never logs a key, never puts one on a command line and never sends one anywhere itself.

### Where keys live

| Head | Where the key goes |
|---|---|
| Desktop | omp's `agent.db`, or `secrets/secrets.json` under `OMPOS_STATE_DIR` (owner-only). See [Run the desktop app](../runbooks/running-the-desktop-app.md). |
| Web | A write-only field on the Models page. In the Demo it is disabled and nothing is stored or sent. Against a real API the key is sent to the API only when the operator enabled `Providers:AllowCredentialWrite`, and only for providers omp supports natively; the page keeps no copy. See the [Web key threat model](../runbooks/web-key-threat-model.md). |
| API (operator) | Writes are off by default. `Providers:AllowCredentialWrite`, `Providers:AllowConfigWrite` and `Providers:AllowLiveTest` each switch one capability on for a single-owner host. A provider that omp does not manage natively takes its key from the environment variable `OMPOS_PROVIDER_<ID>_KEY` that the operator sets. |

## What is verified and what is not

- **Verified (recorded 2026-10-27, omp 18.12.6, read-only):** omp accepted a `models.yml` with one block per route of all 103 rows (151 blocks) and listed every route's provider id (`omp models --json` exit 0, no validation warning), so the writer's output is valid for omp. No model was called.
- **Verified in tests:** the catalogue loader and validator (`CatalogueRowsTests`, `CatalogueRowValidatorTests`), the desktop Models source and the API routes, the web Models page and the Demo's provider routes (`DemoProvidersRouteTests`: Europe, US and China coverage, filters, custom endpoint rules, key never echoed).
- **Not verified:** that a provider answers. No catalogue row was called with a real credential (open question OQ-71); 49 rows are only Partial-verified and 4 Unverified. The live connection test is off by default in the API (`Providers:AllowLiveTest`).

## Limits

- Rows with account placeholders in their URL (Azure, Bedrock, Vertex and similar) need values you supply.
- Nothing here proves a provider answers: no catalogue row was called with a real credential.
- Cursor's own models, billing and teams are not part of OmpOS (no accounts: see the [parity audit](../quality/cursor-parity-audit-2026-10-28.md)).

See also: [Settings guide](settings-guide.md), [Model providers and coding plans](../reference/providers.md).
