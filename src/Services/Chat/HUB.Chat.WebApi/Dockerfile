# syntax=docker/dockerfile:1
# Build context = repo root (see docker-compose.yml). Multi-stage build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central package management — affects every restore, so copy first.
COPY Directory.Build.props Directory.Packages.props ./

# Copy only the project files in this service's dependency graph, then restore.
# This keeps the restore layer cached until a .csproj changes (not on source edits).
COPY src/Services/Chat/HUB.Chat.WebApi/HUB.Chat.WebApi.csproj                 src/Services/Chat/HUB.Chat.WebApi/
COPY src/Services/Chat/HUB.Chat.Application/HUB.Chat.Application.csproj        src/Services/Chat/HUB.Chat.Application/
COPY src/Services/Chat/HUB.Chat.Infrastructure/HUB.Chat.Infrastructure.csproj  src/Services/Chat/HUB.Chat.Infrastructure/
COPY src/Services/Chat/HUB.Chat.Domain/HUB.Chat.Domain.csproj                  src/Services/Chat/HUB.Chat.Domain/
COPY src/BuildingBlocks/HUB.Shared.Auth/HUB.Shared.Auth.csproj                 src/BuildingBlocks/HUB.Shared.Auth/
COPY src/BuildingBlocks/HUB.Shared.Contracts/HUB.Shared.Contracts.csproj       src/BuildingBlocks/HUB.Shared.Contracts/
COPY src/BuildingBlocks/HUB.Shared.Messaging/HUB.Shared.Messaging.csproj       src/BuildingBlocks/HUB.Shared.Messaging/
COPY src/BuildingBlocks/HUB.Shared.Observability/HUB.Shared.Observability.csproj src/BuildingBlocks/HUB.Shared.Observability/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Services/Chat/HUB.Chat.WebApi/HUB.Chat.WebApi.csproj

# Now copy the full source and publish (NuGet cache reused from the restore step).
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Services/Chat/HUB.Chat.WebApi/HUB.Chat.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Npgsql loads Kerberos (GSSAPI) during connection negotiation; the slim aspnet
# image doesn't ship it, causing "Cannot load library libgssapi_krb5.so.2".
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "HUB.Chat.WebApi.dll"]
