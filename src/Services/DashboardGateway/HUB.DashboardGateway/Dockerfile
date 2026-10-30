# syntax=docker/dockerfile:1
# Build context = repo root (see docker-compose.yml). Multi-stage build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management — affects every restore, so copy first.
COPY Directory.Build.props Directory.Packages.props ./

# Copy only the project files in this service's dependency graph, then restore.
# This keeps the restore layer cached until a .csproj changes (not on source edits).
COPY src/Services/DashboardGateway/HUB.DashboardGateway/HUB.DashboardGateway.csproj src/Services/DashboardGateway/HUB.DashboardGateway/
COPY src/BuildingBlocks/HUB.Shared.Auth/HUB.Shared.Auth.csproj                 src/BuildingBlocks/HUB.Shared.Auth/
COPY src/BuildingBlocks/HUB.Shared.Contracts/HUB.Shared.Contracts.csproj       src/BuildingBlocks/HUB.Shared.Contracts/
COPY src/BuildingBlocks/HUB.Shared.Messaging/HUB.Shared.Messaging.csproj       src/BuildingBlocks/HUB.Shared.Messaging/
COPY src/BuildingBlocks/HUB.Shared.Observability/HUB.Shared.Observability.csproj src/BuildingBlocks/HUB.Shared.Observability/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Services/DashboardGateway/HUB.DashboardGateway/HUB.DashboardGateway.csproj

# Now copy the full source and publish (NuGet cache reused from the restore step).
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Services/DashboardGateway/HUB.DashboardGateway/HUB.DashboardGateway.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HUB.DashboardGateway.dll"]
