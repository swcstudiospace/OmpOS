---
description: "Run the Autonogrammer web head against the real API or as a static Demo with a simulated engine, and how to deploy the Demo."
---

# Web head runbook: real API, Demo, and static Demo deployment

Status: Built (the real bundle ran against the local API and the real omp on 2026-10-29, and the Demo is deployed at https://ompos-web.vercel.app, recorded 2026-10-28); the real application is not deployed anywhere

Last verified: 2026-10-29

Applies to: v0.15.0 and the integration branch (Phases 66.2 to 66.4)

The web head (`apps/Autonogrammer.Web`, Blazor WebAssembly) can run in three ways. The decision is [ADR-0017](../decisions/ADR-0017-web-head-demo-mode.md). The only public deployment is the static Demo (simulated engine, no backend, no secret): <https://ompos-web.vercel.app>. The API stays private until the owner deploys it: the first internet-facing mode is the single-operator owner token of [ADR-0021](../decisions/ADR-0021-hosted-single-operator-deployment.md) ([hosting runbook](hosting.md), [deploy and rollback](deploy-and-rollback.md)); multi-user sign-in (OD-03, ADR-0030) is later. The API is not deployed to Railway (a staged private worker is, see the [cloud agents runbook](cloud-agents.md)).

## 1. The real API and the web head, both on this machine

The API refuses the development principal and the in-memory harness outside Development, so this is a loopback-only setup.

```bash
ASPNETCORE_ENVIRONMENT=Development Harness__Adapter=InMemory Sessions__Store=InMemory Coordination__Store=InMemory \
  Api__Auth__AllowDevelopmentPrincipal=true Api__Cors__Origins__0=http://127.0.0.1:5180 \
  dotnet run --project services/Autonogrammer.Api --urls http://127.0.0.1:5187
dotnet publish apps/Autonogrammer.Web -c Release -o /tmp/autonogrammer-web
python3 -m http.server 5180 --bind 127.0.0.1 --directory /tmp/autonogrammer-web/wwwroot
```

Open `http://127.0.0.1:5180/`. The page reads its API origin from `wwwroot/appsettings.json` (`Api:BaseUrl`, default `http://127.0.0.1:5187`); a loopback page may override it with `?apiBaseUrl=http://127.0.0.1:<port>`. The in-memory harness answers with an echo, not a model. `scripts/web-smoke/run-smoke.sh` does the same with a headless browser and axe.

Optional configuration of the API for the web head: `Api:Workspace:Root` (an absolute folder: accepting or rejecting a change then writes the file, only when it still holds the text the agent wrote) and `GIT_SHA` (shown as the source revision).

## 2. The Demo (no backend)

The Demo is a build of the same head whose HTTP client is answered inside the browser.

```bash
scripts/vercel/build-demo.sh            # publishes with -p:AutonogrammerDemo=true into output/demo
scripts/web-demo/serve-demo.sh          # http://127.0.0.1:5190/ (DEMO_PORT to change), loopback only
```

What it shows: the workspace `acme-shop` and six agents: one running (its scripted answer streams markdown, code, a tool call and a diff as soon as the page opens), one waiting for a tool approval, one completed with three changed files, one failed with a retryable error, one closed, and one omp terminal (scripted output). The rail, the Kanban board (ten columns), steering, the model and mode chips, a scripted slash palette, the Changes bar and the stage timeline all work. The Settings dialog has 18 pages: the Models page answers from the real catalogue of 103 provider rows (region, kind and search filters, plan cards, the custom endpoint rules; key entry is disabled and every credential write answers 403 `credential_writes_disabled`), and Rules, Skills, Commands, Hooks, MCP, Runtime and Plugins show the `acme-shop` fixtures (`Demo/DemoWorkspaceSettings`). Sending a prompt gets a scripted reply labelled "Demo". Accepting or rejecting a change, approving a tool, closing and reopening an agent change the in-memory state and nothing else; reloading the page starts over.

Honest limits: no model is called, no file exists, nothing leaves the page, the Content-Security-Policy allows only the page's own origin (`connect-src 'self'`). Changing the model during a running turn is refused with the same 409 the API gives; between turns the Demo allows it.

A normal (non-Demo) build must contain no Demo code. Check it:

```bash
dotnet publish apps/Autonogrammer.Web -c Release -o /tmp/autonogrammer-prod
grep -c DemoApiHandler /tmp/autonogrammer-prod/wwwroot/_framework/Autonogrammer.Web*   # every count must be 0
test ! -e /tmp/autonogrammer-prod/wwwroot/appsettings.demo.json
```

## 3. Deploying the static Demo

`scripts/vercel/build-demo.sh [dir]` writes the published `wwwroot` plus a static `vercel.json` (Content-Security-Policy `connect-src 'self'`, revalidated `_framework` cache (plain file names, no import map), SPA rewrite to `/index.html`). Any static host works; on Vercel:

```bash
scripts/vercel/build-demo.sh
cd output/demo && vercel deploy        # a preview: Deployment Protection stays on; use a Shareable Link to show it
```

Alternatively set `AUTONOGRAMMER_DEMO=1` in a Vercel project that builds from the repository: `vercel.ts` then runs `dotnet publish ... -p:AutonogrammerDemo=true` and needs no `AUTONOGRAMMER_API_ORIGIN`. Without `AUTONOGRAMMER_DEMO=1` the build still fails closed unless `AUTONOGRAMMER_API_ORIGIN` is a valid `https://host[:port]`.

Never point a hosted page at the development API through a tunnel: that would publish the development principal (ADR-0017).

**Recorded deployment (2026-10-28).** The Demo is live at <https://ompos-web.vercel.app>: the Vercel production alias of project `ompos-web` in team `swcstudiospace`. It is the static Demo bundle (simulated engine, no backend, no secret). The repository homepage points to it. Headless Chrome captured it from the live URL with `URL=https://ompos-web.vercel.app node scripts/web-demo/capture.mjs`: no console error and no failed request ([evidence](evidence/web-app-2026-10-28/README.md)). This follows the default recorded in OQ-99.

Deployment record: project id `prj_5clbARYWIOrfgkMSK2Fvw2p0iXAc`. The first deployment, `ompos-px32dymvw-swcstudiospace.vercel.app`, was built from the Wave 5 web tree. After the release gate the Demo was rebuilt from commit `b3d6adc6` (`scripts/vercel/build-demo.sh`, which already carries the Stop control fix of a streaming thread and the accessibility fixes of the gate) and deployed as `dpl_6BbpMM7eVinSxALu6cx4vFCxkLRQ` (`ompos-cv9p9j734-swcstudiospace.vercel.app`), which the production alias <https://ompos-web.vercel.app> now serves; the deployment URLs themselves sit behind Vercel's deployment protection and answer 302, the alias is public. Both were created with `vercel deploy <bundle> --prod --scope swcstudiospace --project ompos-web --yes`. Because the first one was the project's first deployment, Vercel assigned it to Production, which is why the alias is public. To take the Demo down: `vercel remove ompos-web --scope swcstudiospace --yes`, or delete the project in the dashboard; then clear the repository homepage with `gh repo edit swcstudiospace/OmpOS --homepage ""`. To update it: rebuild the bundle and run the same `vercel deploy` command.

## 3a. Capture and smoke

```bash
node scripts/web-demo/capture.mjs                       # serves output/demo, one PNG per screen into /tmp/autonogrammer-shots
THEME=dark W=1440 H=900 PREFIX=D1440- node scripts/web-demo/capture.mjs
URL=https://ompos-web.vercel.app node scripts/web-demo/capture.mjs     # the deployed Demo
scripts/web-smoke/run-smoke.sh                          # SMOKE_TARGETS="api demo": axe on the Demo (68 runs) and the API half
```

Needs a headless Chrome (`CHROME_BIN`, default `google-chrome`). The smoke of the Demo recorded 0 axe violations in 68 runs (Wave 5 tree, 2026-10-28); the API half was not re-run for it.

## 4. Tests

```bash
dotnet test tests/Autonogrammer.Web.Tests
```

The Demo sources are compiled into the test project, so the Demo handler is tested on a manual clock (`ManualTimeProvider`) without a browser or a network. `OpenApiCoverageTests` reads `docs/api/openapi.json`, which the API build regenerates: build the API first.

## 5. The Demo and the real application: what differs and where each gap stands

Recorded 2026-10-29. "Evidence" names a run that exists in the repository; "not exercised" means no run covered it.

| Area | The Demo at the public URL | The real bundle (`scripts/vercel/build-web.sh`) | Evidence | Where it stands |
|------|----------------------------|-----------------------------------------------|----------|-----------------|
| Engine | Every API route is answered in the browser from scripted fixtures (ADR-0017); no agent runs | The API starts `omp acp` per session and streams its events | [hosted-web-2026-10-29](evidence/hosted-web-2026-10-29/README.md): real omp 18.12.6, scripted model | Works on one machine. A deployment needs the owner's approval; a live model turn needs a credential (OQ-172) |
| Sign-in | None | Owner token: its SHA-256 digest is in the API configuration, the browser keeps the token in memory | the same run: `connect`, `credential-storage`, `sign-out`; `Api.IntegrationTests` | Done for one operator. Multi-user sign-in is ADR-0030 (OQ-182) |
| Streaming and resume | Simulated by the scripted fixtures | Server-sent events with sequence numbers; a second connection with `Last-Event-ID` receives the missed events | the same run: `text-turn`, `resume` | Done. A lost connection in a browser, a page reload and an API restart were not exercised |
| Tool approvals | Simulated | The approval endpoints grant a request under `Harness:Acp:Approval` (deny-all by default) | the same run: `tool-turn` (the real omp raised `session/request_permission` for `bash`) | Done. The recipe a deployment should use is OQ-183 |
| What an agent can reach | Nothing | The engine gets an allow-listed environment and a 0700 workspace per session; the API process is non-dumpable | `Api.IntegrationTests` | Done in code; its effect inside a container is not observed (OQ-174). No sandbox provider exists (OQ-173): the host is the boundary |
| Banner and code | "Demo: simulated engine, no real agent, no network" banner; Demo code in the bundle | No banner, no Demo code | `scripts/vercel/check-no-demo.sh` with its positive control | Done |
| Routes, titles, focus, announcements | The same web shell | The same web shell | [web-accessibility-2026-10-29](evidence/web-accessibility-2026-10-29/README.md) | Done as far as scripted checks go; no screen reader pass (OQ-178) |
| Offline shell | Not compared | Never caches cross-origin, `Authorization`, event-stream or `/auth/*` requests | `Web.Tests` (`ServiceWorkerRulesTests`) | Done; the behaviour in a browser was not exercised |
| Public URL | Serves the Demo (`appsettings.demo.json` answered 200 on 2026-10-29) | Not deployed | [deploy and rollback](deploy-and-rollback.md) | The owner decides: a new Vercel project for the real bundle, or the switch of this URL (Q-DEP-1) |
