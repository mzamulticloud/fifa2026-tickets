# infra/phase-06 — Flow Visualizer (Story 2.6 / F6)

IaC e config versionados da Fase 6 (Flow Visualizer com correlation ID em tempo real).
**Nenhum recurso é provisionado automaticamente** — o aluno cria via Portal (workshop)
ou aplica os bicep/config manualmente. O fluxo real é:

```
[0] Gateway YARP  → injeta X-Correlation-ID (nó zero — ADE-004, NÃO APIM)
[1] Function Entry (PurchaseEntryFunction) → publica no Service Bus
[2] Service Bus (tickets-purchase)
[3] Function Consumer (PurchaseConsumerFunction) → grava SQL, dispara n8n
[4] n8n (post-purchase-notification)
[5] SQL (purchases.correlation_id)
```

O serviço **FlowEvents** (`src/Fifa2026.V2.FlowEvents/`) consulta o App Insights por
correlationId (Azure.Monitor.Query) e empurra os 6 eventos via **Azure SignalR Service**
(Hub clássico `FlowHub`) para o grupo `correlation-<id>`.

## Arquivos

| Arquivo | Propósito |
|---|---|
| `signalr.bicep` | Azure SignalR Service — Free tier (20 conexões), Service Mode **Default**, East US 2 (AC-2). |
| `flow-events-containerapp.yaml` | Config do Container App do FlowEvents (.NET 8), App Settings esperados. |

## App Settings esperados (FlowEvents Container App)

| Setting | Origem | Notas |
|---|---|---|
| `AzureSignalRConnectionString` | Keys do SignalR Service (`signalr.bicep`) | OBRIGATÓRIO em prod. Secret. |
| `LogAnalyticsWorkspaceId` | App Insights → Logs → workspace (GUID) | OBRIGATÓRIO (AC-3). |
| `FrontendOrigin` | URL do Web App do front | CORS (AC-9) — não `*` com credentials. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights | Observabilidade de borda (no-op se ausente). |

A identidade do FlowEvents (Managed Identity do Container App) precisa do papel
**Log Analytics Reader** (ou Monitoring Reader) no workspace para o `LogsQueryClient`
(DefaultAzureCredential) ler os traces.

## Gateway

O gateway YARP roteia `/flow-events/api/**` e `/flow-events/hubs/**` ao FlowEvents
(cluster `flow-events`, App Setting `FlowEventsUrl`). O X-Correlation-ID é injetado pelo
mesmo transform global de borda — o gateway permanece o **nó zero** também para o F6.
