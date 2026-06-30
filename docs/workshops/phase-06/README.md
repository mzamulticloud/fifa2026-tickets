# F6 — Flow Visualizer: Distributed Tracing, Correlation ID e o Fluxo Animado em Tempo Real

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" (40h) · **Fase 6 de 6 — a final**
> **Tempo estimado de leitura:** 40-50 min · **Faça ANTES da aula** (esta é a segunda das duas fases longas — 8h).
> **Story:** [2.6](../../stories/2.6.story.md) · **Decisões de arquitetura:** [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md) (Invariante 5 — correlation ponta-a-ponta) + [ADE-004](../../architecture/ade-004-gateway-yarp.md) (gateway YARP é o **nó zero** que injeta `X-Correlation-ID`) + [ADE-005](../../architecture/ade-005-identity-easy-auth.md) (identidade Entra; o serviço FlowEvents fica atrás do gateway)
> **Continuidade:** esta fase **fecha** o ciclo de [F1](../phase-01/README.md) (compra → fila → consumer → SQL), [F2](../phase-02/README.md) (gateway YARP), [F3](../phase-03/README.md) (identidade Entra), [F4](../phase-04/README.md) (n8n) e [F5](../phase-05/README.md) (MCP + chatbot). Você não vai construir um novo subsistema de negócio — vai **enxergar todos os anteriores funcionando juntos**, em tempo real, numa única tela.

---

## 0. Por que você está lendo isto antes da aula (e por que esta fase é diferente)

Nas cinco fases anteriores, você **construiu** peças: uma compra que vira mensagem na fila e depois linha no banco (F1), um gateway profissional que protege o caminho (F2), identidade que sabe **quem** está chamando (F3), automação visual pós-compra (F4) e um chatbot que conversa com dados reais (F5). Cada peça foi um microserviço .NET dedicado, deployado como Container App, seguindo o mesmo padrão.

Mas há um problema humano com sistemas distribuídos: **eles são invisíveis**. Quando o usuário clica em "comprar", o que acontece de verdade são seis saltos por componentes diferentes — gateway, function de entrada, fila do Service Bus, function consumidora, n8n e SQL — cada um num processo separado, possivelmente em máquinas diferentes, cada um com seu próprio log. Se você abrir o App Insights cru, vai ver milhares de linhas de telemetria de todas as compras misturadas. Como saber **quais** dessas linhas pertencem à **mesma** compra? E como mostrar isso a alguém que não é engenheiro?

A Fase 6 responde a essas duas perguntas e termina o workshop com a sua **estrela didática**: a rota `/flow`. Nela, você seleciona uma compra e vê uma "bolinha" animada percorrer os **6 nós** do fluxo, em ordem, cada nó mostrando quanto tempo levou, se deu certo, e o que trafegou. É a tradução visual de tudo que você aprendeu — a prova de que aquela complexidade desacoplada existe e funciona.

> **A frase âncora da fase:** "Se você não consegue **ver** o fluxo, você não entende o sistema — você só torce para ele funcionar." O Flow Visualizer transforma torcida em observabilidade.

O segredo que torna isso possível tem um nome: **correlation ID**. Um identificador único que nasce no **gateway YARP** (o nó zero) e viaja, costurado em cada salto, até o SQL. Com ele, você consulta o App Insights e pergunta: "me dê **todas** as linhas de telemetria desta **uma** compra, em ordem". É distributed tracing na sua forma mais didática.

Esta leitura cobre:

1. O que é **observabilidade** e por que `log` não é o suficiente em sistemas distribuídos
2. O que é **distributed tracing** e o que é um **correlation ID** (a ideia central)
3. Os **6 nós** do fluxo — com o **Gateway YARP como nó zero** (e por que **não existe APIM** aqui)
4. Como o correlation ID **se propaga** pelos 6 componentes (a matriz completa)
5. O que é o **App Insights** e como o serviço `FlowEvents` consulta a telemetria via SDK (`Azure.Monitor.Query`)
6. O que é o **SignalR** e como ele empurra eventos em tempo real para o navegador
7. Como tudo se encaixa: a rota `/flow`, os contratos exatos e a animação `framer-motion`
8. Glossário, a amarração das 5 fases anteriores e o checklist de pré-aula

> **Pré-requisitos de conhecimento:** você fez F1-F5. Você programa em qualquer linguagem. **Não exigimos** experiência prévia com observabilidade, tracing, App Insights ou SignalR. Se você já caçou um bug olhando logs e pensou "queria ver isso como um filme", você já sentiu a dor que esta fase resolve.

---

## 1. Observabilidade: por que `log` não basta

Num programa que roda num único processo, depurar é fácil: você lê o log de cima a baixo, na ordem em que as coisas aconteceram. Tudo está num lugar só, numa linha do tempo só.

Sistemas distribuídos quebram essa premissa. A sua compra v2 atravessa **seis processos diferentes**:

- o **gateway YARP** (um Container App)
- a **PurchaseEntryFunction** (Azure Functions)
- o **Service Bus** (broker gerenciado)
- a **PurchaseConsumerFunction** (Azure Functions)
- o **n8n** (Container App)
- o **SQL** (Azure SQL Database)

Cada um gera o seu próprio log, no seu próprio tempo, possivelmente em ordem entrelaçada com **outras** compras acontecendo ao mesmo tempo. Olhar o log de um componente isolado é como ler uma página arrancada de seis livros diferentes: você vê fragmentos, nunca a história inteira de **uma** transação.

**Observabilidade** é a capacidade de responder perguntas sobre o sistema **sem precisar mudar o código** para investigar. As três perguntas que o Flow Visualizer responde são:

1. **Onde** a compra está agora? (qual nó)
2. **Quanto tempo** cada salto levou? (latência por hop)
3. **Deu certo** em cada salto? (status ok/erro)

Para responder isso, você precisa de duas coisas: (a) um **identificador comum** que apareça no log de todos os seis componentes — o correlation ID; e (b) um lugar central que **junte** toda a telemetria — o App Insights. As próximas seções constroem essas duas ideias.

---

## 2. Distributed tracing e o correlation ID (a ideia central)

**Distributed tracing** é a técnica de seguir **uma** requisição enquanto ela atravessa múltiplos serviços. A peça fundamental é o **correlation ID**: um identificador único (no nosso caso, um **GUID**) atribuído à transação **no ponto de entrada** e **carregado adiante** em cada salto.

A intuição é a etiqueta de bagagem do aeroporto. Quando você despacha uma mala, ela ganha um código único. A mala passa por esteiras, aviões, conexões — cada ponto registra "vi a mala XYZ às 14h32". No fim, alguém pode reconstruir a jornada **inteira** da mala XYZ só filtrando por aquele código. O correlation ID é a etiqueta; a sua compra é a mala; os 6 nós são os pontos de transferência.

### 2.1 Onde nasce o correlation ID: o gateway YARP (nó zero)

No nosso sistema, o correlation ID **nasce no gateway YARP** — o mesmo gateway que você construiu na F2. Ele é o **nó zero** do tracing porque é a **borda** do sistema: toda request entra por ele.

No código real (`src/Fifa2026.V2.Gateway/Program.cs`, via `AddRequestTransform`), o gateway faz exatamente isto:

```csharp
var incoming = transformContext.HttpContext.Request.Headers[CorrelationHeader].ToString();
var correlationId = string.IsNullOrWhiteSpace(incoming)
    ? Guid.NewGuid().ToString()       // gera um GUID novo se o cliente não mandou
    : incoming;                        // ou reaproveita o que veio
transformContext.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId);
transformContext.HttpContext.Response.Headers[CorrelationHeader] = correlationId; // devolve ao cliente
```

Três coisas acontecem aqui, e cada uma é deliberada:

1. **Gera se ausente:** se a request chega sem `X-Correlation-ID`, o gateway cria um GUID novo. Toda compra ganha uma etiqueta, sem exceção.
2. **Injeta downstream:** o header é adicionado à request encaminhada à Function de entrada — é assim que o ID começa a viajar.
3. **Devolve ao cliente:** o mesmo ID volta no header da resposta, então o navegador sabe **qual** correlation ID corresponde à compra que acabou de fazer (e pode ir direto buscá-la em `/flow`).

> **Ponto que vale ouro:** o nó zero **tem** que ser a borda. Se cada componente gerasse o seu próprio ID, você não conseguiria costurar a jornada. Centralizar a geração no gateway é o que faz o tracing funcionar. Esse é o papel do gateway que a F2 não tinha como mostrar ainda — agora ele se revela.

### 2.2 W3C Trace Context: o tracing "nativo" do .NET

Além do nosso `X-Correlation-ID` (de negócio, legível), os SDKs do Azure Functions e do Service Bus propagam automaticamente o **W3C Trace Context** — um padrão da W3C (cabeçalho `traceparent`) que o App Insights entende nativamente para correlacionar traces entre serviços .NET. Os dois andam juntos: o `traceparent` dá a correlação técnica automática (via `Activity.Current`), e o nosso `X-Correlation-ID` dá a correlação **de negócio** que o Flow Visualizer filtra e exibe. Você não precisa configurar o `traceparent` — ele vem de graça dos SDKs.

---

## 3. Os 6 nós do fluxo — Gateway YARP é o nó zero (NÃO existe APIM)

Esta é a parte mais importante para a fidelidade da fase. O Flow Visualizer mostra **exatamente seis nós**, nesta ordem:

```
[0] Gateway YARP      ← NÓ ZERO: gera/injeta X-Correlation-ID, valida JWT Entra (F3)
[1] Function Entry    ← PurchaseEntryFunction: valida e publica no Service Bus
[2] Service Bus       ← fila tickets-purchase: desacopla entrada e processamento
[3] Function Consumer ← PurchaseConsumerFunction: grava no SQL e dispara o n8n
[4] n8n               ← workflow post-purchase-notification: log estruturado
[5] SQL               ← purchases.correlation_id: fim do fluxo, compra persistida
```

> **Atenção — leia isto com cuidado:** o nó zero é o **Gateway YARP**. **NÃO existe APIM** (Azure API Management) neste workshop. Em arquiteturas Azure você verá muito material citando APIM como a porta de entrada que injeta correlation IDs — e isso é verdade **em outras arquiteturas**. Aqui, no EPIC-002, a porta de entrada é o **gateway YARP** que você mesmo construiu na F2 (um ASP.NET Core + `Yarp.ReverseProxy`, hospedado como Container App por aluno). Se em algum momento você ler "APIM" associado a este fluxo, é um resquício de material antigo — corrija mentalmente para **Gateway YARP**. O código não tem nenhuma referência a APIM como componente; há apenas comentários corretivos lembrando "NUNCA APIM".

Esses seis nós são a **fonte única da verdade** em dois lugares do código, que precisam bater 1:1:

- **Backend:** `src/Fifa2026.V2.FlowEvents/Models/FlowEventType.cs` — um `enum` com 6 membros, o ordinal `0` sendo `GATEWAY_YARP_RECEIVED`.
- **Frontend:** `Lovable/World Cup Tickets Hub/src/lib/flowNodes.ts` — um array `FLOW_NODES` com 6 entradas, índice `0` sendo `Gateway YARP`.

O backend define os tipos de evento exatamente assim (`FlowEventType.cs`):

| Ordinal | EventType (backend) | Nó (frontend) | O que sinaliza |
|---|---|---|---|
| 0 | `GATEWAY_YARP_RECEIVED` | Gateway YARP | request recebida; X-Correlation-ID injetado |
| 1 | `FUNCTION_ENTRY_PROCESSED` | Function Entry | compra validada; publicada na fila |
| 2 | `SERVICE_BUS_PUBLISHED` | Service Bus | mensagem enfileirada em `tickets-purchase` |
| 3 | `FUNCTION_CONSUMER_DONE` | Function Consumer | consumida; gravada no SQL; n8n disparado |
| 4 | `N8N_WEBHOOK_TRIGGERED` | n8n | workflow `post-purchase-notification` executado |
| 5 | `SQL_INSERTED` | SQL | linha em `purchases.correlation_id` |

> **Por que a ordem do `enum` importa?** Porque o **ordinal** (0..5) é o número do nó usado pela animação da bolinha no front. Se alguém reordenasse o `enum`, a bolinha pularia para o lugar errado. Por isso a ordem é tratada como contrato, com um teste de regressão (`Node_zero_is_gateway_yarp_never_apim`) que **falha** se o nó zero deixar de ser o Gateway YARP.

---

## 4. Como o correlation ID se propaga pelos 6 componentes

O correlation ID só é útil se ele aparecer no log de **todos** os seis nós. Aqui está a matriz real de propagação — onde cada componente **lê** o ID e onde ele **escreve** o ID. Esta tabela reflete a implementação confirmada no código (não a redação idealizada): o ID trafega no **corpo da mensagem** + `ILogger.BeginScope`, e é via `BeginScope` que ele vira uma **customDimension** no App Insights — que é exatamente o que o FlowEvents consulta.

| Salto | Componente | Lê o ID de | Escreve o ID em | Confirmado em |
|---|---|---|---|---|
| 0 → 1 | **Gateway YARP** | header `X-Correlation-ID` (ou gera GUID) | header downstream + header da resposta ao cliente | `src/Fifa2026.V2.Gateway/Program.cs` |
| 1 | Function Entry | header `X-Correlation-ID` recebido | corpo da `PurchaseMessage` + `ILogger.BeginScope(["CorrelationId"])` | `src/Fifa2026.V2.Functions/Functions/PurchaseEntryFunction.cs` |
| 2 | Service Bus | (carrega no corpo da mensagem) | mensagem `tickets-purchase` | broker |
| 3 | Function Consumer | corpo da `PurchaseMessage` | `ILogger.BeginScope(["CorrelationId"])` + `purchases.correlation_id` | `src/Fifa2026.V2.Functions/Functions/PurchaseConsumerFunction.cs` |
| 4 | n8n | `payload.correlationId` do webhook | execution log do n8n (Set node) | workflow n8n (F4) |
| 5 | SQL | (gravado pelo consumer) | coluna `correlation_id UNIQUEIDENTIFIER` | migration `phase-01.sql` |

> **Nota de fidelidade (importante para o instrutor não tropeçar):** o AC-4 da story menciona `ApplicationProperties` do Service Bus como o veículo do ID. A **implementação real** carrega o correlationId no **corpo** da `PurchaseMessage` e o registra via `ILogger.BeginScope` — e é o `BeginScope` que produz `customDimensions.CorrelationId` no App Insights. O Flow Visualizer consulta **traces** filtrando `customDimensions.CorrelationId`, portanto o caminho real de tracing é corpo + `BeginScope`, não `ApplicationProperties`. Quando ensinar, descreva o que o código faz. (Esse alinhamento já foi registrado pelo PO no gate S2.4.)

### 4.1 O elo entre `BeginScope` e o App Insights

`ILogger.BeginScope(new Dictionary<string,object>{ ["CorrelationId"] = id })` abre um **escopo** de log. Tudo que for logado dentro daquele `using` carrega o `CorrelationId`. O provider do App Insights converte propriedades de escopo em **`customDimensions`** do trace. É por isso que, no App Insights, cada linha de log daquela compra tem `customDimensions.CorrelationId = <guid>`. Sem `BeginScope`, o ID não viraria customDimension e o Flow Visualizer não encontraria os eventos. Esse é o elo invisível que faz tudo funcionar (ADE-000 Invariante 5).

---

## 5. O App Insights e o serviço FlowEvents

O **Application Insights** é o serviço de APM (Application Performance Monitoring) do Azure. Todos os seis componentes enviam telemetria para ele (na verdade, para um **Log Analytics workspace** que o App Insights usa por baixo). É o "lugar central" que junta tudo, da seção 1.

O serviço novo desta fase — **`Fifa2026.V2.FlowEvents`** (`src/Fifa2026.V2.FlowEvents/`) — é quem **lê** essa telemetria e a transforma em eventos do diagrama.

### 5.1 Por que um serviço ASP.NET Core (e não uma Azure Function)

A story chama esse componente de "FlowEventsFunction", mas a implementação é um **serviço ASP.NET Core .NET 8** (mesmo padrão de host do MCP Server da F5, em Container App). O motivo é direto: o AC-2 exige o SignalR em **Service Mode: Default (Hub clássico)**, que precisa de um **host de longa duração** mantendo as conexões WebSocket abertas. O runtime **serverless** das Functions não combina com isso — ele sobe e desce por demanda. Logo, o FlowEvents é um host persistente, como o gateway e o MCP Server. (Decisão de @dev, alinhada com @architect; veja as Completion Notes da story.)

### 5.2 Como o FlowEvents consulta o App Insights: `Azure.Monitor.Query`

Aqui está um ponto de anti-alucinação (AC-13) que vale a pena entender. Há mais de uma forma de "falar" com o App Insights, e é fácil escolher a errada:

- **`TelemetryClient`** (`Microsoft.ApplicationInsights`) — serve para **escrever** telemetria, não para consultá-la. Errado para o nosso caso.
- **REST `api.applicationinsights.io`** — a API REST legada, em processo de desativação. Evitar.
- **`Azure.Monitor.Query` (`LogsQueryClient`)** — o **SDK oficial do Azure for .NET para consultar** logs/traces via Kusto. **É o que usamos.**

No código real (`src/Fifa2026.V2.FlowEvents/Data/AppInsightsFlowEventRepository.cs`), o cliente é criado com `DefaultAzureCredential` (Managed Identity no Container App; `az login`/Visual Studio em dev) e consulta o workspace via `QueryWorkspaceAsync`:

```csharp
_client = new LogsQueryClient(new DefaultAzureCredential());
// ...
var response = await _client.QueryWorkspaceAsync(
    _workspaceId,                          // App Setting LogAnalyticsWorkspaceId (nunca hardcoded)
    query,                                 // Kusto parametrizado
    new QueryTimeRange(TimeSpan.FromHours(1)),
    cancellationToken: cancellationToken);
```

A query Kusto da timeline (também no código) filtra exatamente pela customDimension que o `BeginScope` produziu:

```kusto
traces
| where tostring(customDimensions.CorrelationId) == correlationId
| project timestamp, message, severityLevel, cloud_RoleName, customDimensions
| order by timestamp asc
| limit 100
```

> **Detalhe de segurança real no código:** o `correlationId` é input externo, então a query usa parametrização Kusto (`declare query_parameters(correlationId:string = ...)`) **e** sanitiza o valor para conter apenas hex e hífen (é sempre um GUID) — defesa em profundidade contra injeção. E a autorização é via **Managed Identity** com o papel **Log Analytics Reader** no workspace (você configura isso no PORTAL-GUIDE), nunca uma key embutida.

### 5.3 Do trace bruto ao nó tipado: o `TraceEventMapper`

O App Insights devolve linhas de `traces` cruas (mensagem + `cloud_RoleName` + severidade). O `TraceEventMapper` (lógica pura, 100% testável sem App Insights vivo) classifica cada linha num dos 6 `FlowEventType` pelo **componente emissor** e pelo **conteúdo da mensagem** — por exemplo, "Compra v2 recebida" → `FUNCTION_ENTRY_PROCESSED`, menção a `tickets-purchase` publicada → `SERVICE_BUS_PUBLISHED`, "gravada com sucesso" → `SQL_INSERTED`. Linhas que não correspondem a nenhum hop (ex.: ruído de health probe) são descartadas. A severidade vira o status: `severityLevel >= 3` (Error/Critical) → `"error"`, senão `"ok"`.

---

## 6. SignalR: empurrando eventos em tempo real para o navegador

Consultar a timeline sob demanda (polling) funciona, mas a mágica didática é a bolinha **animando ao vivo** conforme a compra avança. Para isso, o servidor precisa **empurrar** eventos para o navegador sem ele ficar perguntando. Essa é a função do **SignalR**.

O **Azure SignalR Service** é o serviço gerenciado do Azure para comunicação em tempo real (WebSockets, com fallbacks). Ele escala as conexões por você. Nesta fase, ele é provisionado no **tier Free (Free_F1, 20 conexões)**, em **Service Mode: Default**.

> **Por que Service Mode "Default" e não "Serverless"?** Porque o nosso `FlowHub` é um **Hub clássico hospedado** pelo serviço FlowEvents (via `AddSignalR().AddAzureSignalR(...)`). O modo **Serverless** seria para Azure Functions sem host persistente — não é o nosso caso. O modo **Default** é o que permite um Hub .NET de longa duração gerenciar grupos e empurrar mensagens. Escolher o modo errado aqui é um erro comum: anote.

### 6.1 O Hub, os grupos e o método

O `FlowHub` (`src/Fifa2026.V2.FlowEvents/Hubs/FlowHub.cs`) é minimalista e elegante:

```csharp
public sealed class FlowHub : Hub
{
    public static string GroupName(string correlationId) => $"correlation-{correlationId}";
    public Task Subscribe(string correlationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(correlationId));
    public Task Unsubscribe(string correlationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(correlationId));
}
```

A ideia dos **grupos** é o que mantém tudo eficiente: cada compra observada vira um grupo `correlation-<id>`. Quando o usuário seleciona uma compra, o cliente entra naquele grupo. O servidor empurra os eventos daquela compra **só** para quem está no grupo — não para todos os navegadores conectados. O método server→client chama-se **`FlowEvent`** (`SignalRFlowEventPublisher` envia via `IHubContext<FlowHub>.Clients.Group(...).SendAsync("FlowEvent", evento)`).

### 6.2 O cliente: `@microsoft/signalr` + fallback polling

No front, o hook `useFlowConnection.ts` usa o pacote oficial `@microsoft/signalr`:

```ts
const connection = new HubConnectionBuilder()
  .withUrl(FLOW_HUB_URL)          // .../flow-events/hubs/flow (via gateway)
  .withAutomaticReconnect()
  .build();

connection.on('FlowEvent', (event) => { /* anima o nó event.nodeIndex */ });
// ao selecionar uma compra:
connection.invoke('Subscribe', correlationId);
```

E o **fallback polling (2s)** é a rede de segurança do AC-6: se o WebSocket não conectar (CORS, proxy, firewall corporativo), o hook cai para chamar `GET /api/flow/{correlationId}` a cada 2 segundos. A experiência degrada graciosamente — a bolinha ainda anda, só não tão "ao vivo".

### 6.3 O `replay`: como a bolinha "anda" para uma compra antiga

Compras antigas já aconteceram — não há eventos "novos" para empurrar. Então o front, ao selecionar uma compra, chama `POST /api/flow/{correlationId}/replay`. O backend **relê** a telemetria daquela compra no App Insights e **reempurra** cada evento via SignalR para o grupo. Do ponto de vista do navegador, é indistinguível de uma compra acontecendo agora: os eventos chegam pelo `FlowEvent` e a bolinha percorre os nós. É o "replay" que torna a demo reproduzível mesmo fora do horário de pico.

---

## 7. Onde tudo se encaixa: a rota `/flow` e os contratos exatos

### 7.1 Tudo passa pelo gateway (herança F2/F3)

O front **não fala direto** com o serviço FlowEvents. Ele fala com o **gateway YARP** (`VITE_FLOW_EVENTS_BASE_URL = .../flow-events`), que tem um cluster/rotas novos (`/flow-events/api/**` e `/flow-events/hubs/**` para o WebSocket). O gateway continua sendo o nó zero (mesmo transform global de `X-Correlation-ID`) e o guardião de identidade (Bearer Entra da F3). O serviço FlowEvents fica **atrás** dele e não revalida o JWT.

### 7.2 Os contratos exatos

| Contrato | Valor | Onde |
|---|---|---|
| Lista de compras recentes | `GET {FLOW_BASE}/api/flow/recent?top=50` | `flowApi.ts` → `FlowEndpoints.cs` |
| Timeline de uma compra (fallback polling) | `GET {FLOW_BASE}/api/flow/{correlationId}` | `flowApi.ts` → `FlowEndpoints.cs` |
| Replay (dispara animação) | `POST {FLOW_BASE}/api/flow/{correlationId}/replay` | `flowApi.ts` → `FlowEndpoints.cs` |
| Hub SignalR (WebSocket) | `{FLOW_BASE}/hubs/flow` | `useFlowConnection.ts` → `FlowHub.cs` |
| Subscribe a uma compra | `connection.invoke("Subscribe", correlationId)` | cliente → `FlowHub.Subscribe` |
| Evento server→client | `connection.on("FlowEvent", handler)` | `FlowHub`/`SignalRFlowEventPublisher` |
| Env var do front | `VITE_FLOW_EVENTS_BASE_URL` | build do Vite |
| App Setting do workspace | `LogAnalyticsWorkspaceId` (GUID) | Container App do FlowEvents |
| App Setting do SignalR | `AzureSignalRConnectionString` | Container App do FlowEvents |
| App Setting do CORS | `FrontendOrigin` | Container App do FlowEvents |
| Identidade do FlowEvents → App Insights | Managed Identity + papel **Log Analytics Reader** no workspace | RBAC do workspace |

### 7.3 A página `/flow` e a animação

A rota `/flow` (lazy-loaded em `App.tsx`, chunk próprio para não pesar o bundle inicial) tem três peças (`src/components/flow/`):

- **`RecentPurchases`** — lista das últimas 50 compras (sortable por data, searchable por correlationId).
- **`FlowDiagram`** — os 6 nós em ordem (de `flowNodes.ts`), com a bolinha `framer-motion` (`motion.div` com `transition` spring) percorrendo de nó em nó conforme os eventos chegam.
- **`FlowNodeCard`** — cada nó, mostrando duração do hop, status (ok/erro) e payload inspecionável (shadcn/ui `Sheet`).

**Acessibilidade (AC-9):** cada nó é um `<button>` com `aria-label` descritivo; o diagrama é um `<ol>` semântico; a bolinha é `aria-hidden`; há um **modo lista** (sem animação) toggleável e ativado automaticamente por `prefers-reduced-motion`; busca e ordenação têm aria-labels.

---

## 8. A amarração das 5 fases anteriores — o "filme" completo

Esta é a fase que costura tudo. Quando a bolinha percorre os 6 nós, ela está, literalmente, refazendo o caminho que você construiu fase a fase:

| Nó | Construído em | O que você aprendeu lá |
|---|---|---|
| **[0] Gateway YARP** | **F2** | reverse proxy, roteamento, injeção de header — agora revelado como o nó zero do tracing |
| **[1] Function Entry** | **F1** | endpoint de entrada que publica na fila (desacoplamento) |
| **[2] Service Bus** | **F1** | fila como amortecedor entre entrada e processamento |
| **[3] Function Consumer** | **F1** | consumidor idempotente que persiste e dispara efeitos |
| **[4] n8n** | **F4** | automação visual pós-compra (low-code) |
| **[5] SQL** | **F1** | persistência com a coluna `correlation_id` |

E o que **atravessa** todos eles — o correlation ID — depende da **identidade** da **F3** (tudo passa autenticado pelo gateway) e usa o mesmo padrão de hospedagem da **F5** (serviço .NET dedicado em Container App). O Flow Visualizer não é uma sexta peça isolada: é o **espelho** das cinco anteriores.

> **A grande sacada didática:** mostre a tela `/flow` a alguém que nunca programou. A bolinha andando pelos seis nós comunica, em segundos, a complexidade desacoplada por trás de um clique em "comprar ingresso" — algo que nenhum diagrama estático e nenhuma explicação verbal consegue. Essa é a prova de que você entendeu o sistema: você consegue **mostrá-lo**.

---

## 9. Glossário

| Termo | Significado |
|---|---|
| **Observabilidade** | Capacidade de responder perguntas sobre o sistema sem mudar o código para investigar |
| **Distributed tracing** | Seguir uma requisição enquanto ela atravessa múltiplos serviços |
| **Correlation ID** | Identificador único (GUID) da transação, criado no nó zero e propagado em cada salto |
| **Gateway YARP** | A porta de entrada (ASP.NET Core + `Yarp.ReverseProxy`, F2); o **nó zero** que injeta `X-Correlation-ID`. **NÃO é APIM** |
| **`X-Correlation-ID`** | Header de negócio que carrega o correlation ID entre componentes |
| **W3C Trace Context (`traceparent`)** | Padrão de correlação técnica automática entre serviços .NET, propagado pelos SDKs |
| **App Insights** | Serviço de APM do Azure que centraliza a telemetria (usa um Log Analytics workspace) |
| **`customDimensions.CorrelationId`** | A propriedade do trace (vinda do `BeginScope`) que o Flow Visualizer filtra |
| **`Azure.Monitor.Query` / `LogsQueryClient`** | SDK oficial .NET para **consultar** logs/traces via Kusto (o que o FlowEvents usa) |
| **Kusto (KQL)** | Linguagem de query do Log Analytics/App Insights |
| **Managed Identity** | Identidade gerenciada do Azure; o FlowEvents a usa (papel Log Analytics Reader) para ler sem keys |
| **SignalR** | Tecnologia de tempo real (WebSocket + fallbacks); empurra eventos do servidor ao navegador |
| **Service Mode: Default** | Modo do Azure SignalR para Hub clássico hospedado (o nosso caso) — NÃO Serverless |
| **Hub / Grupo** | Abstração SignalR: o `FlowHub` agrupa clientes por `correlation-<id>` |
| **Replay** | Reler a telemetria de uma compra e reempurrar via SignalR para animar a bolinha |
| **`framer-motion`** | Biblioteca React de animação; `motion.div` move a bolinha entre os nós |

---

## 10. Checklist de pré-aula

Antes de entrar na F6, confirme:

- [ ] Li esta página inteira e entendi a diferença entre **log** (um processo) e **distributed tracing** (vários processos costurados por um ID).
- [ ] Sei que o **correlation ID nasce no Gateway YARP** (nó zero) e que **NÃO existe APIM** neste workshop.
- [ ] Sei nomear os **6 nós na ordem**: Gateway YARP → Function Entry → Service Bus → Function Consumer → n8n → SQL.
- [ ] Entendi que o ID vira **`customDimensions.CorrelationId`** no App Insights via `ILogger.BeginScope`, e é por isso que o FlowEvents o encontra.
- [ ] Sei que o FlowEvents consulta o App Insights com **`Azure.Monitor.Query` (`LogsQueryClient`)** e Managed Identity (papel Log Analytics Reader) — não com key, não com a REST legada.
- [ ] Entendi por que o SignalR é **Service Mode: Default** (Hub clássico hospedado) e não Serverless.
- [ ] Sei que há **fallback polling (2s)** se o WebSocket falhar, e o que o **replay** faz.
- [ ] Tenho as **F1-F5 funcionando** (compra → fila → consumer → SQL; gateway; identidade; n8n; MCP/chatbot).
- [ ] Tenho o repositório na branch `phase-06-flow-visualizer`.

> **Próximo passo:** na aula, você provisiona o Azure SignalR Service (Free), deploya o serviço FlowEvents como Container App (com Managed Identity + papel Log Analytics Reader), faz o build do front com a rota `/flow` e roda o smoke ao vivo — faz uma compra v2 e **vê a bolinha andar**. O passo-a-passo está no [PORTAL-GUIDE](./PORTAL-GUIDE.md). É a sua última fase: vá com tudo.
