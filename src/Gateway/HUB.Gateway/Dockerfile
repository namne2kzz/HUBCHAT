# syntax=docker/dockerfile:1
# Build context = repo root (see docker-compose.yml). Multi-stage build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management — affects every restore, so copy first.
COPY Directory.Build.props Directory.Packages.props ./

# Copy only the project files in this service's dependency graph, then restore.
# This keeps the restore layer cached until a .csproj changes (not on source edits).
COPY src/Gateway/HUB.Gateway/HUB.Gateway.csproj                               src/Gateway/HUB.Gateway/
COPY src/BuildingBlocks/HUB.Shared.Auth/HUB.Shared.Auth.csproj                 src/BuildingBlocks/HUB.Shared.Auth/
COPY src/BuildingBlocks/HUB.Shared.Observability/HUB.Shared.Observability.csproj src/BuildingBlocks/HUB.Shared.Observability/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Gateway/HUB.Gateway/HUB.Gateway.csproj

# Now copy the full source and publish. The NuGet cache mount is reused, so this is still fast.
# No --no-restore: a cache mount is scratch space, not a layer, so the packages the restore step
# downloaded are not guaranteed to still be there (parallel builds share the mount, and a cache
# prune empties it). --no-restore then fails with NETSDK1064 on a transitive package that central
# package management does not pin — YARP's System.IO.Hashing, in this service's case.
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Gateway/HUB.Gateway/HUB.Gateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HUB.Gateway.dll"]
