# Autonogrammer API image (Autonogrammer.Api). Build with podman from the repository root:
#   podman build -f deploy/api.Containerfile -t autonogrammer-api:local .
#
# Base image tags:
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0.402-noble     (global.json pins SDK 10.0.402 exactly)
#   Runtime: mcr.microsoft.com/dotnet/aspnet:10.0.14-noble   (shipped runtime; the 10.0.13 bundled with SDK 10.0.402 carries GHSA-p3jp-9v83-76w9, fixed in 10.0.14)
# The noble (Ubuntu 24.04) variant is used rather than chiseled because the entrypoint runs /bin/sh
# to map the platform PORT variable onto ASPNETCORE_URLS. Chiseled images contain no shell.

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
# Project graph of Autonogrammer.Api, plus docs/api, which the build-time OpenAPI generator writes into.
COPY src/ src/
COPY services/Autonogrammer.Api/ services/Autonogrammer.Api/
COPY docs/api/ docs/api/

# Locked restore: every project in the graph has a committed packages.lock.json.
RUN dotnet restore services/Autonogrammer.Api/Autonogrammer.Api.csproj --locked-mode
RUN dotnet publish services/Autonogrammer.Api/Autonogrammer.Api.csproj -c Release -o /app --no-restore

# omp runtime for Harness__Adapter=Acp: the ACP adapter launches the `omp` CLI, which this stage supplies.
#
# Pins (each read from its source; see docs/runbooks/hosting.md, "Known blockers and gaps", item 2):
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

# Licence notices for what the final image conveys (AGPL-3.0 section 6; MIT notice duty). omp's LICENSE comes from its tarball.
# Bun's notice file records version, licence summary and source; the full component list is on Bun's licensing page
# (THIRD-PARTY-NOTICES.md, section 7, carries the same pointer and the source URL).
RUN mkdir -p /licenses \
 && for f in /opt/omp/LICENSE*; do [ -f "$f" ] && cp "$f" "/licenses/omp-$(basename "$f")"; done; \
 printf '%s\n' \
   "Bun ${BUN_VERSION} (/usr/local/bin/bun) is a separate program conveyed unmodified in this image." \
   "Licence: MIT, plus statically linked components (JavaScriptCore under LGPL-2, tinycc under LGPL-2.1, zlib-ng, ICU, SQLite and others)." \
   "Notices and component list: https://bun.sh/docs/project/licensing" \
   "Source: https://github.com/oven-sh/bun/tree/bun-v${BUN_VERSION}" \
   > /licenses/bun-NOTICE.txt

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app
# apt 2.8.3 is in the base image; CVE-2026-109518 is fixed in 2.8.4ubuntu0.1. Upgrade apt alone; nothing else is installed.
RUN apt-get update \
 && apt-get install -y --only-upgrade apt \
 && rm -rf /var/lib/apt/lists/*

# Non-root service account with fixed numeric IDs, so the platform can enforce the user without name lookup.
RUN groupadd --system --gid 10001 autonogrammer \
 && useradd --system --uid 10001 --gid autonogrammer --no-create-home --shell /usr/sbin/nologin autonogrammer
# The API's own files stay root-owned and read-only for uid 10001: the engine child process runs as that uid and executes model-chosen commands, so it must not be able
# to rewrite an assembly or appsettings.json that the API loads (now or after a restart). Writable state lives on a volume (Harness__Acp__WorkspaceRoot, HOME).
COPY --from=build /app ./
RUN chmod -R a=rX /app
# omp for Harness__Adapter=Acp. Bun is the runtime omp needs; the omp package is root-owned and read-only to uid 10001.
COPY --from=omp-runtime /usr/local/bin/bun /usr/local/bin/bun
COPY --from=omp-runtime /opt/omp /opt/omp
RUN ln -s /opt/omp/dist/cli.js /usr/local/bin/omp

# Licences and notices for everything this image conveys, under /licenses (AGPL-3.0 section 6: LICENSE and the source offer;
# the Bun and omp notices come from the omp-runtime stage; the .NET notices are copied when the base image ships them).
COPY LICENSE NOTICE THIRD-PARTY-NOTICES.md /licenses/
COPY --from=omp-runtime /licenses/ /licenses/
RUN for d in /usr/share/dotnet /usr/lib/dotnet; do for f in LICENSE.txt ThirdPartyNotices.txt; do [ -f "$d/$f" ] && cp "$d/$f" "/licenses/dotnet-$f"; done; done; \
 chmod -R a=rX /licenses

# OCI image labels. Pass the real values at build time, for example:
#   podman build -f deploy/api.Containerfile --build-arg AUTONOGRAMMER_REVISION="$(git rev-parse HEAD)" -t autonogrammer-api:local .
ARG AUTONOGRAMMER_SOURCE=https://github.com/swcstudiospace/OmpOS
ARG AUTONOGRAMMER_REVISION=unknown
ARG AUTONOGRAMMER_VERSION=0.15.0
# SourceInfoProvider reads GIT_SHA for GET /source (the AGPL-3.0 section 13 source offer).
ENV GIT_SHA=${AUTONOGRAMMER_REVISION}
LABEL org.opencontainers.image.licenses="AGPL-3.0-or-later" \
      org.opencontainers.image.source="${AUTONOGRAMMER_SOURCE}" \
      org.opencontainers.image.revision="${AUTONOGRAMMER_REVISION}" \
      org.opencontainers.image.version="${AUTONOGRAMMER_VERSION}"

# DOTNET_EnableDiagnostics=0 closes the runtime's diagnostics IPC socket (/tmp/dotnet-diagnostic-*), which a same-uid process such as the engine could use to take a
# memory dump of the API without ptrace, so it must not exist next to Hosting__RestrictProcessInspection (PR_SET_DUMPABLE).
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://[::]:8080 \
    DOTNET_NOLOGO=1 \
    DOTNET_EnableDiagnostics=0
USER 10001:10001

# Documents the default port only; the process binds to PORT (set by the platform) or 8080.
EXPOSE 8080

# Liveness probe without curl or wget (the aspnet base has neither): bash /dev/tcp sends a bare HTTP request and
# the status line must be 200. NOT RUN here: written without a podman build; verify with `podman healthcheck run`.
# HEALTHCHECK exists only in the docker image format: build with `podman build --format docker` (the OCI format drops it).
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD ["/bin/bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/${PORT:-8080} && printf 'GET %s HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' \"/health/live\" >&3 && head -n 1 <&3 | grep -q ' 200 '"]

# Runs under /bin/sh -c so ${PORT} expands at start and binds dual-stack ([::] accepts IPv4 and IPv6; Railway private networking needs IPv6). ASPNETCORE_HTTP_PORTS is unset so it cannot override ASPNETCORE_URLS.
# exec replaces the shell, so the .NET process is PID 1 and receives termination signals.
# "$@" forwards the container arguments (the word after the script is $0) so the one-shot service autonogrammer-migrate can run `migrate` and `migrate --down 3`; with no arguments the API starts as before.
ENTRYPOINT ["/bin/sh", "-c", "unset ASPNETCORE_HTTP_PORTS; export ASPNETCORE_URLS=\"http://[::]:${PORT:-8080}\"; exec dotnet /app/Autonogrammer.Api.dll \"$@\"", "autonogrammer-api"]
