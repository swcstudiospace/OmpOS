# OmpOS API image (OmpOS.Api). Build with podman from the repository root:
#   podman build -f deploy/api.Containerfile -t ompos-api:local .
#
# Base image tags:
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0.402-noble     (global.json pins SDK 10.0.402 exactly)
#   Runtime: mcr.microsoft.com/dotnet/aspnet:10.0.13-noble   (the runtime bundled with SDK 10.0.402)
# The noble (Ubuntu 24.04) variant is used rather than chiseled because the entrypoint runs /bin/sh
# to map the platform PORT variable onto ASPNETCORE_URLS. Chiseled images contain no shell.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.402-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.13-noble

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Repository-wide build inputs: SDK pin, central package versions, analyzer and style inputs
# (.editorconfig and the banned-symbol AdditionalFiles are read by the warnings-as-errors build).
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig BannedSymbols.txt BannedSymbols.NoProcess.txt ./
# Project graph of OmpOS.Api, plus docs/api, which the build-time OpenAPI generator writes into.
COPY src/ src/
COPY services/OmpOS.Api/ services/OmpOS.Api/
COPY docs/api/ docs/api/

# Locked restore: every project in the graph has a committed packages.lock.json.
RUN dotnet restore services/OmpOS.Api/OmpOS.Api.csproj --locked-mode
RUN dotnet publish services/OmpOS.Api/OmpOS.Api.csproj -c Release -o /app --no-restore

# omp runtime for Harness__Adapter=Acp: the ACP adapter launches the `omp` CLI, which this stage supplies.
#
# Pins (each read from its source; see docs/runbooks/hosting.md, "Known blockers and gaps", item 2):
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

# Non-root service account with fixed numeric IDs, so the platform can enforce the user without name lookup.
RUN groupadd --system --gid 10001 ompos \
 && useradd --system --uid 10001 --gid ompos --no-create-home --shell /usr/sbin/nologin ompos
COPY --from=build --chown=10001:10001 /app ./
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
#   podman build -f deploy/api.Containerfile --build-arg OMPOS_REVISION="$(git rev-parse HEAD)" -t ompos-api:local .
ARG OMPOS_SOURCE=https://github.com/swcstudiospace/OmpOS
ARG OMPOS_REVISION=unknown
ARG OMPOS_VERSION=0.15.0
# SourceInfoProvider reads GIT_SHA for GET /source (the AGPL-3.0 section 13 source offer).
ENV GIT_SHA=${OMPOS_REVISION}
LABEL org.opencontainers.image.licenses="AGPL-3.0-or-later" \
      org.opencontainers.image.source="${OMPOS_SOURCE}" \
      org.opencontainers.image.revision="${OMPOS_REVISION}" \
      org.opencontainers.image.version="${OMPOS_VERSION}"

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://[::]:8080 \
    DOTNET_NOLOGO=1
USER 10001:10001

# Documents the default port only; the process binds to PORT (set by the platform) or 8080.
EXPOSE 8080

# Runs under /bin/sh -c so ${PORT} expands at start and binds dual-stack ([::] accepts IPv4 and IPv6; Railway private networking needs IPv6). ASPNETCORE_HTTP_PORTS is unset so it cannot override ASPNETCORE_URLS.
# exec replaces the shell, so the .NET process is PID 1 and receives termination signals.
ENTRYPOINT ["/bin/sh", "-c", "unset ASPNETCORE_HTTP_PORTS; export ASPNETCORE_URLS=\"http://[::]:${PORT:-8080}\"; exec dotnet /app/OmpOS.Api.dll"]
