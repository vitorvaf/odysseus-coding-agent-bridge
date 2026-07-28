# Multi-stage build per ADR-0015 (.NET 8 LTS runtime target).
# Non-root runtime user, read-only rootfs recommended at orchestration layer.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY OcabBridge.slnx ./
COPY src/OcabBridge.Api/OcabBridge.Api.csproj src/OcabBridge.Api/
RUN dotnet restore src/OcabBridge.Api/OcabBridge.Api.csproj

COPY . .
# Note: do NOT pass --no-restore here. The build-stage restore above
# only resolves direct PackageReferences; some transitive deps
# (e.g. Microsoft.Extensions.Configuration.Binder via
# Microsoft.Extensions.Http) need the full transitive resolution
# that publish performs implicitly. Removing --no-restore is the
# observed fix for NETSDK1064 in this image.
RUN dotnet publish src/OcabBridge.Api/OcabBridge.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

RUN groupadd --system --gid 10001 ocab && \
    useradd --system --uid 10001 --gid ocab --home /app --shell /usr/sbin/nologin ocab

# Install curl for the Compose healthcheck probe (the aspnet image
# does not ship with curl or wget). Pin to the LTS Debian base.
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
RUN chown -R ocab:ocab /app

USER ocab
ENV ASPNETCORE_URLS=http://+:9010
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 9010

HEALTHCHECK --interval=10s --timeout=2s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:9010/health || exit 1

ENTRYPOINT ["dotnet", "OcabBridge.Api.dll"]
