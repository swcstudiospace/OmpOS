# Autonogrammer agent worker image (Autonogrammer.AgentWorker). Build with podman from the repository root:
#   podman build -f deploy/worker.Containerfile -t autonogrammer-agent-worker:local .
# Run it locally (loopback only; the public key is the PEM of the control plane's token signing key):
#   podman run --rm -p 127.0.0.1:8088:8080 -v autonogrammer-worker-data:/data \
#     -e Worker__Auth__PublicKeysPem__0="$AUTONOGRAMMER_WORKER_JWT_PUBLIC_KEY_PEM" -e Worker__Auth__Issuer=autonogrammer-api \
#     -e Worker__WorkspaceRoot=/data/work -e PORT=8080 localhost/autonogrammer-agent-worker:local
#
# Base image tags (identical to deploy/api.Containerfile):
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0.402-noble     (global.json pins SDK 10.0.402 exactly)
#   Runtime: mcr.microsoft.com/dotnet/aspnet:10.0.13-noble   (the runtime bundled with SDK 10.0.402)
# The noble (Ubuntu 24.04) variant is used rather than chiseled because the entrypoint is a /bin/sh script and
# the worker needs git, openssh-client and curl. Chiseled images contain no shell and no package manager.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.402-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.13-noble

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
#   Bun 1.4.3, asset bun-linux-x64.zip, SHA-256 3e730528d89775f03f87f05c06787c4df4099d0a37f3f252e7b829cb23816e18
#     Source: oven-sh/bun release bun-v1.4.3 SHASUMS256.txt. omp requires bun >=1.3.14 (engines.bun) and
#     dist/cli.js starts with #!/usr/bin/env bun.
#   @oh-my-pi/pi-coding-agent 18.12.5, npm integrity sha512-MZxW8b5bM2OMwRQzz5j0JQp6nsCXS0gHmwxSpbotWcNt6dpi1Sr1nMABjseHkPk3sFEXdGpSGJgfHmROxSASJA==
#     The tarball is checked against the sha512 hex form of that integrity value before extraction.
#
# NOT FROZEN: the omp package ships no lockfile (no package-lock.json, bun.lock or yarn.lock in its tarball).
# `bun install` therefore resolves transitive dependency ranges at build time, and it writes an uncommitted
# bun.lock inside the image. Only the omp tarball and the Bun zip are pinned. Do not commit an invented lockfile.
FROM ${SDK_IMAGE} AS omp-runtime
ARG BUN_VERSION=1.4.3
ARG BUN_ZIP_SHA256=3e730528d89775f03f87f05c06787c4df4099d0a37f3f252e7b829cb23816e18
ARG OMP_VERSION=18.12.5
ARG OMP_TGZ_SHA512=319c56f1be5b33638cc11433cf98f4250a7a9ec0974b48079b0c52a5ba2d59c36de9da62d52af59cc0018ec78790f937b05117746a5218981f1e644ec5201224
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

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

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

LABEL org.opencontainers.image.title="autonogrammer-agent-worker" \
      org.opencontainers.image.description="Autonogrammer cloud agent worker: serves GET /healthz and the ACP WebSocket GET /acp, runs omp against a workspace on the volume" \
      org.opencontainers.image.source="https://github.com/swcstudiospace/OmpOS" \
      org.opencontainers.image.licenses="AGPL-3.0-or-later"

# The entrypoint maps PORT onto ASPNETCORE_URLS (dual-stack http://[::]:PORT; Railway private networking needs IPv6),
# repairs volume ownership, then drops privileges and execs the worker, so the .NET process is PID 1 and receives
# termination signals. With no arguments it runs `dotnet /app/Autonogrammer.AgentWorker.dll`.
ENTRYPOINT ["/usr/local/bin/worker-entrypoint.sh"]
