# Build context = repo root (see docker-compose.yml). Multi-stage build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Chat/HUB.Chat.WebApi/HUB.Chat.WebApi.csproj
RUN dotnet publish src/Services/Chat/HUB.Chat.WebApi/HUB.Chat.WebApi.csproj -c Release -o /app --no-restore

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
