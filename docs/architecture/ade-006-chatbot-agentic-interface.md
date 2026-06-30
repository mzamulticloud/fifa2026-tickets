# ADE-006 — Chatbot como interface agêntica: split sentidos/ação + n8n como camada de orquestração (Fase B = dois agentes cooperando)

> **Tipo:** Architecture Decision Entry (interaction pattern + integration boundary)
> **Status:** ✅ Accepted · **v1.1** (Fase B redesenhada — Opção 3 híbrida com n8n AI Agent, owner-aprovada 2026-06-10)
> **Date:** 2026-06-08 (v1.0) · **Amended:** 2026-06-10 (v1.1)
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 F5 (`src/Fifa2026.V2.McpServer/` — tools MCP + chatbot React) e fases que dependem da camada de ação/orquestração: F4 (n8n) e F6 (Flow Visualizer). A Fase A foi entregue na story **2.8** (Done, commit 570d2f7). A Fase B (action tool + n8n AI Agent) alimenta a próxima story **2.9**; a Fase C fica para story posterior.
> **Supersedes:** N/A — **estende ADE-002** (não a substitui). As 3 tools read-only originais (`consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket`) permanecem válidas; esta ADE adiciona o catálogo de sentidos e define a camada de ação.
> **Related:** ADE-000 (microsserviço paralelo — Inv 1 backend intocado, Inv 4 idempotência, Inv 5 W3C Trace Context), ADE-002 (MCP SDK 1.4.0 exato, LLM no front, endpoints pinados), ADE-004 (gateway YARP — todo tráfego ao MCP passa pelo gateway), ADE-005 (identidade `oid` propagada como `X-Entra-OID`).

---

## Context

A decisão formalizada aqui foi **tomada pelo owner (Guilherme Prux Campos) em 2026-06-08** — proposta por mim (Aria) na mesma sessão. Esta ADE apenas a registra com o nível de detalhe que a story 2.8 precisa para ser implementada sem invenção (Constitution Art. IV).

O chatbot do v2 (Story 2.5 / F5) hoje expõe **3 tools MCP read-only** sobre o SQL do FIFA 2026 Tickets, via o SDK C# oficial pinado em 1.4.0 exato (ADE-002 Inv 1/2). Verificadas no código real:

| Tool atual | Arquivo | Fonte SQL real |
|---|---|---|
| `consultar_disponibilidade` | `src/Fifa2026.V2.McpServer/Tools/FifaTicketTools.cs` | `matches` + `teams` + `ticket_categories` (PIVOT por rótulo real, ver M-1) |
| `verificar_ingresso` | idem | `purchases` + `users` + `ticket_categories` + `matches` + `teams` |
| `consultar_bracket` | idem | `matches` + `teams` + `stadiums` filtrado por `stage` de mata-mata |

O owner quer que o chat deixe de ser um "balcão de ingressos" e passe a **explorar todo o sistema da Copa** — resultados, classificação, partidas, times, estádios — e que comece a **agir** através do n8n (orquestração / integrações externas), não escrevendo direto no banco.

O problema arquitetural a resolver: como **ampliar a superfície agêntica** (mais tools, e tools que *agem*) sem (a) abrir um vetor de escrita descontrolado a partir de um LLM (que alucina), (b) explodir a contagem de tools a ponto do LLM errar a seleção, e (c) quebrar as invariantes já estabelecidas (ADE-000 backend intocado/idempotência, ADE-002 LLM no front, ADE-004 gateway como guardião, ADE-005 identidade por `oid`).

Esta ADE responde com um **modelo conceitual de "sentidos e mãos"** e o **n8n como única camada de ação/orquestração** para o chatbot.

---

## Decision

Adotamos o pattern **"Agentic Chatbot — Split Sentidos/Mãos com n8n como hub de orquestração"** com 7 invariantes (a 7ª adicionada na v1.1). A peça central é a **regra de ouro** (Invariante 1). A v1.1 (2026-06-10) redesenha a Fase B para o modelo **híbrido de dois agentes** (Opção 3): o front decide *o quê*, o AI Agent do n8n decide *o como* (Invariante 4).

### Invariante 1 — REGRA DE OURO: o chatbot NUNCA escreve direto no banco

O LLM (no front) e o McpServer **jamais executam INSERT/UPDATE/DELETE** disparados por uma conversa. Toda **ação** que mude estado vai pelos **caminhos já existentes e idempotentes** do epic:

- **Compra** → Service Bus → `PurchaseEntryFunction`/`PurchaseConsumerFunction` (F1, ADE-000 Inv 4: UNIQUE + INSERT-catch). O chatbot **não** cria compra direto; no máximo encaminha o usuário ao fluxo de compra v2 existente.
- **Orquestração / integrações externas** (alertas, notificações, consulta a APIs externas) → **webhook n8n** (F4). O n8n é o único componente autorizado a "agir" em nome do chat.

O McpServer continua **somente leitura** (já é: `FifaQueryRepository` abre `SqlConnection` apenas para `SELECT`; comentário de cabeçalho "o McpServer nunca grava"). As tools de ação (Fase B) **não** ganham acesso de escrita ao SQL — elas disparam webhook n8n e retornam o resultado da orquestração.

> **Por quê é a regra de ouro:** um LLM alucina argumentos. Se uma tool pudesse escrever no banco, uma alucinação viraria corrupção de dados irreversível. Roteando ações por Service Bus (idempotente) e n8n (orquestração observável, com auth própria), o blast radius de uma alucinação fica contido — no pior caso, um webhook é chamado com payload inócuo, nunca um `DELETE FROM purchases`.

### Invariante 2 — Split "Sentidos" (ler) vs "Mãos" (agir)

A superfície de tools MCP é dividida em duas naturezas explícitas e auditáveis:

| Natureza | Marca | Caminho de execução | Efeito colateral | Exemplos |
|---|---|---|---|---|
| **Sentidos** (ler) | `[McpServerTool(ReadOnly = true)]` | tool → `IFifaQueryRepository` → **SQL SELECT parametrizado** (Dapper) | Nenhum | as 3 atuais + as 5 da Fase A |
| **Mãos** (agir) | `[McpServerTool]` (ReadOnly = false) | tool → **webhook n8n** (HttpClient server-side) | Orquestração externa (e-mail/alerta/integração) | `criar_alerta_ingresso` (Fase B) |

As duas naturezas convivem no mesmo McpServer e são listadas juntas em `tools/list`. O atributo `ReadOnly` do SDK 1.4.0 é o discriminador formal — o LLM e o front podem (opcionalmente) usar o hint para tratar ações com confirmação do usuário. **Sentidos nunca chamam n8n; Mãos nunca tocam SQL.** Essa separação é o invariante estrutural que torna a regra de ouro verificável por inspeção do código.

### Invariante 3 — Sentidos = SQL SELECT direto, seguindo o padrão existente (sem desvio)

As novas tools read-only (Fase A) seguem **exatamente** o padrão já implementado e validado em F5 (ADE-002, gate S2.5):

1. Método estático em uma classe `[McpServerToolType]` (descoberta por `WithToolsFromAssembly()` em `Program.cs`), cada um com `[McpServerTool(Name = "...", ReadOnly = true)]` + `[Description]` em português (o SDK deriva o JSON Schema da assinatura + `[Description]` — **não se escreve schema à mão**).
2. Dependências injetadas por DI nos parâmetros: `IFifaQueryRepository`, `EntraOidContext`, `ILogger`. Identidade lida via `EntraOidContext.GetMaskedOidForLog()` **só para log mascarado** — nunca revalida JWT (gateway é o guardião, ADE-004/ADE-005).
3. Acesso a dados via `FifaQueryRepository` (Dapper + `Microsoft.Data.SqlClient`), **100% parametrizado** (anti SQL injection — CodeRabbit focus). Cada nova tool ganha um método na interface `IFifaQueryRepository` (mockável nos testes).
4. Parâmetros opcionais recebem **default explícito** (`= null`) — lição do gate S2.5: o SDK 1.4.0 marca nullable sem default como `required` e `tools/call` com argumentos parciais quebra.
5. DTOs de retorno em `McpQueryResults.cs` com `[JsonPropertyName]` (contrato JSON estável e em português, como os atuais).

### Invariante 4 — Mãos = webhook n8n, e dentro do n8n o cérebro é o AI Agent node (Opção 3 híbrida, v1.1)

> **v1.1 (owner-aprovada 2026-06-10):** a Fase B foi **redesenhada** para o modelo de **dois agentes cooperando**. O contrato de disparo (webhook, App Setting, `correlationId`+`entraOid` no body) permanece o de F4; a novidade é **o que roda dentro do n8n**.

A primeira "mão" (`criar_alerta_ingresso`, Fase B) dispara o **webhook n8n** exatamente no padrão que F4 já estabeleceu para a notificação pós-compra (Story 2.4 AC-5/AC-6):

- `HttpClient` server-side no McpServer (já há `AddHttpClient` em `Program.cs`), `POST` num endpoint de webhook n8n com URL em **App Setting** (`N8N_WEBHOOK_URL` ou um setting dedicado por workflow) — **nunca hardcoded, nunca no bundle do front**.
- Body JSON inclui **`correlationId`** (ADE-000 Inv 5 — W3C Trace Context; o n8n loga o `correlationId` em cada node, essencial para o Flow Visualizer F6) e **`entraOid`** (identidade do usuário do chat, propagada pelo gateway como `X-Entra-OID`).
- A tool **não orquestra nem decide** — ela só dispara o webhook com o payload e devolve "alerta registrado". **Tudo o que é decisão/composição vive no workflow n8n.**

**O que mudou na v1.1 — o cérebro do n8n:** dentro do workflow, o nó central deixa de ser um `Switch` determinístico (como no `post-purchase-notification` de F4) e passa a ser um **AI Agent node (Tools Agent)** munido de um **MCP Client Tool** apontando para o **McpServer interno**. O agente n8n recebe a intenção (alerta para a partida X, categoria Y) e **decide e compõe**: consulta disponibilidade via MCP (reusa os "sentidos" da Fase A), redige a notificação em linguagem natural, escolhe o canal, e executa. Isso transforma a "mão" numa **segunda inteligência** — não um trilho fixo.

**Regra de ouro reafirmada:** o AI Agent do n8n também **NÃO escreve no banco**. Suas tools (via MCP Client Tool) são os mesmos sentidos read-only; qualquer efeito que mude estado de negócio continua roteado pelos caminhos idempotentes (Service Bus / integrações externas observáveis). O agente n8n compõe e notifica — não corrompe dados.

**Padrão didático — "o chat decide O QUÊ; o agente n8n decide COMO":**

| Agente | Onde roda | Cérebro | Decide | Tools |
|---|---|---|---|---|
| **Agente 1 (sentidos + intenção)** | front React (browser) | Gemini + function calling (Stories 2.5/2.8) | **O QUÊ** — interpreta o usuário, escolhe a tool, dispara a ação | 7 tools MCP read-only **via gateway YARP** + a "mão" `criar_alerta_ingresso` |
| **Agente 2 (orquestração + execução)** | dentro do n8n (servidor) | AI Agent node (Tools Agent) + LLM (Gemini) | **O COMO** — consulta disponibilidade, redige, escolhe canal, executa | MCP Client Tool → McpServer interno (sentidos read-only) |

São **dois agentes cooperando** numa mesma jornada. O front é a interface conversacional; o n8n é o executor inteligente. A "mão" do front (`criar_alerta_ingresso`) é a fronteira entre os dois.

### Invariante 5 — Princípio de design: poucas tools bem parametrizadas > muitas tools estreitas

A ampliação de "sentidos" é feita **consolidando** capacidades em poucas tools flexíveis e bem parametrizadas, não criando uma tool por pergunta. Motivo empírico: quanto mais tools (e mais parecidas entre si), mais o LLM erra a seleção e os argumentos. Uma `consultar_partidas` parametrizada por `time | fase | estadio | grupo | data` cobre "jogos do Brasil", "jogos nas oitavas", "jogos no Maracanã", "jogos do grupo C" e "jogos de 15/06" — cinco perguntas, uma tool. Quando o schema suporta, **resultado/placar é um campo da mesma partida** (não uma tool separada): `consultar_partidas` retorna o placar quando o jogo já foi disputado, dispensando uma `consultar_resultados` independente.

### Invariante 6 — Tudo continua passando pelo gateway YARP; identidade continua sendo o `oid`

Nenhuma mudança no perímetro: o front (LLM no browser) chama o gateway YARP com Bearer Entra; o gateway valida o JWT, injeta `X-Correlation-ID` (ADE-004) e propaga `X-Entra-OID` (ADE-005) ao McpServer. As novas tools read-only e a tool de ação **herdam** esse perímetro — o McpServer permanece com ingress interno, atrás do gateway, sem revalidar JWT. A tool de ação, ao chamar o n8n, **repassa** `correlationId` e `entraOid` no body (Invariante 4), mantendo a cadeia de rastreabilidade intacta até o n8n (e, em F6, até o SignalR).

### Invariante 7 — Nuance de identidade: a chamada n8n → McpServer é leste-oeste (bypass do gateway), não sessão do usuário (v1.1)

> **Honestidade arquitetural (Art. IV).** A v1.1 introduz um segundo consumidor do McpServer — o **AI Agent do n8n via MCP Client Tool**. É preciso documentar onde esse tráfego se posiciona em relação ao "guardião único" (gateway YARP, ADE-004/005), porque ele **não** passa pelo gateway.

Topologia real (verificável): o **McpServer tem ingress INTERNO** (ADE-002/ADE-004) e o **n8n roda no mesmo Container Apps environment** (`cae-fifa2026-*`, Story 2.4). Logo, o n8n alcança o McpServer **diretamente, por DNS interno do ambiente, BYPASSANDO o gateway YARP.** Isso é deliberado e é o comportamento correto para este caso:

- **É orquestração servidor-a-servidor (leste-oeste), não uma sessão de usuário (norte-sul).** O AI Agent do n8n não "é" o usuário do chat; ele é um componente de backend agindo *em nome de* uma intenção já autorizada no perímetro.
- **A identidade do usuário viaja como DADO, não como credencial.** O `entraOid` chega ao n8n como campo do payload do webhook (Invariante 4) — é um *fato de contexto* ("este alerta é do usuário X"), **não** um Bearer token que conceda acesso. O AI Agent do n8n não porta JWT do usuário ao chamar o MCP; ele chama o MCP interno como serviço confiável do ambiente.
- **A autorização do usuário já aconteceu uma vez, no perímetro.** A intenção entrou pelo front → gateway YARP (validou o JWT, ADE-004/005) → "mão" `criar_alerta_ingresso` → webhook n8n. A partir daí, é confiança de ambiente (leste-oeste), não re-autenticação por hop.

**Trade-off aceito e documentado:** a pedagogia F2/F3 do **"gateway é o guardião único"** vale para o **perímetro externo** (norte-sul: browser → backend). **Chamadas internas leste-oeste** (n8n → McpServer, dentro do mesmo ambiente fechado) são **outro domínio de confiança** — o ambiente Container Apps é a fronteira, e o McpServer só é alcançável de dentro dele (ingress interno). Não é uma brecha no guardião; é o reconhecimento honesto de que perímetro externo e malha interna são camadas de confiança distintas.

> **Por que aceitamos o bypass (vs forçar n8n → gateway → McpServer):** rotear o n8n de volta pelo gateway exigiria que o n8n adquirisse e portasse um token Entra (client-credentials) só para falar com um serviço que já está na sua rede confiável — complexidade e superfície de credencial sem ganho de segurança real (o ambiente interno já é a fronteira). O didático aqui é **distinguir norte-sul de leste-oeste**, não fingir que tudo é norte-sul. Mitigação: o `correlationId` continua atravessando o n8n→MCP (rastreabilidade preservada para F6); o McpServer permanece read-only para os sentidos (regra de ouro intacta independe de quem chama).

---

## Catálogo de tools da Fase A (Sentidos completos) — ENTREGUE na Story 2.8 (Done, commit 570d2f7)

> **Status v1.1:** ✅ **Fase A implementada e mergeada.** Story 2.8 está **Done** (gate PASS). Commit `570d2f7` — "feat(epic-002): 4 tools MCP de exploração — partidas, classificação, time, estádio [Story 2.8]" — entregou as 4 tools read-only (`FifaAgenticTools` em `src/Fifa2026.V2.McpServer/Tools/FifaTickerTools.cs`), `IFifaQueryRepository`/`FifaQueryRepository` (Dapper parametrizado), DTOs em `McpQueryResults.cs` e a bateria de testes (`FifaAgenticToolsTests.cs`, `McpToolCallIntegrationTests.cs`, `BracketStageMappingTests.cs`). Superfície de sentidos do chat: **3 → 7 tools**. O catálogo abaixo permanece como contrato verificado da entrega.

Todas read-only, `[McpServerTool(ReadOnly = true)]`, SQL SELECT parametrizado (Dapper). As tabelas/colunas abaixo foram **verificadas contra o schema real** (`fifa2026-api/database/schema.sql`) e o seed real (`migrations/2026-05-07-update-48-teams.sql`, `2026-05-07-update-16-stadiums.sql`, `2026-05-08-group-stage-72.sql`, `2026-05-07-knockout-matches.sql`, `2026-05-08-real-fifa-prices.sql`).

> **Schema real relevante (fonte da verdade, não inventar):**
> - `matches(id, home_team_id, away_team_id, stadium_id, date DATE, time NVARCHAR(5), stage NVARCHAR(50), group_name NVARCHAR(1), home_score INT NULL, away_score INT NULL, status NVARCHAR(20) DEFAULT 'scheduled')`
> - `teams(id, name, code NVARCHAR(3), flag, group_name NVARCHAR(1), confederation, fifa_ranking)`
> - `stadiums(id, name, city, country, capacity, image, description, address, latitude, longitude)`
> - `ticket_categories(id, match_id, category NVARCHAR(50), price, total_quantity, available_quantity, description)`
> - **`matches.stage` tem valores REAIS:** `'Fase de Grupos'` (grupos), `'round_of_32'`, `'round_of_16'`, `'quarter_final'`, `'semi_final'`, `'third_place'`, `'final'`. ⚠️ O grupo é `'Fase de Grupos'` (com acento/espaços) — **não** um valor `round_of_*`. A migration knockout-matches insere `'round_of_32'`; a group-stage-72 insere `'Fase de Grupos'`.
> - **`matches.group_name`** só é preenchido na fase de grupos (A–L); NULL no mata-mata.
> - **Não existe tabela `standings`/classificação** — a classificação é **calculada por agregação** dos jogos de grupo (ver `consultar_classificacao`).

### A.1 — `consultar_partidas` (a tool-âncora; consolida "jogos" + "resultados")

| Campo | Valor |
|---|---|
| **Nome** | `consultar_partidas` |
| **ReadOnly** | `true` (Sentido) |
| **Descrição (`[Description]`)** | "Consulta partidas da Copa 2026 com filtros flexíveis. Use para perguntas como 'jogos do Brasil', 'jogos nas oitavas', 'jogos no Maracanã', 'jogos do grupo C' ou 'jogos do dia 15/06'. Retorna o placar quando o jogo já foi disputado." |
| **Parâmetros** (todos opcionais, default `= null`) | `time` (string — nome OU código do time, ex.: "Brasil"/"BRA"), `fase` (string em linguagem natural — "grupos", "oitavas", "quartas", "semifinal", "final" — mapeada para `stage`), `estadio` (string — nome/cidade do estádio), `grupo` (string — "A".."L"), `data` (string ISO `YYYY-MM-DD`), `apenasComResultado` (bool — só jogos com placar) |
| **Retorno** | `IReadOnlyList<MatchResult>` com `{ partida, data, horario, estadio, fase, grupo, placarMandante?, placarVisitante?, status }`. Placar `null` quando não disputado. |
| **Fontes SQL** | `matches m` LEFT JOIN `teams ht`/`teams at` (home/away) LEFT JOIN `stadiums s`. Filtros: `time` → `ht.name/at.name LIKE` ou `ht.code/at.code =`; `fase` → mapeia para `m.stage` (reusar `MapRodadaToStage` + adicionar `'Fase de Grupos'`); `grupo` → `m.group_name`; `estadio` → `s.name/s.city LIKE`; `data` → `m.date`; `apenasComResultado` → `m.home_score IS NOT NULL`. Times NULL no mata-mata → `COALESCE(ht.name,'A definir')` (padrão já usado em `consultar_bracket`). |

> **Decisão de consolidação:** `consultar_resultados(time|rodada|data)` do pedido **NÃO vira tool separada** — vira o filtro `apenasComResultado=true` (e os mesmos `time`/`fase`/`data`) de `consultar_partidas`. Placar e partida são a mesma linha em `matches` (`home_score`/`away_score`); separar duplicaria a query e confundiria a seleção do LLM (Invariante 5). Resultado: **menos uma tool**, mesma capacidade.

### A.2 — `consultar_classificacao`

| Campo | Valor |
|---|---|
| **Nome** | `consultar_classificacao` |
| **ReadOnly** | `true` (Sentido) |
| **Descrição** | "Consulta a classificação (tabela de pontos) de um grupo da fase de grupos da Copa 2026. Calculada a partir dos resultados dos jogos do grupo." |
| **Parâmetros** | `grupo` (string obrigatório — "A".."L") |
| **Retorno** | `IReadOnlyList<StandingRow>` com `{ posicao, time, jogos, vitorias, empates, derrotas, golsPro, golsContra, saldo, pontos }` |
| **Fontes SQL** | **Calculado** (não há tabela `standings`): agregação dos `matches` onde `m.group_name = @grupo AND m.stage = 'Fase de Grupos' AND m.home_score IS NOT NULL`, expandindo cada partida em duas linhas (mandante/visitante) via `UNION ALL`, somando 3/1/0 pontos por resultado, agrupando por `team_id`, ordenando por `pontos DESC, saldo DESC, golsPro DESC`. JOIN `teams` para o nome. |

> **Honestidade de schema (Art. IV):** classificação **não existe como tabela** no schema real. A estratégia é **agregação dos jogos do grupo**. Antes dos jogos serem disputados (`home_score` NULL no seed atual), a classificação retorna todos com 0 ponto — comportamento correto e documentado, não um bug. Cabe ao `@dev` (Story 2.8) implementar a agregação; o `@data-engineer` pode ser consultado para otimização da query se necessário (delegação ADE-004 da matriz de autoridade).

### A.3 — `consultar_time`

| Campo | Valor |
|---|---|
| **Nome** | `consultar_time` |
| **ReadOnly** | `true` (Sentido) |
| **Descrição** | "Consulta informações de uma seleção da Copa 2026 (grupo, confederação, ranking FIFA, código)." |
| **Parâmetros** | `nome` (string obrigatório — nome OU código, ex.: "Brasil"/"BRA") |
| **Retorno** | `TeamResult` com `{ encontrado, nome, codigo, grupo, confederacao, rankingFifa, bandeira }` |
| **Fontes SQL** | `teams` (`name`, `code`, `group_name`, `confederation`, `fifa_ranking`, `flag`). Match por `name LIKE @nome OR code = @nome` (case-insensitive). |

### A.4 — `consultar_estadio`

| Campo | Valor |
|---|---|
| **Nome** | `consultar_estadio` |
| **ReadOnly** | `true` (Sentido) |
| **Descrição** | "Consulta informações de um estádio/sede da Copa 2026 (cidade, país, capacidade, descrição)." |
| **Parâmetros** | `nome` (string obrigatório — nome do estádio OU cidade) |
| **Retorno** | `StadiumResult` com `{ encontrado, nome, cidade, pais, capacidade, descricao }` |
| **Fontes SQL** | `stadiums` (`name`, `city`, `country`, `capacity`, `description`). Match por `name LIKE @nome OR city LIKE @nome`. (Nota: Rose Bowl está soft-disabled como "Rose Bowl (legacy)" — não filtrar especialmente; aparece se buscado.) |

### Catálogo final consolidado (entrada para a Story 2.8 / @sm)

| # | Tool | Natureza | Params | Retorno (resumo) | Fontes SQL |
|---|---|---|---|---|---|
| (existente) | `consultar_disponibilidade` | Sentido | `matchId?`, `matchDescription?` | disponibilidade+preço por categoria | `matches`+`teams`+`ticket_categories` |
| (existente) | `verificar_ingresso` | Sentido | `ingressoId` | validade da compra + dados | `purchases`+`users`+`ticket_categories`+`matches`+`teams` |
| (existente) | `consultar_bracket` | Sentido | `rodada` | jogos do mata-mata por stage | `matches`+`teams`+`stadiums` |
| **A.1** | `consultar_partidas` | Sentido | `time? fase? estadio? grupo? data? apenasComResultado?` | lista de partidas (+placar quando houver) | `matches`+`teams`+`stadiums` |
| **A.2** | `consultar_classificacao` | Sentido | `grupo` | tabela de pontos do grupo (**calculada**) | `matches`+`teams` (agregação) |
| **A.3** | `consultar_time` | Sentido | `nome` | dados da seleção | `teams` |
| **A.4** | `consultar_estadio` | Sentido | `nome` | dados do estádio | `stadiums` |

**Total Fase A = 4 tools novas** (não 5 — `consultar_resultados` consolidada em `consultar_partidas`, Invariante 5). Superfície de sentidos do chat passa de 3 → 7 tools, cobrindo disponibilidade, ingresso, mata-mata, partidas (com resultado), classificação, times e estádios — "explorar todo o sistema da Copa".

---

## Fase B — Primeira "mão" via n8n + AI Agent (Opção 3 híbrida, owner-aprovada 2026-06-10) — alimenta a Story 2.9

**Fora do escopo da Story 2.8 (já Done).** Decisão registrada e redesenhada na v1.1; story própria a draftar (**Story 2.9**, @sm).

A Fase B tem **duas metades** que cooperam (ver Invariante 4): (1) a **"mão" no front** (`criar_alerta_ingresso`) que dispara o webhook n8n; (2) o **AI Agent dentro do n8n** que decide e compõe a ação. A primeira é uma tool MCP `ReadOnly=false`; a segunda é configuração de workflow no n8n UI.

### B.1 — `criar_alerta_ingresso` (action tool — a "mão" do front)

| Campo | Valor |
|---|---|
| **Nome** | `criar_alerta_ingresso` |
| **ReadOnly** | `false` (Mão) |
| **Descrição** | "Cria um alerta para avisar o usuário quando houver ingressos disponíveis para uma partida. Aciona uma automação de orquestração." |
| **Parâmetros** | `matchId` (int) OU `matchDescription` (string), `categoria` (string — rótulo real via `CategoryLabelMapper`, opcional; ver Inv anti-alucinação 4) |
| **Efeito** | Dispara **webhook n8n** (`POST`, URL em App Setting), body `{ correlationId, entraOid, matchId, matchDescription, categoria, requestedAt }`. **Não escreve no SQL** (regra de ouro). Retorna `{ registrado: true, alertaId? }`. |
| **Orquestração** | NÃO vive na tool. A tool só dispara e devolve. **A decisão/composição vive no AI Agent do workflow n8n** (B.2). |

**Por que prova o padrão:** é o menor incremento que demonstra `chatbot → gateway → MCP (mão) → n8n (AI Agent)` sem mover lógica de negócio para a tool nem violar a regra de ouro. Reusa 100% o contrato de webhook de F4 (URL em App Setting, `correlationId`+`entraOid` no body, auth no n8n — Story 2.4 AC-6/AC-7).

### B.2 — Workflow n8n com AI Agent node (o "cérebro" da mão — Agente 2)

O workflow n8n deixa de ser determinístico (`Switch` do `post-purchase-notification`, F4) e passa a ter, no centro, um **AI Agent node (Tools Agent)** munido de **MCP Client Tool** apontando para o **McpServer interno** (DNS do ambiente, bypass do gateway — Invariante 7). O agente:

1. Recebe do webhook a intenção + contexto (`matchId`/`matchDescription`, `categoria`, `correlationId`, `entraOid`).
2. **Consulta disponibilidade via MCP** (reusa os sentidos read-only da Fase A — `consultar_disponibilidade`/`consultar_partidas`).
3. **Redige** a notificação em linguagem natural e **escolhe o canal**.
4. **Executa** a notificação (HTTP node — mock como em F4, ou canal real) propagando `correlationId` em cada node (rastreabilidade F6).

**Regra de ouro no Agente 2:** as tools do AI Agent (via MCP Client Tool) são os **mesmos sentidos read-only**. Qualquer efeito que mude estado de negócio continua roteado por caminhos idempotentes/observáveis — o agente **compõe e notifica, não corrompe dados**.

### B.3 — Pré-requisito de verificação (anti-alucinação, OBRIGATÓRIO antes de implementar a Story 2.9)

> **Invariante de verificação (Art. IV — não afirmar como certeza).** Os nomes/versões dos nós abaixo **NÃO são afirmados como fato** nesta ADE — são marcados **"a verificar na instância n8n real"** antes do draft/implementação da Story 2.9. O n8n roda em `n8nio/n8n:latest` num Container App (Story 2.4 AC-3 — owner quer sempre a versão mais nova), então a superfície de nós AI **pode variar entre versões** (o n8n já teve breaking change em major 2.0). Verificar na instância viva, não assumir.

Itens a verificar **na instância n8n real** (`cae-fifa2026-*`, Story 2.4) e registrar no draft da Story 2.9:

| # | A verificar na instância (NÃO assumir) | Por quê |
|---|---|---|
| V-1 | Existência e **nome exato** do nó **AI Agent** (família "Tools Agent" / "AI Agent") na versão `latest` instalada | O nome/categoria do nó varia entre versões do n8n; afirmar agora seria invenção. |
| V-2 | Existência e **nome exato** do **MCP Client Tool** (nó que conecta o AI Agent a um McpServer externo) e seu modo de transporte (HTTP/SSE vs stdio) | Define se o AI Agent consegue falar com o McpServer interno; o transporte deve casar com o ingress interno do McpServer. |
| V-3 | **Como configurar a credencial LLM (Gemini)** no n8n (tipo de credencial, nome do provider/nó de modelo, env var de API key) | O cérebro do Agente 2 precisa de um LLM; a forma de plugar Gemini no AI Agent é específica do n8n e deve ser confirmada, não inventada. |
| V-4 | Se o McpServer interno é alcançável do AI Agent por **DNS interno do ambiente** (Invariante 7) e qual URL/porta interna usar | Confirma a topologia leste-oeste (bypass do gateway) na prática, não só no papel. |

**Regra para a Story 2.9:** o @sm/@dev **não** escreve nomes de nós/versões como fato no código ou na story sem antes confrontar a instância viva. Achados de V-1..V-4 viram o contrato de configuração do workflow.

---

## Fase C — n8n externo + F6 (decisão registrada; implementação em story futura)

**Fora do escopo da Story 2.8.** Decisão registrada agora.

Na v1.1, o **AI Agent do n8n** (Agente 2, Fase B) evolui para **consultar uma API externa** (placar ao vivo / notícias) e **emitir um FlowEvent**, fechando a narrativa agêntica no Flow Visualizer (F6) como **uma jornada de dois agentes**. A jornada visualizada:

```
chat (LLM no front — Agente 1: decide O QUÊ)
  → gateway YARP (valida JWT, injeta X-Correlation-ID, propaga X-Entra-OID)   [norte-sul]
    → McpServer (tool de ação: criar_alerta_ingresso)
      → n8n AI Agent (Agente 2: decide O COMO)
          → MCP Client Tool → McpServer interno (sentidos read-only)          [leste-oeste, bypass do gateway — Inv 7]
          → node de integração com API externa (placar/notícias) → emite evento com correlationId
        → SignalR (FlowHub.SendFlowEvent) → Flow Visualizer /flow (bolinha animada)
```

Isso reusa a infra de F6 já desenhada (Story 2.6): `FlowEventsFunction` lê App Insights por `correlationId`, `FlowHub` empurra `SendFlowEvent(correlationId, eventType, ...)` para o grupo `correlation-<id>`; o n8n já é nó da timeline do visualizer (nó 5). A novidade da Fase C é o **disparo a partir do chat** (não só pós-compra), a **integração externa dentro do n8n** e a visualização de **dois agentes cooperando** numa só jornada. Perímetro externo (norte-sul) coberto por Invariante 6; a malha interna n8n→MCP (leste-oeste) coberta por Invariante 7.

> **Honestidade (Art. IV):** o Flow Visualizer atual (F6) rastreia a jornada **de compra** (Gateway → Entry → Service Bus → Consumer → n8n → SQL). A Fase C adiciona uma jornada **de chat** à mesma malha SignalR/App Insights. A reutilização é real (mesmos componentes), mas o caminho do chat precisa que a tool de ação propague `correlationId`/`entraOid` ao n8n (Invariante 4) — pré-requisito já garantido pelo design da Fase B.

---

## Invariantes anti-alucinação (modelo ADE-002, estendido para a interface agêntica)

1. **SDK oficial faz o framing** (ADE-002 Inv 1/2): `tools/list`/`tools/call` e o JSON Schema de input vêm do SDK 1.4.0 a partir da assinatura + `[Description]`. Não se escreve schema/JSON-RPC à mão.
2. **Sentidos só leem; SQL só parametrizado** (Inv 1/3): zero concatenação de string; `IFifaQueryRepository` é a única porta de dados, somente `SELECT`.
3. **Mãos só agem por caminhos idempotentes/observáveis** (Inv 1/4): nenhuma escrita direta no SQL a partir de tool; ações vão por Service Bus (compra) ou webhook n8n (orquestração). Uma alucinação de argumento não corrompe dados.
4. **Schema é fonte da verdade** (Art. IV): rótulos de categoria são `'VIP Premium'`/`'Categoria 1'`/`'Categoria 2'` (via `CategoryLabelMapper`, fonte única); `stage` de grupo é `'Fase de Grupos'` (não `round_of_*`); classificação é **calculada** (não há tabela). Tools que citarem dados inexistentes são rejeitadas em review.
5. **Identidade nunca é inventada nem revalidada na tool** (ADE-004/005): `oid` chega via `X-Entra-OID` propagado pelo gateway; a tool só o usa para log mascarado e para repassar ao n8n. A tool nunca decide autorização — o gateway é o guardião.
6. **Defaults explícitos em parâmetros opcionais** (gate S2.5): todo parâmetro opcional tem `= null`/default, senão o SDK 1.4.0 o marca `required` e `tools/call` parcial quebra ao vivo.
7. **Nós/versões do n8n são verificados na instância viva, não afirmados de memória** (v1.1 — Fase B.3): nome do AI Agent node, do MCP Client Tool, modo de transporte e configuração de credencial LLM (Gemini) são marcados **"a verificar na instância"** e confirmados contra o n8n real (`n8nio/n8n:latest`) antes da Story 2.9. Afirmar nome/versão de nó como fato é invenção (Art. IV) — a superfície de nós AI do n8n varia entre versões.

---

## Rationale

### Por que o split "sentidos/mãos" (vs tools genéricas sem natureza)?

- **Auditabilidade da regra de ouro:** com sentidos (`ReadOnly=true` → SQL) e mãos (`ReadOnly=false` → n8n) fisicamente separados, "o chat não escreve no banco" é verificável por inspeção — não é uma promessa, é uma propriedade estrutural.
- **UX/segurança:** o hint `ReadOnly` permite ao front exigir confirmação só nas ações (mãos), deixando leitura fluida.
- **Didático:** ensina a distinção entre *retrieval* e *actuation* num agente — conceito central de sistemas agênticos, transferível para a vida pós-workshop.

### Por que n8n como única camada de ação (vs tool escrevendo no SQL)?

- **Blast radius:** LLM alucina; SQL direto a partir de alucinação = corrupção. n8n/Service Bus contêm o dano.
- **Reuso real:** o contrato de webhook (URL em App Setting, `correlationId` no body, auth no n8n) já existe e foi validado em F4. A Fase B não inventa nada — replica o padrão.
- **Observabilidade fecha em F6:** ação via n8n já é nó do Flow Visualizer; a jornada agêntica vira visível "de graça".

### Por que consolidar em poucas tools (vs uma tool por pergunta)?

- **Acerto de seleção do LLM:** muitas tools estreitas e parecidas aumentam erro de roteamento. `consultar_partidas` parametrizada cobre 5+ intenções com 1 tool (Invariante 5).
- **Manutenção:** menos superfície, menos schema, menos testes — coerente com 8h de workshop por fase.

---

## Consequences

### Positivas

- ✅ Chat passa a "explorar todo o sistema da Copa" (7 sentidos) sem abrir vetor de escrita (regra de ouro).
- ✅ Padrão `chatbot → MCP → n8n` provado com o menor incremento (Fase B) e fechado no visualizer (Fase C).
- ✅ Zero desvio de padrões: novas tools reusam `IFifaQueryRepository`/Dapper/`EntraOidContext`/DTOs; ação reusa o webhook de F4; perímetro reusa gateway/`oid`.
- ✅ Amarra didaticamente F5 (tools) + F4 (n8n) + F6 (visualizer) numa narrativa agêntica única — ouro pedagógico.
- ✅ `-1` tool por consolidação (`consultar_resultados` → filtro de `consultar_partidas`): menos superfície, menos erro de LLM.

### Negativas / Trade-offs aceitos

- ⚠️ **Classificação calculada custa uma query de agregação** (vs SELECT trivial de uma tabela `standings` inexistente). Mitigado: agregação é simples (UNION ALL + GROUP BY); `@data-engineer` pode otimizar/indexar se necessário. Honestamente documentado — não se inventa tabela.
- ⚠️ **Ação assíncrona via n8n** não devolve resultado de negócio imediato à tool (a tool retorna "registrado", a orquestração roda depois). Mitigado: é o comportamento correto de uma "mão" (fire-and-forth orquestrado); o usuário é notificado pelo workflow, não pela resposta síncrona.
- ⚠️ **`consultar_partidas` flexível tem SQL com vários filtros opcionais** (mais complexo que as 3 atuais). Mitigado: padrão `(@p IS NULL OR coluna = @p)` parametrizado; consolidar reduz o número total de queries a manter.
- ⚠️ **Mais tools = mais tokens no `tools/list`** enviado ao LLM. Mitigado: 7 tools é folgado para os modelos-alvo; a consolidação (Inv 5) mantém a contagem baixa.

---

## Alternatives Considered (rejeitadas)

### Decisão de design da Fase B (v1.1, owner 2026-06-10) — 3 opções avaliadas; **Opção 3 escolhida**

O owner escolheu **como** desenhar a Fase B entre três opções. As duas rejeitadas ficam registradas (Art. IV):

#### Opção 1 — n8n determinístico, sem IA (apenas `Switch`/`HTTP`, como o `post-purchase-notification` de F4)

- **Rejeitada porque:** desperdiça o **AI Agent node** do n8n (recurso central que o workshop quer ensinar) e é **menos didático** — não demonstra "dois agentes cooperando". O fluxo seria um trilho fixo, indistinguível do workflow de F4 já entregue; não acrescenta a narrativa agêntica que justifica a fase.

#### Opção 2 — n8n como **único** cérebro (o front só repassa a fala crua; toda a inteligência, inclusive a leitura, mora no n8n)

- **Rejeitada porque:** joga fora o **loop de function calling no front** (Gemini + tools MCP) já construído e validado nas Stories 2.5/2.8, e **tira o gateway YARP do caminho do chat** (a leitura passaria a entrar pelo n8n, não pelo front→gateway→MCP). Perde-se a pedagogia norte-sul (gateway como guardião) e o investimento em 7 sentidos read-only chamados do browser. Centralizar tudo no n8n também concentra um único ponto de falha/latência no chat.

#### Opção 3 — **híbrido (escolhida):** front decide *o quê* (function calling + sentidos via gateway), AI Agent do n8n decide *o como* (orquestração + execução)

- **Aceita porque:** preserva 100% do que já funciona (Stories 2.5/2.8: function calling no front, gateway no caminho da leitura), **e** introduz o AI Agent do n8n como segunda inteligência só na camada de ação — onde ele agrega valor (consultar disponibilidade, redigir, escolher canal). Materializa "dois agentes cooperando" sem regredir capacidade (gold-standard baseline preservado) e fecha didaticamente F5+F4+F6. Nuance leste-oeste documentada honestamente (Invariante 7). Ver Invariante 4 e Fase B.

---

### Alt 1 — LLM com SQL livre (text-to-SQL / tool genérica `executar_query`)

- **Rejected porque:** dar SQL arbitrário a um LLM é o oposto da regra de ouro — alucinação vira `DROP`/`DELETE`/exfiltração de dados. Quebra ADE-000 Inv 1 (backend intocado) e a postura anti-injection do projeto. Tools tipadas e parametrizadas (sentidos) entregam a mesma capacidade de leitura com superfície de ataque/alucinação controlada.

### Alt 2 — Muitas tools estreitas (uma por pergunta: `jogos_do_time`, `jogos_do_dia`, `jogos_da_fase`, `resultados`, ...)

- **Rejected porque:** explode a contagem de tools parecidas, piora a seleção do LLM e multiplica schema/testes. Consolidado em `consultar_partidas` parametrizada (Invariante 5).

### Alt 3 — Tool de ação escrevendo direto no SQL (sem n8n)

- **Rejected porque:** viola a regra de ouro (Inv 1). Mesmo "só inserir um alerta" abriria um caminho de escrita a partir do LLM. Roteando por n8n, a ação fica idempotente, observável e com auth própria — e fecha no Flow Visualizer.

### Alt 4 — Mover a orquestração da ação para dentro da tool (McpServer faz e-mail/agendamento)

- **Rejected porque:** transforma o McpServer (servidor de tools) num orquestrador, acoplando lógica de negócio à camada de tools. n8n é a camada de orquestração do epic (F4); a tool só dispara o webhook (Inv 4). Mantém responsabilidades separadas.

### Alt 5 — `consultar_resultados` como tool separada de `consultar_partidas`

- **Rejected porque:** placar e partida são a mesma linha em `matches` (`home_score`/`away_score`). Duas tools sobre a mesma tabela confundem a seleção do LLM e duplicam query. Consolidado como filtro `apenasComResultado` (Invariante 5).

---

## Validation

Esta decisão é considerada **validada** (Fase A / Story 2.8) quando:

- [ ] 4 novas tools (`consultar_partidas`, `consultar_classificacao`, `consultar_time`, `consultar_estadio`) listadas em `tools/list` com `ReadOnly=true` e JSON Schema derivado pelo SDK.
- [ ] Cada nova tool tem método em `IFifaQueryRepository` com SQL **parametrizado** (sem concatenação), mockado nos testes; nenhum acesso de escrita.
- [ ] `consultar_partidas` resolve "jogos do Brasil" (filtro `time`), "jogos nas oitavas" (`fase`→`round_of_16`), "jogos do grupo C" (`grupo`), "jogos no <estádio>" (`estadio`) e retorna placar quando `home_score IS NOT NULL`.
- [ ] `consultar_classificacao('C')` retorna tabela calculada por agregação dos `matches` de grupo (não lê tabela `standings` — que não existe).
- [ ] `stage` da fase de grupos tratado como `'Fase de Grupos'` (não `round_of_*`); rótulos de categoria via `CategoryLabelMapper`.
- [ ] Smoke no chat: "Quais os jogos do Brasil?", "Como está o grupo C?", "Fale do Maracanã" respondidos via tool, não alucinação.
- [ ] Nenhuma tool de Fase A é `ReadOnly=false`; nenhuma escreve no SQL (regra de ouro preservada).

A Fase A (Story 2.8) está **Done** (gate PASS, commit 570d2f7) — os critérios acima ficam como contrato verificado.

Para a **Fase B (Story 2.9, a draftar)** a decisão é considerada validada quando:

- [ ] `criar_alerta_ingresso` (`ReadOnly=false`) dispara webhook n8n com `correlationId`+`entraOid` no body (sem escrita SQL — regra de ouro).
- [ ] Workflow n8n usa um **AI Agent node** com **MCP Client Tool** apontando para o McpServer interno (B.2) — nomes/transporte confirmados na instância viva (B.3 / V-1..V-4), **não assumidos**.
- [ ] AI Agent do n8n consulta os sentidos read-only via MCP e **não** executa nenhuma escrita SQL (regra de ouro no Agente 2).
- [ ] Credencial LLM (Gemini) configurada no n8n conforme verificação V-3 (sem inventar tipo de credencial/nó).
- [ ] `correlationId` atravessa o n8n→MCP e os nodes do AI Agent (rastreabilidade preservada para F6).

Para a **Fase C (story posterior):** o AI Agent do n8n consulta API externa e emite FlowEvent; a jornada de **dois agentes** aparece no Flow Visualizer via SignalR.

---

## Relação com ADEs anteriores

- **ADE-002 (estende, não substitui):** as 3 tools read-only e o pin do SDK 1.4.0, a posição do LLM no front e os endpoints LLM pinados permanecem **inalterados**. Esta ADE adiciona 4 sentidos e a camada de ação — todos sob as mesmas invariantes de ADE-002 (SDK faz o framing, defaults explícitos, identidade só para log mascarado). A correção M-1 do gate S2.5 (rótulos de categoria reais) é **invariante herdada** (anti-alucinação Inv 4).
- **ADE-000:** regra de ouro materializa a Inv 1 (backend intocado) e a Inv 4 (idempotência) — ações por Service Bus/n8n, nunca escrita ad-hoc. Webhook n8n propaga `correlationId` (Inv 5, W3C Trace Context).
- **ADE-004:** todo tráfego (sentidos e mãos) entra pelo gateway YARP, que injeta `X-Correlation-ID` e é o guardião do JWT.
- **ADE-005:** identidade é o `oid` (`X-Entra-OID`); a tool de ação repassa `entraOid` ao n8n sem revalidar.

---

## Impact on EPIC-002

| Story | Impacto | Ação (executor) |
|---|---|---|
| **2.8 (F5+)** | ✅ **Done** (gate PASS, commit 570d2f7). Entregou as 4 tools de sentidos (catálogo Fase A). | Concluída — sem ação. |
| **2.9 (nova — Fase B)** | **A draftar.** Implementa `criar_alerta_ingresso` (mão via webhook n8n, B.1) + workflow n8n com **AI Agent node + MCP Client Tool** (B.2), precedido da **verificação V-1..V-4 na instância n8n viva** (B.3). Esta ADE v1.1 é o contrato de entrada. | **@sm** drafta (este ADE alimenta o draft; @architect satisfeito com a decisão Opção 3). |
| **Story posterior (Fase C)** | AI Agent do n8n consulta API externa + emite FlowEvent; jornada de **dois agentes** no Flow Visualizer (reusa F6). | **@sm**/@pm quando priorizado. |
| **2.5 (F5)** | Sem re-draft. As 3 tools atuais permanecem; esta ADE referencia-as como baseline. | Nenhuma — referência apenas. |

> **NÃO altero código nem stories** (autoridade de @dev/@sm/@po). Esta ADE v1.1 fecha a decisão de design da Fase B (Opção 3 híbrida) e entrega o contrato + a invariante de verificação para o @sm draftar a Story 2.9.

---

## Change Log

| Date | Author | Description |
|---|---|---|
| 2026-06-08 | @architect (Aria) | ADE-006 criada (v1.0) — split sentidos/mãos + n8n como hub de orquestração + regra de ouro (chatbot nunca escreve direto no banco). Catálogo Fase A (4 tools novas, `consultar_resultados` consolidada em `consultar_partidas`) verificado contra schema/seed reais. Fases B (action tool via webhook n8n) e C (n8n externo + F6) registradas para stories futuras. Estende ADE-002 sem substituir. |
| 2026-06-10 | @architect (Aria) | **v1.1 — Owner aprovou a Opção 3 (híbrida com n8n AI Agent) para a Fase B.** (1) Invariante 4 redesenhada: dentro do n8n o cérebro passa a ser um **AI Agent node (Tools Agent) + MCP Client Tool** → McpServer interno; padrão didático "o chat decide O QUÊ; o agente n8n decide COMO" — dois agentes cooperando. (2) Nova **Invariante 7**: a chamada n8n→McpServer é **leste-oeste (bypass do gateway, ingress interno)**, não sessão do usuário; identidade do usuário viaja como **dado** (`entraOid` no payload), não como Bearer token — trade-off aceito e documentado (perímetro externo norte-sul ≠ malha interna leste-oeste). (3) Fase B reescrita (B.1 mão + B.2 AI Agent + **B.3 pré-requisito de verificação anti-alucinação V-1..V-4**: nomes/versões dos nós AI Agent/MCP Client Tool e credencial LLM Gemini **a verificar na instância n8n real** `latest`, não afirmar de memória). (4) Nova invariante anti-alucinação 7 (verificar nós na instância viva). (5) Alternativas de design registradas: **Opção 1** (n8n determinístico — desperdiça o AI Agent, menos didático) e **Opção 2** (n8n único cérebro — joga fora function calling 2.5/2.8 e tira o gateway do caminho do chat) rejeitadas. (6) Status da Fase A atualizado para **ENTREGUE** (Story 2.8 Done, commit 570d2f7). Fase B passa a alimentar a **Story 2.9**. |

**Authority:** Aria (Architect) — designado por @aiox-master para padrões de integração, design de API/tools e seleção de tecnologia. Detalhe de DDL/otimização de query da classificação calculada pode ser delegado a @data-engineer (matriz de autoridade).
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.
