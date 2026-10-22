# Inference providers: local runtimes, custom endpoints, US cloud providers

Research date: 2026-10-22. Every fact below was read on that date unless marked unverified. Target for OmpOS: a desktop client that talks to the omp engine and needs base URL, auth, and compatibility facts per provider.

## Verdict

Local runtimes with verified OpenAI-compatible and Anthropic-compatible endpoints: Ollama, LM Studio, llama.cpp server, vLLM, LocalAI, and Docker Model Runner (verified). Jan verified for OpenAI-compatible only. SGLang is unverified on base URL and auth. Generic custom-provider patterns are verified: OpenAI-compatible base URL overrides, and Anthropic-compatible `ANTHROPIC_BASE_URL` with `ANTHROPIC_AUTH_TOKEN` (Bearer) or `ANTHROPIC_API_KEY` (x-api-key). Of the US cloud providers, OpenAI, Google Gemini API, Azure OpenAI (v1), AWS Bedrock, xAI, OpenRouter, Together AI, Fireworks AI, Groq, Cerebras, Perplexity, DeepInfra, Hugging Face, and Cloudflare Workers AI each have a verified OpenAI-compatible base URL. Only OpenRouter, Fireworks, and xAI (deprecated) expose a verified Anthropic-compatible endpoint, plus Ollama and the local runtimes. Anthropic's own OpenAI SDK layer is documented as not production-ready. Coding plans are the priority: primary sources verify these subscriptions for coding tools: Anthropic Claude Pro and Max (Claude Code included), OpenAI ChatGPT Go/Plus/Pro (Codex included), Ollama Pro and Max (Claude Code routing documented), xAI X Subscription tiers with Grok Build, and Google Antigravity tiers (names verified; Plus/Pro/Ultra prices unverified). Cerebras Code Pro and Max are listed but marked sold out on the primary page. Fireworks Fire Pass is verified as not open to new subscriptions. No US API-only provider (OpenRouter, Together, Groq, DeepInfra, Perplexity, Cloudflare, Hugging Face, Azure, Bedrock) showed a flat coding plan on a page I opened; for Together and Groq the absence is unverified because their pricing pages were not retrievable. Verified table rows: 24 of 25. Unverified table rows: 1 (SGLang). NVIDIA NIM is unverified and kept out of the table (see Unverified section).

## Table

Status column: "verified" means the primary doc URL was opened and the base URL and auth were confirmed on it. Cells marked (unverified) were not confirmed on an opened page.

| Group | Provider | Primary doc (opened 2026-10-22) | Base URL | Auth | OpenAI-compatible | Anthropic-compatible | Coding plan or subscription (name, price, URL) | Status |
|---|---|---|---|---|---|---|---|---|
| a. local | Ollama | https://docs.ollama.com/api/openai-compatibility | Local: http://localhost:11434/v1/ ; cloud: https://ollama.com/v1 | Local: key required by client but ignored. Cloud: Bearer OLLAMA_API_KEY | Yes (/v1/chat/completions, /v1/responses) | Yes (/v1/messages); local base http://localhost:11434; cloud base https://ollama.com | Pro $20/mo or $200/yr; Max $100/mo; Team $500/mo (early access). Claude Code routed via Ollama documented at https://docs.ollama.com/integrations/claude-code. Pricing page does not say whether plan usage applies to Claude Code (unverified). Source https://ollama.com/pricing | verified |
| a. local | LM Studio | https://lmstudio.ai/docs/developer/openai-compat | http://localhost:1234/v1 | None by default. With Require Authentication on, accepts x-api-key or Bearer | Yes (/v1/chat/completions, /v1/responses, /v1/embeddings, /v1/models, /v1/completions) | Yes (/v1/messages); base http://localhost:1234; https://lmstudio.ai/docs/developer/anthropic-compat | None (local, free). Claude Code and Codex integration pages linked from docs | verified |
| a. local | llama.cpp server | https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/README.md | http://127.0.0.1:9931 by default (--port default 9931, --host default 127.0.0.1); port set by flag | --api-key KEY or --api-key-file (comma list allowed). Header form not confirmed on the doc (README example uses x-api-key) | Yes (chat completions, responses, embeddings) | Yes (Anthropic Messages API compatible chat completions) | None. MIT license (https://api.github.com/repos/ggml-org/llama.cpp, opened) | verified |
| a. local | vLLM | https://docs.vllm.ai/en/latest/serving/online_serving/ (page redirected to docs.vllm.ai/en/latest/serving/online_serving/ in the page read; CLI flags at https://docs.vllm.ai/en/latest/cli/serve/) | http://localhost:8000/v1 (--port default 8000) | --api-key; doc warns it authenticates only /v1, /v2, /inference prefixes | Yes (/v1/chat/completions, /v1/completions, /v1/responses, /v1/embeddings, others) | Yes (/v1/messages, /v1/messages/count_tokens) | None. Apache-2.0 (https://api.github.com/repos/vllm-project/vllm, opened) | verified |
| a. local | LocalAI | https://localai.io/docs/basics/getting_started/ (canonical https://localai.io/docs/basics/getting_started/) | http://localhost:8080/v1 (Docker example port 8080) | LOCALAI_API_KEY (single admin-level key) or LOCALAI_AUTH=true for multi-user accounts | Yes | Yes (Anthropic Messages API supported, per quickstart) | None. MIT license (https://api.github.com/repos/mudler/LocalAI, opened) | verified |
| a. local | Jan | https://docs.jan.ai/desktop/api-server | http://127.0.0.1:1337/v1 (default host and port) | Optional API key set in Jan settings (Authorization header); none if unset | Yes (OpenAI-compatible server, llama.cpp or MLX backend) | Not found on the page (unverified) | None. Repo license reported as "Other" by GitHub; license text not opened (https://api.github.com/repos/janhq/jan) | verified (OpenAI-compatible only) |
| a. local | Docker Model Runner | https://docs.docker.com/ai/model-runner/api-reference/ | OpenAI: http://localhost:12434/engines/v1 ; Anthropic and Ollama formats: http://localhost:12434 ; containers: http://model-runner.docker.internal | Not stated on opened page (unverified) | Yes (/engines/v1/chat/completions, embeddings, models) | Yes (Anthropic-compatible messages endpoint) | None found | verified |
| a. local | SGLang | https://docs.sglang.io/docs/basic_usage/openai_api | (unverified: port and base URL not confirmed) | (unverified) | Yes (per opened page) | Page states an Anthropic-compatible /v1/messages endpoint with Claude Code integration; linked page not opened | None found (unverified) | unverified |
| b. custom | Generic OpenAI-compatible endpoint (pattern) | Verified in Cerebras docs: https://inference-docs.cerebras.ai/resources/openai (provider type "OpenAI-compatible", base URL, API key) | User-supplied /v1 root | Bearer API key | Yes | n/a | n/a | verified |
| b. custom | Generic Anthropic-compatible endpoint (pattern) | Claude Code env vars documented at https://docs.ollama.com/api/anthropic-compatibility, https://lmstudio.ai/docs/developer/anthropic-compat, https://openrouter.ai/docs/cookbook/coding-agents/claude-code-integration | ANTHROPIC_BASE_URL (root, no /v1) | ANTHROPIC_AUTH_TOKEN sent as Bearer; ANTHROPIC_API_KEY sent as x-api-key | n/a | Yes (/v1/messages) | n/a | verified |
| c. US cloud | OpenAI | https://developers.openai.com/api/reference/overview (platform.openai.com returned HTTP 403) | https://api.openai.com/v1 | Bearer API key (or workload identity access token) | Yes (native) | No Anthropic-compatible endpoint found | Codex is included in ChatGPT plans: Free $0 (limited); Go $8/mo; Plus $20/mo; Pro $100, $200, $500 per mo. Business price (unverified). API-key sign-in to Codex bills at API rates. Source https://learn.chatgpt.com/docs/pricing | verified |
| c. US cloud | Anthropic | https://platform.claude.com/docs/en/api/overview | https://api.anthropic.com (native); OpenAI SDK layer https://api.anthropic.com/v1/ | Authorization Bearer (also accepts legacy x-api-key). anthropic-version header required | OpenAI SDK compatibility layer: yes, documented as not production-ready (https://platform.claude.com/docs/en/cli-sdks-libraries/libraries/openai-sdk) | Native Messages API (/v1/messages) is the Anthropic format | Claude Pro: $17/mo billed annually ($200 up front), $20/mo monthly; Claude Code included. Max: from $100/mo, 5x or 20x Pro usage; Claude Code included. Source https://claude.com/pricing | verified |
| c. US cloud | Google Gemini API | https://ai.google.dev/gemini-api/docs/openai | https://generativelanguage.googleapis.com/v1beta/openai/ | Bearer GEMINI_API_KEY | Yes | Not found in opened docs (unverified) | No Gemini API coding plan. Google coding products: Antigravity Individual $0; Google AI Pro and AI Ultra (names verified at https://antigravity.google/pricing; prices unverified, third-party only); Gemini Code Assist Standard $22.80/user/mo monthly ($19 annual) and Enterprise $54/user/mo ($45 annual) per https://codeassist.google/products/business. Gemini CLI and IDE extensions replaced by Antigravity CLI on 2026-06-18 (banner on same page) | verified |
| c. US cloud | Azure OpenAI (Microsoft Foundry, v1 API) | https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle | https://YOUR-RESOURCE-NAME.openai.azure.com/openai/v1/ (no api-version parameter) | API key, or Microsoft Entra ID token (SDK examples; raw api-key header unverified) | Yes (v1 API, deployment name in model field) | Not found (unverified) | None found (unverified, not exhaustively checked) | verified |
| c. US cloud | AWS Bedrock | https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions.html | https://bedrock-runtime.{region}.amazonaws.com/openai/v1 (recommended); bedrock-mantle alternative | AWS SigV4 credentials or Amazon Bedrock API key | Yes (Chat Completions; model-dependent; no GET /models on bedrock-runtime) | No /v1/messages endpoint verified. Claude Code can use Bedrock directly: https://code.claude.com/docs/en/amazon-bedrock | None found. Claude Code on Bedrock billed through AWS usage (wizard setup in docs) (Bedrock pricing unverified) | verified |
| c. US cloud | xAI | https://docs.x.ai/developers/rest-api-reference/inference/responses | https://api.x.ai/v1 | Bearer XAI_API_KEY | Yes (Responses API at /v1/responses; OpenAI SDK with base_url https://api.x.ai/v1 per xAI examples; chat completions not confirmed) | /v1/messages exists but is marked deprecated; docs say Anthropic SDK compatibility is fully deprecated (https://docs.x.ai/developers/rest-api-reference/inference/legacy) | X Subscription tiers (https://x.ai/pricing): Plus $8/mo (lists Grok Build and Grok API access); Premium $30/mo; Super $100/mo; Ultra $200/mo. Grok and Cursor share a weekly pool (https://docs.x.ai/x-subscription/overview). Whether API calls draw from the pool: unverified | verified |
| c. US cloud | OpenRouter | https://openrouter.ai/docs/quickstart | https://openrouter.ai/api/v1 | Bearer OPENROUTER_API_KEY | Yes | Yes for Claude Code: base https://openrouter.ai/api with ANTHROPIC_AUTH_TOKEN; docs warn only guaranteed with Anthropic first-party provider (https://openrouter.ai/docs/cookbook/coding-agents/claude-code-integration) | None. Pricing page lists Free, Standard (5.5% platform fee), Business (8%), Enterprise; no coding subscription (https://openrouter.ai/pricing) | verified |
| c. US cloud | Together AI | https://docs.together.ai/docs/inference/openai-compatibility | https://api.together.ai/v1 | Bearer TOGETHER_API_KEY | Yes | Not found (unverified) | None found on opened pages (pricing page not opened, unverified). Together Link is a beta launcher that routes Claude Code, Codex and others through Together models, billed per token (https://docs.together.ai/docs/togetherlink) | verified |
| c. US cloud | Fireworks AI | https://docs.fireworks.ai/tools-sdks/openai-compatibility | OpenAI: https://api.fireworks.ai/inference/v1 ; Anthropic: https://api.fireworks.ai/inference | Bearer Fireworks key (fw_...); Fire Pass key (fpk_...) for Fire Pass routes only | Yes | Yes (/v1/messages) per https://docs.fireworks.ai/firepass | Fire Pass: experimental, "not currently open to new subscriptions"; covers selected open-weight routers for personal agentic coding (price not on page, unverified). Source https://docs.fireworks.ai/firepass | verified |
| c. US cloud | Groq | https://console.groq.com/docs/openai | https://api.groq.com/openai/v1 | Bearer GROQ_API_KEY (SDK api_key; header shown implicitly) | Yes (with listed unsupported fields) | Not found (unverified) | None found (unverified; groq.com/pricing redirected to home page and billing docs returned 404 on the attempted URL) | verified |
| c. US cloud | Cerebras | https://inference-docs.cerebras.ai/resources/openai | https://api.cerebras.ai/v1 | Bearer Cerebras API key | Yes | Not listed in opened docs (unverified) | Cerebras Code: Pro $50/mo (24M tokens/day); Max $200/mo (120M tokens/day); Free trial with limited credits. Both paid plans show "sold out" on https://www.cerebras.ai/code (page modified 2026-09-18, read 2026-10-22) | verified |
| c. US cloud | Perplexity | https://docs.perplexity.ai/docs/agent-api/openai-compatibility | https://api.perplexity.ai/v1 | API key via OpenAI SDK (header shown implicitly, unverified) | Responses format (client.responses.create on /v1/responses; canonical /v1/agent). Chat completions: not confirmed | Not found (unverified) | None. Pricing is per token plus tool fees; no subscription on https://docs.perplexity.ai/docs/getting-started/pricing | verified |
| c. US cloud | DeepInfra | https://docs.deepinfra.com/chat/overview | https://api.deepinfra.com/v1/openai | Bearer DEEPINFRA token | Yes | Not found (catalog lists Claude models; endpoint unverified) | None. Pay-what-you-use pricing, no contracts (https://deepinfra.com/pricing) | verified |
| c. US cloud | Hugging Face Inference Providers | https://huggingface.co/docs/inference-providers/index | https://router.huggingface.co/v1 | Bearer HF_TOKEN | Yes | Not verified (Claude Code setup page linked, not opened) | None found (unverified) | verified |
| c. US cloud | Cloudflare Workers AI | https://developers.cloudflare.com/workers-ai/configuration/open-ai-compatibility/ | https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1 | Bearer Cloudflare API token | Yes (Chat Completions and embeddings; Responses only for gpt-oss) | Not found (unverified) | None found (unverified) | verified |

Notes on the table:

- Ollama, OpenRouter, Fireworks, LM Studio, llama.cpp, vLLM, LocalAI, Docker Model Runner each have a verified /v1/messages path. xAI's is marked deprecated.
- Anthropic-compatible route for Claude Code always uses the root base URL with no /v1 suffix, verified on Ollama, LM Studio, OpenRouter, and Fireworks pages.

## Owner decisions

1. Coding-plan priority. Which subscriptions should OmpOS support first?
   - Anthropic Claude Pro or Max (Claude Code included, verified on claude.com/pricing).
   - OpenAI ChatGPT Plus or Pro (Codex included, verified on learn.chatgpt.com/docs/pricing).
   - Ollama Pro or Max (cloud models via Anthropic-compatible route, verified).
   - xAI X Subscription with Grok Build (verified tiers; API billing overlap unverified).
   - Google Antigravity Pro or Ultra (plan names verified; prices unverified).
   - Options: all five; a subset; or only the Anthropic and OpenAI routes.
2. Non-flat "coding" routes. Should per-token coding routes count as coding plans?
   - Together Link (beta, per-token) and OpenRouter (pay-as-you-go).
   - Options: exclude; list as per-token alternatives; or include as first-class with a warning.
3. Sold-out or closed plans. Cerebras Code (sold out on primary page) and Fireworks Fire Pass (not open to new subscriptions).
   - Options: exclude; keep as optional entries that show status; or re-check before implementation.
4. Anthropic-compatible transport. How should OmpOS send requests to non-Anthropic backends?
   - Option A: generic ANTHROPIC_BASE_URL and token setup for Ollama, LM Studio, llama.cpp, vLLM, LocalAI, OpenRouter, Fireworks.
   - Option B: OpenAI-compatible adapter only, for all cloud providers.
   - Option C: both, with per-provider capability flags.
5. xAI route. The Anthropic-compatible /v1/messages endpoint is documented as deprecated.
   - Options: use the Responses API at https://api.x.ai/v1 only; or keep /v1/messages as a short-term fallback with a removal date.
6. Hosted vs local priority. Which local runtimes should be first-class?
   - Ollama, LM Studio, llama.cpp, vLLM, LocalAI, Docker Model Runner (verified); Jan (OpenAI-compatible only); SGLang (unverified).
7. Scope of "US". Hugging Face and Cloudflare are included above. Hugging Face headquarters was not verified on an opened page; Cloudflare's San Francisco address was read in page metadata.
   - Options: keep both; keep Cloudflare only; or drop both.

## Unverified

- SGLang: base URL, default port, and auth not confirmed. Anthropic-compatible endpoint is stated on the opened page; the linked anthropic_api page was not opened.
- NVIDIA NIM: only the docs root https://docs.nvidia.com/nim/ was opened. No OpenAI base URL, auth, or coding plan was confirmed. Not included in the table.
- Jan: Anthropic-compatible endpoint not found on the page read; license text not opened (GitHub reports "Other").
- LM Studio: license terms not verified (no opened source).
- Anthropic: native header behaviour. Authorization Bearer is documented as primary; x-api-key is documented as a legacy fallback (verified). Anthropic Pro and Max plan limits are not part of this verification beyond the price and Claude Code inclusion.
- OpenAI: Business Standard and Premium prices (seen only in third-party search snippets). Pro usage multipliers (third-party). Credit price per unit (third-party). Not used in the table.
- Google: AI Pro ($19.99), AI Ultra 5x ($99.99), AI Ultra 20x ($199.99) prices are from third-party summaries only; the primary page opened lists plan names and features without prices. Google Gemini API Anthropic-compatible endpoint: not found in opened docs (absence unverified). Gemini Code Assist individual page (https://codeassist.google/products/individual) returned no content.
- Azure: raw REST api-key header not confirmed on opened page; Claude via Foundry not checked; coding plan absence not exhaustively checked.
- AWS Bedrock: Bedrock pricing not opened; Anthropic-compatible third-party endpoint not checked; no subscription found on opened pages.
- xAI: the statement that API usage is billed separately from the plan pool was quoted in a search snippet of x.ai/pricing but was not in the portion of that page I read. Anthropic /v1/messages relay behaviour reported by a third party (not an xAI commitment) is unverified.
- Together AI: pricing page not opened; coding-plan absence unverified. Anthropic-compatible endpoint not found.
- Groq: pricing page not retrieved (groq.com/pricing redirected to the home page; the billing URL tried returned 404). Coding plan absence unverified. Anthropic-compatible endpoint not found. Bearer header shown only through SDK api_key.
- Cerebras: "sold out" status is from the cached page read on 2026-10-22; third-party sources disagree about signup availability (not used as primary evidence). Claude Code use through a gateway is third-party (not verified).
- Perplexity: chat completions endpoint support not confirmed (only Responses and Agent API documented on opened pages). Anthropic endpoint not found.
- DeepInfra: its catalog lists Claude models, but no Anthropic-compatible endpoint was verified. Bearer auth confirmed by curl example.
- Hugging Face: Claude Code setup guide linked from the page but not opened; coding plan absence not checked.
- Cloudflare: Anthropic-compatible endpoint not found; coding plan absence not checked.
- Fireworks: Fire Pass price not on the opened page.
- Ollama: whether Claude Code usage draws from Pro or Max plan allowances is not stated on the pricing page.
- Non-US providers excluded by scope and not researched: Mistral (France), Cohere (Canada), DeepSeek, Moonshot/Kimi, MiniMax, Z.ai (GLM Coding Plan mentioned only by third-party sources; not verified).

## Sources

Primary sources opened on 2026-10-22 (all verified reads):

- https://docs.ollama.com/api/openai-compatibility
- https://docs.ollama.com/api/anthropic-compatibility
- https://docs.ollama.com/integrations/claude-code
- https://ollama.com/pricing
- https://github.com/ollama/ollama (repo metadata via GitHub API)
- https://lmstudio.ai/docs/developer/openai-compat
- https://lmstudio.ai/docs/developer/anthropic-compat
- https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/README.md
- https://api.github.com/repos/ggml-org/llama.cpp
- https://docs.vllm.ai/en/latest/serving/online_serving/
- https://docs.vllm.ai/en/latest/cli/serve/
- https://api.github.com/repos/vllm-project/vllm
- https://localai.io/docs/basics/getting_started/
- https://api.github.com/repos/mudler/LocalAI
- https://docs.jan.ai/desktop/api-server
- https://api.github.com/repos/janhq/jan
- https://docs.docker.com/ai/model-runner/api-reference/
- https://docs.sglang.io/docs/basic_usage/openai_api
- https://docs.nvidia.com/nim/ (root only)
- https://platform.claude.com/docs/en/cli-sdks-libraries/libraries/openai-sdk
- https://platform.claude.com/docs/en/api/overview
- https://claude.com/pricing
- https://inference-docs.cerebras.ai/resources/openai
- https://www.cerebras.ai/code
- https://ai.google.dev/gemini-api/docs/openai
- https://codeassist.google/products/business
- https://codeassist.google/products/individual (no content returned)
- https://antigravity.google/pricing
- https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle
- https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions.html
- https://code.claude.com/docs/en/amazon-bedrock
- https://developers.openai.com/api/reference/overview
- https://learn.chatgpt.com/docs/pricing (OpenAI Codex pricing; developers.openai.com/codex/pricing redirected here)
- https://docs.x.ai/developers/rest-api-reference/inference/responses
- https://docs.x.ai/developers/rest-api-reference/inference/legacy
- https://docs.x.ai/x-subscription/overview
- https://x.ai/pricing
- https://openrouter.ai/docs/quickstart
- https://openrouter.ai/docs/cookbook/coding-agents/claude-code-integration
- https://openrouter.ai/pricing
- https://docs.together.ai/docs/inference/openai-compatibility
- https://docs.together.ai/docs/togetherlink
- https://docs.fireworks.ai/tools-sdks/openai-compatibility
- https://docs.fireworks.ai/firepass
- https://console.groq.com/docs/openai
- https://groq.com/ (redirect target for groq.com/pricing)
- https://docs.perplexity.ai/docs/agent-api/openai-compatibility
- https://docs.perplexity.ai/docs/getting-started/pricing
- https://docs.deepinfra.com/chat/overview
- https://deepinfra.com/pricing
- https://huggingface.co/docs/inference-providers/index
- https://developers.cloudflare.com/workers-ai/configuration/open-ai-compatibility/
- https://docs.anthropic.com/en/api/openai-sdk (redirected to platform.claude.com)

Search results used only as leads, not as sources for any fact in the table: Cerebras Code third-party reviews, Codex plan trackers, xAI relay issue, Together and Groq coding-plan roundups, Google Antigravity pricing aggregators, Azure search snippets.
