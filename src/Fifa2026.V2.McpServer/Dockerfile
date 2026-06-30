# =============================================================================
# Fifa2026.V2.McpServer — Dockerfile multi-stage (Story 2.5 / F5 — AC-2)
# MCP Server (ASP.NET Core .NET 8, SDK ModelContextProtocol 1.4.0) hospedado em
# Azure Container App por aluno (ADE-002 Inv 2 — host recomendado p/ Streamable HTTP).
# =============================================================================

# ---- Stage 1: build + publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore isolado (camada de cache do Docker — só re-roda se o csproj mudar).
COPY ["Fifa2026.V2.McpServer.csproj", "./"]
RUN dotnet restore "Fifa2026.V2.McpServer.csproj"

# Copia o restante do código-fonte e publica em Release.
COPY . .
RUN dotnet publish "Fifa2026.V2.McpServer.csproj" -c Release -o /app/publish --no-restore

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Container App roteia tráfego para a porta 8080 (ingress target port).
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Fifa2026.V2.McpServer.dll"]
