# F6 — Speaker Notes: roteiro de 8h + o grande encerramento do workshop

> **Notas do instrutor** · Workshop "Living Lab Azure-Native" · **Fase 6 de 6 — a final** · **Duração: 8h** (a segunda das duas fases longas)
> **Story:** [2.6](../../stories/2.6.story.md) · **Arquitetura:** [ADE-004](../../architecture/ade-004-gateway-yarp.md) (gateway YARP = nó zero) + [ADE-000](../../architecture/ade-000-microservice-parallel-pattern.md) (Inv 5)
> **Material de apoio:** [README](./README.md) (leitura prévia), [PORTAL-GUIDE](./PORTAL-GUIDE.md) (passo-a-passo), [slides](./slides.md), [intro-video-script](./intro-video-script.md)

Estas notas são para **você, instrutor**. Trazem o cronômetro, as perguntas para a turma, os pontos onde o aluno trava, a demo "para um leigo", o **grande encerramento** e a **retro do workshop completo**. Esta é a fase em que você colhe tudo que plantou nas cinco anteriores — reserve energia para o final.

> **Premissa de fidelidade:** todo número, nome de tipo, endpoint e arquivo aqui bate com o código real (`src/Fifa2026.V2.FlowEvents/`, `Lovable/World Cup Tickets Hub/src/`, `src/Fifa2026.V2.Gateway/`). Se a turma perguntar "onde isso está no código?", aponte o arquivo. **Não invente APIs.** E a regra inegociável da fase: o nó zero é o **Gateway YARP** — **NUNCA APIM** (APIM não existe no EPIC-002).

---

## Mapa de blocos (8h = 480 min)

| Bloco | Tema | Duração | Acumulado |
|---|---|---|---|
| 0 | Abertura + recap das 5 fases + objetivo da F6 | 25 min | 0:25 |
| 1 | Conceitos: observabilidade, distributed tracing, correlation ID | 60 min | 1:25 |
| 2 | Os 6 nós + propagação do correlation ID (Gateway YARP = nó zero) | 75 min | 2:40 |
| — | **Almoço** | 60 min | 3:40 |
| 3 | FlowEvents .NET: App Insights via `Azure.Monitor.Query` + mapper | 70 min | 4:50 |
| 4 | SignalR: Hub, grupos, tempo real + fallback polling | 60 min | 5:50 |
| 5 | Frontend `/flow`: diagrama, bolinha framer-motion, a11y | 50 min | 6:40 |
| 6 | Deploy + **smoke ao vivo** (a bolinha anda) | 50 min | 7:30 |
| 7 | **Encerramento "para um leigo" + retro do workshop** | 30 min | 8:00 |

> Ajuste a janela do almoço ao seu horário; mantenha Conceitos (B1-B2) antes do almoço e Deploy/Encerramento (B6-B7) na energia da tarde. O Bloco 7 é o coração emocional do workshop — não o atropele.

---

## Bloco 0 — Abertura + recap das 5 fases (25 min)

**Objetivo:** reancorar a turma na jornada inteira e vender o "porquê" da fase final.

- Recap em 1 frase por fase, escrevendo no quadro a sequência que vai virar os 6 nós:
  - **F1** — Compra: `POST /purchase` → fila → consumer → SQL.
  - **F2** — Gateway YARP profissional (injeta `X-Correlation-ID`).
  - **F3** — Identidade Entra (JWT validado, `oid` → `X-Entra-OID`).
  - **F4** — Automação visual (n8n).
  - **F5** — Conversa com o sistema (MCP + chatbot).
- Frase âncora no quadro: **"Se você não consegue VER o fluxo, você não entende o sistema — você só torce para ele funcionar."**
- Venda a fase: "Hoje você não constrói uma sexta peça de negócio. Hoje você constrói o **espelho** das cinco — uma tela que mostra a compra atravessando tudo, ao vivo."

**Pergunta para a turma:** "Quando uma compra dá errado, hoje, onde vocês olhariam?" → respostas variadas (log do gateway? da function? do SQL?). Esse é o gancho: "e se desse para ver a compra inteira, costurada, numa tela só?"

> **Onde travam (emocional):** alguns acham que "só visualizar" é menos importante que "construir". Reposicione: observabilidade é o que separa um protótipo de um sistema de produção. Você não opera o que não enxerga.

---

## Bloco 1 — Observabilidade, tracing, correlation ID (60 min)

**Objetivo:** a turma entende o problema (sistemas distribuídos são invisíveis) e a solução (correlation ID) **antes** de ver código.

### 1.1 O problema (15 min)
- Desenhe os 6 processos separados, cada um com seu log. "Como saber quais linhas são da MESMA compra?"
- Analogia da etiqueta de bagagem: código único na mala, registrada em cada ponto de transferência, jornada reconstruída filtrando pelo código.

### 1.2 Correlation ID e o nó zero (25 min)
- O ID nasce no **Gateway YARP** (nó zero). Mostre o trecho real de `src/Fifa2026.V2.Gateway/Program.cs` (`AddRequestTransform`): gera GUID se ausente, injeta downstream, devolve ao cliente.
- **Insista nas três coisas:** gera se ausente, injeta downstream, devolve ao cliente. Pergunte: "por que devolver ao cliente?" → para o navegador saber qual ID procurar no `/flow`.

### 1.3 O grande alerta da fase (10 min) — APIM NÃO existe
- Escreva no quadro: **"Nó zero = Gateway YARP. NÃO existe APIM aqui."**
- Explique o porquê do alerta: muito material Azure cita APIM como porta de entrada que injeta correlation IDs. É verdade **em outras arquiteturas**. Aqui, a porta é o gateway YARP que **eles** construíram na F2.
- O código já trava isso: teste `Node_zero_is_gateway_yarp_never_apim`. Se alguém digitar "APIM" no diagrama, o teste falha.

### 1.4 W3C Trace Context (10 min)
- Mencione que, além do nosso `X-Correlation-ID` (de negócio), os SDKs propagam o `traceparent` (W3C) automaticamente — correlação técnica de graça. Não precisa configurar.

**Pergunta para a turma:** "Se cada componente gerasse seu próprio ID, o tracing funcionaria?" → Não — você não conseguiria costurar a jornada. Por isso a geração é **centralizada** no nó zero.

---

## Bloco 2 — Os 6 nós + propagação (75 min)

**Objetivo:** fixar os 6 nós (na ordem) e como o ID viaja por cada um. Hands-on nas fontes da verdade.

### 2.1 Os 6 nós, fonte única (25 min)
- Backend: `src/Fifa2026.V2.FlowEvents/Models/FlowEventType.cs` — `enum` de 6 membros, ordinal 0 = `GATEWAY_YARP_RECEIVED`.
- Frontend: `Lovable/World Cup Tickets Hub/src/lib/flowNodes.ts` — `FLOW_NODES` de 6 entradas, índice 0 = `Gateway YARP`.
- Reforce: **o ordinal é o número do nó na animação**. Reordenar o `enum` quebraria a bolinha. Por isso há teste de regressão.
- Faça a turma recitar em coro os 6 nós: **Gateway YARP → Function Entry → Service Bus → Function Consumer → n8n → SQL.**

### 2.2 A matriz de propagação (30 min) — o ponto de fidelidade
- Mostre a matriz do README (seção 4). Vá hop a hop.
- **Ponto crítico de fidelidade (você precisa saber isto):** o AC-4 da story menciona `ApplicationProperties` do Service Bus. A **implementação real** carrega o correlationId no **corpo** da `PurchaseMessage` + `ILogger.BeginScope`. E é o `BeginScope` que produz `customDimensions.CorrelationId` no App Insights — que é o que o FlowEvents consulta. **Ensine o que o código faz**, não a redação idealizada. (Alinhamento já registrado pelo PO no gate S2.4.)
- O elo invisível: `BeginScope` → customDimension → query Kusto. Sem `BeginScope`, o FlowEvents não acharia nada (ADE-000 Inv 5).

### 2.3 Os 6 EventTypes (20 min)
- Liste os 6 `FlowEventType` (FlowEventType.cs) e o que cada um sinaliza.
- Antecipe o Bloco 3: "como uma linha de log crua vira um desses 6? Isso é o `TraceEventMapper` — vamos ver depois do almoço."

**Pergunta para a turma:** "Onde, no App Insights, está o correlationId de cada linha?" → em `customDimensions.CorrelationId`, colocado lá pelo `BeginScope`.

**Onde travam:** confundir `ApplicationProperties` (redação da story) com corpo+`BeginScope` (código real). Seja explícito: descreva o código.

---

## Bloco 3 — FlowEvents .NET (70 min)

**Objetivo:** construir/entender o serviço que lê o App Insights. Hands-on em `src/Fifa2026.V2.FlowEvents/`.

### 3.1 Por que ASP.NET Core e não Function (10 min)
- A story chama de "FlowEventsFunction", mas é um **serviço ASP.NET Core .NET 8** (mesmo host do MCP Server da F5). Motivo: SignalR **Default mode** exige host de longa duração; serverless não serve. Decisão de @dev alinhada com @architect.

### 3.2 App Insights via `Azure.Monitor.Query` (30 min) — anti-alucinação
- Três formas de "falar" com o App Insights, e a escolha certa:
  - `TelemetryClient` → **escreve** telemetria (errado para query).
  - REST `api.applicationinsights.io` → legada, em desativação (evitar).
  - `Azure.Monitor.Query` / `LogsQueryClient` → SDK oficial para **consultar** (✅ usamos este).
- Mostre `Data/AppInsightsFlowEventRepository.cs`: `new LogsQueryClient(new DefaultAzureCredential())` + `QueryWorkspaceAsync(workspaceId, query, timeRange)`.
- A query Kusto real: `traces | where tostring(customDimensions.CorrelationId) == correlationId | ...`.
- **Segurança real no código:** parametrização Kusto (`declare query_parameters`) + sanitização (só hex e hífen, é um GUID). Defesa em profundidade.
- Auth: **Managed Identity + papel Log Analytics Reader** (não key, não REST legada). Ponto "No Invention" — APIs reais do Azure SDK for .NET.

### 3.3 Do trace bruto ao nó: `TraceEventMapper` (30 min)
- `Data/TraceEventMapper.cs` — lógica **pura** (sem I/O), 100% testável sem App Insights vivo.
- Classifica por `cloud_RoleName` + conteúdo da mensagem: "Compra v2 recebida" → `FUNCTION_ENTRY_PROCESSED`, `tickets-purchase` publicada → `SERVICE_BUS_PUBLISHED`, "gravada com sucesso" → `SQL_INSERTED`, etc.
- Descarta ruído (health probe não é hop).
- Status pela severidade: `severityLevel >= 3` → `"error"`, senão `"ok"`.
- Mostre os testes: `TraceEventMapperTests` cobre cada mapeamento e o teste que força o nó zero = Gateway YARP.

**Pergunta para a turma:** "Por que isolar o mapper como lógica pura?" → testabilidade sem infra viva; é a parte mais propensa a regressão.

**Onde travam:** querer usar `TelemetryClient` para query. Corrija: TelemetryClient é de escrita.

---

## Bloco 4 — SignalR (60 min)

**Objetivo:** entender tempo real, grupos, e o fallback.

### 4.1 SignalR Service Default mode (15 min)
- Azure SignalR Service, Free_F1, **Service Mode: Default**. Explique de novo por que Default e não Serverless (Hub clássico hospedado pelo FlowEvents via `AddAzureSignalR`).
- Erro comum: provisionar Serverless. Sintoma: `AddAzureSignalR` falha ou mensagens não chegam.

### 4.2 Hub, grupos, método (25 min)
- `Hubs/FlowHub.cs`: `Subscribe(correlationId)` → entra no grupo `correlation-<id>`; `Unsubscribe`.
- `Hubs/SignalRFlowEventPublisher.cs`: envia via `IHubContext<FlowHub>.Clients.Group(...).SendAsync("FlowEvent", evento)`.
- A ideia dos grupos: cada compra observada = um grupo. O servidor empurra só para quem está no grupo — não broadcast. Eficiente.

### 4.3 Cliente + fallback + replay (20 min)
- `useFlowConnection.ts`: `HubConnectionBuilder().withUrl(FLOW_HUB_URL).withAutomaticReconnect()`, `connection.on('FlowEvent', ...)`, `connection.invoke('Subscribe', id)`.
- **Fallback polling 2s** (AC-6): se o WebSocket falhar, `GET /api/flow/{id}` a cada 2s. Degrada graciosamente.
- **Replay:** compras antigas não têm eventos novos → `POST /api/flow/{id}/replay` relê a telemetria e reempurra via SignalR. Indistinguível de tempo real para o navegador. É o que torna a demo reproduzível.

**Pergunta para a turma:** "Por que o front pede `replay` para uma compra que já aconteceu?" → porque não há eventos "ao vivo" para empurrar; o backend relê e reempurra.

---

## Bloco 5 — Frontend `/flow` (50 min)

**Objetivo:** a tela. Hands-on em `Lovable/World Cup Tickets Hub/src/`.

### 5.1 Estrutura (20 min)
- Rota `/flow` lazy em `App.tsx` (chunk próprio; framer-motion + signalr fora do bundle inicial — performance AC-8).
- `components/flow/RecentPurchases.tsx` — últimas 50 compras (sortable por data, searchable por correlationId).
- `components/flow/FlowDiagram.tsx` — os 6 nós (de `flowNodes.ts`) + a bolinha.
- `components/flow/FlowNodeCard.tsx` — cada nó: duração, status, payload no `Sheet` (shadcn/ui).

### 5.2 A bolinha framer-motion (15 min)
- `motion.div` com `transition` spring percorrendo de nó em nó conforme `event.nodeIndex` chega.
- API real: `motion.div` / `animate` / `transition`. Ponto "No Invention".

### 5.3 Acessibilidade (15 min) — AC-9, não é opcional
- Cada nó é `<button>` com `aria-label` descritivo; diagrama é `<ol>` semântico; bolinha `aria-hidden`.
- **Modo lista** (sem animação) toggleável + automático via `prefers-reduced-motion`.
- Busca/sort com aria-labels; linhas keyboard-acionáveis.
- Frase: "uma visualização que exclui quem usa leitor de tela não é boa visualização."

---

## Bloco 6 — Deploy + smoke ao vivo (50 min)

**Objetivo:** subir tudo e **fazer a bolinha andar** na frente da turma. Siga o [PORTAL-GUIDE](./PORTAL-GUIDE.md).

- SignalR (Free, Default), FlowEvents (Container App + Managed Identity + Log Analytics Reader), gateway (`FlowEventsUrl`), front (`VITE_FLOW_EVENTS_BASE_URL`).
- **Os 3 erros que vão acontecer ao vivo (esteja pronto):**
  1. **403 na query** → faltou o papel Log Analytics Reader na Managed Identity (Passo 5). É o mais comum.
  2. **Sempre polling, nunca SignalR** → `--transport auto` no Container App ou CORS.
  3. **Bolinha trava num nó logo após a compra** → latência de ingestão do App Insights (~2 min). Por isso a **compra de aquecimento**.

> **Dica de ouro para a demo:** faça **uma compra de aquecimento ~5 min antes** do Bloco 6 (no intervalo do Bloco 5). Quando chegar a hora, essa compra já está indexada no App Insights e a bolinha anda **na hora**, sem esperar ingestão. Faça também uma compra "ao vivo" para mostrar o tempo real — mas tenha a aquecida como rede de segurança.

- Smoke (AC-7): compra v2 → `/flow` → clicar na compra → bolinha percorre os 6 nós em < 30s → cada nó com tempo + payload → correlationId igual do nó 0 ao nó 5.

---

## Bloco 7 — Encerramento "para um leigo" + retro do workshop (30 min)

Este é o momento que justifica o workshop inteiro. Não corra.

### 7.1 A demo "para um leigo" (10 min)
- Convide alguém (um colega de outra área, ou faça a turma imaginar a mãe/o chefe não-técnico).
- Mostre a tela `/flow` e diga **uma frase**: "Quando você clica em comprar um ingresso, é isto que acontece por dentro." Deixe a bolinha andar.
- **Não explique tecnicamente.** Deixe a visualização falar. A bolinha percorrendo seis caixinhas, cada uma acendendo, comunica em 20 segundos o que seis fases de aula construíram.
- Frase de fechamento no quadro: **"Vocês passaram 40 horas tornando visível algo que dura meio segundo e que ninguém nunca vê. Isso é engenharia."**

### 7.2 Recap da jornada completa (10 min)
- Aponte cada nó e a fase que o construiu:

  | Nó | Fase | Conceito-chave |
  |---|---|---|
  | Gateway YARP | F2 | reverse proxy, nó zero do tracing |
  | Function Entry | F1 | entrada desacoplada (publica na fila) |
  | Service Bus | F1 | fila como amortecedor |
  | Function Consumer | F1 | consumidor idempotente |
  | n8n | F4 | automação low-code |
  | SQL | F1 | persistência + correlation_id |

- E o que atravessa tudo: **identidade** (F3) e **conversação** (F5) usando o mesmo gateway e o mesmo padrão de host.
- Mensagem: "Vocês não aprenderam seis tecnologias soltas. Aprenderam a **compor** um sistema distribuído Azure-native, peça por peça, e a **observá-lo**."

### 7.3 Retro do workshop completo (10 min) — AC-12
Conduza uma retro estruturada. Escreva as três perguntas no quadro e dê post-its (ou um Miro):

1. **O que eu aprendi que não sabia antes?** (cada aluno escreve 1-2)
2. **O que foi mais desafiador?** (e como destravaram)
3. **O que eu usaria em produção amanhã?** (priorização real)

Perguntas-guia adicionais, se houver tempo:
- Qual fase mudou mais a sua forma de pensar arquitetura?
- Onde vocês viram um trade-off (ex.: scale-to-zero vs cold start, polling vs WebSocket)?
- O que vocês fariam diferente se começassem o sistema do zero?

Encerre coletando 1 palavra de cada aluno sobre o workshop. Agradeça. **Comemore** — eles terminaram 40h e seis fases cumulativas. A tag `v2.0.0` (mergeada por @devops) marca isso no repo.

---

## Perguntas finais sugeridas para os alunos (avaliação rápida)

Use para fechar (oral ou quiz):

1. Quem é o **nó zero** do fluxo, e o que ele faz com o correlation ID? (Gateway YARP; gera/injeta `X-Correlation-ID`. **Não é APIM.**)
2. Por que o FlowEvents usa `Azure.Monitor.Query` e não `TelemetryClient`? (TelemetryClient escreve; LogsQueryClient consulta.)
3. Como o correlationId vira `customDimensions.CorrelationId` no App Insights? (via `ILogger.BeginScope`.)
4. Por que o SignalR é Service Mode **Default** e não Serverless? (Hub clássico hospedado pelo FlowEvents.)
5. O que o **replay** faz e por que ele existe? (relê a telemetria e reempurra via SignalR; permite animar compras antigas.)
6. O que acontece se o WebSocket falhar? (fallback polling a cada 2s.)
7. Por que conceder **Log Analytics Reader** à Managed Identity em vez de usar uma key? (sem segredo para vazar; padrão Azure RBAC.)

---

## Transição final (não há F7)

Esta é a última fase. Em vez de "próxima fase", o fechamento é: **o sistema está completo, observável e mergeado na `main` com a tag `v2.0.0`**. Convide os alunos a estender por conta própria — adicionar um nó (ex.: notificação por e-mail), trocar o front, ou aplicar o padrão de correlation ID + Flow Visualizer no sistema **deles**, no trabalho. O Living Lab acabou; a prática começa.
