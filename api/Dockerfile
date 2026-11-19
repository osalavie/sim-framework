# --- Project-files Stage (cached unless *.csproj changes) ---
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS project-files
WORKDIR /src
COPY framework/ framework/
COPY modules/ modules/
COPY src/ src/
COPY tests/ tests/
# Strip source code — keep only csproj/props/targets so this layer
# only changes when project references or NuGet packages change
RUN find . -type f ! -name "*.csproj" ! -name "*.props" ! -name "*.targets" -delete && \
    find . -type d -empty -delete 2>/dev/null || true

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src

# Root-level props and config
COPY Directory.Packages.props Directory.Build.props common.props ./
COPY NuGet.Config ./

# Project files only — cache hit unless a csproj changes
COPY --from=project-files /src ./

# Restore: cached as long as no *.csproj changes
RUN dotnet restore src/Sim.Admin.HttpApi.Host/Sim.Admin.HttpApi.Host.csproj

# Full source (invalidates on any code change, but restore is already done)
COPY framework/ framework/
COPY modules/ modules/
COPY src/ src/
COPY tests/ tests/

# Publish (no restore needed — already restored above)
RUN dotnet publish src/Sim.Admin.HttpApi.Host/Sim.Admin.HttpApi.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9 AS runtime
WORKDIR /app

ARG APP_VERSION=dev
ARG GIT_COMMIT=unknown

LABEL org.opencontainers.image.title="SimFramework API" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_COMMIT}"

# Install runtime dependencies: curl for healthcheck, krb5 for PostgreSQL GSSAPI auth, sqlite3 for SQLitePCL initialization
# Use Aliyun mirror to avoid archive.ubuntu.com connectivity issues
RUN sed -i 's|http://archive.ubuntu.com|http://mirrors.aliyun.com|g; s|http://security.ubuntu.com|http://mirrors.aliyun.com|g' /etc/apt/sources.list.d/ubuntu.sources && \
    apt-get update && \
    apt-get install -y --no-install-recommends curl libgssapi-krb5-2 libsqlite3-0 && \
    rm -rf /var/lib/apt/lists/*

# Run as numeric UID/GID 1000 to align with the host user. Idempotently create
# the UID/GID entries only when the base image does not already provide them,
# so the build remains stable if the base image changes.
RUN if ! getent group 1000 >/dev/null; then groupadd -g 1000 app; fi && \
    if ! getent passwd 1000 >/dev/null; then useradd -u 1000 -g 1000 -d /app -s /sbin/nologin app; fi

# Create directories for logs, data, and keys
RUN mkdir -p /app/Logs /app/App_Data/keys /app/App_Data/file-processing && \
    chown -R 1000:1000 /app

# Copy published application
COPY --from=build --chown=1000:1000 /app/publish .

# Remove baked-in production config — mount externally via volume
RUN rm -f /app/appsettings.Production.json && \
    mkdir -p /app/config && \
    chown 1000:1000 /app/config

# Set environment defaults
ENV DOTNET_ENVIRONMENT=Production
ENV HTTP_PORTS=7777
ENV APP_UID=1000
ENV APP_GID=1000
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Switch to non-root user
USER 1000:1000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:${HTTP_PORTS:-7777}/health/live || exit 1

EXPOSE 7777

ENTRYPOINT ["dotnet", "Sim.Admin.HttpApi.Host.dll"]
