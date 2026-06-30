---
title: "F5 — Inteligência Conversacional: MCP Server + Chatbot + Gemini 2.0 Flash"
subtitle: "Workshop Living Lab Azure-Native · Fase 5 de 6 · 8h"
story: "2.5"
adr: "ADE-002"
---

# F5 — Inteligência Conversacional
## MCP Server + Chatbot React + Gemini 2.0 Flash

Workshop "Living Lab Azure-Native" · Fase 5 de 6 · **8h**

> "O LLM raciocina; o MCP Server tem os fatos."

---

## Onde estamos no Living Lab

- **F1** — Compra: `POST /purchase` → fila → consumer → SQL
- **F2** — Gateway YARP profissional (`X-Correlation-ID`)
- **F3** — Identidade Entra (JWT validado, `oid` → `X-Entra-OID`)
- **F4** — Automação visual (n8n)
- **F5** — **Hoje: o usuário CONVERSA com o sistema**
- F6 — Observabilidade ponta a ponta (Flow Visualizer)

---

## O problema

Para saber "tem ingresso pra Brasil x Argentina?", o usuário hoje:

- navega telas
- filtra
- lê tabelas

**E se ele só pudesse perguntar?**

---

## A armadilha do LLM "puro"

Pergunte a um LLM sem ferramentas:

> "Tem ingresso pra Brasil x Argentina?"

Ele **inventa** uma resposta plausível.
Ele não conhece o **seu** banco.

Isso é **alucinação** — inaceitável num produto.

---

## A solução: function calling (tool use)

O LLM recebe a pergunta **+ um catálogo de ferramentas**.

1. Lê a pergunta e o catálogo
2. **Decide** chamar uma tool (devolve nome + args)
3. **NÃO executa** — só devolve a intenção
4. **Seu código** executa (consulta o SQL)
5. Resultado volta ao LLM → ele **redige** a resposta

---

## O loop, em uma figura

```
usuário ─► LLM ─(pede tool)─► seu código ─► SQL
                ◄─(resposta natural)──┘ (resultado)
```

O LLM é o **gerente**. A tool é o **operário**.

Limite real: **4 rodadas/mensagem** (`MAX_TOOL_ITERS`).

---

## O que é o MCP

**Model Context Protocol** — protocolo aberto
([modelcontextprotocol.io](https://modelcontextprotocol.io/))

Padroniza como um **servidor** expõe tools e como um
**cliente** as descobre e usa.

> O "USB-C das ferramentas de IA".

---

## MCP usa JSON-RPC 2.0

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "consultar_disponibilidade",
    "arguments": { "matchDescription": "Brasil x Argentina" }
  }
}
```

Você **não** escreve esse envelope — o SDK cuida.

---

## Os dois métodos que importam

| Método | O que faz | Quem provê |
|---|---|---|
| `tools/list` | catálogo de tools | SDK (automático) |
| `tools/call` | executa uma tool | SDK → seu handler |

**No Invention:** não inventamos métodos JSON-RPC.
Está na spec, ou não existe.

---

## Transporte: Streamable HTTP

- Cliente faz `POST /mcp` com a mensagem JSON-RPC
- Resposta vem como JSON **ou** SSE (`text/event-stream`)
- Por isso: `Accept: application/json, text/event-stream`

Escolhemos HTTP porque o MCP Server fica **atrás do gateway YARP**
(mesmo padrão da F2).

---

# Parte 2 — O MCP Server .NET

`src/Fifa2026.V2.McpServer/`

---

## Um microserviço separado

- **.NET 8**, separado das Functions de compra (F1)
- Só faz uma coisa: **expor 3 tools de leitura** sobre o SQL
- **NÃO** chama o LLM (isso é do front)
- Host: **Azure Container App** (servidor HTTP de longa duração)

---

## SDK oficial pinado em 1.4.0 EXATO

| Pacote | Versão |
|---|---|
| `ModelContextProtocol.AspNetCore` | `1.4.0` |
| `ModelContextProtocol` | `1.4.0` |
| `ModelContextProtocol.Core` | `1.4.0` |

Por que **exato**? SDK recente → minor pode mudar assinatura **ao vivo**.
(n8n da F4 é a exceção: `latest` por decisão de owner.)

---

## Montar o servidor: 3 linhas

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport()        // Streamable HTTP
    .WithToolsFromAssembly();   // descobre [McpServerTool]
```

```csharp
app.MapMcp("/mcp");   // POST /mcp JSON-RPC
app.MapGet("/health", ...);
app.MapLlmProxy();    // proxy LLM (parte 4)
```

---

## As 3 tools

| Tool | Recebe | Consulta |
|---|---|---|
| `consultar_disponibilidade` | `matchId` ou `matchDescription` | matches + teams + ticket_categories |
| `verificar_ingresso` | `ingressoId` | purchases + users + categories + matches |
| `consultar_bracket` | `rodada` | matches + teams + stadiums (por stage) |

Cada uma = método `[McpServerTool]` numa classe `[McpServerToolType]`.

---

## Uma tool por dentro

```csharp
[McpServerTool(Name = "consultar_disponibilidade", ReadOnly = true)]
[Description("Consulta disponibilidade e preços ... Brasil x Argentina")]
public static async Task<AvailabilityResult> ConsultarDisponibilidadeAsync(
    IFifaQueryRepository repository,   // DI
    EntraOidContext oidContext,        // DI
    ILogger<DiagnosticsCategory> logger,
    int? matchId = null,               // opcional
    string? matchDescription = null,   // opcional
    CancellationToken cancellationToken = default)
```

inputSchema **derivado** pelos `[Description]` — sem schema à mão.

---

## Bug real que vale ouro

O SDK 1.4.0 marca nullable como **required** sem default.

`consultar_disponibilidade` (ambos opcionais) **quebrava**
com só um argumento.

**Fix:** `= null` nos dois parâmetros.
Coberto por `McpToolCallIntegrationTests`.

> Lição: leia o comportamento do SDK; teste o caminho real.

---

## Dados: Dapper parametrizado

`Data/FifaQueryRepository.cs` — mesmo padrão das Functions F1.

```sql
WHERE (@MatchId IS NOT NULL AND m.id = @MatchId)
   OR (@MatchId IS NULL AND @MatchDescription IS NOT NULL ...)
```

- **Tudo parametrizado** (`@MatchId`, `@IngressoId`, `@Stage`)
- Sem concatenação → sem SQL injection
- **Somente leitura** — nunca grava

---

## Mapear rodada → stage

```
"oitavas" / "16"  → round_of_16
"quartas"          → quarter_final
"semi"             → semi_final
"final"            → final
```

`MapRodadaToStage` (valores reais da migration knockout-matches).

---

# Parte 3 — Chatbot React + LLM

`Lovable/World Cup Tickets Hub/src/`

---

## A integração LLM vive no FRONT

> ADE-002 Invariante 3

- O **MCP Server NÃO chama o LLM**
- Quem conversa com Gemini/Groq/Mistral é o **navegador**
- O MCP Server só **expõe** tools

Por quê? Para **desacoplar** o LLM dos dados (= portabilidade).

---

## As peças do front

```
Chatbot.tsx       UI (Sheet + Input + Badge do provider)
useLlmChat.ts     loop function calling
lib/llm/gemini.ts adapter Gemini
lib/llm/openaiCompat.ts  adapter Groq + Mistral
lib/mcpTools.ts   catálogo (espelha as 3 tools reais)
lib/mcpClient.ts  callMcpTool → gateway /mcp
lib/llm/proxy.ts  envia ao proxy server-side
```

---

## O loop no hook

```ts
for (let iter = 0; iter < MAX_TOOL_ITERS; iter++) {
  const turn = await llm.chat(working, MCP_TOOLS, toolResults);
  if (turn.toolCalls.length === 0) { /* resposta final */ return; }
  for (const call of turn.toolCalls)
    toolResults.push(await callMcpTool(call.name, call.arguments));
}
```

Pede tool → executa via gateway → devolve → repete.

---

## callMcpTool: sempre via gateway

```ts
fetch(`${GATEWAY_V2_URL}/mcp`, {
  method: 'POST',
  headers: {
    Accept: 'application/json, text/event-stream',
    Authorization: `Bearer ${token}`,   // Entra (MSAL F3)
  },
  body: JSON.stringify({
    jsonrpc: '2.0', id, method: 'tools/call',
    params: { name: toolName, arguments: args },
  }),
});
```

Nunca direto no MCP Server. **Tudo passa pelo gateway.**

---

## Dois adapters, um formato compartilhado

| Provider | Formato | Adapter |
|---|---|---|
| Gemini | `functionDeclarations` + `tool_config` | `gemini.ts` |
| Groq | OpenAI-compat (`tools`/`tool_calls`) | `openaiCompat.ts` |
| Mistral | OpenAI-compat (`tools`/`tool_calls`) | `openaiCompat.ts` |

Groq e Mistral **compartilham** o adapter — mesmo formato.

---

# Parte 4 — Segurança da key

---

## O problema: key no bundle = key roubada

Se você embute a key da LLM no React:

- ela vai no **bundle JS** baixado por todos
- qualquer um abre o DevTools e **lê a sua key**
- gasta sua cota / sua conta

**Nunca embuta um segredo num bundle de browser.**

---

## A solução: proxy server-side

```
front ─(Bearer Entra, sem key)─► gateway ─► MCP Server proxy
                                              │ injeta a key (App Setting)
                                              ▼
                                     endpoint OFICIAL do provider
```

O front conhece só a **URL** do proxy. Nunca a key.

---

## As rotas do proxy

```
POST /llm/gemini/{path}  → generativelanguage.googleapis.com/v1beta/{path}?key=KEY
POST /llm/groq/{path}    → api.groq.com/openai/v1/{path}    (Bearer KEY)
POST /llm/mistral/{path} → api.mistral.ai/v1/{path}         (Bearer KEY)
```

Gemini → key na **query**. Groq/Mistral → header **Bearer**.

`Llm/LlmProxyEndpoints.cs`

---

## Fail-safe, nunca fail-open

- Sem `VITE_LLM_PROXY_URL` → o front **lança erro** (não embute key)
- Sem key no server → **503**
- **Guard de CI:** build falha se `gsk_`/`AIza`/nome de App Setting
  aparecer no `dist/`

> Segredo de servidor mora **no servidor**.

---

# Parte 5 — Identidade e contratos

---

## Tudo passa pelo gateway (herança F2/F3)

```
chatbot ─Bearer Entra─► gateway YARP
                          │ valida JWT
                          │ injeta X-Entra-OID (anti-spoofing)
                          ▼
                        MCP Server
                          │ lê X-Entra-OID → log MASCARADO
                          │ NUNCA revalida o JWT
```

O gateway é o guardião único.

---

## X-Entra-OID: PII-safe

```csharp
// EntraOidContext.cs
oid.Length <= 8 ? "********" : oid[..8] + "…"
```

- Lido **só** para logging/personalização
- Logado **mascarado** (8 chars + `…`)
- **Nunca** o GUID completo (é PII)
- **Nunca** revalida o token (gateway já fez)

---

## Contratos exatos

| Contrato | Valor |
|---|---|
| MCP | `POST {VITE_GATEWAY_V2_URL}/mcp` |
| Proxy LLM | `POST {VITE_LLM_PROXY_URL}/llm/{provider}/{path}` |
| Auth | `Authorization: Bearer <Entra>` |
| Identidade | `X-Entra-OID` (gateway → MCP) |
| Keys (server) | `GEMINI/GROQ/MISTRAL_API_KEY` |
| SQL (server) | `SqlConnectionString` |

---

# Parte 6 — Smoke + Portabilidade

---

## As 3 perguntas canônicas

| Pergunta | Tool |
|---|---|
| "Tem ingresso para Brasil x Argentina?" | `consultar_disponibilidade` |
| "Esse ingresso ID 123 é válido?" | `verificar_ingresso` |
| "Quem está nas oitavas?" | `consultar_bracket` |

Resposta com **dados do SEU SQL** · SLA < 10s · warmup antes (cold start).

---

## Bônus: portabilidade entre LLMs

Trocar o LLM = trocar **uma env var**:

```
VITE_LLM_PROVIDER=gemini   → Gemini 2.0 Flash    (default)
VITE_LLM_PROVIDER=groq     → llama-3.3-70b-versatile
VITE_LLM_PROVIDER=mistral  → mistral-large-latest
```

Zero mudança em tools, hook, componente ou MCP Server.

---

## A demo ao vivo

1. Chatbot com `gemini` — badge mostra `gemini`
2. Troca para `groq` (rebuild / 2º build pronto)
3. **Mesma pergunta** → badge `groq` → resposta **equivalente**

> "Não toquei no MCP Server, nas tools nem no SQL.
> Só troquei uma env var."

---

## Por que isso funciona

O MCP **desacopla** o LLM dos dados.

- O MCP Server nem sabe qual LLM o chamou
- Os fatos vêm sempre do **mesmo** servidor
- Só o "cérebro" que redige muda

Todos os 3 providers: tier gratuito **sem cartão**.

---

## Fallback: se o Gemini cair (AC-12)

**A** — cache pré-carregado (capturado no warmup): screenshot
das 3 respostas, para a pior das hipóteses.

**B** — troca emergencial: sirva o build `groq` (já pronto).

> O fallback **é** a portabilidade.
> A resiliência é propriedade da arquitetura.

---

# Parte 7 — Transição p/ F6

---

## O que vocês construíram hoje

- Um **MCP Server** com 3 tools sobre o SQL
- Um **chatbot** que entende perguntas e responde com fatos
- Um **proxy** que protege a key
- **Portabilidade** entre 3 LLMs por env var

Tudo passando pelo gateway, com identidade Entra.

---

## O que vem na F6

Hoje vocês deram **voz** ao sistema.
Cada pergunta dispara: front → gateway → MCP → SQL → LLM → resposta.

**Vocês viram isso acontecer?** Não. Só a resposta.

**F6:** o **Flow Visualizer** ilumina a cadeia inteira.
O `correlationId` (F2) e o `entraOid` (F3) costuram o fluxo.

---

## Obrigado!

**F5 concluída.**

3 tools · MCP 1.4.0 · proxy server-side · portabilidade

> "O LLM raciocina; o MCP Server tem os fatos."

Próxima e última: **F6 — Observabilidade ponta a ponta.**
