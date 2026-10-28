# Inference providers: EU and CN catalog (API keys and coding plans)

Read date for all primary sources: 2026-10-22. Legend: **V** = fact read on the primary page (opened and verified). **U** = not confirmed from a primary page (a third-party source, an unreadable page, or not checked). Status: **VERIFIED** = core facts (base URL, auth, coding plan price) read on primary pages; **PARTIAL** = some key fact is U; **UNVERIFIED** = primary page not readable or the endpoint is unconfirmed.

## Verdict

Of 22 providers (9 EU, 13 CN): 4 are VERIFIED (Hugging Face, DeepSeek, MiniMax, iFlytek Spark), 14 are PARTIAL (core facts confirmed, but a price, compatibility claim or coding-plan detail is unverified), and 4 are UNVERIFIED (Aleph Alpha, Infomaniak, Tencent Hunyuan/TokenHub, Meituan LongCat). Coding plans exist across most CN providers (Z.ai, Alibaba, MiniMax, iFlytek, StepFun, Volcengine, Baidu, Xiaomi MiMo, Moonshot/Kimi membership, Tencent) and in Mistral (Vibe Pro); the EU API providers reviewed (Scaleway, OVHcloud, IONOS, Nebius, STACKIT, Aleph Alpha) publish no coding plan in the pages read, so they are pay-per-token only. Nearly every CN coding plan is restricted to interactive coding tools and forbids API or backend use, and several say misuse can suspend the subscription; this is the binding constraint for an app client, not price. Several plans have separate base URLs and keys that must not be mixed with the standard pay-as-you-go key (Alibaba, Z.ai, iFlytek, Baidu, Tencent, Volcengine, Xiaomi). Region-specific hosts apply to Alibaba (intl and CN), StepFun (.ai and .com), MiniMax, Moonshot/Kimi and Xiaomi (CN, SGP, AMS). Time-limited offers include the MiniMax 50% first-month launch offer (ended 2026-10-14, V), the Baidu Token Plan 2-fold off-peak discount through 2026-11-30 (V), Volcengine discount windows through 2026-12-30 (V; the 2.5x first-purchase price is U), and iFlytek's first-month 速通版 price (V). Most list prices for Kimi membership, Z.ai Pro/Max, Tencent and Volcengine are U and need console confirmation.

## Table

| # | Group | Provider | Primary doc (opened 2026-10-22) | Base URL(s) | Auth | OpenAI-compat | Anthropic-compat | Coding plan / subscription (name, price, usage, URL, separate key or endpoint?) | Status | Flags |
|---|---|---|---|---|---|---|---|---|---|---|
| EU-1 | EU | Mistral AI | docs.mistral.ai/getting-started/platform-overview (V); mistral.ai/pricing (V) | https://api.mistral.ai (V, OpenAPI server; chat route /v1/chat/completions V) | Bearer API key (V, OpenAPI security scheme) | U (native chat API; OpenAI wording not checked) | U | Mistral Vibe Pro: list $14.99/mo (V, pricing FAQ "normally $14.99"); students $5.99 (V). Includes Vibe Code CLI/IDE (V). Team $24.99/user/mo (V). Subscription, not an API; separate key for Vibe Code: U. URL: mistral.ai/pricing | PARTIAL | Le Chat renamed to Vibe (V). Pricing card mapping of "API credits" ($15 vs $25.5/mo) ambiguous (U). |
| EU-2 | EU | Aleph Alpha | docs.aleph-alpha.com/phariaai-home/latest/index.html (V, enterprise PhariaAI stack); aleph-alpha.com/signup-kolibri-api/ (V, German) | U (legacy api.aleph-alpha.com: third-party only) | U | U | U | None found. Enterprise sales only per third-party (U) | UNVERIFIED | Homepage and signup page say Kolibri is usable directly "per API" (V), but no endpoint, model ID, or price is in any opened page. Cohere merger status is third-party only (U). |
| EU-3 | EU | Scaleway Generative APIs | scaleway.com/en/docs/generative-apis/quickstart/ (V); scaleway.com/en/generative-apis/ (V, price table) | https://api.scaleway.ai/v1 (V) | Scaleway API key passed as Bearer (V, quickstart) | V (OpenAI Python client in quickstart) | U | None found in opened pages (absence U). Pay per 1M tokens in EUR (V): glm-5.3 €1.80, deepseek-v4.1-flash €0.60 (input, V); qwen3.8-27b €0.60; gpt-oss-120b €0.15 (V, input column partly truncated) | PARTIAL | Hosted in France per generative-apis FAQ (V, but same FAQ still says "free while in beta", stale). Free 1M-token tier: third-party only (U). |
| EU-4 | EU | OVHcloud AI Endpoints | docs.ovhcloud.com/en/guides/public-cloud/ai-machine-learning/ai-endpoints-getting-started.md (V, updated 2026-10-08); ai-endpoints-billing.md (V, updated 2026-10-20) | https://oai.endpoints.kepler.ai.cloud.ovh.net/v1 (V, docs bundle) | API access key from a Public Cloud project with a payment method, Bearer (V) | V | U | None found (absence U). Rates on OVH website, not in opened pages (U) | PARTIAL | Rate limit 400 req/min per project per model; anonymous 2 req/min (V). No token cap (V). Model decommission table (V). Discovery-mode keys cannot call the service (V). |
| EU-5 | EU | IONOS AI Model Hub | docs.ionos.com/cloud/ai/ai-model-hub.md (V); how-tos/text-generation.md (V) | https://openai.inference.de-txl.ionos.com/v1 (V) | Authentication Token, Bearer (V) | V (documented as OpenAI-compatible) | U | None found (absence U). Token-based billing (V), prices not read (U) | PARTIAL | Data processed in Germany (V). Separate API reference at api.ionos.com/docs/inference-openai/v1 (V, link only). |
| EU-6 | EU | Nebius Token Factory (docs call it Token Factory; "AI Studio" name not confirmed in opened pages) | docs.tokenfactory.nebius.com/quickstart (V); api-reference/introduction.md (V); switch.md (V); integrations/overview.md (V) | https://api.tokenfactory.nebius.com/v1/ (V). Legacy host api.studio.nebius.com: U | API key from tokenfactory.nebius.com, Bearer (V) | V | U | None found (absence U). Coding-assistant integrations listed (Cline, Continue, Cursor, Kilo, VS Code, Zed) (V); no subscription seen (U) | PARTIAL | Pricing page is JS-only; no prices read (U). Example model moonshotai/Kimi-K2.5 (V). |
| EU-7 | EU (routed) | Hugging Face Inference Providers | huggingface.co/docs/inference-providers/index (V); /pricing (V); huggingface.co/subscribe/pro (V) | https://router.huggingface.co/v1 (V) | HF token, Bearer (V) | V | U (Claude Code setup linked, not read) | No coding plan. HF PRO $9/mo (V) includes $2/mo Inference Provider credits (V). Team/Enterprise $2/seat/mo (V). Pay-as-you-go beyond credits (V). Separate custom provider key option (V). | VERIFIED | Requests route to partner providers (V partner table); :fastest/:cheapest suffixes (V). Billing goes through HF or a custom provider key. |
| EU-8 | EU | STACKIT AI Model Serving (Schwarz Digits, Germany) | docs.stackit.cloud/.../ai-model-serving/getting-started/getting-started-with-shared-models/ (V, updated 2026-08-25); /faq/ (V) | https://api.openai-compat.model-serving.eu01.onstackit.cloud/v1 (V, primary curl) | Auth token created in STACKIT portal, Bearer (V); token is "ordered fee-based" (V) | V (chat completions per primary curl) | U | None found (absence U). Per-1M-token EUR tiers €0.15–€1.75 (U, third-party citing STACKIT price-list PDF, not opened) | PARTIAL | Open-source shared models only (V). Empty tools array triggers hallucinated tool calls on some Llama models (V). |
| EU-9 | Europe (non-EU: Switzerland) | Infomaniak AI Services / AI Tools | infomaniak.com/en/hosting/ai-services (V); developer.infomaniak.com docs unreadable (U) | https://api.infomaniak.com/2/ai/{product_id}/openai/v1 (U, third-party excerpt) | API token with ai-tools scope (U) | V (page says OpenAI-compatible) | U | None found (absence U). Pay per token after 1M free credits (V, page) | UNVERIFIED | Hosted in Switzerland (V); not EU. Developer API docs not readable; base URL is third-party only. |
| CN-1 | CN | DeepSeek | api-docs.deepseek.com/ (V); guides/anthropic_api (V); quick_start/pricing (V) | OpenAI: https://api.deepseek.com (V). Anthropic: https://api.deepseek.com/anthropic (V) | API key, Bearer (V) | V | V | None found. Pay-as-you-go pricing only (V). Claude Code, OpenCode, Copilot use the API key (V) | VERIFIED | Models deepseek-flash (DeepSeek-V4.1-Flash-1017) and deepseek-v4-pro (V). Search/Fetch per-request billing starts 2026-11-01 (V). |
| CN-2 | CN | Moonshot AI (Kimi) | platform.kimi.ai/docs/overview.md (V); docs/guide/claude-code-kimi.md (V); docs/api/chat.md (V); kimi.com/code (V, plan table not rendered) | OpenAI: https://api.moonshot.ai/v1 (V). Anthropic: https://api.moonshot.ai/anthropic (V). China-region host: U | API key, MOONSHOT_API_KEY (V) | V | V | Kimi Code membership (kimi.com): Plus $19, Pro $39, Max $99, Ultra $199/mo USD (U, third-party; primary plan table not readable). Kimi Code CLI accepts the platform API key (V). Membership key and API key are separate balances (U). | PARTIAL | Platform domain now platform.kimi.ai (V). Models kimi-k3, kimi-k2.7-code, kimi-k2.6 (V). Membership price and tier names changed in 2026 (U). |
| CN-3 | CN | Zhipu AI (Z.ai, GLM Coding Plan) | docs.z.ai/devpack/overview (V); devpack/quick-start.md (V); devpack/faq.md (V); z.ai/subscribe (V, meta only) | Anthropic: https://api.z.ai/api/anthropic (V). OpenAI Chat: https://api.z.ai/api/coding/paas/v4 (V). OpenAI Responses: https://api.z.ai/api/v1 (V) | Coding-plan API key, managed separately from the standard API key (V) | V | V | GLM Coding Plan: Lite from $18/mo (V, primary overview and subscribe meta). Credits per 5h: Lite 2,000, Pro 12,000, Max 28,000; per week: 10,000, 60,000, 140,000 (V). Pro $80 and Max $168/mo (U, third-party). Separate key and endpoint (V). URL: docs.z.ai/devpack/overview | PARTIAL | Usable only in listed tools; API calls outside the plan not allowed (V). Models GLM-5.3, GLM-5.3-Flash (V). Off-peak credits at 50% (V). Peak 14:00–18:00 SGT (V). |
| CN-4 | CN | Alibaba Cloud Model Studio (DashScope, Qwen) | alibabacloud.com/help/en/model-studio/coding-plan(.md) (V, updated 2026-10-11); claude-code.md (V) | Coding Plan Anthropic intl: https://coding-intl.dashscope.aliyuncs.com/apps/anthropic (V). China: https://coding.dashscope.aliyuncs.com/apps/anthropic (V). Coding Plan OpenAI base: U. Pay-as-you-go Anthropic: https://dashscope.aliyuncs.com/apps/anthropic (V) | Coding Plan key starts sk-sp- (V); a general sk- key bills pay-as-you-go (V) | U | V | Coding Plan Pro only: $50/mo (V). Quota up to 6,000 req/5h, 45,000/wk, 90,000/mo (V). Models qwen3.7-plus, qwen3.6-plus, kimi-k2.5, glm-5, MiniMax-M2.5, qwen3.5-plus (V). Lite closed to new purchases 2026-03-20, renewals stopped 2026-04-13 (V). Separate key and endpoint (V). | PARTIAL | Limited, first-come slots restocked daily (V). Token Plan Personal/Team is a separate product (V). Charges if the general key is used (V). |
| CN-5 | CN | MiniMax (M Plan; the old Coding/Token Plan name redirects here) | platform.minimax.io/docs/m-plan/intro.md (V); m-plan/claude-code.md (V); m-plan/monthly-offer.md (V); api-reference/text-openai-api.md (V) | OpenAI: https://api.minimax.io/v1 (V). Anthropic: https://api.minimax.io/anthropic (V) | M Plan API key from console/plan (V) | V | V | M Plan Go $22/mo, Explore $55/mo, Build $132/mo (V, regular prices on monthly-offer page). Usage 1x / 3x / 7.5x Go (V). Explore and Build add video model H3 (V). Annual $220 / $550 / $1,320 (U, third-party). URL: platform.minimax.io/docs/m-plan/intro | VERIFIED | 50% first-month launch offer ran through 2026-10-14 (V); expired as of today. Usage caps unpublished (V). Official tool list: Claude Code, Codex, Cursor, OpenCode, Pi, Hermes Agent, OpenClaw (V). |
| CN-6 | CN | Volcengine Ark (Doubao) Coding Plan | docs.volcengine.com/docs/ark/coding-plan-personal-plan-overview?lang=zh (V) | OpenAI: https://ark.cn-beijing.volces.com/api/coding/v3 (V). Anthropic: https://ark.cn-beijing.volces.com/api/coding (V) | Coding Plan key, distinct from standard Ark key (U) | V | V | Coding Plan Lite and Pro (V). List ¥40 / ¥200 per mo (U, third-party; price table is an iframe). 2.5x promo ¥9.9 / ¥49.9 for first two months to 2026-11-08 (U). Quota Lite ~1,200 req/5h, 18,000/mo; Pro ~6,000 req/5h, 90,000/mo (U). Plan may be used only in AI coding tools; API calls forbidden (V). | PARTIAL | Agent Plan (all-modal, separate product) exists (V); its prices are U. Discount windows verified in doc through 2026-12-30 (V). Models include Kimi-K3, GLM-5.3, DeepSeek-V4-Flash (V). |
| CN-7 | CN | Baidu Qianfan (千帆) | cloud.baidu.com/doc/qianfan/s/imlg0beiu (V, updated 2026-10-08); cloud.baidu.com/doc/qianfan/s/Dmrabu8b6 (V, updated 2026-10-21) | Coding Plan OpenAI: https://qianfan.baidubce.com/v2/coding (V). Coding Plan Anthropic: https://qianfan.baidubce.com/anthropic/coding (V). Token Plan Personal: https://qianfan.baidubce.com/v2/tokenplan/personal and .../anthropic/tokenplan/personal (U) | Coding Plan dedicated key, not the normal API key (V) | V | V | Coding Plan Lite ¥40/mo; Pro ¥200/mo (V). Lite 1,200 req/5h, 9,000/wk, 18,000/mo; Pro 6,000/5h, 45,000/wk, 90,000/mo (V). Token Plan Personal: Mini ¥9.9 (10M tokens), Lite ¥40 (42M), Pro ¥200 (230M), Max ¥600 (700M) per mo (V). Coding Plan holders may upgrade free to Token Plan (V). Non-refundable (V). | PARTIAL | Coding Plan no longer updates models; migrate to Token Plan Personal (V). Renewal stop: third-party only (U). Model alias qianfan-code-latest (V). Promo first-purchase prices: U. |
| CN-8 | CN | Tencent Hunyuan and TokenHub | cloud.tencent.com/document/product/1729/111007 (blocked by bot check; U); product/1823/130092 (blocked; U) | Hunyuan OpenAI-compat: https://api.hunyuan.cloud.tencent.com/v1 (U, search excerpt of primary doc). Coding Plan: https://api.lkeap.cloud.tencent.com/coding/v3 and .../coding/anthropic (U) | Coding-plan key sk-sp- (U) | U (excerpt says OpenAI-compat) | U | Coding Plan Lite ¥40/mo, Pro ¥200/mo (U, third-party). First-month promo ¥7.9 / ¥39.9 (U). Quota Lite 1,200 req/5h, Pro 6,000 req/5h (U). International TokenHub tier from $17/mo (U). | UNVERIFIED | Primary Tencent pages unreadable; third-party prices conflict (¥49 / ¥99 / ¥299 claims, U). Plan forbids automation and backend use (U). |
| CN-9 | CN | iFlytek Spark (Astron MaaS Coding Plan) | xfyun.cn/doc/spark/CodingPlan.html (V; v2.3, updated 2026-07-22) | OpenAI: https://maas-coding-api.cn-huabei-1.xf-yun.com/v2 (V). Anthropic: .../anthropic (V). Responses: .../v1/responses (V) | Coding-plan key, not interchangeable with the maas-api key (V) | V | V | 高效版 ¥199/mo: 6,000 req/5h, 45,000/wk, 90,000/mo (V). 速通版 ¥999/mo, first month ¥699 (V); 30,000 req/mo (V). Quarterly 高效版 ¥538/季 (V). 无忧版 ¥19 and 专业版 ¥39 discontinued (V). Model ID astron-code-latest (V). URL: xfyun.cn/doc/spark/CodingPlan.html | VERIFIED | Upgrades only to 速通版; no downgrade (V). Validity 31 days per plan (V). Off-peak factor 0.8 (V). Per-model deduction factors (V). |
| CN-10 | CN | StepFun (Step Plan) | platform.stepfun.ai/docs/en/step-plan/overview (V) | Anthropic: https://api.stepfun.ai/step_plan (V). OpenAI: https://api.stepfun.ai/step_plan/v1 (V). China equivalents on api.stepfun.com: U | Step API Key (V) | V | V | Intl (V): Flash Mini $6.99/mo (400M credits), Plus $9.99 (1,600M), Pro $29 (8,000M), Max $99 (40,000M). Quarterly and yearly also (V). China ¥49 / ¥99 / ¥199 / ¥699 (U, third-party). Credits ≈ $1 = 7M (V). Booster packs (V). | PARTIAL | Credit-based monthly allowance; no RPM/TPM tiers (V). Fees not refundable; cancel at period end (V). Peak-hour multiplier since 2026-10-15 is third-party only (U). Old request-based plan migrated (V). |
| CN-11 | CN | SiliconFlow (硅基流动) | docs.siliconflow.com/en/userguide/introduction (V; no base URL); docs.siliconflow.com/en/usercases/use-siliconcloud-in-ClaudeCode.md (V) | OpenAI-compat global: https://api.siliconflow.com/v1 (V, connectivity curl). Anthropic: https://api.siliconflow.com/ (V). China: https://api.siliconflow.cn/v1 (U) | API key (V) | V | V | None found in primary (absence U). Third-party says pay-as-you-go with free credits, no subscription (U). A Moore Threads coding plan is a different product (U). | PARTIAL | China and global sites may have separate accounts (U). |
| CN-12 | CN | Xiaomi MiMo Token Plan | mimo.mi.com/docs/en-US/price/token-plan (V; updated 2026-10-21) | Token Plan hosts per region, e.g. token-plan-cn / token-plan-sgp / token-plan-ams .xiaomimimo.com with /v1 and /anthropic (U, third-party). Pay-as-you-go: https://api.xiaomimimo.com/v1 and /anthropic (U) | tp- keys for Token Plan, sk- for pay-as-you-go (U) | U | U | Individual monthly (V): Lite $6 / ¥39 (4.1B credits), Standard $16 / ¥99 (11B), Pro $50 / ¥329 (38B), Max $100 / ¥659 (82B). Annual about 12% less (V). Off-peak 0.8x credits 00:00–08:00 Beijing (V). First purchase 12% off (V). Team plan also exists (V). Separate key and endpoint (V). | PARTIAL | Europe region host token-plan-ams is U (third-party) and is relevant to the EU question. Subscription is positioned for AI programming (V). |
| CN-13 | CN | Meituan LongCat | longcat.chat/platform/docs/tools-overview and /platform/docs/ (attempted; 404 or not readable; U) | https://api.longcat.chat/openai/v1 and https://api.longcat.chat/anthropic (U, search excerpt) | Bearer API key (U) | U | U | None found; pay-as-you-go and token pack (U). Models LongCat-2.5-Preview, LongCat-2.0 (U). | UNVERIFIED | Primary docs unreadable; no "Coding Plan" product found in any source (U). |

## Owner decisions

1. **Coding-plan terms vs. this client.** Most CN plans (Z.ai, Alibaba, iFlytek, Baidu, Tencent, Volcengine, MiniMax, Xiaomi) allow use only in listed coding tools and forbid automation or backend calls. Is Autonogrammer, which drives the interactive omp CLI, an allowed "coding tool"? Options: (a) treat it as an allowed interactive tool and use plan keys, after reading each provider's terms; (b) use pay-as-you-go keys only; (c) allow plans only for the owner's personal development, not shipped builds.
2. **Sovereignty scope.** Do you require EU hosting only, or does Switzerland (Infomaniak) count? Options: (a) EU-only providers (Mistral, Scaleway, OVHcloud, IONOS, Nebius, STACKIT; HF routes to partners and must be checked per model); (b) include Infomaniak (Swiss); (c) any European provider.
3. **Aleph Alpha / Kolibri.** Include it? Options: (a) include only after written confirmation of an API endpoint, model ID and price; (b) exclude; (c) keep as a watch item.
4. **CN region hosts.** Use international or China-mainland endpoints? Options: (a) international hosts (api.z.ai, api.moonshot.ai, api.minimax.io, api.stepfun.ai, coding-intl.dashscope, api.siliconflow.com); (b) China hosts (.cn, dashscope.aliyuncs.com, api.siliconflow.cn); (c) both, selected per account.
5. **Promotions.** Plan on list prices or on time-limited offers? Options: (a) list prices only; (b) offers as upside with a fallback; (c) decide per provider once the offer window is confirmed in the console.
6. **Confirm U-marked prices.** Several plan prices rest on third-party sources (Kimi membership, Z.ai Pro/Max, Volcengine list, Tencent, StepFun CN, iFlytek annual). Options: (a) owner confirms in each provider's console before adoption; (b) accept third-party figures for planning; (c) drop providers whose prices cannot be confirmed.

## Unverified

- **Aleph Alpha:** no public API doc or endpoint in any opened page; the homepage says Kolibri is usable per API, but the signup page gives no endpoint, pricing, or model ID. Cohere merger details are third-party only.
- **Infomaniak:** developer.infomaniak.com docs returned an empty shell; base URL and auth are from a third-party excerpt only.
- **Tencent Hunyuan / TokenHub:** primary product pages (1729/111007, 1823/130092, and mirrors) returned an anti-bot script; endpoints came from a search excerpt of the primary doc, prices from third-party pages that conflict with each other.
- **Meituan LongCat:** primary doc pages returned 404 for .md URLs and were otherwise unreadable; endpoints come from a search excerpt.
- **Scaleway:** EUR price columns for input/output were truncated in the opened table; the free-tier claim is third-party only.
- **OVHcloud, IONOS, Nebius:** token prices not read (OVH rates are on its website; Nebius pricing page is JS-only). Nebius "AI Studio" name not confirmed in primary text.
- **STACKIT:** EUR price tiers come from a third-party page citing a STACKIT price-list PDF that was not opened.
- **Mistral:** whether the API is "OpenAI-compatible" was not checked; the plan card mapping of API credits is ambiguous.
- **Kimi / Moonshot:** kimi.com plan table not rendered; the membership prices are third-party. China-region host (api.moonshot.cn) not found in docs.
- **Z.ai:** Pro ($80) and Max ($168) prices are third-party; the z.ai/subscribe price table did not render.
- **Alibaba:** Coding Plan OpenAI-compatible base URL not found in opened text.
- **Volcengine:** list prices, promo prices and quotas are third-party; the primary price table is in an iframe.
- **Baidu Token Plan:** base URLs for Token Plan Personal are third-party (the primary section was truncated); promo prices third-party.
- **StepFun China:** platform.stepfun.com pages not opened; ¥ prices third-party.
- **Xiaomi MiMo:** region hosts and key prefixes are third-party excerpts of the platform pages; the primary pages opened carry prices and features, not base URLs.
- **SiliconFlow:** no coding plan found in primary docs (absence is U); China base URL api.siliconflow.cn not read in primary.
- **Absence claims** (no coding plan): Scaleway, OVHcloud, IONOS, Nebius, STACKIT, Aleph Alpha, Infomaniak, SiliconFlow, LongCat are "none found in pages opened", not proof of absence.

## Sources

Primary, opened 2026-10-22 (verified):
- https://docs.mistral.ai/getting-started/platform-overview (opened; V)
- https://docs.mistral.ai/openapi.yaml (opened; V; servers, bearer scheme, chat route)
- https://mistral.ai/pricing (opened; V; Pro, Team, FAQ prices)
- https://docs.aleph-alpha.com/phariaai-home/latest/index.html (opened; V)
- https://aleph-alpha.com/ (opened; V; German homepage)
- https://aleph-alpha.com/signup-kolibri-api/ (opened; V; German signup page)
- https://www.scaleway.com/en/docs/generative-apis/quickstart/ (opened; V)
- https://www.scaleway.com/en/docs/generative-apis/reference-content/supported-models/ (opened; V)
- https://www.scaleway.com/en/docs/generative-apis/reference-content/openai-compatibility/ (opened; V)
- https://www.scaleway.com/en/generative-apis/ (opened; V; per-1M-token EUR table, FAQ)
- https://docs.ovhcloud.com/en/guides/public-cloud/ai-machine-learning/ai-endpoints-getting-started.md (opened; V)
- https://docs.ovhcloud.com/en/guides/public-cloud/ai-machine-learning/ai-endpoints-billing.md (opened; V)
- https://docs.ovhcloud.com/en/guides/public-cloud/ai-machine-learning/ai-endpoints-capabilities.md (opened; V)
- https://docs.ovhcloud.com/en/llms-full.txt (grep; V; base URL and bearer examples)
- https://docs.ionos.com/cloud/ai/ai-model-hub.md (opened; V)
- https://docs.ionos.com/cloud/ai/ai-model-hub/how-tos/text-generation.md (opened; V)
- https://docs.tokenfactory.nebius.com/quickstart (opened; V)
- https://docs.tokenfactory.nebius.com/api-reference/introduction.md (opened; V)
- https://docs.tokenfactory.nebius.com/switch.md (opened; V)
- https://docs.tokenfactory.nebius.com/integrations/overview.md (opened; V)
- https://docs.tokenfactory.nebius.com/llms.txt (opened; V)
- https://tokenfactory.nebius.com/pricing (opened; JS-only; no prices; U)
- https://huggingface.co/docs/inference-providers/index (opened; V)
- https://huggingface.co/docs/inference-providers/pricing (opened; V)
- https://huggingface.co/subscribe/pro (opened; V)
- https://docs.stackit.cloud/products/data-and-ai/ai-model-serving/faq/ (opened; V)
- https://docs.stackit.cloud/products/data-and-ai/ai-model-serving/getting-started/getting-started-with-shared-models/ (opened; V)
- https://www.infomaniak.com/en/hosting/ai-services (opened; V)
- https://api-docs.deepseek.com/ (opened; V)
- https://api-docs.deepseek.com/guides/anthropic_api/ (opened; V)
- https://api-docs.deepseek.com/quick_start/pricing/ (opened; V)
- https://platform.kimi.ai/docs/overview.md (opened; V)
- https://platform.kimi.ai/docs/guide/claude-code-kimi.md (opened; V)
- https://platform.kimi.ai/docs/guide/kimi-code-cli.md (opened; V)
- https://platform.kimi.ai/docs/api/chat.md (opened; V)
- https://www.kimi.com/code/ (opened; V; no plan prices rendered)
- https://www.kimi.com/membership (opened; returned homepage; U)
- https://docs.z.ai/devpack/overview.md (opened; V)
- https://docs.z.ai/devpack/quick-start.md (opened; V)
- https://docs.z.ai/devpack/faq.md (opened; V)
- https://z.ai/subscribe (opened; V for meta "from 18"; U for prices)
- https://www.alibabacloud.com/help/en/model-studio/coding-plan.md (opened; V)
- https://www.alibabacloud.com/help/en/model-studio/claude-code.md (opened; V; Coding Plan and PAYG base URLs via grep)
- https://platform.minimax.io/docs/m-plan/intro.md (opened; V)
- https://platform.minimax.io/docs/m-plan/claude-code.md (opened; V)
- https://platform.minimax.io/docs/m-plan/monthly-offer.md (opened; V)
- https://platform.minimax.io/docs/api-reference/text-openai-api.md (opened; V)
- https://www.minimax.io/m-plan (opened; meta only; U for prices)
- https://docs.volcengine.com/docs/ark/coding-plan-personal-plan-overview?lang=zh (opened; V for URLs, models, usage rules; prices U)
- https://cloud.baidu.com/doc/qianfan/s/imlg0beiu (opened; V)
- https://cloud.baidu.com/doc/qianfan/s/Dmrabu8b6 (opened; V)
- https://www.xfyun.cn/doc/spark/CodingPlan.html (opened; V)
- https://platform.stepfun.ai/docs/en/step-plan/overview.md (opened; V)
- https://docs.siliconflow.com/en/userguide/introduction.md (opened; V; no base URL)
- https://docs.siliconflow.com/en/usercases/use-siliconcloud-in-ClaudeCode.md (opened; V)
- https://platform.xiaomimimo.com/token-plan (opened; meta only; U)
- https://mimo.mi.com/docs/en-US/price/token-plan (opened; V)

Attempted, not readable (U):
- https://cloud.tencent.com/document/product/1729 and /1729/111007 (+ .md, + cloud.tencent.com.cn mirror): anti-bot script returned
- https://cloud.tencent.com/document/product/1823 and /1823/130092 (+ .md): anti-bot script returned
- https://developer.infomaniak.com/docs/api/post/2/ai/%7Bproduct_id%7D/openai/v1/chat/completions: empty shell
- https://longcat.chat/platform/docs/tools-overview.md and https://longcat.chat/platform/docs/.md: 404
- https://www.alibabacloud.com/help/en/model-studio/coding-plan (HTML): read; Coding Plan base URLs taken from the .md mirror
- https://docs.aleph-alpha.com/ (redirect notice only)

Third-party only (search excerpts; used only for U-marked items, never as the sole source of a V fact): blogs and aggregators covering Z.ai Pro/Max prices, Kimi membership prices, Volcengine list and promo prices, Tencent coding plan, StepFun China prices, Xiaomi regional hosts, LongCat endpoints, STACKIT EUR tiers, Infomaniak base URL, Cohere/Aleph Alpha merger, SiliconFlow coding-plan absence, MiniMax annual prices.
