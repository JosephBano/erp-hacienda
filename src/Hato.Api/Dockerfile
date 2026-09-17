# syntax=docker/dockerfile:1.7

# Build context is the repository root (compose sets `context: .`).
# This file lives at src/Hato.Api/Dockerfile.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Hato.Api/Hato.Api.csproj
RUN dotnet publish src/Hato.Api/Hato.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --no-self-contained \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN groupadd --system --gid 1001 hato \
    && useradd --system --uid 1001 --gid hato --no-create-home hato

COPY --from=build --chown=hato:hato /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

USER hato
EXPOSE 8080

ENTRYPOINT ["dotnet", "Hato.Api.dll"]
