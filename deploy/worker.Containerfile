# OmpOS Temporal worker image (OmpOS.Workflows.Worker). Build with podman from the repository root:
#   podman build -f deploy/worker.Containerfile -t ompos-worker:local .
#
# Base image tags:
#   SDK:     mcr.microsoft.com/dotnet/sdk:10.0.402-noble     (global.json pins SDK 10.0.402 exactly)
#   Runtime: mcr.microsoft.com/dotnet/runtime:10.0.13-noble  (the runtime bundled with SDK 10.0.402)
# The worker is a generic host with no HTTP listener, so it uses the plain runtime image rather than ASP.NET.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.402-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0.13-noble

FROM ${SDK_IMAGE} AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Repository-wide build inputs: SDK pin, central package versions, analyzer and style inputs.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig BannedSymbols.txt BannedSymbols.NoProcess.txt ./
# Project graph of OmpOS.Workflows.Worker.
COPY src/ src/
COPY services/OmpOS.Workflows.Worker/ services/OmpOS.Workflows.Worker/

# Locked restore: every project in the graph has a committed packages.lock.json.
RUN dotnet restore services/OmpOS.Workflows.Worker/OmpOS.Workflows.Worker.csproj --locked-mode
RUN dotnet publish services/OmpOS.Workflows.Worker/OmpOS.Workflows.Worker.csproj -c Release -o /app --no-restore

FROM ${RUNTIME_IMAGE} AS final
WORKDIR /app

# Non-root service account with fixed numeric IDs.
RUN groupadd --system --gid 10001 ompos \
 && useradd --system --uid 10001 --gid ompos --no-create-home --shell /usr/sbin/nologin ompos
COPY --from=build --chown=10001:10001 /app ./

ENV DOTNET_NOLOGO=1
USER 10001:10001

# The worker listens on no port. With Workflows:Enabled=false it prints a message and exits 0.
ENTRYPOINT ["dotnet", "/app/OmpOS.Workflows.Worker.dll"]
