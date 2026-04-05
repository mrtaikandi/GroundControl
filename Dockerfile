# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution, build config, and versioning files (separate layer for caching)
COPY GroundControl.slnx .
COPY Directory.Build.props .
COPY Directory.Build.targets .
COPY Directory.Packages.props .
COPY BannedSymbols.txt .
COPY global.json .
COPY version.json .
COPY .editorconfig .
COPY nuget.config .
COPY .globalconfig .

# Nerdbank.GitVersioning needs a git repo for version calculation
RUN git init && git config user.email "build@docker" && git config user.name "build" \
    && git add -A && git commit -m "build" --allow-empty

# Copy source projects and restore
COPY src/ src/
RUN dotnet restore src/GroundControl.Api/GroundControl.Api.csproj

# Publish
RUN dotnet publish src/GroundControl.Api/GroundControl.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:OpenApiGenerateDocuments=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for container health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Data Protection keys volume mount point (use built-in 'app' user, UID 1654)
RUN mkdir -p /keys && chown app:app /keys

USER app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "GroundControl.Api.dll"]
