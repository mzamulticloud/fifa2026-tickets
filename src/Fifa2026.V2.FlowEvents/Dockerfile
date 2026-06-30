# =============================================================================
# Fifa2026.V2.FlowEvents — Dockerfile multi-stage (Story 2.6 / F6 — AC-3)
# Flow Visualizer backend (ASP.NET Core .NET 8): consulta App Insights por
# correlationId (Azure.Monitor.Query) e empurra eventos via Azure SignalR Service
# (Hub clássico, Service Mode Default). Hospedado em Azure Container App por aluno
# (SignalR Default mode exige host de longa duração — alinhar com @architect).
# =============================================================================

# ---- Stage 1: build + publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore isolado (camada de cache do Docker — só re-roda se o csproj mudar).
COPY ["Fifa2026.V2.FlowEvents.csproj", "./"]
RUN dotnet restore "Fifa2026.V2.FlowEvents.csproj"

# Copia o restante do código-fonte e publica em Release.
COPY . .
RUN dotnet publish "Fifa2026.V2.FlowEvents.csproj" -c Release -o /app/publish --no-restore

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Container App roteia tráfego para a porta 8080 (ingress target port).
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Fifa2026.V2.FlowEvents.dll"]
