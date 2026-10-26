# Railway as the substrate for a per-session serverless Linux desktop

Research date: 2026-10-22. All pages below were read with the read tool on that date unless the row says otherwise. Quotes are kept under fifteen words; statements are paraphrased.

## Verdict

Railway's documentation describes no desktop, display-server, VNC or browser-streaming product, and states nothing about graphical sessions on any compute type. Of the three options: (b) a Railway Function is not viable as a desktop host, because a Function is one TypeScript file of at most 96 KB running on Bun, with no documented control of the image, system packages or start command. (a) is achievable in documented terms in two forms. The stronger fit is a Railway Sandbox per session: an isolated Linux VM created and destroyed at runtime through the SDK, CLI or GraphQL, available on every plan, with optional HTTPS public domains (only in PRIVATE network mode), SSH port forwarding, and checkpoints. The alternative is one Docker-image Railway service per session, with the Serverless sleep feature and TCP or HTTP exposure; this works on paper but makes session lifecycle, idle sleep and API rate limits the owner's problem. (c) "not achievable" does not follow from the docs, but there is a gating policy issue: Railway's Trust and Safety page lists "remote desktops for personal or professional browsing" as prohibited, and the Acceptable Use Policy prohibits operating proxies or anonymization services. An agent-driven, browser-enabled desktop used for the owner's work may fall inside that wording. Railway does not say either way. **Recommendation:** prototype only a Sandbox-based desktop (Xvfb plus a VNC or noVNC stack installed through a sandbox template), and do not build production on it until Railway confirms in writing (Central Station or abuse@railway.com, which the docs invite) that this use is allowed. Treat GUI feasibility inside a sandbox as unverified until a prototype runs. A paused desktop cannot keep running applications: checkpoint and restore, and fork, both boot fresh and drop processes and memory.

## Table

### A. Facts stated by Railway

| # | Topic | Stated fact (paraphrase) | Source URL | Verified (opened) | Date read |
|---|---|---|---|---|---|
| 1 | Docker image service | Services deploy from a GitHub repo, a local directory, or a Docker image; Docker Hub, GHCR, Quay, GitLab and MCR paths are given as examples. | https://docs.railway.com/services.md | yes | 2026-10-22 |
| 2 | Docker image update | Railway monitors image tags and offers an update; untagged images redeploy the latest digest. | https://docs.railway.com/services.md | yes | 2026-10-22 |
| 3 | Service files | Files outside a volume are ephemeral and do not persist across deployments. | https://docs.railway.com/services.md | yes | 2026-10-22 |
| 4 | Serverless: scope | A per-service toggle (Settings > Deploy > Serverless). It takes effect only when the container is created, so a redeploy is needed. | https://docs.railway.com/deployments/serverless.md ; https://docs.railway.com/pricing/cost-control.md | yes | 2026-10-22 |
| 5 | Serverless: sleep rule | A service is treated as inactive when it sends no outbound packets (any kind, including NTP and DB connections) for 5 minutes; sampling means it sleeps 5 to 10 minutes after the last outbound traffic. Inbound traffic is not measured directly; replies to it are. | https://docs.railway.com/deployments/serverless.md | yes | 2026-10-22 |
| 6 | Serverless: wake | Woken by inbound internet traffic or private-network traffic from the same project. The first request after sleep has a cold-boot delay and may return 502. | https://docs.railway.com/deployments/serverless.md | yes | 2026-10-22 |
| 7 | Serverless: WebSocket | Railway's own Jupyter guide states that open JupyterLab tabs keep WebSocket traffic flowing, so the server only sleeps once tabs close. This is stated for that application; the general rule for an idle WebSocket with no frames is not stated. | https://docs.railway.com/guides/jupyter-server-team.md | yes | 2026-10-22 |
| 8 | Serverless: caveats | Static outbound IPs can keep a service awake; enabling applies to all replicas; slept services still hold a slot on infrastructure and may need a rebuild in rare cases. | https://docs.railway.com/deployments/serverless.md | yes | 2026-10-22 |
| 9 | Volumes: basics | Persistent per-service storage at a mount path; mounted at container start, not during build; mounted as root (set RAILWAY_RUN_UID=0 for non-root images); live resize on paid plans. | https://docs.railway.com/volumes.md | yes | 2026-10-22 |
| 10 | Volumes: size by plan | Trial and Free 0.5 GB; Hobby 5 GB; Pro up to 1 TB self-serve; Enterprise 5 TB. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 11 | Volumes: price | Volume storage $0.15 per GB per month. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 12 | Volumes: region | A volume follows its service's region; changing the region of a service with a volume requires migration, with downtime. | https://docs.railway.com/deployments/regions.md | yes | 2026-10-22 |
| 13 | Volumes: retention | After a plan lapses, volume data is deleted after 30 days (Free or Trial), 60 days (Hobby) or 90 days (Pro). | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 14 | TCP proxy | Exposes a raw TCP port as a generated domain and port; traffic is spread randomly over replicas in the closest region; custom domain via CNAME (Cloudflare must be DNS-only); can coexist with HTTP on one service; also usable on the private network. | https://docs.railway.com/networking/tcp-proxy.md | yes | 2026-10-22 |
| 15 | HTTP domains | HTTP and HTTP/2 supported; HTTPS required (TLS 1.2 or later; SNI mandatory); plain HTTP GET redirected with 301. Certificates are Let's Encrypt, 90 days, renewed at 30 days remaining. | https://docs.railway.com/networking/public-networking/specs-and-limits.md | yes | 2026-10-22 |
| 16 | WebSocket limits | Supported over HTTP/1.1 upgrade. Exempt from the 5-minute no-data and 15-minute maximum that apply to ordinary HTTP requests; may stay open indefinitely while idle. Idle HTTP/1.1 keep-alive connections close at 60 s, not applied to WebSockets. | https://docs.railway.com/networking/public-networking/specs-and-limits.md | yes | 2026-10-22 |
| 17 | Edge limits | 10,000 concurrent connections; about 11,000 HTTP requests per second per domain; 10,000 requests per connection; 32 KB combined header size; request bodies must finish uploading within 5 minutes. Higher limits by request. | https://docs.railway.com/networking/public-networking/specs-and-limits.md | yes | 2026-10-22 |
| 18 | DDoS and WAF | Layer-4 attacks mitigated at network level; layer-7 limits may not suffice; a WAF "Under Attack Mode" is referenced. | https://docs.railway.com/networking/public-networking/specs-and-limits.md | yes | 2026-10-22 |
| 19 | Per-service ceilings | Maximums per service including replicas: Free 1 replica, 0.5 GB RAM, 1 vCPU; Trial 2, 1 GB, 2 vCPU; Hobby 6, 48 GB, 48 vCPU; Pro 42, 1 TB, 1,000 vCPU; Enterprise 50, 2.4 TB, 2,400 vCPU. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 20 | Image size | Free and Trial 4 GB; Hobby 100 GB; Pro and Enterprise unlimited. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 21 | Container pricing | RAM $10 per GB per month; CPU $20 per vCPU per month; egress $0.05 per GB; builds free. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 22 | VM pricing | Sandboxes and cloud agents bill at VM rates: RAM $50 per GB per month and vCPU $50 per vCPU per month, metered per second on usage, not allocation; egress $0.05 per GB. Memory billed while idle-but-running. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 23 | Plans | Free $0 per month; Hobby $5 per month including $5 of usage; Pro $20 per month including $20 of usage. | https://docs.railway.com/pricing/plans.md | yes | 2026-10-22 |
| 24 | Spend guard | Workspace usage limits: a hard compute limit takes workloads offline; the agent limit disables only the agent. Settable from the dashboard or `railway usage limit set`. | https://docs.railway.com/pricing/cost-control.md | yes | 2026-10-22 |
| 25 | Replica limits | Per-service CPU and memory caps; set too low, the service crashes. | https://docs.railway.com/pricing/cost-control.md | yes | 2026-10-22 |
| 26 | Regions | Four regions: us-west2 (California), us-east4-eqdc4a (Virginia), europe-west4-drams3a (Amsterdam), asia-southeast1-eqsg3a (Singapore). | https://docs.railway.com/deployments/regions.md | yes | 2026-10-22 |
| 27 | Functions: what | A Function is a service running one TypeScript file on Bun, with no repository. Bun APIs and npm imports are supported; volumes can be attached. | https://docs.railway.com/functions.md | yes | 2026-10-22 |
| 28 | Functions: limits | One file per function; maximum file size 96 KB. | https://docs.railway.com/functions.md | yes | 2026-10-22 |
| 29 | Functions: runtime model | The IaC runtime defaults to `bun`, described as the only runtime; the image is `function-bun`. Railway stores the code base64-encoded in the start command. | https://docs.railway.com/functions.md | yes | 2026-10-22 |
| 30 | Functions: install model | Railway writes the file as `index.tsx`, derives a `package.json` from imports, and runs `bun install` at every start. | https://docs.railway.com/observability/tracing/functions.md | no (seen in the llms-full.txt copy of docs.railway.com only) | 2026-10-22 |
| 31 | Sandboxes: what | Isolated Linux VMs created on demand, run commands in, and destroy. Scoped to an environment. Available on every plan. Default image is Ubuntu with git, Node and common coding agents. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 32 | Sandboxes: system packages | Templates can install system packages through apt-get (`withPackages`); templates are built once and cached. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 33 | Sandboxes: GUI | No mention of a display server, VNC, noVNC or GPU in the sandbox documentation. | https://docs.railway.com/sandboxes.md | yes (absence checked across the full doc set) | 2026-10-22 |
| 34 | Sandboxes: network | Outbound internet via NAT always. Default ISOLATED: no private-network access in either direction. PRIVATE: joins environment network (for example `postgres.railway.internal`) and keeps outbound. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 35 | Sandboxes: public domains | Only with PRIVATE mode. Up to 10 domains, ports 1 to 65535, Railway-provided HTTPS, set at creation only; removed on destroy. Documented for HTTP servers; the docs do not say WebSocket upgrade works. | https://docs.railway.com/sandboxes.md ; https://docs.railway.com/cli/sandbox.md | yes | 2026-10-22 |
| 36 | Sandboxes: port forward | `railway sandbox forward` maps a local port to a sandbox port over SSH; needs an SSH key on the account; keeps the sandbox alive while connected. | https://docs.railway.com/sandboxes.md ; https://docs.railway.com/cli/sandbox.md | yes | 2026-10-22 |
| 37 | Sandboxes: idle timeout | Hobby and Pro: default 30 min, finite 1 to 120 min, 0 disables. Trial and Free: default 5 min, 1 to 5 min only, cannot be disabled. Keepalive via `heartbeat()` or the CLI SSH and forward sessions. A background process alone does not keep it alive. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 38 | Sandboxes: size | Trial and Free: default and max 2 vCPU / 2 GB. Hobby: default 4 / 4 GB, max 8 / 8. Pro: default 8 / 8, max 32 / 32. Enterprise by agreement. Fractional vCPU allowed. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 39 | Sandboxes: count cap | Concurrent sandboxes per environment: Trial and Free 10; Hobby 50; Pro 100. Counts only CREATING and RUNNING. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 40 | Sandboxes: checkpoint | Named server-side snapshot of the disk; boots fresh from a copy; files kept, processes and memory not. Source sandbox need not exist. Count shares the plan cap. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 41 | Sandboxes: fork | Clones a running sandbox's disk into an independent sandbox; processes and memory not carried; same region; settings not inherited. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 42 | Sandboxes: env | `env` values are baked in and visible to every command for the sandbox's life; may reference Railway variables; secrets allowed; not inherited by forks. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 43 | Sandboxes: region | Default us-west2 for fresh sandboxes; `region` settable via SDK or API (CLI has no flag); forks and checkpoint restores stay where the data lives. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 44 | Sandboxes: long commands | `exec` runs independently of the client; a durable session name lets a client detach and reattach from another process. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 45 | Sandboxes: SDK | `railway` npm package, Node 22+ required; `Sandbox.create`, `connect`, `list`, `exec`, `files`, `destroy`, `heartbeat`, `fork`, `checkpoint`. | https://docs.railway.com/sandboxes.md | yes | 2026-10-22 |
| 46 | Services: runtime create | GraphQL `serviceCreate` takes a GitHub repo, a Docker image or no source; `serviceInstanceDeployV2`, `serviceInstanceRedeploy` and `serviceInstanceLimits` exist. | https://docs.railway.com/integrations/api/manage-services.md | yes | 2026-10-22 |
| 47 | Services: runtime delete | `serviceDelete` deletes the service and all its deployments (marked danger). CLI `railway service delete` removes a service from an environment. | https://docs.railway.com/integrations/api/manage-services.md ; https://docs.railway.com/cli/service.md | yes | 2026-10-22 |
| 48 | Services: CLI create | `railway add --image`, `--service`, `--repo`; `railway service source connect --image`. | https://docs.railway.com/cli/add.md ; https://docs.railway.com/cli/service.md | yes | 2026-10-22 |
| 49 | API rate limits | Requests per hour: Free 100, Hobby 1,000, Pro 10,000. Requests per second: Hobby 10, Pro 50. Over-limit returns HTTP 429 with Retry-After. | https://docs.railway.com/integrations/api.md | yes (read at the rate-limit section) | 2026-10-22 |
| 50 | Public API endpoint | GraphQL at backboard.railway.com/graphql/v2; account, workspace, project and environment token types. | https://docs.railway.com/integrations/api.md | yes | 2026-10-22 |
| 51 | Cloud agents | A personal VM per user in a project environment; Ubuntu image with Claude Code, Codex, Chromium, Node, Python and SSH access; public HTTPS domain on port 8080; sleep stops compute and keeps disk; no idle timeout. The docs do not describe a graphical desktop. | https://docs.railway.com/cloud-agents.md ; https://docs.railway.com/cloud-agents/manage.md | yes | 2026-10-22 |
| 52 | Cloud agents: size and limits | VM size: Free 2 vCPU / 1 GB; Hobby 2 / 2 GB; Pro 4 / 4 GB. Creation capped at 25 agents per user per day. Feature is in Priority Boarding (beta). | https://docs.railway.com/cloud-agents.md | yes | 2026-10-22 |
| 53 | Private networking | Services communicate over WireGuard with internal DNS; no configuration needed. | https://docs.railway.com/guides/running-agents-on-railway.md | yes | 2026-10-22 |
| 54 | Trust and Safety | Prohibits "operating proxies, VPNs, anonymization services, or remote desktops for personal or professional browsing". Reverse proxies and private networking tools that serve the user's own services are allowed. Restrictions can affect a service, a feature or a workspace; appeal is available once. | https://docs.railway.com/platform/trust-and-safety.md | yes | 2026-10-22 |
| 55 | Trust and Safety: how to ask | Railway invites users to ask via Central Station or abuse@railway.com before deploying when unsure. | https://docs.railway.com/platform/trust-and-safety.md | yes | 2026-10-22 |
| 56 | Acceptable Use Policy | Prohibits, among other things, operating proxies or anonymization services, reselling compute, evading usage or billing limits, and bots or scrapers that break terms of service. Also cryptocurrency mining and torrenting. | https://railway.com/legal/acceptable-use | yes | 2026-10-22 |

### B. Options for a per-session serverless Linux desktop

| Option | Verified facts | Stated limits | Fit and risk |
|---|---|---|---|
| (a1) Sandbox per session | Rows 31 to 45. Isolated VM, runtime create and destroy, optional HTTPS domain, SSH forward, checkpoint and fork, per-environment cap. | Idle 30 min default on Hobby and Pro (max 120, or 0 for none); 5 min only on Free; size 8/8 Hobby, 32/32 Pro; 50 or 100 concurrent per environment. Pause loses processes. Desktop support not stated. | Best primitive for one VM per session. Needs a prototype to prove VNC or noVNC runs and the public domain carries the view (WebSocket not stated). Exposure and policy questions apply (see section C). |
| (a2) Docker-image service per session | Rows 1 to 8, 14 to 17, 46 to 49. Image deploy, runtime create and delete via API or CLI, TCP proxy or HTTP domain, WebSocket exemption, Serverless sleep. | Serverless sleep after 5 to 10 min with no outbound traffic; cold boot and possible 502 on first request; WebSocket traffic counts as activity. Service creation at runtime limited by API rate limits (10 RPS on Hobby). Service count on paid plans not stated. | Works, but every session becomes a deploy, the Serverless sleep is not a session pause, and volumes are per service. Good for shared or long-lived desktops, poor for one-shot sessions. |
| (b) Railway Function | Rows 27 to 30. One Bun TypeScript file, 96 KB, auto-installed npm packages. | No documented image control, system packages, binary execution, GUI, timeout or memory limit. Not stated. | Not viable as a desktop host as documented. |
| (c) Not achievable | None: (a1) and (a2) are documented as possible. | The policy rows 54 to 56 are a conditional blocker, not a technical one. | Not the right conclusion unless Railway rules the use out. |

### C. Security implications of running an agent inside a desktop

| # | Implication | Basis | Verified |
|---|---|---|---|
| S1 | A desktop with a browser used by an agent may be treated as a prohibited "remote desktop" under the Trust and Safety wording. A restriction can take the workspace offline. | Row 54 and row 56. | yes (wording); application to this use unverified |
| S2 | Public sandbox domains and service domains are reachable from the internet. Railway documents no authentication layer for them; the app must authenticate (for example VNC password plus token on the noVNC page). | Rows 15, 35. The absence of an auth feature is not stated, so it is unverified that none exists. | partly |
| S3 | PRIVATE network mode lets the sandbox reach internal services, and they reach it. Default ISOLATED limits blast radius. | Row 34. | yes |
| S4 | Env values are visible to every command for the life of the sandbox; secrets baked there persist until destroy. | Row 42. | yes |
| S5 | A checkpoint is a snapshot of the whole disk, so anything an agent saved to disk (tokens, OAuth state, SSH keys) is in every sandbox booted from it, environment-wide. This is an inference from the snapshot definition in row 40. | Row 40. | inference |
| S6 | Outbound internet is always available through NAT; no egress allow-list is documented. Egress is billed at $0.05 per GB. | Rows 34, 22. | yes (no allow-list stated: unverified absence) |
| S7 | Sandbox isolation is described as "fully isolated" VM; the isolation model for Railway services is not stated. | Row 31; services isolation not stated. | partly |
| S8 | Railway's CLI refuses service-file deletion when an AI agent invokes it. This shows Railway guards destructive actions against agents; it does not cover sandboxes. | cli/service.md, read. | yes |

## Owner decisions

1. **Compute primitive.** Options: (A) Sandbox per session (recommended for prototype); (B) Docker-image service per session with Serverless; (C) Function (not recommended).
2. **Policy confirmation before building.** Options: (A) ask Railway (Central Station or abuse@railway.com) whether an agent-driven browser desktop is permitted (recommended, before any build); (B) build on the assumption it is allowed and accept the restriction risk; (C) drop the desktop and use headless sessions (Cloud agents, row 51, or a sandbox with a headless browser).
3. **Plan tier.** Options: (A) Hobby (50 concurrent sandboxes per environment, max 8 vCPU / 8 GB); (B) Pro (100 per environment, max 32 / 32); (C) Enterprise (limits by agreement). Free is not suited: 5-minute idle cap and no disable.
4. **Idle and lifetime policy.** Options: (A) default 30 min idle with heartbeats while a client is attached; (B) `idleTimeoutMinutes: 0` (no idle teardown, billing continues until destroy); (C) short idle with checkpoint-and-destroy between sessions (loses running apps).
5. **Remote view transport.** Options: (A) noVNC or WebSocket over the sandbox HTTPS domain (WebSocket support unverified); (B) SSH port forward from the owner's machine, nothing public (requires SSH key on the account); (C) TCP proxy on a service (raw VNC; no encryption or auth documented).
6. **Network isolation.** Options: (A) ISOLATED (default); (B) PRIVATE, when the desktop must reach internal services.
7. **Session persistence.** Options: (A) accept that pause or restore drops running applications; (B) keep sandboxes alive with a long idle timeout and pay for memory while idle.
8. **Credential handling.** Options: (A) inject per-session secrets through `env` at create time; (B) never checkpoint sessions that hold credentials; (C) use a dedicated low-privilege account for the agent.
9. **Spend guard.** Options: (A) set a workspace hard compute limit (takes all workloads offline when hit); (B) rely on per-environment sandbox caps only.

## Unverified

- GUI, X11, Wayland, VNC and noVNC support on any Railway compute type: not stated in any page searched (full docs text searched for desktop, VNC, noVNC, Xvfb, X11, graphical, display).
- Whether sandbox public domains proxy WebSocket upgrades, and whether the edge limits in row 16 and 17 apply to sandbox domains: not stated.
- Sandbox disk size, GPU availability, boot latency, nested Docker and privileged operations: not stated.
- Whether a sandbox's maximum command duration is limited: the SDK says no timeout unless set; a platform maximum is not stated.
- Function request timeout, memory, CPU, and whether a Function can execute binaries present in the image: not stated.
- Serverless behaviour for an open WebSocket with no frames: only the Jupyter guide statement (row 7) exists; the general rule is not stated.
- Whether Serverless applies to Functions or sandboxes: not stated (sandboxes have their own idle timer).
- TCP proxy: UDP support, encryption, and access control: not stated.
- Authentication options for public domains on services and sandboxes: not stated; the WAF page was referenced but not opened.
- Service count per project on Hobby and Pro: not stated (the 5-services limit appears only for the Trial in the llms-full copy; not opened separately).
- Whether a volume can attach to more than one service: not stated.
- Isolation level of services (VM or container): not stated.
- Whether Railway would treat an agent-driven browser desktop as a prohibited remote desktop: cannot be determined from documents; needs Railway's answer.
- Cloud agents: no desktop or remote-view support is documented; not stated either way.

## Sources

Opened with the read tool on 2026-10-22 (verified yes):

- https://docs.railway.com/services.md
- https://docs.railway.com/deployments/serverless.md
- https://docs.railway.com/guides/jupyter-server-team.md
- https://docs.railway.com/pricing/cost-control.md
- https://docs.railway.com/volumes.md
- https://docs.railway.com/networking/tcp-proxy.md
- https://docs.railway.com/networking/public-networking.md
- https://docs.railway.com/networking/public-networking/specs-and-limits.md
- https://docs.railway.com/pricing/plans.md
- https://docs.railway.com/deployments/regions.md
- https://docs.railway.com/functions.md
- https://docs.railway.com/sandboxes.md
- https://docs.railway.com/cli/sandbox.md
- https://docs.railway.com/cli/service.md
- https://docs.railway.com/cli/add.md
- https://docs.railway.com/cloud-agents.md
- https://docs.railway.com/cloud-agents/manage.md
- https://docs.railway.com/integrations/api.md
- https://docs.railway.com/integrations/api/manage-services.md
- https://docs.railway.com/guides/running-agents-on-railway.md
- https://docs.railway.com/platform/trust-and-safety.md
- https://docs.railway.com/deployments/scaling.md (opened; not cited in rows)
- https://railway.com/legal/acceptable-use

Seen only in the llms-full.txt copy (verified no): https://docs.railway.com/observability/tracing/functions.md (row 30). Page URL: https://docs.railway.com/llms-full.txt (searched for desktop/VNC/WebSocket/limit terms, 2026-10-22).

Not opened, not cited: docs.railway.com/reference/volumes.md returned HTTP 404 on 2026-10-22.
