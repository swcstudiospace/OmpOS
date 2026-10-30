# R-PROV-US: US and global model providers, aggregators, coding plans, local runtimes, omp built-ins

Research date 2026-10-27. Status legend: V27 = page opened in this task on 2026-10-27; P22 = opened 2026-10-22 by the earlier scout (docs/plans/vision-research/providers-local-us.md), not reopened; R26 = verified by the repo against omp 18.12.6 on 2026-10-26 (apps/Autonogrammer.Native/Settings/Models/OmpBuiltInProviders.cs); S = web_search answer only, official page named but not opened; U = unverified. Anything marked [INFERENCE] was not read on a page. No key page is listed unless a page or the repo verified it.

## Corrections to the 2026-10-22 doc and to the catalogue

| Item | Finding | URL |
|---|---|---|
| GitHub Models | RETIRED 2026-07-30: playground, catalog, inference API and BYOK gone. Do not add a github-models provider. | https://docs.github.com/en/github-models (V27) |
| Lambda Inference API | api.lambda.ai/v1 was reported shut down 2025-09-25; Lambda docs page opened shows only GPU cloud. Treat as not available. | https://docs.lambda.ai/public-cloud/lambda-inference-api/ (V27, no inference docs on page); shutdown claim S |
| Hyperbolic | Old api.hyperbolic.xyz/v1 now returns 404 (decommissioned). New base https://api.hyperbolic.ai/inference/v1, sk_live_ keys, Pro plan ($5 one-time deposit) or Organization needed. | https://hyperbolic.ai/docs/inference/overview.md (V27) |
| Copilot pricing | Copilot Pro $10, Pro+ $39, Max $100, Business $19/seat, Enterprise $39/seat; usage is metered in GitHub AI Credits ($0.01 per credit beyond allowance). | https://docs.github.com/en/copilot/get-started/plans (V27) |
| Gemini CLI | Replaced by Antigravity CLI on 2026-06-18 per codeassist.google banner (P22). Antigravity plans: Individual $0, Google AI Pro, Google AI Ultra, Business via Google Cloud (Gemini Enterprise from $30/seat). | https://antigravity.google/pricing (V27) |
| Cerebras Code | Pro $50 (24M tokens/day) and Max $200 (120M tokens/day) both show sold out; GLM 4.7; page modified 2026-09-18. | https://www.cerebras.ai/code (V27) |
| omp ids vs catalogue | ProviderCatalogue.cs ids use suffixes (ollama-local, lm-studio-local, llama-cpp-local, vllm-local, localai-local, jan-local, docker-model-runner-local, ollama-cloud, perplexity, openrouter-anthropic, huggingface-router, deepseek-anthropic, minimax-m-plan-anthropic, iflytek-astron-coding-openai/-anthropic) and two patterns (openai-compatible-generic, anthropic-compatible-generic). omp ids differ: ollama, lm-studio, llama.cpp, vllm, huggingface. Map catalogue id to omp id explicitly. | src/Autonogrammer.Harness.Providers/ProviderCatalogue.cs |
| omp custom api values | models.yml api accepts openai-completions, openai-responses, openai-codex-responses, azure-openai-responses, anthropic-messages, bedrock-converse-stream, google-generative-ai, google-gemini-cli, google-vertex, typesafe. OmpProviderApiNames.cs maps only the first three of these (openai-completions, openai-responses, anthropic-messages). | omp://models.md lines 135-144; src/Autonogrammer.Harness.OmpConfig/OmpProviderApiNames.cs |

## Provider table

Columns: ID slug (proposed Autonogrammer slug; omp id in the omp column) | Display name | Kind | Base URL(s) and protocol | Auth scheme and header | Models-list endpoint | Docs URL | Key page | omp-native | Coding plan | Status and URL opened.

| ID | Name | Kind | Base URL(s) / protocol | Auth | Models list | Docs | Key page | omp-native | Coding plan | Status / opened |
|---|---|---|---|---|---|---|---|---|---|---|
| openai | OpenAI API | API | https://api.openai.com/v1 (Chat Completions, Responses) | Authorization: Bearer key | GET /v1/models [INFERENCE: standard OpenAI route] | https://developers.openai.com/api/reference/overview | https://platform.openai.com/api-keys not verified (platform.openai.com answered 403 on 2026-10-22) | yes: openai, OPENAI_API_KEY | none for API | P22 https://developers.openai.com/api/reference/overview |
| openai-codex | ChatGPT sign-in for Codex | coding plan | Codex backend host not documented on opened pages; no public base URL | Browser OAuth (codex login), tokens cached ~/.codex/auth.json; alternative API-key sign-in bills at API rates | n/a | https://learn.chatgpt.com/docs/auth | n/a | yes: openai-codex, OPENAI_CODEX_OAUTH_TOKEN, /login openai-codex | Codex included in ChatGPT: Free $0 limited, Go $8/mo, Plus $20/mo, Pro $100/$200/$500 per mo (P22 https://learn.chatgpt.com/docs/pricing). Quota windows and third-party-tool policy: not found on opened pages (U) | V27 auth page https://learn.chatgpt.com/docs/auth ; price P22 |
| anthropic | Anthropic API | API | https://api.anthropic.com (native Messages /v1/messages); OpenAI-SDK layer https://api.anthropic.com/v1/ documented as not production-ready | Bearer token or x-api-key; anthropic-version header required | GET /v1/models | https://platform.claude.com/docs/en/api/overview | https://platform.claude.com/settings/keys (R26) | yes: anthropic, ANTHROPIC_API_KEY (OAuth token ANTHROPIC_OAUTH_TOKEN checked first) | none for API | V27 https://platform.claude.com/docs/en/api/overview (models route); base P22 |
| anthropic-subscription | Claude Pro / Max login | coding plan | Same host as API; credentials are OAuth tokens | OAuth (Claude account) | as API | https://code.claude.com/docs/en/legal-and-compliance | n/a | yes: anthropic via /login (OAuth) | Pro $17/mo annual ($200 up front) or $20 monthly; Max from $100/mo (5x or 20x Pro usage), Claude Code included, Max includes $100-$200/mo API credits (https://claude.com/pricing V27). Quota windows: linked usage-limit article not opened. POLICY as documented: OAuth is intended exclusively for purchasers of Free/Pro/Max/Team/Enterprise plans for ordinary use of Claude Code and other native Anthropic applications; Anthropic does not permit third-party developers to offer Claude.ai login in their applications or route requests through Free/Pro/Max credentials on behalf of users, nor collect or store Claude.ai credentials; developers should use API keys via Claude Console or Bedrock/Google/Foundry. An end user may sign in to the UNMODIFIED Claude Code binary with their own plan even when a platform hosts it. | V27 https://code.claude.com/docs/en/legal-and-compliance.md ; https://claude.com/pricing |
| google | Gemini API (AI Studio) | API | https://generativelanguage.googleapis.com/v1beta/openai/ (OpenAI-compatible); native google-generative-ai | Bearer GEMINI_API_KEY on compat route | GET {base}/models on the compat route [INFERENCE]; native models route not opened | https://ai.google.dev/gemini-api/docs/openai | https://aistudio.google.com/apikey (R26; also linked on V27 https://ai.google.dev/gemini-api/docs/models) | yes: google, GEMINI_API_KEY | No Gemini API coding plan | V27 models page (gemini-4-argon, gemini-3.8-flash listed, updated 2026-10-22); base P22 |
| google-vertex | Vertex AI | API | URL pattern not opened; omp handles it natively | GOOGLE_CLOUD_API_KEY or ADC (GOOGLE_APPLICATION_CREDENTIALS) plus project and location | n/a | omp://providers.md | https://console.cloud.google.com [INFERENCE] | yes: google-vertex, GOOGLE_CLOUD_API_KEY or ADC; project aliases GOOGLE_CLOUD_PROJECT/GCP_PROJECT/GCLOUD_PROJECT; location aliases GOOGLE_VERTEX_LOCATION/GOOGLE_CLOUD_LOCATION/VERTEX_LOCATION | consumption billing [INFERENCE] | omp docs only; vendor page not opened (U) |
| google-gemini-cli | Gemini Code Assist / Gemini CLI login | coding plan | omp api google-gemini-cli | OAuth via /login | n/a | https://codeassist.google/products/business | n/a | yes: google-gemini-cli (OAuth, /login) | Code Assist Standard $22.80/user/mo monthly ($19 annual), Enterprise $54/user/mo ($45 annual) (P22). Gemini CLI superseded by Antigravity CLI 2026-06-18 (P22); check that omp provider still works [INFERENCE] | P22 |
| google-antigravity | Google Antigravity | coding plan | omp provider google-antigravity | OAuth via /login | n/a | https://antigravity.google/pricing | n/a | yes: google-antigravity (OAuth) | Individual $0/mo (basic weekly limits, unlimited Tab and Command); Google AI Pro (more limits + AI credit pool); Google AI Ultra (even more); Business via Google Cloud: API consumption, Gemini Enterprise Standard/Plus from $30/seat/mo. Prices for Pro/Ultra not on page. | V27 https://antigravity.google/pricing |
| azure | Azure OpenAI / Microsoft Foundry | API | https://YOUR-RESOURCE-NAME.openai.azure.com/openai/v1/ (v1 API, no api-version, deployment name in model) | API key or Entra ID token; raw header name unverified | GET models route [INFERENCE] | https://learn.microsoft.com/en-us/azure/foundry/openai/api-version-lifecycle | https://ai.azure.com [INFERENCE: named by GitHub docs as Foundry portal] | yes: azure, AZURE_OPENAI_API_KEY (api azure-openai-responses) | none found | P22 |
| amazon-bedrock | AWS Bedrock | API | https://bedrock-runtime.{region}.amazonaws.com/openai/v1 (Chat Completions); bedrock-mantle alt: https://bedrock-mantle.{region}.api.aws/anthropic (anthropic-messages, per omp models.md line 1053) | SigV4 or Bedrock API key (AWS_BEARER_TOKEN_BEDROCK) | no GET /models on bedrock-runtime | https://docs.aws.amazon.com/bedrock/latest/userguide/inference-chat-completions.html | AWS console [INFERENCE] | yes: amazon-bedrock (AWS_BEARER_TOKEN_BEDROCK, profiles, access keys, ECS/IRSA/EC2); bedrock-mantle same vars | none; AWS usage billing | P22 ; omp lines V27 |
| xai | xAI Grok API | API | https://api.x.ai/v1 (Responses; Chat Completions not confirmed); /v1/messages deprecated | Bearer XAI_API_KEY | GET /v1/models, GET /v1/models/{id}, plus language-models and image-generation-models variants | https://docs.x.ai/developers/rest-api-reference/inference/models | https://console.x.ai [INFERENCE from models page links] | yes: xai, XAI_API_KEY | X Subscription Plus $8, Premium $30, Super $100, Ultra $200 (https://x.ai/pricing P22); Grok and Cursor share a weekly pool (https://docs.x.ai/x-subscription/overview P22); API draw from pool unverified | V27 models route; rest P22 |
| xai-oauth | SuperGrok login | coding plan | xAI host | OAuth (XAI_OAUTH_TOKEN) | as xai | https://docs.x.ai/x-subscription/overview | n/a | yes: xai-oauth, XAI_OAUTH_TOKEN then XAI_API_KEY | subscription tiers above | omp V27, plan P22 |
| meta | Meta Llama API | API | https://api.llama.com/compat/v1/ (OpenAI-compatible: models, chat/completions, moderations; no embeddings or Responses listed) | Bearer LLAMA_API_KEY (dashboard key) | GET /compat/v1/models | https://llama.developer.meta.com/docs/features/compatibility/ | Llama API dashboard (URL not opened) | probably: omp id meta, MODEL_API_KEY then META_API_KEY [INFERENCE that omp meta equals Llama API] | none found | S (page named, not opened; https://dev.meta.ai/docs/features/compatibility returned 404) |
| github-copilot | GitHub Copilot | coding plan | omp provider github-copilot; no public base URL read | OAuth device login or COPILOT_GITHUB_TOKEN | n/a | https://docs.github.com/en/copilot/get-started/plans | n/a | yes: github-copilot, COPILOT_GITHUB_TOKEN | Free (limited credits); Student free; Pro $10/mo 1,000 credits; Pro+ $39 3,900; Max $100 10,000; Business $19/seat 1,900; Enterprise $39/seat 3,900; credits $0.01 beyond pool; completions unlimited on paid plans; Student and Free are Auto model only / limited | V27 https://docs.github.com/en/copilot/get-started/plans |
| github-models | GitHub Models | RETIRED | gone | gone | gone | https://docs.github.com/en/github-models | n/a | no | n/a | V27 retired 2026-07-30 |
| openrouter | OpenRouter | aggregator | https://openrouter.ai/api/v1 (OpenAI); Claude Code base https://openrouter.ai/api (Anthropic) | Bearer OPENROUTER_API_KEY | GET /api/v1/models | https://openrouter.ai/docs/quickstart | https://openrouter.ai/keys (R26) | yes: openrouter, OPENROUTER_API_KEY | none; Free/Standard 5.5% fee/Business 8%/Enterprise (https://openrouter.ai/pricing P22) | V27 quickstart (models route); base P22 |
| together | Together AI | API | https://api.together.ai/v1 (OpenAI); geo hosts us-api.together.ai (+25%) and ca-api.together.ai (+25%) | Bearer TOGETHER_API_KEY | GET /v1/models [INFERENCE] | https://docs.together.ai/docs/inference/openai-compatibility | https://api.together.ai/settings/api-keys (R26) | yes: together, TOGETHER_API_KEY | none; Together Link beta launcher (https://docs.together.ai/docs/togetherlink P22) | V27 https://docs.together.ai/reference/models-1 (geo hosts) ; base P22 |
| fireworks | Fireworks AI | API | OpenAI https://api.fireworks.ai/inference/v1 ; Anthropic https://api.fireworks.ai/inference | Bearer fw_ key | GET /v1/accounts/{account_id}/models (Gateway REST) | https://docs.fireworks.ai/tools-sdks/openai-compatibility | https://fireworks.ai [INFERENCE] ; no page verified | yes: fireworks, FIREWORKS_API_KEY | Fire Pass (separate omp id firepass, FIREPASS_API_KEY): experimental, closed to new subscriptions, fpk_ key (https://docs.fireworks.ai/firepass P22) | V27 https://docs.fireworks.ai/api-reference/list-models ; rest P22 |
| groq | Groq | API | https://api.groq.com/openai/v1 | Bearer GROQ_API_KEY | GET https://api.groq.com/openai/v1/models | https://console.groq.com/docs/openai | https://console.groq.com/keys (R26) | yes: groq, GROQ_API_KEY | no subscription; pay per token (gpt-oss-120b $0.15/$0.60 per 1M) | V27 https://console.groq.com/docs/models ; base P22 |
| cerebras | Cerebras | API / coding plan | https://api.cerebras.ai/v1 | Bearer key | GET /v1/models [INFERENCE] | https://inference-docs.cerebras.ai/resources/openai | https://cloud.cerebras.ai/ (R26) | yes: cerebras, CEREBRAS_API_KEY | Cerebras Code Pro $50 (24M tokens/day), Max $200 (120M tokens/day), GLM 4.7, both sold out; usable with Cline, OpenCode, Crush | V27 https://www.cerebras.ai/code |
| sambanova | SambaNova SambaCloud | API | https://api.sambanova.ai/v1 (OpenAI Chat Completions and POST /v1/responses); Anthropic Messages also supported (page features/anthropic-compatibility not opened) | API key as OpenAI api_key (Bearer) | GET /v1/models [INFERENCE] | https://docs.sambanova.ai/docs/en/features/openai-compatibility.md | https://cloud.sambanova.ai/apis | yes: sambanova, SAMBANOVA_API_KEY | none found; SambaStack uses a customer-specific base URL | V27 openai-compatibility.md and get-started/api-keys-urls.md |
| perplexity | Perplexity API | API | https://api.perplexity.ai/v1 (Responses via /v1/responses, canonical /v1/agent) | API key | not confirmed | https://docs.perplexity.ai/docs/agent-api/openai-compatibility | https://console.perplexity.ai [INFERENCE] | NO: omp 18.12.6 listed no perplexity row (R26); needs models.yml block with api openai-responses | none; per-token plus tool fees | P22 ; R26 |
| deepinfra | DeepInfra | API | https://api.deepinfra.com/v1/openai | Bearer token | GET /v1/openai/models [INFERENCE] | https://docs.deepinfra.com/chat/overview | https://deepinfra.com/dash/api_keys (R26) | yes: deepinfra, DEEPINFRA_API_KEY | none; pay as you go | P22 |
| hyperbolic | Hyperbolic | API | https://api.hyperbolic.ai/inference/v1 (POST /chat/completions, GET /models; text models only; 300 req/min per key) | Bearer sk_live_ key | GET /models | https://hyperbolic.ai/docs/inference/overview.md | Settings > API Keys in console (URL app.hyperbolic.ai referenced for GPUs) | NO: needs models.yml block api openai-completions | none; credits; Pro = $5 one-time deposit; models glm-5.3, kimi-k3, minimax-m3, deepseek-v4.1-flash, qwen3.8-27b | V27 |
| novita | Novita AI | API | https://api.novita.ai/openai (ChatCompletion and Completion) | API key | not on page | https://docs.novita.ai/guides/llm-api.md | https://novita.ai/settings/key-management (R26) | yes: novita, NOVITA_API_KEY | none found | V27 |
| lambda | Lambda Inference | RETIRED / unverified | api.lambda.ai/v1 (S: sunset 2025-09-25) | Bearer | GET /v1/models | https://docs.lambda.ai/public-cloud/lambda-inference-api/ (no inference content) | n/a | NO | n/a | U; shutdown S |
| replicate | Replicate | API | https://api.replicate.com/v1 OpenAI-compatible [S, third-party reports]; native POST /v1/predictions | Replicate API token | /v1/models [S] | https://replicate.com/docs/topics/openai-compatibility (404 on 2026-10-27) | https://replicate.com/account/api-tokens (S) | NO | none | U: official compat page not reachable; do not ship without a live check |
| cloudflare-workers-ai | Cloudflare Workers AI | API | https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1 (Chat Completions, embeddings; Responses only for @cf/openai/gpt-oss-120b/20b, non-streaming) | Bearer Cloudflare API token (Workers AI Read and Edit) | not on page | https://developers.cloudflare.com/workers-ai/configuration/open-ai-compatibility/ | https://dash.cloudflare.com/profile/api-tokens (omp://providers.md) | yes: cloudflare-workers-ai, CLOUDFLARE_API_TOKEN or CLOUDFLARE_AUTH_TOKEN + CLOUDFLARE_ACCOUNT_ID | none found | V27 page; base P22 |
| cloudflare-ai-gateway | Cloudflare AI Gateway | aggregator | per-gateway route chosen by omp | gateway token | n/a | omp://providers.md | n/a | yes: CLOUDFLARE_AI_GATEWAY_API_KEY + CLOUDFLARE_ACCOUNT_ID + CLOUDFLARE_GATEWAY_ID | none | omp docs |
| nvidia | NVIDIA NIM (build.nvidia.com) | API | https://integrate.api.nvidia.com ; POST /v1/chat/completions | Bearer nvapi key [INFERENCE] | GET /v1/models [INFERENCE] | https://docs.api.nvidia.com/nim/reference/llm-apis | https://build.nvidia.com/ (R26) | yes: nvidia, NVIDIA_API_KEY | none; self-hosted NIM needs NVIDIA AI Enterprise entitlement (page note) | V27 |
| databricks | Databricks Foundation Model APIs | API | https://<workspace-host>/serving-endpoints (OpenAI client; model = endpoint name e.g. databricks-claude-opus-5, databricks-kimi-k3); REST POST /serving-endpoints/{name}/invocations. Page also points to Unity Gateway model services and an ai-gateway/mlflow/v1 path (S, unverified) | Bearer Databricks personal access token (dapi...) | GET /api/2.0/serving-endpoints [INFERENCE] | https://docs.databricks.com/aws/en/machine-learning/model-serving/score-foundation-models | workspace Settings > Developer [INFERENCE] | NO: needs models.yml block api openai-completions | pay per token | V27 page (token requirement, endpoint names); base URL S |
| snowflake | Snowflake Cortex | API | https://<account-identifier>.snowflakecomputing.com/api/v2/cortex/v1 : chat/completions (all models), messages (Claude only, anthropic-version 2023-06-01), responses (OpenAI models only) | Authorization: Bearer PAT (JWT/OAuth also); X-Snowflake-Authorization-Token-Type optional | not on opened excerpt | https://docs.snowflake.com/en/user-guide/snowflake-cortex/cortex-rest-api | PAT created with ALTER USER ... ADD PROGRAMMATIC ACCESS TOKEN; role needs SNOWFLAKE.CORTEX_USER [S] | yes: snowflake, SNOWFLAKE_PAT + SNOWFLAKE_ACCOUNT, or /login snowflake | compute-credit billing | V27 cortex-rest-api |
| watsonx | IBM watsonx.ai | API | Native https://us-south.ml.cloud.ibm.com/ml/v1/text/chat?version=YYYY-MM-DD (needs model_id and project_id, not OpenAI-shaped); OpenAI-shaped Model Gateway POST /ml/gateway/v1/chat/completions (preview, Toronto per one source) | Bearer IAM token from POST https://iam.cloud.ibm.com/identity/token (apikey grant, 60 min) | GET /ml/v1/foundation_model_specs?version= | https://cloud.ibm.com/docs/apis/watsonx-ai | IBM Cloud API keys [INFERENCE] | NO: IAM token refresh not supported by plain apiKey; needs a gateway or custom flow | n/a | S only; IBM tutorial URL 404 |
| cohere | Cohere Compatibility API | API | https://api.cohere.ai/compatibility/v1 (chat/completions, embeddings; audio/transcriptions on api.cohere.com) | Authorization: Bearer key | not on page | https://docs.cohere.com/docs/compatibility-api | https://dashboard.cohere.com/api-keys [INFERENCE] | NO: omp 18.12.6 listed no cohere row (R26); needs models.yml block | not found | V27 |
| ai21 | AI21 Jamba | API | https://api.ai21.com/studio/v1/chat/completions (Jamba chat; OpenAI SDK works per third party) | Authorization: Bearer AI21_API_KEY | models route reported 410 (S) | https://docs.ai21.com/reference/jamba-1-6-api-ref | https://studio.ai21.com [INFERENCE] | NO | none found | S; https://docs.ai21.com/docs/openai-compatibility 404 |
| huggingface | Hugging Face Inference Providers | aggregator | https://router.huggingface.co/v1 | Bearer HF_TOKEN | GET /v1/models | https://huggingface.co/docs/inference-providers/index | https://huggingface.co/settings/tokens (R26) | yes: huggingface, HUGGINGFACE_HUB_TOKEN then HF_TOKEN | none found | V27 models route; base P22 |
| moonshot | Moonshot / Kimi (global) | API | https://api.moonshot.ai/v1 (Chat Completions, Responses); https://api.moonshot.ai/anthropic (Messages) | Authorization: Bearer MOONSHOT_API_KEY | GET /v1/models | https://platform.kimi.ai/docs/api/overview.md | https://platform.kimi.ai/console/api-keys | yes: moonshot, MOONSHOT_API_KEY then KIMI_API_KEY (also kimi-code OAuth) | omp kimi-code login [coding plan terms not read] | V27 |
| moonshot-via-gateways | Kimi K3 through US hosts | aggregator route | Hyperbolic id kimi-k3 ($3.00 in/$15.00 out per 1M, V27); Fireworks accounts/fireworks/models/kimi-k3 (V27 spec); Databricks databricks-kimi-k3 (V27); OpenRouter via moonshotai slug [INFERENCE] | per host | per host | see hosts | n/a | via those omp providers | n/a | V27 for first three |
| cursor | Cursor account | n/a | omp provider cursor exists; Autonogrammer must not resell Cursor inference | CURSOR_ACCESS_TOKEN or /login cursor | n/a | omp://providers.md | n/a | yes: cursor | out of scope (Cursor-free) | omp docs |

### Local runtimes

| ID | Name | Kind | Base URL(s) / protocol | Auth | Models list | Docs | omp-native | Status |
|---|---|---|---|---|---|---|---|---|
| ollama | Ollama | local (+cloud) | http://localhost:11434/v1/ OpenAI (chat/completions, responses); /v1/messages Anthropic on http://localhost:11434; cloud https://ollama.com/v1 | none locally; cloud Bearer OLLAMA_API_KEY | GET /v1/models; omp discovery type ollama | https://docs.ollama.com/api/openai-compatibility | yes: ollama (OLLAMA_BASE_URL, OLLAMA_HOST, default http://127.0.0.1:11434, implicit models use openai-responses); ollama-cloud OLLAMA_CLOUD_API_KEY. Plans Pro $20, Max $100, Team $500 (https://ollama.com/pricing) | P22 ; omp V27 |
| lm-studio | LM Studio | local | http://localhost:1234/v1 (also /v1/messages at http://localhost:1234) | none by default; x-api-key or Bearer when Require Authentication | GET /v1/models | https://lmstudio.ai/docs/developer/openai-compat | yes: lm-studio, LM_STUDIO_BASE_URL default http://127.0.0.1:1234/v1, openai-completions | P22 ; omp V27 |
| llama.cpp | llama.cpp server | local | omp default http://127.0.0.1:8080 (LLAMA_CPP_BASE_URL); upstream README default port reported 9931 (P22) - conflict: pass the port explicitly | --api-key / --api-key-file | GET /v1/models | https://raw.githubusercontent.com/ggml-org/llama.cpp/master/tools/server/README.md | yes: llama.cpp, LLAMA_CPP_API_KEY, openai-responses | P22 ; omp V27 |
| vllm | vLLM | local | http://localhost:8000/v1 | --api-key (only /v1 prefixes) | GET /v1/models | https://docs.vllm.ai/en/latest/serving/online_serving/ | yes: vllm, VLLM_API_KEY optional (omp discovery type openai-models-list in examples) | P22 ; omp V27 |
| localai | LocalAI | local | http://localhost:8080/v1 | LOCALAI_API_KEY server side | GET /v1/models | https://localai.io/docs/basics/getting_started/ | NO: models.yml with auth none + discovery openai-models-list | P22 |
| jan | Jan | local | http://127.0.0.1:1337/v1 | optional key set in Jan | GET /v1/models | https://docs.jan.ai/desktop/api-server | NO: models.yml | P22 |
| docker-model-runner | Docker Model Runner | local | http://localhost:12434/engines/v1 OpenAI; Anthropic and Ollama formats at http://localhost:12434; containers http://model-runner.docker.internal | not stated | GET /engines/v1/models | https://docs.docker.com/ai/model-runner/api-reference/ | NO: models.yml | P22 |
| sglang | SGLang | local | http://127.0.0.1:30000/v1 (docs example); Anthropic /v1/messages documented | docs use api_key EMPTY / None | GET /v1/models [INFERENCE] | https://docs.sglang.io/docs/basic_usage/openai_api_completions.md | NO: models.yml | V27 (port from examples; default port flag not read) |
| tgi | Hugging Face TGI | local / self-hosted | http://localhost:3000/v1 (Messages API, OpenAI Chat Completions; model name tgi); Inference Endpoints https://<id>.endpoints.huggingface.cloud/v1/ | api_key - locally; HF token on Inference Endpoints | GET /v1/models [INFERENCE] | https://huggingface.co/docs/text-generation-inference/en/messages_api | NO: models.yml | V27 |

### Generic patterns (custom provider)

| Pattern | Rule | Source |
|---|---|---|
| OpenAI-compatible | models.yml provider: baseUrl, apiKey (env name or literal), api openai-completions or openai-responses, optional authHeader: true, discovery.type openai-models-list (injectV1 default true), auth none for keyless | omp://models.md |
| Anthropic-compatible | api anthropic-messages, authHeader true for Bearer proxies, disableStrictTools true for non-Anthropic backends; Bearer vs x-api-key depends on the host (Snowflake needs Bearer, SDK default is x-api-key) | omp://models.md, https://docs.snowflake.com/en/user-guide/snowflake-cortex/cortex-rest-api |

## omp built-in providers

Source: omp://providers.md (read 2026-10-27) env tables. R26 marks ids the repo confirmed with a fake key on omp 18.12.6 (OmpBuiltInProviders.cs: openai, anthropic, google, groq, openrouter, mistral, xai, cerebras, deepseek, fireworks, together, nvidia, deepinfra, huggingface, moonshot, stepfun, novita, venice, inception, baseten, nanogpt, vercel-ai-gateway, zai, minimax; siliconflow, perplexity, cohere failed the check although siliconflow is in the docs table).

| omp provider id | Environment variable(s) / login |
|---|---|
| anthropic | ANTHROPIC_OAUTH_TOKEN, then ANTHROPIC_API_KEY (ANTHROPIC_FOUNDRY_API_KEY when CLAUDE_CODE_USE_FOUNDRY=true) |
| openai | OPENAI_API_KEY |
| openai-codex | OPENAI_CODEX_OAUTH_TOKEN |
| google | GEMINI_API_KEY |
| google-vertex | GOOGLE_CLOUD_API_KEY or ADC (GOOGLE_APPLICATION_CREDENTIALS) + project + location |
| groq | GROQ_API_KEY |
| openrouter | OPENROUTER_API_KEY |
| mistral | MISTRAL_API_KEY |
| xai | XAI_API_KEY |
| xai-oauth | XAI_OAUTH_TOKEN, then XAI_API_KEY |
| github-copilot | COPILOT_GITHUB_TOKEN |
| cursor | CURSOR_ACCESS_TOKEN |
| azure | AZURE_OPENAI_API_KEY |
| amazon-bedrock | AWS_BEARER_TOKEN_BEDROCK, AWS profiles, AWS_ACCESS_KEY_ID + AWS_SECRET_ACCESS_KEY, or ECS/IRSA/EC2 chain |
| abliteration | ABLITERATION_API_KEY, then ABLIT_KEY |
| aiand | AIAND_API_KEY |
| cerebras | CEREBRAS_API_KEY |
| alibaba-token-plan | ALIBABA_TOKEN_PLAN_API_KEY, then BAILIAN_TOKEN_PLAN_API_KEY |
| baseten | BASETEN_API_KEY |
| bedrock-mantle | AWS_BEARER_TOKEN_BEDROCK or AWS credential chain |
| commandcode | COMMAND_CODE_API_KEY, then COMMANDCODE_API_KEY |
| coralbricks | CORAL_API_KEY, then CORALBRICKS_API_KEY |
| deepinfra | DEEPINFRA_API_KEY |
| deepseek | DEEPSEEK_API_KEY |
| siliconflow | SILICONFLOW_API_KEY |
| siliconflow-cn | SILICONFLOW_CN_API_KEY |
| fireworks | FIREWORKS_API_KEY |
| together | TOGETHER_API_KEY |
| typesafe | TYPESAFE_API_KEY |
| coreweave | COREWEAVE_API_KEY, then WANDB_API_KEY |
| nvidia | NVIDIA_API_KEY |
| devin | DEVIN_API_KEY |
| gmi-cloud | GMI_API_KEY |
| huggingface | HUGGINGFACE_HUB_TOKEN, then HF_TOKEN |
| inception | INCEPTION_API_KEY |
| moonshot | MOONSHOT_API_KEY, then KIMI_API_KEY |
| stepfun | STEPFUN_API_KEY |
| helmcode | HELMCODE_API_KEY |
| meta | MODEL_API_KEY, then META_API_KEY |
| nanogpt | NANO_GPT_API_KEY |
| novita | NOVITA_API_KEY |
| venice | VENICE_API_KEY |
| vercel-ai-gateway | AI_GATEWAY_API_KEY (also VERCEL_AI_GATEWAY_API_KEY for catalog discovery) |
| cloudflare-ai-gateway | CLOUDFLARE_AI_GATEWAY_API_KEY + CLOUDFLARE_ACCOUNT_ID + CLOUDFLARE_GATEWAY_ID |
| cloudflare-workers-ai | CLOUDFLARE_API_TOKEN, then CLOUDFLARE_AUTH_TOKEN, + CLOUDFLARE_ACCOUNT_ID |
| snowflake | SNOWFLAKE_PAT + SNOWFLAKE_ACCOUNT |
| litellm | LITELLM_API_KEY; optional LITELLM_BASE_URL |
| kilo | KILO_API_KEY |
| zai | ZAI_API_KEY |
| zenmux | ZENMUX_API_KEY |
| zhipu-coding-plan | ZHIPU_API_KEY |
| umans | UMANS_AI_CODING_PLAN_API_KEY |
| qianfan | QIANFAN_API_KEY |
| qwen-portal | QWEN_OAUTH_TOKEN, then QWEN_PORTAL_API_KEY |
| synthetic | SYNTHETIC_API_KEY |
| minimax-code | MINIMAX_CODE_API_KEY |
| minimax-code-cn | MINIMAX_CODE_CN_API_KEY |
| minimax | MINIMAX_API_KEY |
| alibaba-coding-plan | ALIBABA_CODING_PLAN_API_KEY |
| sakana | SAKANA_API_KEY, then FUGU_API_KEY |
| sambanova | SAMBANOVA_API_KEY |
| aimlapi | AIMLAPI_API_KEY |
| gitlab-duo, gitlab-duo-agent | GITLAB_TOKEN |
| opencode-zen, opencode-go | OPENCODE_API_KEY |
| cline-pass | CLINE_API_KEY |
| factory-droid | none; /login factory-droid (device flow) |
| firepass | FIREPASS_API_KEY |
| wafer-serverless | WAFER_SERVERLESS_API_KEY |
| xiaomi | XIAOMI_API_KEY |
| xiaomi-token-plan-ams | XIAOMI_TOKEN_PLAN_AMS_API_KEY |
| xiaomi-token-plan-cn | XIAOMI_TOKEN_PLAN_CN_API_KEY |
| xiaomi-token-plan-sgp | XIAOMI_TOKEN_PLAN_SGP_API_KEY |
| ollama-cloud | OLLAMA_CLOUD_API_KEY |
| ollama | OLLAMA_API_KEY optional; OLLAMA_BASE_URL / OLLAMA_HOST; default http://127.0.0.1:11434 |
| lm-studio | LM_STUDIO_API_KEY optional; LM_STUDIO_BASE_URL; default http://127.0.0.1:1234/v1 |
| llama.cpp | LLAMA_CPP_API_KEY; LLAMA_CPP_BASE_URL; default http://127.0.0.1:8080 |
| vllm | VLLM_API_KEY optional |
| yolo-auto | YOLO_AUTO_API_KEY |
| charm-hyper | CHARM_HYPER_API_KEY, then HYPER_API_KEY |
| singularityapi-dev | SINGULARITYAPI_DEV_API_KEY |
| singularityapi-tech | SINGULARITYAPI_TECH_API_KEY |
| kimi-code, muse-code, google-gemini-cli, google-antigravity | OAuth via /login only (no env var listed) |
| apple | keyless on-device bridge (macOS arm64 only) |

Notes: Autonogrammer-facing rows without an omp id (replicate, hyperbolic, databricks, watsonx, ai21, perplexity, cohere, localai, jan, docker-model-runner, sglang, tgi) need a managed models.yml block. Discovery types accepted: ollama, llama.cpp, lm-studio, openai-models-list, proxy, litellm, apple-foundation-models.
