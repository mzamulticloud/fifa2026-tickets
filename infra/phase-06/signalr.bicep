// =============================================================================
// Azure SignalR Service — Flow Visualizer (Story 2.6 / F6 — AC-2)
//
// IaC VERSIONADO (NÃO provisiona automaticamente no workshop — o aluno cria via
// Portal seguindo PORTAL-GUIDE, ou aplica este bicep manualmente). Declara o
// SignalR Service no tier FREE (Free_F1, 20 conexões simultâneas) em Service Mode
// DEFAULT (Hub clássico — NÃO Serverless), pois o FlowHub é hospedado pelo serviço
// FlowEvents .NET (AddAzureSignalR) e precisa do modo Default.
//
// AC-2: signalr-fifa2026-<iniciais>, Free tier, 20 conexões, East US 2, Default.
//
// Deploy manual (exemplo):
//   az deployment group create -g <rg> -f infra/phase-06/signalr.bicep \
//     -p signalRName=signalr-fifa2026-gpc location=eastus2 frontendOrigin=https://<front>
//
// A connection string resultante vira o App Setting AzureSignalRConnectionString do
// Container App do FlowEvents (NUNCA hardcoded — ADE-003 Inv 3).
// =============================================================================

@description('Nome do recurso SignalR (AC-2: signalr-fifa2026-<iniciais>).')
param signalRName string

@description('Região Azure (AC-2: East US 2).')
param location string = 'eastus2'

@description('Origin do frontend para CORS do SignalR (AC-9 — não usar "*" com credentials).')
param frontendOrigin string

resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
  location: location
  // AC-2 — tier FREE: 20 conexões simultâneas, 20k mensagens/dia. Suficiente p/ workshop.
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    // AC-2 — Service Mode DEFAULT (Hub clássico). NÃO 'Serverless': o FlowHub é
    // hospedado pelo FlowEvents .NET (AddAzureSignalR) — exige modo Default.
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'true'
      }
    ]
    cors: {
      // AC-9/AC-6 — origin restrito do front (WebSocket SignalR usa credentials).
      allowedOrigins: [
        frontendOrigin
      ]
    }
  }
}

@description('Resource ID do SignalR Service.')
output signalRId string = signalR.id

@description('Nome do host do SignalR (para referência; a connection string vem das keys).')
output signalRHostName string = signalR.properties.hostName
