# F5 — Speaker Notes: roteiro de 8h (MCP Server + Chatbot + Gemini)

> **Notas do instrutor** · Workshop "Living Lab Azure-Native" · Fase 5 de 6 · **Duração: 8h** (uma das duas fases longas)
> **Story:** [2.5](../../stories/2.5.story.md) · **Arquitetura:** [ADE-002](../../architecture/ade-002-mcp-pinning.md)
> **Material de apoio:** [README](./README.md) (leitura prévia), [PORTAL-GUIDE](./PORTAL-GUIDE.md) (passo-a-passo), [slides](./slides.md), [intro-video-script](./intro-video-script.md)

Estas notas são para **você, instrutor**. Trazem o cronômetro, as perguntas para a turma, os pontos onde o aluno costuma travar, a demo de portabilidade ao vivo, o procedimento de fallback (Gemini cair) e a transição para a F6.

> **Premissa de fidelidade:** todo número, nome de tool, endpoint e arquivo aqui bate com o código real (`src/Fifa2026.V2.McpServer/`, `Lovable/World Cup Tickets Hub/src/`). Se a turma perguntar "onde isso está no código?", você aponta o arquivo. Não invente APIs.

---

## Mapa de blocos (8h = 480 min)

| Bloco | Tema | Duração | Acumulado |
|---|---|---|---|
| 0 | Abertura + recap F1-F4 + objetivo da F5 | 20 min | 0:20 |
| 1 | Conceitos: function calling + MCP (JSON-RPC, tools) | 60 min | 1:20 |
| 2 | MCP Server .NET: SDK 1.4.0, `MapMcp`, as 3 tools | 90 min | 2:50 |
| — | **Almoço** | 60 min | 3:50 |
| 3 | Chatbot React + integração LLM no front | 80 min | 5:10 |
| 4 | Segurança: a key no proxy server-side | 45 min | 5:55 |
| 5 | Deploy + smoke das 3 tools ao vivo | 60 min | 6:55 |
| 6 | **Bônus: portabilidade entre LLMs (demo ao vivo)** | 35 min | 7:30 |
| 7 | Fallback, retro e transição p/ F6 | 30 min | 8:00 |

> Ajuste a janela do almoço ao seu horário; o que importa é manter Conceitos (B1-B2) antes do almoço e Deploy/Bônus (B5-B6) na energia da tarde.

---

## Bloco 0 — Abertura + recap (20 min)

**Objetivo:** reancorar a turma no fio cumulativo e vender o "porquê" da F5.

- Recap em 1 frase por fase: F1 compra → F2 gateway → F3 identidade → F4 automação. "Hoje o usuário vai **conversar** com tudo isso."
- Mostre o estado atual: para saber "tem ingresso pra Brasil x Argentina", o usuário navega telas. Pergunte: **"E se ele só pudesse perguntar?"**
- Frase âncora no quadro: **"O LLM raciocina; o MCP Server tem os fatos."**

**Pergunta para a turma:** "Se eu perguntar a um ChatGPT puro 'tem ingresso pra Brasil x Argentina?', o que ele responde?" → Resposta esperada da turma: "ele inventa". Esse é o gancho para function calling.

---

## Bloco 1 — Conceitos: function calling + MCP (60 min)

**Objetivo:** a turma entende o loop e o protocolo **antes** de ver código.

### 1.1 Function calling (20 min)
- Desenhe o loop no quadro: LLM lê pergunta + catálogo de tools → **decide** chamar uma tool (devolve nome + args, **não executa**) → seu código executa → resultado volta ao LLM → LLM redige a resposta.
- Reforce: o LLM **não toca no banco**. Quem toca é a tool. O LLM é o gerente, a tool é o operário.
- Cite o limite real: **4 rodadas por mensagem** (`MAX_TOOL_ITERS` em `useLlmChat.ts`) — evita loop infinito.

### 1.2 MCP (40 min)
- "MCP é o USB-C das ferramentas de IA." Protocolo aberto (modelcontextprotocol.io).
- JSON-RPC 2.0: mostre o envelope (`jsonrpc`/`id`/`method`/`params`). Os dois métodos que importam: **`tools/list`** e **`tools/call`**.
- Streamable HTTP: por que `Accept: application/json, text/event-stream`.
- **Ponto de "No Invention":** o SDK provê `tools/list`/`tools/call`. Você **não inventa** métodos JSON-RPC. Se um aluno propõe um método que não existe na spec, pare: "está na spec? então não existe."

**Pergunta para a turma:** "Por que padronizar com um protocolo, em vez de cada um inventar seu formato de tools?" → Resposta: interoperabilidade — qualquer cliente MCP fala com qualquer servidor MCP.

**Onde travam:** confundir "o LLM executa a tool" com "o LLM pede a tool". Insista: o LLM só **pede**; o código executa.

---

## Bloco 2 — MCP Server .NET (90 min)

**Objetivo:** construir/entender o servidor de tools. Hands-on no `src/Fifa2026.V2.McpServer/`.

### 2.1 SDK 1.4.0 exato (15 min)
- Mostre o `.csproj`: `ModelContextProtocol.AspNetCore Version="1.4.0"` (exato). Explique o **porquê** do pin (ADE-002): SDK recente, minor pode mudar assinatura ao vivo. Contraste com o n8n da F4 (`latest` por decisão de owner) — pinar é a regra, n8n é a exceção.

### 2.2 Montagem do servidor (20 min)
- `Program.cs`, as 3 linhas: `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`.
- `app.MapMcp("/mcp")` — o endpoint. `/health` para o probe. `MapLlmProxy()` (deixe para o Bloco 4).
- Reforce: o framing JSON-RPC é do SDK. Você não escreve parser.

### 2.3 As 3 tools (35 min) — o coração do bloco
- `Tools/FifaTickerTools.cs`. Cada tool = método estático `[McpServerTool]` numa classe `[McpServerToolType]`.
- inputSchema **derivado** dos atributos `[Description]` — não se escreve schema à mão.
- DI nos parâmetros: `IFifaQueryRepository`, `EntraOidContext`, `ILogger` são injetados; os parâmetros "de negócio" (`matchId`, `ingressoId`, `rodada`) vêm do `arguments`.
- **Bug real para ensinar (vale ouro):** o SDK 1.4.0 marca nullable como **required** sem default. `consultar_disponibilidade` quebrava com só um argumento. Fix: `= null` nos dois. Coberto por `McpToolCallIntegrationTests`. Lição: leia o comportamento do SDK; teste o caminho real.

### 2.4 Dados: Dapper parametrizado (20 min)
- `Data/FifaQueryRepository.cs`. Mesmo padrão das Functions F1. **Tudo parametrizado** (`@MatchId`, `@IngressoId`, `@Stage`) — sem concatenação (SQL injection).
- Mostre o mapeamento `rodada → stage`: "oitavas"→`round_of_16`, "quartas"→`quarter_final`, etc. (`MapRodadaToStage`).
- Somente leitura — o MCP Server nunca grava.

**Pergunta para a turma:** "Por que as queries são parametrizadas e não montadas com string?" → SQL injection.

**Onde travam:** esquecer o `SqlConnectionString` (App Setting) → erro na 1ª query. E rótulos de categoria divergentes do seed (`VIP`/`Cat1`/`Cat2`).

---

## Bloco 3 — Chatbot React + LLM no front (80 min)

**Objetivo:** entender que a integração LLM é do **front**, e ver o loop em código.

- Arquivos: `components/Chatbot.tsx`, `hooks/useLlmChat.ts`, `lib/llm/*`, `lib/mcpTools.ts`, `lib/mcpClient.ts`.
- **Ponto-chave (ADE-002 Inv 3):** o MCP Server **não** chama o LLM. O front chama. O MCP Server só expõe tools.
- `Chatbot.tsx`: UI shadcn/ui (Sheet + Input + Badge). O badge mostra o **provider ativo** — guarde isso para o Bloco 6.
- `useLlmChat.ts`: o loop. Enquanto o LLM pedir tools → `callMcpTool` (via gateway) → devolve resultado. Quando o LLM responde só texto → exibe.
- `mcpClient.ts`: `callMcpTool` faz `POST {VITE_GATEWAY_V2_URL}/mcp` com `tools/call`, **Bearer Entra**. **Sempre via gateway**, nunca direto.
- `mcpTools.ts`: o catálogo que o front entrega ao LLM **espelha** as 3 tools reais (sincronia manual).
- Adapters: `gemini.ts` (formato próprio: `functionDeclarations` + `tool_config`) vs `openaiCompat.ts` (Groq+Mistral, formato OpenAI: `tools`/`tool_calls`).

**Pergunta para a turma:** "Por que pôr o LLM no front e não no .NET?" → Desacoplamento: o MCP Server não sabe qual LLM o chama; é o que permite a portabilidade.

**Onde travam:** achar que precisam de um SDK .NET do Gemini. Não — a integração é REST no front, via proxy.

---

## Bloco 4 — Segurança: a key no proxy server-side (45 min)

**Objetivo:** gravar a regra "segredo de servidor mora no servidor".

- **O problema:** key no bundle = qualquer um lê no DevTools. Demonstre ao vivo: abra o DevTools → Sources, mostre que tudo do front é público.
- **A solução:** proxy server-side (`Llm/LlmProxyEndpoints.cs` + `lib/llm/proxy.ts`).
  - Front chama `VITE_LLM_PROXY_URL/llm/{provider}/{path}` (Bearer Entra, via gateway).
  - Proxy injeta a key (App Setting) e encaminha ao endpoint oficial.
  - Gemini: key na query (`?key=`); Groq/Mistral: header `Bearer`.
- **Fail-safe:** sem `VITE_LLM_PROXY_URL` → o front **lança erro**, não embute key. Sem key no server → **503**.
- **Guard de CI:** mostre o step do `deploy-phase-05.yml` que falha o build se `gsk_`/`AIza`/nome de App Setting aparecer no `dist/`.

**Pergunta para a turma:** "Se a key está no servidor, o que o front conhece?" → Só a URL do proxy.

**Demo rápida (5 min):** rode `grep -rE 'AIza|gsk_' dist/assets/*.js` e mostre "nenhum match" → a key não vazou.

---

## Bloco 5 — Deploy + smoke das 3 tools ao vivo (60 min)

**Objetivo:** colocar tudo no ar e ver o chatbot responder com dados reais. Siga o PORTAL-GUIDE Passos 2-6.

- Provisione o Container App do MCP Server (reuse RG/Environment da F2).
- App Settings: `SqlConnectionString` + 3 keys (secretref). Reforce o `secretref` (esquecê-lo = 503).
- Conecte o gateway (URL do MCP Server via `McpServerDestinationConfigFilter`).
- Build do front com as 3 `VITE_*`.
- **Warmup:** chame `/health` antes da demo (scale-to-zero = cold start ~15s).
- Login v2 (MSAL F3) → abra o chatbot → 3 perguntas canônicas:

| Pergunta | Tool | Confira |
|---|---|---|
| "Tem ingresso para Brasil x Argentina?" | `consultar_disponibilidade` | preço por categoria do **seu** SQL |
| "Esse ingresso ID 123 é válido?" | `verificar_ingresso` | válido/inválido + dados da compra |
| "Quem está nas oitavas?" | `consultar_bracket` | jogos round_of_16 + placares |

- Mostre nos logs do MCP Server o `oid=...` **mascarado** (prova de PII-safe).
- SLA-alvo: < 10s por pergunta.

**Onde travam:** 401 (não fez Login v2), 503 (faltou secretref), resposta genérica (LLM não chamou a tool — refraseie).

---

## Bloco 6 — Bônus: portabilidade entre LLMs (demo ao vivo, 35 min)

**Objetivo:** a "virada de chave" pedagógica — o MCP desacopla o LLM dos dados.

### Roteiro da demo (5 min de execução, dentro do bloco)

1. **Estado inicial:** chatbot rodando com `gemini`. Badge mostra `gemini`. Faça a pergunta 1, mostre a resposta.
2. **Troca:** mude `VITE_LLM_PROVIDER=groq` e rebuild do front (ou troque a `var` do workflow e re-rode o job `deploy-frontend`). Em ambiente de demo, tenha **dois builds prontos** (um por provider) para trocar instantaneamente — evita esperar o build ao vivo.
3. **Mesma pergunta, novo cérebro:** faça **a mesma pergunta 1**. Badge agora mostra `groq`. A resposta é **equivalente** — porque os fatos vêm do **mesmo** MCP Server.
4. **Repita com `mistral`** se houver tempo/key.

### O que dizer enquanto troca

> "Olhem: eu **não toquei** no MCP Server. **Não toquei** nas tools. **Não toquei** no SQL. Só troquei uma env var. O Gemini, o Groq e o Mistral são cérebros diferentes que falam com os **mesmos** fatos. Isso é o MCP fazendo o que promete: desacoplar o LLM dos dados."

### Pontos técnicos para reforçar
- Groq e Mistral compartilham **um** adapter (`OpenAiCompatProvider`) — mesmo formato OpenAI. Só o Gemini tem adapter próprio.
- Modelos default reais: Groq `llama-3.3-70b-versatile`, Mistral `mistral-large-latest`, Gemini `gemini-2.0-flash`.
- Todos têm tier gratuito **sem cartão** — por isso são bons para workshop.

**Pergunta para a turma:** "Se amanhã sair um LLM melhor, quanto código eu mudo?" → Idealmente, um adapter novo (se o formato for diferente) ou zero (se for OpenAI-compat) + a env var.

---

## Bloco 7 — Fallback, retro e transição p/ F6 (30 min)

### 7.1 Fallback documentado: e se o Gemini cair? (AC-12) — 10 min

Cenário real de aula: o Gemini estoura cota ou fica fora do ar no meio da demo. Tenha **dois mecanismos** prontos:

**Mecanismo A — cache de respostas pré-carregado (para a pior das hipóteses):**
Tenha um screenshot/gravação das 3 respostas canônicas funcionando, capturado **antes da aula**. Se **todos** os LLMs falharem (raro), você mostra o cache e segue a explicação sem travar a aula. As 3 respostas de referência para cache:

| Pergunta | Resposta de referência (cache — capturar antes da aula com seu seed) |
|---|---|
| "Tem ingresso para Brasil x Argentina?" | "Sim, para Brasil x Argentina há `<N>` ingressos VIP a R$ `<X>`, `<N>` Cat1 a R$ `<Y>` e `<N>` Cat2 a R$ `<Z>`." |
| "Esse ingresso ID 123 é válido?" | "O ingresso 123 está `<válido/inválido>`. Comprador: `<nome>`, partida: `<jogo>`, categoria: `<cat>`, compra em `<data>`." |
| "Quem está nas oitavas?" | "Nas oitavas: `<jogo1>` (`<placar>`), `<jogo2>` ... classificados: `<times>`." |

> Capture o cache real durante o warmup (Bloco 5), antes da turma chegar — assim os números batem com o seu SQL.

**Mecanismo B — troca emergencial de provider (o caminho preferido):**
Como Gemini, Groq e Mistral são intercambiáveis por env var, a falha de **um** não derruba a aula. Tenha o build com `VITE_LLM_PROVIDER=groq` pronto:

1. Sinal de que o Gemini caiu: timeouts > 10s, ou 429 (quota), ou erro do proxy.
2. Sirva o build `groq` (já pronto). Diga à turma: "isto é a portabilidade salvando a demo — não é teatro, é o mecanismo real da F5."
3. Refaça a pergunta. Groq tem tier generoso e baixa latência — costuma salvar.

> **Mensagem pedagógica:** o fallback **é** a portabilidade. A resiliência não é um plano B improvisado; é uma propriedade da arquitetura que você construiu hoje.

### 7.2 Retro (10 min)
- O que a turma achou mais difícil? (geralmente: entender que o LLM "pede" e não "executa").
- Confirme os aprendizados: function calling, MCP/JSON-RPC, key no proxy, portabilidade.
- Cheque o checklist de conclusão do PORTAL-GUIDE com a turma.

### 7.3 Transição para a F6 (10 min)

> "Hoje vocês deram **voz** ao sistema. Mas reparem: cada pergunta do chatbot disparou uma cadeia — front → gateway → MCP Server → SQL → LLM → resposta. Vocês **viram** isso acontecer? Não — viram só a resposta. Na **F6** vamos **iluminar** essa cadeia: o **Flow Visualizer**, observabilidade de ponta a ponta. O chatbot vira **mais um nó** do fluxo, carregando o **`correlationId`** que nasceu no gateway (F2) e o **`entraOid`** que vocês propagam desde a F3. Vocês vão **ver** a request viajar."

Pontos de continuidade para citar:
- O `X-Entra-OID` que o MCP Server loga mascarado hoje vira um **atributo de span** rastreável na F6.
- O `correlationId` do gateway (F2) já passa em toda request — na F6 ele costura o fluxo inteiro.
- A F6 é a **última** fase: fecha o Living Lab mostrando o sistema completo, observável.

---

## Cola rápida do instrutor (one-liners de fidelidade)

- 3 tools: `consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket`.
- Endpoint: `POST /mcp` (Streamable HTTP via `MapMcp`), JSON-RPC 2.0, `tools/list`/`tools/call` do SDK.
- SDK: `ModelContextProtocol*` **1.4.0 exato** (.NET 8).
- LLM no **front** (ADE-002 Inv 3); MCP Server **não** chama LLM.
- Key da LLM no **proxy server-side** (`LlmProxyEndpoints.cs`), nunca no bundle; guard de CI.
- Chatbot → **gateway YARP** (Bearer Entra F3) → `/mcp`; `X-Entra-OID` propagado, lido **mascarado**, JWT **não** revalidado.
- Portabilidade: `VITE_LLM_PROVIDER=gemini|groq|mistral` (factory `createLlmProvider`).
- Gemini default `gemini-2.0-flash` (`v1beta`); Groq/Mistral OpenAI-compat (`/chat/completions`).
