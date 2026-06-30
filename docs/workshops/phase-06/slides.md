---
title: "F6 — Flow Visualizer: Distributed Tracing em Tempo Real"
subtitle: "Workshop Living Lab Azure-Native · Fase 6 de 6 · 8h · A FINAL"
story: "2.6"
adr: "ADE-004 + ADE-000"
---

# F6 — Flow Visualizer
## Distributed Tracing & Correlation ID em Tempo Real

Workshop "Living Lab Azure-Native" · **Fase 6 de 6 — a final** · **8h**

> "Se você não consegue VER o fluxo, você não entende o sistema — você só torce para ele funcionar."

---

## Você chegou na fase final

Cinco fases. Seis microserviços. 40 horas.

Hoje você não constrói uma sexta peça de negócio.

Hoje você constrói o **espelho** de tudo: uma tela que mostra a compra
atravessando o sistema inteiro, **ao vivo**.

---

## Onde estamos no Living Lab

- **F1** — Compra: `POST /purchase` → fila → consumer → SQL
- **F2** — Gateway YARP profissional (injeta `X-Correlation-ID`)
- **F3** — Identidade Entra (JWT validado, `oid` → `X-Entra-OID`)
- **F4** — Automação visual (n8n)
- **F5** — Conversa com o sistema (MCP + chatbot)
- **F6** — **Hoje: VER tudo isso funcionando junto, em tempo real**

---

## O problema: sistemas distribuídos são invisíveis

Sua compra atravessa **6 processos diferentes**:

gateway · function entry · service bus · function consumer · n8n · SQL

Cada um com **seu próprio log**, no **seu próprio tempo**,
misturado com **outras** compras.

**Como saber quais linhas pertencem à MESMA compra?**

---

## Observabilidade

Responder perguntas sobre o sistema **sem mudar o código** para investigar:

1. **Onde** a compra está agora? (qual nó)
2. **Quanto tempo** cada salto levou? (latência por hop)
3. **Deu certo** em cada salto? (ok / erro)

Precisa de: um **ID comum** + um **lugar central** que junte tudo.

---

## A ideia central: distributed tracing

Seguir **uma** requisição enquanto ela atravessa **vários** serviços.

A peça-chave: o **correlation ID** — um GUID único, criado na entrada,
**carregado adiante** em cada salto.

---

## Analogia: a etiqueta de bagagem

Você despacha a mala → ela ganha um **código único**.

Cada ponto registra: "vi a mala XYZ às 14h32".

No fim, dá pra reconstruir a **jornada inteira** filtrando pelo código.

> Correlation ID = etiqueta · Sua compra = mala · Os 6 nós = pontos de transferência

---

## Onde nasce o correlation ID: o Gateway YARP

O ID nasce no **Gateway YARP** — o mesmo da F2.

Ele é o **nó zero** porque é a **borda**: toda request entra por ele.

```csharp
var correlationId = string.IsNullOrWhiteSpace(incoming)
    ? Guid.NewGuid().ToString()   // gera se ausente
    : incoming;
proxyRequest.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId); // injeta downstream
response.Headers[CorrelationHeader] = correlationId;  // devolve ao cliente
```

`src/Fifa2026.V2.Gateway/Program.cs`

---

## ⚠️ NÃO existe APIM aqui

O nó zero é o **Gateway YARP**. **Ponto.**

Muito material Azure cita **APIM** como a porta que injeta correlation IDs.
Verdade — **em outras arquiteturas**.

Aqui, no EPIC-002, a porta é o **gateway YARP** que **você** construiu na F2.

> Viu "APIM" associado a este fluxo? É material antigo. Corrija para **Gateway YARP**.
> O código trava isso: teste `Node_zero_is_gateway_yarp_never_apim`.

---

## W3C Trace Context: de graça

Além do nosso `X-Correlation-ID` (de negócio, legível)...

...os SDKs do Functions e do Service Bus propagam o **`traceparent`** (W3C)
automaticamente → correlação técnica nativa no App Insights.

Você **não configura** — vem dos SDKs.

---

## Os 6 nós — na ordem REAL

```
[0] Gateway YARP      → injeta X-Correlation-ID (nó zero)
[1] Function Entry    → PurchaseEntryFunction (publica na fila)
[2] Service Bus       → tickets-purchase
[3] Function Consumer → PurchaseConsumerFunction (SQL + n8n)
[4] n8n               → post-purchase-notification
[5] SQL               → purchases.correlation_id
```

**Recitem comigo:** Gateway YARP → Function Entry → Service Bus →
Function Consumer → n8n → SQL

---

## Fonte única da verdade (back + front)

**Backend** — `Models/FlowEventType.cs` (enum, 6 membros):

```csharp
GATEWAY_YARP_RECEIVED = 0,   // nó zero
FUNCTION_ENTRY_PROCESSED = 1,
SERVICE_BUS_PUBLISHED = 2,
FUNCTION_CONSUMER_DONE = 3,
N8N_WEBHOOK_TRIGGERED = 4,
SQL_INSERTED = 5
```

**Frontend** — `lib/flowNodes.ts` (`FLOW_NODES`, índice 0 = Gateway YARP)

> O **ordinal** é o número do nó na animação. Reordenar quebraria a bolinha.

---

## Como o correlation ID se propaga

| Salto | Componente | Escreve o ID em |
|---|---|---|
| 0→1 | **Gateway YARP** | header downstream + resposta |
| 1 | Function Entry | corpo da mensagem + `BeginScope` |
| 2 | Service Bus | mensagem `tickets-purchase` |
| 3 | Function Consumer | `BeginScope` + `correlation_id` |
| 4 | n8n | `payload.correlationId` |
| 5 | SQL | coluna `correlation_id` |

---

## O elo invisível: BeginScope → customDimension

```csharp
using (logger.BeginScope(new Dictionary<string,object>
       { ["CorrelationId"] = id }))
{
    logger.LogInformation("...");  // carrega CorrelationId
}
```

O provider do App Insights vira isso em **`customDimensions.CorrelationId`**.

É **isso** que o Flow Visualizer filtra. Sem `BeginScope`, nada aparece.

> ADE-000 Invariante 5

---

## Nota de fidelidade

O AC-4 menciona `ApplicationProperties` do Service Bus.

A **implementação real**: o ID viaja no **corpo** da mensagem + `BeginScope`.

E é o `BeginScope` que produz a customDimension que o FlowEvents consulta.

> **Ensine o que o código faz.** (Alinhamento registrado pelo PO no gate S2.4.)

---

## O App Insights

Serviço de APM do Azure. Centraliza a telemetria dos **6 componentes**.

Por baixo, usa um **Log Analytics workspace**.

É o "lugar central" que junta tudo.

---

## O serviço novo: FlowEvents

`src/Fifa2026.V2.FlowEvents/` — ASP.NET Core .NET 8, Container App.

Ele **LÊ** a telemetria e a transforma em eventos do diagrama.

> **Por que não Azure Function?** SignalR **Default mode** precisa de host
> de longa duração. Serverless não serve. Mesmo padrão do MCP Server (F5).

---

## Como consultar o App Insights? 3 opções

| Opção | Para quê | Usar? |
|---|---|---|
| `TelemetryClient` | **escrever** telemetria | ❌ |
| REST `api.applicationinsights.io` | query (legada) | ❌ em desativação |
| `Azure.Monitor.Query` (`LogsQueryClient`) | **consultar** logs/traces | ✅ |

> Ponto "No Invention": APIs reais do Azure SDK for .NET.

---

## O repositório real

```csharp
_client = new LogsQueryClient(new DefaultAzureCredential());

var response = await _client.QueryWorkspaceAsync(
    _workspaceId,                          // LogAnalyticsWorkspaceId (App Setting)
    query,                                 // Kusto parametrizado
    new QueryTimeRange(TimeSpan.FromHours(1)));
```

`Data/AppInsightsFlowEventRepository.cs`

---

## A query Kusto

```kusto
traces
| where tostring(customDimensions.CorrelationId) == correlationId
| project timestamp, message, severityLevel, cloud_RoleName, customDimensions
| order by timestamp asc
| limit 100
```

**Segurança real:** parametrização (`declare query_parameters`) +
sanitização (só hex e hífen — é um GUID). Defesa em profundidade.

---

## Autenticação: Managed Identity, não key

`DefaultAzureCredential` → **Managed Identity** do Container App.

Papel necessário: **Log Analytics Reader** no workspace.

> Sem key para vazar. A identidade do serviço **é** a credencial.
> (Esquecer o papel = **403**. É o erro nº 1 ao vivo.)

---

## Do trace bruto ao nó: TraceEventMapper

Lógica **pura** (sem I/O) → 100% testável sem App Insights vivo.

```
"Compra v2 recebida"        → FUNCTION_ENTRY_PROCESSED
tickets-purchase publicada  → SERVICE_BUS_PUBLISHED
"gravada com sucesso"       → SQL_INSERTED
health probe                → descartado (não é hop)
```

Status: `severityLevel >= 3` → `"error"`, senão `"ok"`.

---

## Tempo real: SignalR

A mágica didática: a bolinha animando **ao vivo**.

O servidor **empurra** eventos para o navegador (não fica perguntando).

**Azure SignalR Service** — Free_F1, 20 conexões, **Service Mode: Default**.

---

## ⚠️ Service Mode: Default (não Serverless)

O `FlowHub` é um **Hub clássico hospedado** pelo FlowEvents
(`AddSignalR().AddAzureSignalR(...)`).

**Serverless** = para Functions sem host persistente. Não é o nosso caso.

> Provisionar Serverless = `AddAzureSignalR` falha ou mensagens não chegam.

---

## O Hub e os grupos

```csharp
public sealed class FlowHub : Hub
{
    public static string GroupName(string id) => $"correlation-{id}";
    public Task Subscribe(string id)   => Groups.AddToGroupAsync(...);
    public Task Unsubscribe(string id) => Groups.RemoveFromGroupAsync(...);
}
```

Cada compra observada = um **grupo** `correlation-<id>`.
O servidor empurra só para o grupo — **não** broadcast.

Método server→client: **`FlowEvent`**.

---

## O cliente: @microsoft/signalr

```ts
const connection = new HubConnectionBuilder()
  .withUrl(FLOW_HUB_URL)
  .withAutomaticReconnect()
  .build();

connection.on('FlowEvent', (e) => animarNó(e.nodeIndex));
connection.invoke('Subscribe', correlationId);
```

`hooks/useFlowConnection.ts`

---

## Fallback polling (2s) — AC-6

WebSocket falhou (CORS / proxy / firewall)?

→ Cai para `GET /api/flow/{id}` a cada **2 segundos**.

A bolinha ainda anda — só não tão "ao vivo". **Degrada graciosamente.**

---

## E as compras antigas? O `replay`

Compra antiga não tem eventos "novos" para empurrar.

→ Front chama `POST /api/flow/{id}/replay`.

→ Backend **relê** a telemetria e **reempurra** via SignalR.

Para o navegador, é indistinguível de tempo real. **Demo reproduzível.**

---

## A rota /flow

Lazy-loaded (chunk próprio — framer-motion + signalr fora do bundle inicial).

- **RecentPurchases** — últimas 50 (sortable, searchable)
- **FlowDiagram** — os 6 nós + a bolinha
- **FlowNodeCard** — duração, status, payload no Sheet (shadcn/ui)

---

## A bolinha: framer-motion

```tsx
<motion.div
  animate={{ x: nodePositions[currentNode] }}
  transition={{ type: 'spring' }}
/>
```

Percorre de nó em nó conforme `event.nodeIndex` chega.

> APIs reais: `motion.div` / `animate` / `transition`. No Invention.

---

## Acessibilidade não é opcional (AC-9)

- Cada nó é `<button>` com `aria-label` descritivo
- Diagrama é `<ol>` semântico · bolinha `aria-hidden`
- **Modo lista** (sem animação): toggle + auto via `prefers-reduced-motion`
- Busca/sort com aria-labels · linhas keyboard-acionáveis

> "Uma visualização que exclui quem usa leitor de tela não é boa visualização."

---

## Tudo passa pelo gateway (herança F2/F3)

```
Front  →  Gateway YARP  →  FlowEvents
       /flow-events/api/**  → /api/**
       /flow-events/hubs/** → /hubs/**  (WebSocket)
```

O gateway continua **nó zero** (X-Correlation-ID) e **guardião** (Bearer Entra).

Front só conhece `VITE_FLOW_EVENTS_BASE_URL = .../flow-events`.

---

## Contratos exatos

| Contrato | Valor |
|---|---|
| Recentes | `GET /api/flow/recent?top=50` |
| Timeline | `GET /api/flow/{id}` |
| Replay | `POST /api/flow/{id}/replay` |
| Hub | `/hubs/flow` |
| Subscribe | `connection.invoke("Subscribe", id)` |
| Evento | `connection.on("FlowEvent", ...)` |

---

## Deploy: os 5 passos

1. SignalR Service (Free, **Default**)
2. FlowEvents Container App (`--transport auto`, port 8080)
3. **Managed Identity → Log Analytics Reader** (não esqueça!)
4. Gateway: `FlowEventsUrl` = FQDN do FlowEvents
5. Front: `VITE_FLOW_EVENTS_BASE_URL` = `.../flow-events`

---

## Os 3 erros que VÃO acontecer ao vivo

1. **403 na query** → faltou Log Analytics Reader na Managed Identity
2. **Sempre polling** → `--transport auto` ou CORS
3. **Bolinha trava após compra** → latência de ingestão (~2 min)

> Dica: faça uma **compra de aquecimento ~5 min antes** da demo.

---

## Smoke test (AC-7)

1. Compra v2 (autenticada, via gateway) — anote o `X-Correlation-ID`
2. `/flow` → compra aparece na lista
3. Clique → bolinha percorre os **6 nós** em **< 30s**
4. Cada nó: tempo + status + payload
5. `correlationId` **igual** do nó 0 (Gateway) ao nó 5 (SQL)

**Tracing ponta-a-ponta funcionando. ✅**

---

## Demo "para um leigo"

Mostre `/flow` a alguém que nunca programou.

Diga **uma frase:** "Quando você clica em comprar, é isto que acontece por dentro."

Deixe a bolinha andar. **Não explique.**

> 20 segundos de bolinha = 40 horas de aula tornadas visíveis.

---

## A jornada completa — cada nó, uma fase

| Nó | Fase | Conceito |
|---|---|---|
| Gateway YARP | **F2** | reverse proxy, nó zero do tracing |
| Function Entry | **F1** | entrada desacoplada |
| Service Bus | **F1** | fila amortecedora |
| Function Consumer | **F1** | consumidor idempotente |
| n8n | **F4** | automação low-code |
| SQL | **F1** | persistência + correlation_id |

E atravessando tudo: **identidade (F3)** + **conversação (F5)**.

---

## Retro do workshop (AC-12)

Três perguntas. Post-its. Honestidade.

1. **O que eu aprendi que não sabia antes?**
2. **O que foi mais desafiador?** (e como destravei)
3. **O que eu usaria em produção amanhã?**

> Vocês não aprenderam 6 tecnologias soltas.
> Aprenderam a **compor** e **observar** um sistema distribuído.

---

## O sistema completo — os 6 nós

```
  ┌──────────────┐   X-Correlation-ID    ┌──────────────┐
  │ [0] Gateway  │ ───────────────────▶  │ [1] Function │
  │     YARP     │   (nó zero, F2)       │   Entry (F1) │
  └──────────────┘                       └──────┬───────┘
        ▲ JWT Entra (F3)                        │ publica
        │                                       ▼
  ┌──────────────┐                       ┌──────────────┐
  │  Você / UI   │                       │ [2] Service  │
  └──────────────┘                       │   Bus (F1)   │
                                         └──────┬───────┘
  ┌──────────────┐    grava + dispara          │ consome
  │ [5] SQL (F1) │ ◀──────────────┐            ▼
  │ correlation_ │                │     ┌──────────────┐
  │     id       │                └─────┤ [3] Function │
  └──────────────┘                      │ Consumer(F1) │
                                        └──────┬───────┘
  ┌──────────────┐    webhook            │ dispara
  │ [4] n8n (F4) │ ◀────────────────────┘
  │ post-purchase│
  └──────────────┘

      Tudo observado em tempo real por [F6] Flow Visualizer
      (App Insights · Azure.Monitor.Query · SignalR · framer-motion)
```

---

## Você terminou.

6 fases. 6 microserviços. 40 horas.
Um sistema distribuído Azure-native — **construído e observável**.

Tag **`v2.0.0`** na `main`. 🎉

> "Vocês passaram 40 horas tornando visível algo que dura meio segundo
> e que ninguém nunca vê. **Isso é engenharia.**"

**Obrigado — e bons sistemas distribuídos.**
