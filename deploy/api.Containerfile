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

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

# Non-root service account with fixed numeric IDs, so the platform can enforce the user without name lookup.
RUN groupadd --system --gid 10001 ompos \
 && useradd --system --uid 10001 --gid ompos --no-create-home --shell /usr/sbin/nologin ompos
COPY --from=build --chown=10001:10001 /app ./

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1
USER 10001:10001

# Documents the default port only; the process binds to PORT (set by the platform) or 8080.
EXPOSE 8080

# Runs under /bin/sh -c so ${PORT} expands at start. ASPNETCORE_HTTP_PORTS is unset so it cannot override ASPNETCORE_URLS.
# exec replaces the shell, so the .NET process is PID 1 and receives termination signals.
ENTRYPOINT ["/bin/sh", "-c", "unset ASPNETCORE_HTTP_PORTS; export ASPNETCORE_URLS=\"http://+:${PORT:-8080}\"; exec dotnet /app/OmpOS.Api.dll"]
