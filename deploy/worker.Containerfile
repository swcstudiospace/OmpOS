# Autonogrammer agent worker image (Autonogrammer.AgentWorker). Build with podman from the repository root:
#   podman build -f deploy/worker.Containerfile -t autonogrammer-agent-worker:local .
# Run it locally (loopback only; the public key is the PEM of the control plane's token signing key):
#   podman run --rm -p 127.0.0.1:8088:8080 -v autonogrammer-worker-data:/data \
#     -e Worker__Auth__PublicKeysPem__0="$AUTONOGRAMMER_WORKER_JWT_PUBLIC_KEY_PEM" -e Worker__Auth__Issuer=ompos-api \
#     -e Worker__WorkspaceRoot=/data/work -e PORT=8080 localhost/autonogrammer-agent-worker:local
#
# Base image tags (identical to deploy/api.Containerfile):
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0.402-noble     (global.json pins SDK 10.0.402 exactly)
#   Runtime: mcr.microsoft.com/dotnet/aspnet:10.0.14-noble   (shipped runtime; the 10.0.13 bundled with SDK 10.0.402 carries GHSA-p3jp-9v83-76w9, fixed in 10.0.14)
# The noble (Ubuntu 24.04) variant is used rather than chiseled because the entrypoint is a /bin/sh script and
# the worker needs git, openssh-client and curl. Chiseled images contain no shell and no package manager.

# Digests resolved from mcr.microsoft.com (manifest-list digest, HTTP Docker-Content-Digest header): SDK 2026-10-29, runtime 2026-11-08. Refresh with the tag bump.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:1a48394b6f29d3a32c79d9edecd4abda0ecbe8776d567dbfc4590ed6979c740c

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Repository-wide build inputs: SDK pin, central package versions, analyzer and style inputs
# (.editorconfig and the banned-symbol AdditionalFiles are read by the warnings-as-errors build).
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig BannedSymbols.txt BannedSymbols.NoProcess.txt ./
# Project graph of Autonogrammer.AgentWorker (it references src/Autonogrammer.Harness.Acp and src/Autonogrammer.Harness.Abstractions).
COPY src/ src/
COPY services/Autonogrammer.AgentWorker/ services/Autonogrammer.AgentWorker/

# Locked restore: every project in the graph has a committed packages.lock.json.
RUN dotnet restore services/Autonogrammer.AgentWorker/Autonogrammer.AgentWorker.csproj --locked-mode
RUN dotnet publish services/Autonogrammer.AgentWorker/Autonogrammer.AgentWorker.csproj -c Release -o /app --no-restore

# omp runtime for the worker: the worker launches the `omp` CLI (`omp acp`), which this stage supplies.
#
# Pins (identical to deploy/api.Containerfile; each read from its source; see docs/runbooks/hosting.md, "Known blockers and gaps", item 2):
#   Bun 1.4.4, asset bun-linux-x64.zip, SHA-256 bf8ccbfb2b0b80159e0f9a6d15c55317add8077534dbc6130a3edb52fccb395c
#     Source: oven-sh/bun release bun-v1.4.4 SHASUMS256.txt (the zip's sha256 was checked against that file). omp requires bun >=1.4.4 (engines.bun) and
#     dist/cli.js starts with #!/usr/bin/env bun.
#   @oh-my-pi/pi-coding-agent 18.18.1, npm dist.integrity sha512-+ZTKxJofs5NTL2ENS/6rqrm5tB8MbRHyTAJ00SQEVqMj8woQA/zV+ubzXxxXXlp6IPmA626vjCDmEg/Kf/CaHw==
#     Source: registry.npmjs.org tarball pi-coding-agent-18.18.1.tgz (its sha512 hex equals the integrity value above).
#     The tarball is checked against the sha512 hex form of that integrity value before extraction.
#
# NOT FROZEN: the omp package ships no lockfile (no package-lock.json, bun.lock or yarn.lock in its tarball).
# `bun install` therefore resolves transitive dependency ranges at build time, and it writes an uncommitted
# bun.lock inside the image. Only the omp tarball and the Bun zip are pinned. Do not commit an invented lockfile.
FROM ${SDK_IMAGE} AS omp-runtime
ARG BUN_VERSION=1.4.4
ARG BUN_ZIP_SHA256=bf8ccbfb2b0b80159e0f9a6d15c55317add8077534dbc6130a3edb52fccb395c
ARG OMP_VERSION=18.18.1
ARG OMP_TGZ_SHA512=f994cac49a1fb393532f610d4bfeabaab9b9b41f0c6d11f24c0274d1240456a323f30a1003fcd5fae6f35f1c575e5a7a20f980eb6eaf8c20e6120fca7ff09a1f
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl unzip \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /tmp/omp-build
RUN curl -fsSL -o bun.zip "https://github.com/oven-sh/bun/releases/download/bun-v${BUN_VERSION}/bun-linux-x64.zip" \
 && echo "${BUN_ZIP_SHA256}  bun.zip" | sha256sum -c - \
 && unzip -q bun.zip \
 && install -m 0755 bun-linux-x64/bun /usr/local/bin/bun \
 && test "$(/usr/local/bin/bun --version)" = "${BUN_VERSION}" \
 && rm -rf bun.zip bun-linux-x64
RUN curl -fsSL -o omp.tgz "https://registry.npmjs.org/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-${OMP_VERSION}.tgz" \
 && echo "${OMP_TGZ_SHA512}  omp.tgz" | sha512sum -c - \
 && mkdir -p /opt/omp \
 && tar -xzf omp.tgz -C /opt/omp --strip-components=1 \
 && rm omp.tgz
RUN cd /opt/omp \
 && BUN_INSTALL_CACHE_DIR=/tmp/bun-cache /usr/local/bin/bun install --production \
 && chmod 0755 /opt/omp/dist/cli.js \
 && rm -rf /tmp/bun-cache /tmp/omp-build

# Dependency drift gate: the resolved top-level omp dependencies must equal deploy/omp-deps.lock.json
# (scripts/ci/snapshot-omp-deps.sh). With OMP_DEPS_MODE=write the build writes /tmp/omp-deps.lock.json instead, to
# produce the committed snapshot. While the committed file says "pending": true the check only warns (first podman build).
ARG OMP_DEPS_MODE=check
COPY scripts/ci/snapshot-omp-deps.sh /opt/snapshot-omp-deps.sh
COPY deploy/omp-deps.lock.json /opt/omp-deps.committed.json
RUN if [ "${OMP_DEPS_MODE}" = "write" ]; then \
      bash /opt/snapshot-omp-deps.sh write /opt/omp /tmp/omp-deps.lock.json; \
    else \
      bash /opt/snapshot-omp-deps.sh check /opt/omp /opt/omp-deps.committed.json; \
    fi \
 && rm -f /opt/snapshot-omp-deps.sh /opt/omp-deps.committed.json

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app
# apt 2.8.3 is in the base image; CVE-2026-109518 is fixed in 2.8.4ubuntu0.1. Upgrade apt alone; nothing else is installed.
RUN apt-get update \
 && apt-get install -y --only-upgrade apt \
 && rm -rf /var/lib/apt/lists/*

# git and openssh-client: the agent clones and pushes repositories. ca-certificates: TLS to model providers and git hosts.
# curl: the container health check calls GET /healthz. setpriv (used by the entrypoint)
# comes from util-linux, which is Essential on Ubuntu noble and therefore already present; it is not installed here.
RUN apt-get update \
 && apt-get install -y --no-install-recommends git openssh-client ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*

# Service account with fixed numeric IDs, so the platform can enforce the user without name lookup. Its home lives on
# the volume (/data/home) so omp state (HOME/.omp), git config and ssh state survive a sleep; the volume is mounted at
# /data. /data is created owned by the account so a fresh local named volume inherits that ownership.
RUN groupadd --system --gid 10001 autonogrammer \
 && useradd --system --uid 10001 --gid autonogrammer --home-dir /data/home --no-create-home --shell /usr/sbin/nologin autonogrammer \
 && install -d -m 0755 -o 10001 -g 10001 /data
# The app is root-owned and read-only to uid 10001.
COPY --from=build /app ./
# omp for the worker. Bun is the runtime omp needs; the omp package is root-owned and read-only to uid 10001.
COPY --from=omp-runtime /usr/local/bin/bun /usr/local/bin/bun
COPY --from=omp-runtime /opt/omp /opt/omp
RUN ln -s /opt/omp/dist/cli.js /usr/local/bin/omp
COPY --chown=0:0 --chmod=0755 deploy/worker-entrypoint.sh /usr/local/bin/worker-entrypoint.sh

# PI_CODING_AGENT_DIR is deliberately not set: omp keeps its state under HOME/.omp, which is on the volume.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    HOME=/data/home \
    AUTONOGRAMMER_VOLUME_PATH=/data

# Deliberately NO `USER` instruction: the image starts as root ONLY so the entrypoint can chown the volume mount
# point and then drop to uid/gid 10001 with setpriv before the worker starts. Railway mounts volumes root-owned
# (https://docs.railway.com/volumes.md); the alternative is to set RAILWAY_RUN_UID=0 on the service, which keeps the
# whole process root and is therefore not used. Started as a non-root user, the entrypoint skips the chown.

# Documents the default port only; the entrypoint binds to PORT (set by the platform) or 8080.
EXPOSE 8080

# Liveness probe without curl or wget (the aspnet base has neither): bash /dev/tcp sends a bare HTTP request and
# the status line must be 200. NOT RUN here: written without a podman build; verify with `podman healthcheck run`.
# HEALTHCHECK exists only in the docker image format: build with `podman build --format docker` (the OCI format drops it).
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD ["/bin/bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/${PORT:-8080} && printf 'GET %s HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' \"/healthz\" >&3 && head -n 1 <&3 | grep -q ' 200 '"]

LABEL org.opencontainers.image.title="autonogrammer-agent-worker" \
      org.opencontainers.image.description="Autonogrammer cloud agent worker: serves GET /healthz and the ACP WebSocket GET /acp, runs omp against a workspace on the volume" \
      org.opencontainers.image.source="https://github.com/swcstudiospace/OmpOS" \
      org.opencontainers.image.licenses="AGPL-3.0-or-later"

# The entrypoint maps PORT onto ASPNETCORE_URLS (dual-stack http://[::]:PORT; Railway private networking needs IPv6),
# repairs volume ownership, then drops privileges and execs the worker, so the .NET process is PID 1 and receives
# termination signals. With no arguments it runs `dotnet /app/Autonogrammer.AgentWorker.dll`.
ENTRYPOINT ["/usr/local/bin/worker-entrypoint.sh"]
