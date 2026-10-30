# Hosted end-to-end harness (real `omp acp`, scripted model)

No live model turn has ever been run (OD-07): there is no provider credential. This harness proves the rest of the
path anyway, with the real engine and a scripted model, and needs no credential and no network egress.

```
e2e-api.mjs / e2e-browser.mjs  ->  Autonogrammer API  ->  omp acp (real)  ->  fake-model-server.mjs (scripted, loopback)
```

**What a pass proves:** the API starts the real `omp acp` engine, the engine reaches its model, text streams back as
server-sent events with resumable ids, a tool call becomes a permission request that the API enforces and that the
tool-approval endpoints decide, the run completes, the session log persists, authentication (owner token) and the rate
limiter work over HTTP, and the web head can sign in, send a prompt and show the streamed answer with no console error
and no axe violation.

**What it does not prove:** anything about a real model. The fake model only echoes, replies with canned text and
emits canned tool calls. It says nothing about answer quality, real provider streaming quirks, rate limits, tool-use
schemas of a real model, reasoning output, long contexts, cost or latency. OD-07 stays open until one run with a real
provider credential.

Everything is plain Node ESM with `node:` imports only (Node 24, as in the repo). Nothing here runs in CI.

| File | Purpose |
| --- | --- |
| `fake-model-server.mjs` | OpenAI Chat Completions subset: `GET /v1/models`, `POST /v1/chat/completions` (SSE and non-streaming) |
| `prepare-omp-home.mjs` | Throwaway omp agent directory (`models.yml`, `config.yml`) that points omp at the fake model |
| `e2e-api.mjs` | Drives the API over HTTP: auth, session, SSE, approvals, resume, 401, 429 |
| `e2e-browser.mjs` | Drives the web head in headless Chrome over CDP: sign in, prompt, answer, axe, screenshots |
| `sse.mjs` | The SSE reader the scripts share |
| `selftest.mjs` | Verifies the SSE reader and the fake model without omp or the API |

## The scripted model

The reply depends on the last user message. A line that starts with a directive selects the scenario:

| Directive | Reply |
| --- | --- |
| `@say <text>` | `<text>` |
| `@tool <name> <json>` | a call to tool `<name>` with the JSON object as arguments (streamed in fragments); after the tool result arrives, `done: <length of the result text>`. A request that does not offer that tool gets a text reply that says so |
| `@slow <ms>` | waits `<ms>` before every streamed chunk; the reply is the echo unless another directive is present |
| `@error <status>` | HTTP `<status>` (400..599) with an OpenAI-style error body |
| anything else | `You said: <message>` |

The reply is a pure function of the request body (ids are a hash of it); the only timer is `@slow`.

Self-test (no omp, no API, no .NET build):

```sh
node scripts/hosted/selftest.mjs
```

## (a) Local run

Prerequisites: Node 24, `omp` on `PATH` (or its path), the .NET SDK in `$HOME/.dotnet`, Chrome or Chromium for the
browser script. Run everything from the repository root. Use two or three terminals, or put the servers in the
background as shown.

`scripts/hosted/run-local.sh [--web-bundle <dir>] [--out <dir>] [--no-approvals] [--skip <steps>]` does everything below in one
command and removes what it created: the scripted model, the isolated agent directory, a token and its digest, the API (a
Development run with the engine environment on the allow-list, a per-session workspace and the approval policy for `execute`
and `edit`), `e2e-api.mjs`, and, when a published real web bundle is given with `--web-bundle`, a static server and
`e2e-browser.mjs`. It needs a Release build of `services/Autonogrammer.Api` and exits with the status of the checks. The commands
below are the same steps by hand.

```sh
ROOT=$(pwd)
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
OMP=$(command -v omp)

# 1. The scripted model (loopback only).
node scripts/hosted/fake-model-server.mjs --port 18080 --verbose &

# 2. An isolated omp home. Prints PI_CODING_AGENT_DIR, HOME and PI_NO_TITLE; never touches ~/.omp.
OMP_ENV=$(node scripts/hosted/prepare-omp-home.mjs --fake-url http://127.0.0.1:18080/v1)
echo "$OMP_ENV"

# 3. An owner token (32 random bytes, base64url) and the SHA-256 digest the API is configured with.
TOKEN=$(openssl rand 32 | openssl base64 -A | tr '+/' '-_' | tr -d '=')
DIGEST=$(printf %s "$TOKEN" | sha256sum | cut -d' ' -f1)

# 4. A Release build of the API and a scratch working directory (omp runs tools there).
dotnet publish services/Autonogrammer.Api/Autonogrammer.Api.csproj -c Release --no-restore -o /tmp/autonogrammer-api
mkdir -p /tmp/autonogrammer-e2e-work

# 5. The API with the real omp adapter. Production refuses the in-memory stores, so this is a Development run.
(cd /tmp/autonogrammer-e2e-work && env $OMP_ENV \
  ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5187 \
  Sessions__Store=InMemory Coordination__Store=InMemory \
  Harness__Adapter=Acp Harness__Acp__OmpPath="$OMP" \
  Api__Auth__Mode=OwnerToken Api__Auth__OwnerToken__Sha256__0="$DIGEST" \
  "Api__Cors__Origins__0=http://127.0.0.1:5180" \
  DOTNET_ROOT="$DOTNET_ROOT" dotnet /tmp/autonogrammer-api/Autonogrammer.Api.dll) &
until curl -fsS http://127.0.0.1:5187/health/ready >/dev/null; do sleep 1; done

# 6. The API checks. The rate-limit step goes last and drains the session-creation bucket for about a minute.
AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-api.mjs --api http://127.0.0.1:5187 --out /tmp/e2e-api-report.json
```

`env $OMP_ENV` relies on the three generated values containing no spaces (they are `mktemp` paths). `HOME` is replaced
only for the API process (and so for the `omp` it launches), not for your shell, so `dotnet` keeps working; that is
why `DOTNET_ROOT` is passed explicitly.

Check that the API reached the engine and the engine reached the fake model: the `--verbose` log of step 1 prints one
line per chat-completions request (`echo`, `say`, `tool`, ...). If the log stays empty, omp did not read the isolated
agent directory; check that the API passes its environment to `omp acp`.

The browser check needs the published web head pointed at this API (`Api:BaseUrl` is rewritten with the repository
script) and a static server:

```sh
dotnet publish apps/Autonogrammer.Web/Autonogrammer.Web.csproj -c Release --no-restore -o /tmp/autonogrammer-web
AUTONOGRAMMER_API_ORIGIN=http://127.0.0.1:5187 sh scripts/render-web-config.sh /tmp/autonogrammer-web/wwwroot/appsettings.json
python3 -m http.server 5180 --bind 127.0.0.1 --directory /tmp/autonogrammer-web/wwwroot &
AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-browser.mjs --url http://127.0.0.1:5180/ --out /tmp/e2e-browser
```

Stop everything afterwards (`kill %1 %2 ...` or `jobs`) and remove `/tmp/autonogrammer-*`. The token only ever exists
in your shell variable, the API's digest and the browser's memory.

### What the API script asserts

`e2e-api.mjs` uses the real routes (`services/Autonogrammer.Api/Endpoints/*.cs`). Steps, skippable with
`--skip name[,name]`:

| Step | Assertion |
| --- | --- |
| `auth-config` | `GET /auth/config` is 200, anonymous, `Cache-Control: no-store`, and has only `mode`, `authority`, `audience` |
| `unauthenticated` | a request without a credential is 401 (skipped when the mode is `development`) |
| `bad-token` | a wrong bearer token is 401 with a `WWW-Authenticate: Bearer` challenge; the body is empty (as the JWT handler's) or `application/problem+json` with `status` 401 and a `title`; it never echoes the token |
| `text-turn` | `POST /sessions` (201, `Location`), `POST /sessions/{id}/prompts` (202), `GET /sessions/{id}/events` yields numeric, gap-free, growing ids; the SSE event name equals the payload `kind`; `text_delta` events carry the scripted reply; the stream ends with `completed` and the session settles |
| `tool-turn` | `@tool bash {...}`: omp's permission request is handled (see below), the run completes, and the reply after the tool result is `done: <n>` |
| `resume` | a second prompt; the client drops the stream after the first `text_delta`; reconnecting with `Last-Event-ID` continues at the next id with no gap or duplicate, the stitched text is complete, every event matches the session log, and a replay from the turn start returns the same ids |
| `rate-limit` | hammering `POST /sessions` ends in 429 with a numeric `Retry-After` (`--rate-limit-burst`, default 200, must exceed `Api:RateLimit:SessionsPerMinute`) |

Tool approvals. The direct-run adapter denies every permission request unless `Harness:Acp:Approval` allows its kind:
`Kinds` lists the engine's tool kinds that reach the approval endpoints (`read`, `search`, `edit`, `delete`, `move`,
`execute`, ...) and `GrantableTools` lists the tool names an `allow` may grant (`*` means any). With the defaults
(empty lists) nothing is grantable; `run-local.sh` starts the API with `Kinds=execute,edit` and `GrantableTools=*` and
with `--no-approvals` without them. The script handles all three outcomes and reports which one happened:

- `approved`: the request is announced (`tool_approval_requested`), listed by `GET /sessions/{id}/tool-approvals`, and an `allow` is 204;
- `denied-after-ceiling`: `allow` is refused with 403 (the request stays pending), `deny` is 204, a second decision is 409;
- `policy-denied`: the approval policy denied the request before any run saw it; the event log holds the diagnostic "Approval policy denied permission request ...".

`--approval-expect auto` (default) accepts any of them but fails when omp asked for no permission at all (it needs
`tools.approvalMode: always-ask`, which `prepare-omp-home.mjs` writes). Use `approved`, `denied`, `policy-denied` or
`none` to pin one. `run-local.sh` pins `approved` with the policy on and `policy-denied` with `--no-approvals`.

## (b) Against a hosted deployment

The deployment must run `Api:Auth:Mode=OwnerToken` with the digest of a token you hold. The runtime image
(`deploy/api.Containerfile`) ships Bun and `omp` but no Node, so the fake model runs under Bun inside the container
(`bun` runs these `node:`-only scripts unchanged; that is expected, not verified). Everything below is a throwaway:
the fake model, the isolated agent directory and two service variables, all removed at the end.

```sh
# 1. Point the engine at an isolated agent directory under /tmp. The variables redeploy the service, which empties /tmp,
#    so wait until the new deployment is healthy BEFORE step 2. omp reads the directory when it starts, per run.
railway variable set PI_CODING_AGENT_DIR=/tmp/hosted-e2e/omp/agent PI_NO_TITLE=1

# 2. In the new container: copy the two scripts in (base64 on the command line, no stdin needed), write the omp files and
#    start the fake model on loopback.
railway ssh -- sh -c 'mkdir -p /tmp/hosted-e2e'
for f in fake-model-server.mjs prepare-omp-home.mjs; do
  railway ssh -- sh -c "echo $(base64 -w0 scripts/hosted/$f) | base64 -d > /tmp/hosted-e2e/$f"
done
railway ssh -- sh -c 'cd /tmp/hosted-e2e && bun prepare-omp-home.mjs --dir /tmp/hosted-e2e/omp --approval-mode always-ask --shared'
railway ssh -- sh -c 'cd /tmp/hosted-e2e && (nohup bun fake-model-server.mjs --port 18080 > fake.log 2>&1 &)'

# 3. From your machine: the same checks as locally, against the hosted URLs.
AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-api.mjs --api https://<api-host> --out /tmp/e2e-api-report.json
AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-browser.mjs --url https://<web-host>/ --out /tmp/e2e-browser

# 4. Undo. Deleting the variables redeploys the service, which removes /tmp/hosted-e2e and the fake model with it.
railway variable delete PI_CODING_AGENT_DIR
railway variable delete PI_NO_TITLE
```

`--shared` opens the permissions of that directory (it holds only a dummy key) because the shell that creates it may be a
different user from the service account that runs `omp`. `prepare-omp-home.mjs` points the agent at `http://127.0.0.1:18080/v1`, so the model server must run in the same
container as the API. The commands use `railway` 5.x syntax (`railway variable set`, `railway ssh -- <command>`); check
`railway ssh --help` if your CLI differs. Use `--service` / `--environment` if the repository is not linked.

The hosted run uses the deployment's real session store and leaves the sessions it created; the rate-limit step
creates up to `--rate-limit-burst` of them and drains the creation bucket for about a minute. Skip that step
(`--skip rate-limit`) on a deployment you keep, and do not run it while real users depend on the deployment.

The browser script's exit status and `report.json` (steps, axe results, problems, screenshots) are the evidence. It
checks that there is no Demo banner, that no console error, uncaught exception or 4xx/5xx request occurred, that axe
finds no WCAG 2.0/2.1/2.2 A/AA violation on the connect screen, the workbench and the answered thread, that the
token is in no browser storage, and that sign-out followed by a reload returns to the connect screen.

## Verification status of the harness itself

- `selftest.mjs` passes (SSE reader, scenario DSL, fake model over HTTP).
- `omp models find fake` and `omp config get modelRoles` accept the files `prepare-omp-home.mjs` writes (no model turn).
- `e2e-api.mjs` and `e2e-browser.mjs` were dry-run against throwaway mocks of the API routes and of the connect/workbench
  screens, not against the real API, the real engine or the real web head. The first run against them is the real test.
- Not verified: what omp actually sends to a custom `openai-completions` provider during a turn (the fake accepts the
  documented Chat Completions shapes), whether omp requests permission for the `bash` call in ACP mode, and the
  hosted (`railway`) procedure.

## Notes

- Nothing is read from or written to `~/.omp`: `prepare-omp-home.mjs` refuses a target inside it, and the printed
  `HOME` is an empty directory.
- Nothing prints the token; the scripts redact it from every message and refuse to send it over plain http to a
  non-loopback host (`--allow-insecure` overrides).
- Exit status: `0` all steps passed, `1` a step failed, `2` bad usage or no browser. Progress goes to stderr and a JSON
  report to stdout.
- axe-core is the version pinned in `scripts/web-smoke/axe-core-4.14.0.sha256`; `e2e-browser.mjs` downloads it with
  `npm pack` into the system temp directory (or uses `--axe-js` / `AXE_JS`) and refuses a file whose digest differs.
