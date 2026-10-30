# Container images for LogTracker.
#
#   docker build -t logtracker-api .                                 (API, the default target)
#   docker build --target simulator -t logtracker-simulator .        (traffic simulator)
#
# docker-compose.yml builds both for you.

# ---------------------------------------------------------------------------
# Build stages: the SDK image compiles the code; nothing from here ends up in
# the final images except the published output.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS source
WORKDIR /src
# Copy the whole repository (minus .dockerignore) so that new projects and project
# references - e.g. src/LogTracker.Core - are picked up without editing this file.
COPY . .

FROM source AS publish-api
# The cache mount keeps downloaded NuGet packages between builds.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/LogTracker.Api/LogTracker.Api.csproj \
        --configuration Release --output /app -p:UseAppHost=false

FROM source AS publish-simulator
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/LogTracker.Simulator/LogTracker.Simulator.csproj \
        --configuration Release --output /app -p:UseAppHost=false

# ---------------------------------------------------------------------------
# Simulator image: plain .NET runtime, no ASP.NET Core needed.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS simulator
WORKDIR /app
COPY --from=publish-simulator /app .
# Run as the unprivileged "app" user that the .NET images provide.
USER $APP_UID
ENTRYPOINT ["dotnet", "LogTracker.Simulator.dll"]

# ---------------------------------------------------------------------------
# API image (last stage = default target of "docker build").
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
# curl is only here for the health check in docker-compose.yml (GET /health);
# the .NET runtime images do not include an HTTP client tool.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish-api /app .
USER $APP_UID
# .NET 8+ images listen on port 8080 by default (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080
ENTRYPOINT ["dotnet", "LogTracker.Api.dll"]
