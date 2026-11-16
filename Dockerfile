# ─────────────────────────────────────────────────────────────────────────────
# Project Vital — Backend Dockerfile (.NET 8 LTS)
# Multi-stage build: SDK para compilar, runtime para executar (imagem final menor)
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas os .csproj primeiro para aproveitar cache do dotnet restore
COPY ProjectVital.sln ./
COPY ProjectVital.Domain/ProjectVital.Domain.csproj                 ProjectVital.Domain/
COPY ProjectVital.Application/ProjectVital.Application.csproj       ProjectVital.Application/
COPY ProjectVital.Infrastructure/ProjectVital.Infrastructure.csproj ProjectVital.Infrastructure/
COPY ProjectVital.API/ProjectVital.API.csproj                       ProjectVital.API/
COPY ProjectVital.ML/ProjectVital.ML.csproj                         ProjectVital.ML/
COPY ProjectVital.Tests/ProjectVital.Tests.csproj                   ProjectVital.Tests/
COPY ProjectVital.IntegrationTests/ProjectVital.IntegrationTests.csproj ProjectVital.IntegrationTests/

RUN dotnet restore ProjectVital.sln

# Copia o restante do código e compila
COPY . .
RUN dotnet publish ProjectVital.API/ProjectVital.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# wget para o healthcheck do docker-compose
RUN apt-get update && apt-get install -y --no-install-recommends wget  && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Sem privilégios: usuário "app" (UID 1654) das imagens oficiais do .NET 8.
# Porta 8080 (portas < 1024 exigiriam root). Em PaaS, a variável PORT tem prioridade.
ENV ASPNETCORE_URLS=http://+:8080     DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "ProjectVital.API.dll"]
