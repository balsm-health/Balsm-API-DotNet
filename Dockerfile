# syntax=docker/dockerfile:1
# Portable cloud image — runs on Railway, Cloud Run, Fly.io, ECS/Fargate,
# Azure Container Apps, DigitalOcean, and plain k8s from a single build.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# Default port; overridden at runtime by $PORT (Cloud Run/Railway/Fly inject it).
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ src/
RUN dotnet restore src/Balsm.API/Balsm.API.csproj --locked-mode
RUN dotnet publish src/Balsm.API/Balsm.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:DebugType=none \
    -p:DebugSymbols=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cloud defaults: hosted mode (no Supervisor/mDNS/self-signed cert), Production config.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DeploymentMode=Cloud \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=1

# The app creates /app/var (logs + runtime files) on boot — see Program.cs.
# Pre-create and hand it to the non-root user (group 0 + group-write keeps it
# OpenShift-compatible) so startup doesn't hit UnauthorizedAccessException.
RUN mkdir -p /app/var/logs \
    && chown -R $APP_UID:0 /app/var \
    && chmod -R g+w /app/var

# Run as the non-root user the .NET images already ship (UID 1654).
USER $APP_UID

# Bind to $PORT when the platform injects one (Cloud Run=8080, Railway/Fly=dynamic),
# else fall back to 5000. exec keeps dotnet as PID 1 for correct signal handling.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet Balsm.API.dll --urls http://0.0.0.0:${PORT:-5000}"]
