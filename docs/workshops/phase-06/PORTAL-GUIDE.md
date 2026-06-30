# F6 — Portal Guide: provisionar o SignalR + FlowEvents + a rota `/flow`

> **Guia de execução passo-a-passo** · Workshop "Living Lab Azure-Native" · **Fase 6 de 6 — a final**
> **Story:** [2.6](../../stories/2.6.story.md) · **Arquitetura:** [ADE-004](../../architecture/ade-004-gateway-yarp.md) (gateway YARP = nó zero) + [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md) (Inv 5 — correlation) · **Workflow CI/CD:** [`.github/workflows/deploy-phase-06.yml`](../../../.github/workflows/deploy-phase-06.yml)
> **Pré-requisito:** F1-F5 provisionadas e funcionando (gateway YARP da F2 no ar, identidade Entra da F3 ativa, SQL com seed, compras v2 fluindo, App Insights coletando telemetria).

Este guia leva você de "branch criada" até "fazer uma compra v2 e ver a bolinha animar na rota `/flow`". Há **dois entregáveis de infra**: o **Azure SignalR Service** (Free) e o **serviço FlowEvents** (Container App .NET), além do **build do front** com a rota `/flow`.

> **Convenção:** onde aparecer `<...>` substitua pelo seu valor. Comandos usam Azure CLI (`az`). Tudo é possível pelo Portal também — indico os equivalentes nos pontos-chave.

> **A regra de ouro desta fase:** o serviço FlowEvents só **lê** a telemetria. Ele não gera o correlation ID (isso é do **gateway YARP**, o nó zero, desde a F2) e não escreve no SQL. Se a bolinha não aparece num nó, o problema quase sempre é **telemetria que não chegou** ou **permissão de leitura faltando** — não o FlowEvents.

---

## Visão geral do que você vai criar

```
┌─────────────────────────────────────────────────────────────┐
│  Frontend (Web App)                                          │
│  VITE_FLOW_EVENTS_BASE_URL = https://<gateway>/flow-events   │
│  rota nova: /flow  (diagrama 6 nós + bolinha framer-motion)  │
└───────────────┬─────────────────────────────────────────────┘
                │ Bearer Entra (MSAL F3) · WebSocket p/ SignalR
                ▼
┌─────────────────────────────────────────────────────────────┐
│  Gateway YARP (Container App da F2) — NÓ ZERO                │
│  /flow-events/api/**  → cluster flow-events (→ /api/**)      │
│  /flow-events/hubs/** → cluster flow-events (→ /hubs/**)     │
│  injeta X-Correlation-ID · valida JWT Entra                  │
└───────────────┬─────────────────────────────────────────────┘
                ▼
┌─────────────────────────────────────────────────────────────┐
│  FlowEvents (Container App NOVO desta fase)                  │
│  /api/flow/recent · /api/flow/{id} · /api/flow/{id}/replay  │
│  /hubs/flow  (FlowHub SignalR, Service Mode: Default)        │
│  /health                                                     │
│  Managed Identity → Log Analytics Reader no workspace        │
│  App Settings: LogAnalyticsWorkspaceId, AzureSignalRConn...  │
└──────────────┬───────────────────────────┬──────────────────┘
               │ LogsQueryClient (Kusto)    │ AddAzureSignalR
               ▼                            ▼
   ┌────────────────────────┐    ┌──────────────────────────┐
   │ App Insights /          │    │ Azure SignalR Service    │
   │ Log Analytics workspace │    │ Free_F1 · Default mode   │
   └────────────────────────┘    └──────────────────────────┘
```

---

## Passo 0 — Branch e pré-checagens (5 min)

1. Garanta que está na branch da fase:

   ```bash
   git checkout phase-06-flow-visualizer
   ```

2. Confirme que o **gateway YARP da F2** está no ar e injetando `X-Correlation-ID`. Faça uma compra v2 pela UI e confira, no DevTools (aba Network, response headers), que a resposta traz `X-Correlation-ID`. **Anote esse GUID** — você vai procurá-lo no `/flow` depois.

3. Confirme que o **App Insights** está coletando. No Portal → seu App Insights → **Logs**, rode:

   ```kusto
   traces
   | where isnotempty(tostring(customDimensions.CorrelationId))
   | order by timestamp desc
   | take 20
   ```

   Se vier vazio, **pare aqui**: sem `customDimensions.CorrelationId`, o FlowEvents não terá o que mostrar. Verifique se as Functions estão com `ILogger.BeginScope(["CorrelationId"])` ativo (é a fonte da customDimension).

> **Por que isso primeiro?** F6 é a fase mais "cumulativa" de todas — ela apenas **lê** o que F1-F5 produzem. Se a telemetria não está chegando, o sintoma aparece como "bolinha não anima", e você perde tempo caçando no FlowEvents quando o problema é upstream.

---

## Passo 1 — Anotar o GUID do Log Analytics workspace (5 min)

O FlowEvents consulta um **Log Analytics workspace** (o que está por baixo do seu App Insights), não o App Insights diretamente.

1. Portal → seu **App Insights** → menu **Logs**. No topo, ele indica o workspace vinculado.
2. Abra esse **Log Analytics workspace** → **Overview** → **Workspace ID** (um GUID). **Anote** — é o valor de `LogAnalyticsWorkspaceId`.

   Via CLI:
   ```bash
   az monitor log-analytics workspace show \
     -g <rg> -n <workspace-name> --query customerId -o tsv
   ```

> **Atenção:** é o **Workspace ID** (GUID), não o resource ID nem o nome. O `LogsQueryClient.QueryWorkspaceAsync` recebe esse GUID.

---

## Passo 2 — Provisionar o Azure SignalR Service (Free) (10 min)

**AC-2:** Free tier, 20 conexões, East US 2, **Service Mode: Default**.

### Pelo Portal
1. **Create a resource** → procure **SignalR Service** → **Create**.
2. Resource name: `signalr-fifa2026-<suas-iniciais>` (ex.: `signalr-fifa2026-gpc`).
3. Region: **East US 2**.
4. Pricing tier: **Free** (Free_F1 — 20 conexões, 20k mensagens/dia).
5. **Service Mode: Default** ⚠️ (NÃO "Serverless" — o `FlowHub` é um Hub clássico hospedado pelo FlowEvents).
6. Create. Após o deploy, vá em **Keys** → copie a **Connection String** (Primary). É o valor de `AzureSignalRConnectionString`.

### Pela CLI (ou aplicando o bicep versionado)
```bash
# Equivalente ao infra/phase-06/signalr.bicep
az signalr create \
  --name signalr-fifa2026-<iniciais> \
  --resource-group <rg> \
  --location eastus2 \
  --sku Free_F1 \
  --service-mode Default

# Connection string:
az signalr key list --name signalr-fifa2026-<iniciais> \
  -g <rg> --query primaryConnectionString -o tsv
```

Ou, se preferir IaC versionado:
```bash
az deployment group create -g <rg> -f infra/phase-06/signalr.bicep \
  -p signalRName=signalr-fifa2026-<iniciais> location=eastus2 \
     frontendOrigin=https://<seu-front>
```

> **Por que Default e não Serverless (de novo, porque erram muito):** Serverless é para Azure Functions sem host persistente. O nosso `FlowHub` roda **dentro** do serviço FlowEvents (`AddSignalR().AddAzureSignalR(...)`), um host de longa duração. Só o modo **Default** suporta isso. Escolher Serverless faz o `AddAzureSignalR` falhar ou as mensagens nunca chegarem.

> **CORS do SignalR (AC-9):** nas configurações do SignalR Service, em **CORS**, deixe como allowed origin a URL do seu front (não use `*` — o WebSocket usa credentials). O bicep já faz isso via `frontendOrigin`.

---

## Passo 3 — Build e teste local do FlowEvents (10 min)

Antes de subir, valide localmente. O FlowEvents roda em ASP.NET Core na porta **5060** em dev (a mesma que o cluster `flow-events` do gateway aponta — ver `appsettings.json` do gateway: `http://localhost:5060/`).

```bash
cd src/Fifa2026.V2.FlowEvents

# Para o smoke local, autentique-se (DefaultAzureCredential usa az login):
az login

# Defina o workspace (e, opcionalmente, a connection string do SignalR):
export LogAnalyticsWorkspaceId=<workspace-guid>
# Sem AzureSignalRConnectionString o Hub roda in-proc (suficiente p/ smoke local).

dotnet run --urls http://localhost:5060
```

Smoke local (noutro terminal):
```bash
curl http://localhost:5060/health
# → {"status":"healthy","service":"flow-events"}

# Pegue um correlationId real (do Passo 0) e teste a timeline:
curl http://localhost:5060/api/flow/<seu-correlation-guid>
```

> Se o `/api/flow/recent` ou `/api/flow/{id}` der **403**, é a sua conta `az login` sem permissão de leitura no workspace — resolvido com o papel do Passo 5 (em produção é a Managed Identity; em dev é a sua identidade).

Rode os testes (paridade com o que o CI faz):
```bash
dotnet test ../Fifa2026.V2.FlowEvents.Tests   # 22/22 esperado
```

---

## Passo 4 — Deploy do FlowEvents como Container App (15 min)

O FlowEvents é um Container App **no mesmo Container Apps Environment** do gateway/n8n/MCP. A definição versionada está em `infra/phase-06/flow-events-containerapp.yaml`.

### 4.1 Build da imagem e push para o ACR
```bash
az acr build --registry <acr-name> \
  --image flow-events:f6 \
  --file src/Fifa2026.V2.FlowEvents/Dockerfile \
  src/Fifa2026.V2.FlowEvents
```

### 4.2 Criar o Container App (com System-Assigned Identity)
```bash
az containerapp create \
  --name ca-flow-fifa2026-<iniciais> \
  --resource-group <rg> \
  --environment <cae-fifa2026-xy> \
  --image <acr-login-server>/flow-events:f6 \
  --target-port 8080 \
  --ingress external \
  --transport auto \
  --system-assigned \
  --registry-server <acr-login-server> \
  --registry-identity system \
  --min-replicas 0 --max-replicas 2 \
  --cpu 0.5 --memory 1Gi \
  --secrets azure-signalr-conn="<AZURE_SIGNALR_CONNECTION_STRING>" \
  --env-vars \
    AzureSignalRConnectionString=secretref:azure-signalr-conn \
    LogAnalyticsWorkspaceId=<workspace-guid> \
    FrontendOrigin=https://<seu-front> \
    APPLICATIONINSIGHTS_CONNECTION_STRING=<appinsights-conn-string>
```

> ⚠️ **`--transport auto`** é o que habilita o **WebSocket** no Container App. Sem isso, o SignalR cai sempre no fallback polling. O YAML versionado já fixa `transport: auto`.

> ⚠️ **`--target-port 8080`** — o Dockerfile do FlowEvents expõe `ASPNETCORE_URLS=http://+:8080`. Em **dev** a porta é 5060; em **container** é 8080. Não confunda.

Alternativa: `az containerapp create --yaml infra/phase-06/flow-events-containerapp.yaml` (substitua os `<...>` antes).

Anote a **FQDN** do Container App do FlowEvents:
```bash
az containerapp show -n ca-flow-fifa2026-<iniciais> -g <rg> \
  --query properties.configuration.ingress.fqdn -o tsv
```

---

## Passo 5 — Conceder à Managed Identity o papel Log Analytics Reader (10 min)

**Esta é a permissão que mais gente esquece.** Sem ela, o `LogsQueryClient` (via `DefaultAzureCredential` → Managed Identity) recebe **403** ao consultar o workspace.

```bash
# Principal ID da Managed Identity do Container App:
PRINCIPAL=$(az containerapp show -n ca-flow-fifa2026-<iniciais> -g <rg> \
  --query identity.principalId -o tsv)

# Resource ID do workspace:
WS_ID=$(az monitor log-analytics workspace show \
  -g <rg> -n <workspace-name> --query id -o tsv)

# Atribui Log Analytics Reader (ou "Monitoring Reader") no escopo do workspace:
az role assignment create \
  --assignee "$PRINCIPAL" \
  --role "Log Analytics Reader" \
  --scope "$WS_ID"
```

> **Pelo Portal:** Workspace → **Access control (IAM)** → **Add role assignment** → role **Log Analytics Reader** → assign access to **Managed identity** → selecione o Container App `ca-flow-fifa2026-<iniciais>`.

> **Por que esse papel e não uma key?** Porque keys vazam e expiram. Managed Identity + RBAC é o padrão Azure: a identidade do serviço **é** a credencial, gerenciada pela plataforma. O FlowEvents nunca carrega segredo de leitura do App Insights.

---

## Passo 6 — Apontar o gateway YARP para o FlowEvents (5 min)

O gateway tem o cluster `flow-events` com a destination externalizada via `FlowEventsUrl` (`FlowEventsDestinationConfigFilter` injeta o valor). Configure no Container App do **gateway**:

```bash
az containerapp update -n <gateway-app> -g <rg> \
  --set-env-vars FlowEventsUrl=https://<fqdn-do-flowevents>/
```

As rotas já existem no `appsettings.json` do gateway (não precisa editar):
- `/flow-events/api/{**catch-all}` → `/api/{**catch-all}` no FlowEvents
- `/flow-events/hubs/{**catch-all}` → `/hubs/{**catch-all}` no FlowEvents (WebSocket)

Smoke via gateway:
```bash
curl https://<gateway>/flow-events/api/flow/recent?top=5
```

> **Por que via gateway e não direto?** Para manter a herança F2/F3: o gateway é o nó zero (injeta `X-Correlation-ID`) e o guardião de identidade (Bearer Entra). O front só conhece a URL do gateway (`VITE_FLOW_EVENTS_BASE_URL = .../flow-events`); o FlowEvents fica atrás dele.

---

## Passo 7 — Build e deploy do frontend com a rota `/flow` (10 min)

A rota `/flow` é lazy-loaded (chunk próprio). Configure a env var do Vite apontando para o **gateway** (sufixo `/flow-events`):

```bash
cd "Lovable/World Cup Tickets Hub"

# .env (build-time do Vite):
echo "VITE_FLOW_EVENTS_BASE_URL=https://<gateway>/flow-events" >> .env

npm ci
npm run build      # gera o chunk Flow-*.js separado (framer-motion + signalr fora do bundle inicial)
```

Deploy do `dist/` como Web App (mesmo destino das fases anteriores). Confirme que a rota `/flow` está acessível: `https://<seu-front>/flow`.

> **CORS:** se o navegador reclamar de CORS no WebSocket, revise: (a) `FrontendOrigin` no FlowEvents = URL exata do front; (b) CORS do SignalR Service = URL do front; (c) o front fala com o **gateway** (`/flow-events`), não direto com o FlowEvents.

---

## Passo 8 — Smoke test ponta-a-ponta (AC-7) (10 min)

Esta é a hora da verdade — a demo que fecha o workshop.

1. **Faça uma compra v2** pela UI (autenticada, via gateway). Anote o `X-Correlation-ID` da resposta (DevTools → Network).
2. Vá para **`/flow`**. A lista de **compras recentes** deve mostrar a sua compra no topo.
3. **Clique** na compra (ou cole o correlationId na busca). O front:
   - entra no grupo SignalR `correlation-<id>` (`Subscribe`),
   - dispara o `replay` (`POST /api/flow/{id}/replay`),
   - começa a receber eventos `FlowEvent`.
4. **Observe a bolinha** percorrer os **6 nós** na ordem: Gateway YARP → Function Entry → Service Bus → Function Consumer → n8n → SQL, em **< 30s**.
5. Em cada nó, abra o **Sheet** (clique no nó) e confira: duração do hop, status (ok), e o correlationId correto.
6. Confirme que o `correlationId` é o **mesmo** do nó 0 (Gateway YARP) ao nó 5 (SQL) — é o tracing ponta-a-ponta funcionando.

> **Latência de ingestão:** o App Insights pode levar **até ~2 min** para indexar a telemetria de uma compra recém-feita. Se a bolinha "trava" num nó logo após a compra, espere e clique de novo (o `replay` relê a timeline). Para a demo ao vivo, faça uma compra "de aquecimento" alguns minutos antes (ver SPEAKER-NOTES).

---

## Tabela de configuração (resumo)

| Onde | Chave | Valor | Origem |
|---|---|---|---|
| FlowEvents (Container App) | `LogAnalyticsWorkspaceId` | GUID do workspace | Passo 1 |
| FlowEvents (Container App) | `AzureSignalRConnectionString` | secret `azure-signalr-conn` | Passo 2 |
| FlowEvents (Container App) | `FrontendOrigin` | URL do front | Passo 4 |
| FlowEvents (Container App) | `APPLICATIONINSIGHTS_CONNECTION_STRING` | conn string do App Insights | Passo 4 |
| FlowEvents (RBAC) | papel **Log Analytics Reader** | Managed Identity → workspace | Passo 5 |
| Gateway (Container App) | `FlowEventsUrl` | FQDN do FlowEvents | Passo 6 |
| Front (Vite build) | `VITE_FLOW_EVENTS_BASE_URL` | `https://<gateway>/flow-events` | Passo 7 |
| SignalR Service | Service Mode | **Default** | Passo 2 |
| SignalR Service | tier / região | Free_F1 / East US 2 | Passo 2 |

---

## Troubleshooting

| Sintoma | Causa provável | Solução |
|---|---|---|
| `/api/flow/recent` ou `/timeline` retorna **403** | Managed Identity sem papel no workspace | Passo 5 (Log Analytics Reader). Em dev, `az login` com a sua conta com leitura |
| Bolinha não aparece para **nenhum** nó | telemetria não chegou / workspace errado | Confirme `customDimensions.CorrelationId` no workspace (Passo 0); confira `LogAnalyticsWorkspaceId` (é o GUID, Passo 1) |
| Bolinha aparece só em **alguns** nós | aquele componente sem App Insights, ou latência de ingestão | Aguarde ~2 min e clique de novo (replay); confirme App Insights configurado naquele componente |
| Nó **"APIM"** no diagrama | material antigo | NÃO existe APIM. O nó zero é **Gateway YARP**. O código já força isso (teste `Node_zero_is_gateway_yarp_never_apim`) |
| SignalR cai sempre para **polling** | WebSocket bloqueado / `transport` errado | `--transport auto` no Container App (Passo 4); CORS do SignalR e `FrontendOrigin` corretos |
| `AddAzureSignalR` falha no startup | Service Mode = Serverless | recrie o SignalR em **Default** (Passo 2) |
| Erro de CORS no WebSocket | origin não bate | `FrontendOrigin` (FlowEvents) = CORS (SignalR Service) = URL exata do front |
| Latência > 30s para a bolinha completar | ingestão lenta do App Insights | compra de aquecimento antes da demo; o replay relê a timeline já indexada |

> **Próximo passo:** com o smoke passando, você completou as 6 fases. O fechamento do workshop — recap das 6 fases, a demo "para um leigo" e a retro — está nas [SPEAKER-NOTES](./SPEAKER-NOTES.md).
