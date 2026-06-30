# Architect Quality Gate — Story 2.5 (F5: MCP Server + Chatbot + Gemini 2.0 Flash)

> **Gate:** @architect (Aria) · **Date:** 2026-06-06 · **Branch:** `phase-05-ai-mcp`
> **Story:** [docs/stories/2.5.story.md](../stories/2.5.story.md) · **Epic:** EPIC-002
> **Gate tools:** code-review · mcp-spec-validation · llm-prompt-validation
> **Related:** [ADE-002](../architecture/ade-002-mcp-pinning.md) (pinning + Addendum com as 3 decisões)

---

## Verdict: **CONCERNS** (GO com 1 correção obrigatória antes do deploy)

A implementação de F5 é de alta qualidade: spec MCP correta via SDK oficial 1.4.0 exato, segurança da key da LLM tratada de forma exemplar (proxy server-side + guard de CI), identidade via `X-Entra-OID` sem revalidar JWT, queries 100% parametrizadas. **Nenhum problema de segurança crítico** (sem key vazada, sem SQL injection) — não há motivo de FAIL.

Há **1 defeito de correção em runtime** (M-1, severidade média): os rótulos de categoria no PIVOT de `consultar_disponibilidade` não batem com o seed real do banco. Não bloqueia o merge da story, mas **deve ser corrigido antes do deploy/demo** (AC-3/AC-11 falhariam ao vivo). Por ser corrigível com 1 linha de SQL e estar fora do caminho de segurança, o veredito é **CONCERNS**, com a correção registrada como carry-forward obrigatório.

---

## Confirmações de segurança e spec (pedido do mission)

| Item | Status | Evidência |
|---|---|---|
| API key da LLM **nunca no bundle** | ✅ | `src/lib/llm/proxy.ts` chama proxy server-side; sem `VITE_LLM_PROXY_URL` lança erro (nunca embute key). `LlmProxyEndpoints.cs` injeta a key de App Setting (`GEMINI/GROQ/MISTRAL_API_KEY`); sem key → 503; key nunca volta na resposta/log. Workflow `deploy-phase-05.yml` step "Guard" faz `grep` por `gsk_`/`AIza`/nomes de App Setting em `dist/assets/*.js` e falha o build. Teste `LlmProxyEndpointsTests` cobre 503-sem-key e 400-provider-desconhecido. |
| **Parameterized queries** (sem SQL injection) | ✅ | As 3 queries em `FifaQueryRepository.cs` usam `CommandDefinition` + objeto anônimo de parâmetros (`@MatchId`, `@MatchDescription`, `@IngressoId`, `@Stage`). Zero concatenação de input. O `LIKE` de `matchDescription` também é parametrizado (`@MatchDescription LIKE '%' + ht.name + '%'` — o input é o parâmetro, os nomes vêm da tabela). |
| **MCP SDK 1.4.0 exato** | ✅ | `.csproj` pina os 3 pacotes em `Version="1.4.0"` (sem range/wildcard); `dotnet list package` resolve `1.4.0/1.4.0` nos três. `MapMcp("/mcp")` + `AddMcpServer().WithHttpTransport().WithToolsFromAssembly()`. |
| **X-Entra-OID lido sem revalidar JWT, mascarado** | ✅ | `EntraOidContext.GetMaskedOidForLog()` retorna só 8 chars + `…` (ou "anônimo"); nunca o GUID inteiro. Gateway (`Program.cs`) extrai `oid` do claim **após** `AddJwtBearer` validar, e **remove qualquer X-Entra-OID do cliente** antes de injetar (anti-spoofing). McpServer apenas lê o header. |

---

## MCP spec compliance (mcp-spec-validation)

- **Transport:** Streamable HTTP via `MapMcp("/mcp")` (ADE-002 Inv 2). ✅
- **`tools/list` + `tools/call`:** providos pelo SDK (framing JSON-RPC 2.0 não implementado à mão — AC-15). O teste de integração `McpToolCallIntegrationTests` usa o **cliente MCP oficial** (`McpClient.CreateAsync` + `HttpClientTransport` StreamableHttp) e exercita `tools/call` ponta-a-ponta com DI real → prova que o despacho funciona e que o método/transport são reais, não inventados. ✅
- **3 tools com schema válido:** `consultar_disponibilidade`, `verificar_ingresso`, `consultar_bracket` com `[McpServerTool]` + `[Description]`; inputSchema derivado pelo SDK a partir da assinatura. ✅
- **Bug capturado por teste (qualidade):** SDK 1.4.0 marca parâmetro nullable como `required` salvo default; `consultar_disponibilidade` (ambos args opcionais — AC-3) quebrava em `tools/call` com 1 arg. Fix: defaults `= null`. Capturado pelo teste de integração. ✅

## LLM (llm-prompt-validation)

- **Gemini 2.0 Flash default:** `gemini.ts` usa `models/gemini-2.0-flash:generateContent`, `functionDeclarations` + `tool_config.function_calling_config.mode: AUTO` (ADE-002 Inv 3 / doc oficial). ✅
- **Portabilidade (AC-10):** `openaiCompat.ts` cobre Groq + Mistral (`chat/completions`, `tools`/`tool_calls`, `tool_choice: auto`); switch por `VITE_LLM_PROVIDER` (factory). Endpoints pinados por URL+modelo. ✅
- **Anti-hallucination:** o front não inventa endpoints — encaminha ao proxy, que fala o endpoint oficial pinado. ✅

---

## Decisão das 3 questões do @dev (registradas na ADE-002 Addendum)

1. **Host do McpServer = Azure Container App** (resolve o "em aberto" da Inv 2). Servidor HTTP de longa duração que serve Streamable HTTP; mesma justificativa/host do gateway YARP; Dockerfile + workflow já coerentes. Function isolated não recomendada para streaming.
2. **Proxy LLM dentro do McpServer = aceitável** para o escopo do workshop (reuso de host/App Settings, "tudo via gateway", menos um artefato em 8h). Separação fraca mas mitigada (endpoints minimal-API independentes; key fail-safe). Ressalva pós-epic: num produto real seria um BFF próprio. Não bloqueante.
3. **Rótulos VIP/Cat1/Cat2 ≠ seed real → CONCERN (correção obrigatória).** Ver M-1 abaixo.

---

## Issues por severidade

### Médio (M) — correção obrigatória antes do deploy/demo

- **M-1 — [requirements/data] Rótulos de categoria do PIVOT não batem com o seed real.**
  `FifaQueryRepository.ConsultarDisponibilidadeAsync` filtra `tc.category = 'VIP' / 'Cat1' / 'Cat2'`, mas o seed canônico (`fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql`; idem `legacy/seed.sql`) usa **`'VIP Premium'`, `'Categoria 1'`, `'Categoria 2'`** (legacy ainda tem `'Categoria 3'`). Resultado em runtime: todas as somas → 0 e preços → NULL para qualquer partida real. AC-3 e o smoke AC-11 ("Tem ingresso para Brasil x Argentina?") dariam resposta incorreta ao vivo.
  **Por que não foi pego:** os 32 testes mockam o repositório; o SQL real nunca roda contra o schema real (lacuna que o @dev sinalizou como "seed em runtime").
  **Recomendação (patch sugerido):** alinhar o PIVOT aos rótulos reais, tolerante a variação entre seeds:
  ```sql
  SUM(CASE WHEN tc.category LIKE 'VIP%'      THEN tc.available_quantity ELSE 0 END) AS VipDisponivel,
  SUM(CASE WHEN tc.category IN ('Categoria 1','Cat1') THEN tc.available_quantity ELSE 0 END) AS Cat1Disponivel,
  SUM(CASE WHEN tc.category IN ('Categoria 2','Cat2') THEN tc.available_quantity ELSE 0 END) AS Cat2Disponivel
  -- (idem para os MAX(...price...))
  ```
  Validar a query real contra a base no início da aula (paridade com a validação de seed das outras fases).

### Baixo (L) — observações, não bloqueiam

- **L-1 — [data] `consultar_bracket` referencia `s.name` (stadiums) e `m.time`.** Confirmado contra `schema.sql` (tabela `stadiums.name`, `matches.time NVARCHAR(5)`) — OK. Apenas registro de que a query depende de `stadiums` populado; se o seed de estádios variar, `Estadio` pode vir NULL (não quebra).
- **L-2 — [requirements] AC-4 `verificar_ingresso` recebe `ingressoId` int.** A AC-4 dizia "string ou integer (QR code ou ID)"; o código aceita só `int` (id da `purchases`). Decisão de @dev coerente com o schema (`purchases.id INT`). Aceitável; documentado para evitar surpresa se o material citar QR alfanumérico.
- **L-3 — [evolução] Proxy LLM co-hospedado:** ver Decisão B (ADE-002). Nota de evolução pós-epic (BFF próprio), não ação desta fase.

---

## ACs: código (verificado agora) vs runtime (ao vivo)

| AC | Tipo | Status |
|---|---|---|
| AC-1 branch+workflow | código | ✅ `deploy-phase-05.yml` (2 jobs, test sem `--no-build`) |
| AC-2 McpServer separado | código | ✅ `src/Fifa2026.V2.McpServer/`, `/mcp` via MapMcp |
| AC-3 consultar_disponibilidade | código | ⚠️ implementada, mas **M-1** (rótulos do PIVOT) |
| AC-4 verificar_ingresso | código | ✅ (ver L-2 sobre tipo do id) |
| AC-5 consultar_bracket | código | ✅ mapping rodada→stage real |
| AC-6 pinning ADE-002 | código | ✅ 1.4.0 exato (3 pacotes) |
| AC-7 Chatbot React | código | ✅ `Chatbot.tsx` + shadcn/ui montado no Layout |
| AC-8 integração via gateway | código | ✅ `mcpClient.ts` → `/mcp` Bearer Entra |
| AC-9 entraOid | código | ✅ gateway propaga, McpServer lê mascarado |
| AC-10 portabilidade env var | código | ✅ `VITE_LLM_PROVIDER` factory |
| AC-15 anti-hallucination | código | ✅ SDK oficial + endpoints pinados |
| AC-11 smoke ao vivo | runtime | ⏳ instrutor (depende de M-1 corrigido) |
| AC-12 fallback Groq | runtime/docs | ✅ mecanismo (env var) testado; cache em SPEAKER-NOTES |
| AC-13/14 artefatos+roteiro | docs | ✅ 6 artefatos em `docs/workshops/phase-05/` (@analyst) |

## Testes (executados neste gate)

| Suite | Resultado |
|---|---|
| `Fifa2026.V2.McpServer.Tests` | **32/32** aprovados |
| `Fifa2026.V2.Gateway.Tests` (regressão) | **11/11** aprovados |
| `Fifa2026.V2.Functions.Tests` (regressão) | **35/35** aprovados |

Sem regressão. (Frontend lint/tsc/build reportados OK pelo @dev; não re-executados neste gate .NET.)

## Docs — fidelidade

Os 6 artefatos de `docs/workshops/phase-05/` refletem o código real (3 tools corretas, `MapMcp`, SDK 1.4.0 exato, LLM no front, proxy server-side com key em App Setting, gateway YARP Bearer Entra, `X-Entra-OID` mascarado, portabilidade `VITE_LLM_PROVIDER`). Fidelidade Art. IV mantida. **Ressalva:** o PORTAL-GUIDE/SPEAKER-NOTES devem refletir a correção de M-1 quando aplicada (rótulos reais de categoria), para o smoke AC-11 funcionar como descrito.

---

## Carry-forward (obrigatório para próxima ação)

- **M-1 (obrigatório antes do deploy/demo):** corrigir rótulos de categoria do PIVOT em `FifaQueryRepository.ConsultarDisponibilidadeAsync` para os reais (`VIP Premium`/`Categoria 1`/`Categoria 2`, tolerante a variação) + validar a query contra a base no início da aula. Owner: @dev (1 linha de SQL) + validação de seed pelo instrutor.

---

## Authority

Aria (Architect) — quality gate de spec MCP, segurança e seleção/versão de tecnologia (EPIC-002). Decisões arquiteturais registradas em [ADE-002 Addendum](../architecture/ade-002-mcp-pinning.md).
