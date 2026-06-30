# PO Validation Report — Stories 2.4, 2.5, 2.6, 2.7 (EPIC-002 Living Lab)

**Validator:** Pax (Product Owner)
**Date:** 2026-06-06
**Branch:** `docs/epic-002-stories-2.4-2.7` (v0.2 re-drafts by @sm)
**Story files:** `docs/stories/2.4.story.md`, `2.5`, `2.6`, `2.7`
**Parent epic:** `docs/epics/EPIC-002-living-lab-workshop.md`
**Source decisions (lei):** ADE-000, ADE-003, ADE-004 (gateway YARP), ADE-005 (identity workforce + MSAL/Easy Auth)
**Task executed:** `validate-next-story.md` (10-point checklist)
**Baseline de qualidade:** S2.1–S2.3 Done; prior PO reports S2.2 (9/10) e S2.3 (8/10)
**Context:** Re-escopo 2026-06-03 (APIM→YARP / External ID→App Reg workforce). As decisões arquiteturais são lei — esta validação afere **qualidade e coerência das stories**, não rediscute arquitetura.

## Verdicts (resumo)

| Story | Verdict | Score | Confidence | Status final |
|---|---|---|---|---|
| 2.4 — F4 n8n em Container Apps | ✅ GO | 9/10 | HIGH | Draft → Ready |
| 2.5 — F5 MCP + Chatbot + Gemini | ✅ GO | 9/10 | HIGH | Draft → Ready |
| 2.6 — F6 Flow Visualizer | ✅ GO | 9/10 | HIGH | Draft → Ready |
| 2.7 — Materiais didáticos transversais | ✅ GO | 9/10 | HIGH | Draft → Ready |

Nenhuma Critical Issue em qualquer story. Todos os GO. Detalhes abaixo.

## Verificações de fonte (anti-hallucination) — base factual desta validação

Verifiquei as claims técnicas centrais contra o código-fonte e ADEs reais:

| Claim na story | Verificado em | Resultado |
|---|---|---|
| Gateway YARP injeta `X-Correlation-ID` (nó zero) via `AddRequestTransform` | `src/Fifa2026.V2.Gateway/Program.cs` L52-66 | ✅ Confirmado literal |
| Gateway propaga `X-Entra-OID` (claim `oid`) downstream | `src/Fifa2026.V2.Gateway/Program.cs` L77-99 | ✅ Confirmado (anti-spoofing inclusive) |
| `PurchaseConsumerFunction` existe e expõe `InsertOutcome.Inserted/Duplicate` | `src/Fifa2026.V2.Functions/Functions/PurchaseConsumerFunction.cs` L63-83 | ✅ Confirmado (2.4 AC-6 fiel) |
| `entraOid` disponível ao consumer para o webhook F4 | `PurchaseEntryFunction.cs` L104-112 + `Models/PurchaseMessage.cs` (`EntraOid`) | ✅ Disponível (ver nuance Should-Fix 2.4) |
| `BeginScope(CorrelationId)` em Entry e Consumer (hops 1 e 3 de 2.6) | Entry L98 / Consumer L57 | ✅ Confirmado |
| MCP Server (`src/Fifa2026.V2.McpServer/`) ainda não existe | Glob | ✅ Inexistente — forward dependency correta (2.5 cria), sem invenção |
| FlowEvents (`src/Fifa2026.V2.FlowEvents/`) ainda não existe | Glob | ✅ Inexistente — forward dependency correta (2.6 cria), sem invenção |
| Artefatos F1-F3 já existem em `docs/workshops/phase-01..03/` | Glob (15 arquivos) | ✅ Confirmado (2.7 AC-7 fiel) |
| `coderabbit_integration.enabled: true` | `.aiox-core/core-config.yaml` L210-211 | ✅ Seção CodeRabbit é obrigatória — presente nas 4 stories |

---

## Story 2.4 — F4: Workflow Visual com n8n Self-Hosted em Container Apps

### Checklist 10 pontos

| # | Critério | Verdict | Observação |
|---|---|---|---|
| 1 | Título claro | ✅ PASS | "F4: Workflow Visual com n8n Self-Hosted em Container Apps" |
| 2 | Descrição completa | ✅ PASS | As a/I want/so that do aluno; disparo correto vindo do PurchaseConsumerFunction (F1), não do gateway |
| 3 | AC testáveis | ✅ PASS | 13 ACs verificáveis (401 sem auth, smoke E2E < 10s, execução visível no n8n UI, fire-and-forget timeout 5s) |
| 4 | Escopo bem definido | ✅ PASS | IN: CAE + Azure Files + n8n container + webhook no consumer; OUT implícito (não toca v1/gateway) |
| 5 | Dependências mapeadas | ✅ PASS | Depends on 2.1+2.2+2.3 Done; branch parte de `phase-03-identity`; F1 consumer como alvo |
| 6 | Complexidade estimada | ✅ PASS | MEDIUM (CodeRabbit) + 6h (alinhado ao epic S4) |
| 7 | Valor de negócio/didático | ✅ PASS | Workflow automation low-code + container hosting; quinto hop do fluxo v2 |
| 8 | Riscos documentados | ✅ PASS | Tabela troubleshooting (5 sintomas) + AC-13 anti-hallucination |
| 9 | DoD claro | ✅ PASS | 13 ACs + 10 tasks + Testing (smoke, security, persistência, idempotência) |
| 10 | Alinhamento PRD/Epic + re-escopo | ✅ PASS | Branch `phase-04-orchestration`, gateway YARP como nó zero do correlationId, entraOid (ADE-005) no payload |

**Score checklist: 10/10**

### Validações estruturais
- **Template:** todas as seções presentes; zero placeholders de template. `<container-app-fqdn>`, `<iniciais>`, `<tag-pinned>` são placeholders de runtime/decisão legítimos.
- **Executor Assignment:** executor `@devops` ✅ · quality_gate `@architect` ✅ · executor ≠ quality_gate ✅ · tools `[container-validation, iac-validation, security-check]` (non-empty) ✅ · Type-to-executor: Infra/Container/Deploy → @devops com @architect ✅
- **AC coverage:** 100% (AC-1→T1, AC-2→T2, AC-3/10→T4, AC-4→T3, AC-5/8→T5, AC-6/7→T6, AC-9→T7, AC-10→T8, AC-11/12→T9, AC-13→T10)
- **Security:** basic auth obrigatório (AC-10), HTTPS only, env vars nunca hardcoded, fire-and-forget não bloqueia consumer SB ✅
- **CodeRabbit (enabled:true):** seção completa (Deployment+Integration MEDIUM, @devops/@analyst primary, gates, self-healing report_only, focus areas) — PASS
- **Anti-hallucination:** AC-13 rastreia env vars a docs.n8n.io; `InsertOutcome.Inserted` verificado no código real; entraOid disponível ao consumer

### Findings
- **Critical:** Nenhum.
- **Should-Fix (não bloqueiam GO):**
  1. **Nuance de mecanismo de transporte do correlationId/entraOid.** A story (AC-6, AC-7, Dev Notes) diz que `correlationId`/`entraOid` chegam ao consumer "via Application Properties do Service Bus". A implementação real (F1) os transporta no **corpo da mensagem** (`PurchaseMessage` serializado via output binding `[ServiceBusOutput]`), não em `ApplicationProperties`. O dado **está disponível ao consumer de qualquer forma** (o webhook F4 consegue lê-los), então não bloqueia; mas a descrição do mecanismo é imprecisa. Recomendo @sm/@dev alinhar a redação a "campos da `PurchaseMessage` no corpo da mensagem" para evitar que o @dev procure por `message.ApplicationProperties` que não existem hoje. (Mesma nuance aparece na 2.6 — ver Should-Fix 2.6.)
- **Pendência de design (NÃO reprova — tratada como dependência sinalizada):**
  - **Tag do container n8n para @architect** (AC-3 + Task 4.2 + Dev Notes `[DECISAO-PENDENTE]`). Está **claramente marcada** como decisão de fase de design, com instrução explícita "não usar `latest`". Análogo ao AUTO-DECISION aceito na 2.3. O @dev/@devops tem caminho executável (pin de tag stable) e o @architect (quality gate desta story) confirma no design. Não é lacuna bloqueante.
- **Nice-to-have:** AC-5 lista `httpbin.org/post` para ambas as branches do Switch com "payload diferente" — poderia explicitar a diferença de payload; cosmético.

**Verdict 2.4: ✅ GO — 9/10 (HIGH).** -1 pela nuance Application Properties vs body (Should-Fix 1).

---

## Story 2.5 — F5: Inteligência Conversacional com MCP Server + Chatbot + Gemini 2.0 Flash

### Checklist 10 pontos

| # | Critério | Verdict | Observação |
|---|---|---|---|
| 1 | Título claro | ✅ PASS | "F5: Inteligência Conversacional com MCP Server + Chatbot + Gemini 2.0 Flash" |
| 2 | Descrição completa | ✅ PASS | As a/I want/so that do aluno; 3 tools nomeadas; portabilidade entre LLMs |
| 3 | AC testáveis | ✅ PASS | 15 ACs verificáveis (3 smoke tests canônicos, JSON-RPC 2.0, env var switch de provider, 401 sem token) |
| 4 | Escopo bem definido | ✅ PASS | IN: MCP Server Function + chatbot React + integração LLM; separação explícita das Functions de compra |
| 5 | Dependências mapeadas | ✅ PASS | Depends on 2.1-2.4 Done; chatbot via gateway YARP (Bearer + X-Entra-OID); data access alinhado a `Functions/Data/` |
| 6 | Complexidade estimada | ✅ PASS | HIGH (CodeRabbit) + 8h (alinhado ao epic S5, fase longa) |
| 7 | Valor de negócio/didático | ✅ PASS | Protocolo MCP + integração LLM em produto real + bônus portabilidade |
| 8 | Riscos documentados | ✅ PASS | Tabela troubleshooting (5 sintomas) + fallback Gemini→Groq (AC-12) + AC-15 anti-hallucination |
| 9 | DoD claro | ✅ PASS | 15 ACs + 9 tasks + Testing (unit, integration, smoke, security) |
| 10 | Alinhamento PRD/Epic + re-escopo | ✅ PASS | Branch `phase-05-ai-mcp`, MCP via gateway YARP (não direto do browser), entraOid via header (ADE-005), tools batem com decisão #5 do epic |

**Score checklist: 10/10**

### Validações estruturais
- **Template:** seções completas; zero placeholders de template. A `DECISAO PENDENTE @architect` no header é nota intencional, não placeholder.
- **Executor Assignment:** executor `@dev` ✅ · quality_gate `@architect` ✅ · ≠ ✅ · tools `[code-review, mcp-spec-validation, llm-prompt-validation]` (non-empty) ✅ · Type-to-executor: código/API → @dev com @architect ✅
- **AC coverage:** 100% (AC-1→T1, AC-6→T2, AC-2/3/4/5/15→T3, AC-7→T4, AC-8/9/10→T5, AC-11→T6, AC-12→T7, AC-13/14→T8, AC-15→T9)
- **Frontend completeness:** `Chatbot.tsx` com shadcn/ui (Sheet/Dialog), `useLlmChat` hook, state management — suficiente; confirma padrões existentes no projeto (T4.1)
- **Security:** API keys como App Setting (nunca em código), entraOid via header sem revalidar JWT na Function, prompt injection defense (CodeRabbit focus), oid não logado como PII ✅
- **CodeRabbit (enabled:true):** seção completa (API+Frontend+Integration HIGH, @dev/@analyst primary, gates, self-healing @dev light 2 iter, focus areas MCP spec + API key) — PASS
- **Anti-hallucination:** AC-15 rastreia spec MCP a modelcontextprotocol.io; AUTO-DECISION explícito (Dev Notes) para não inventar parâmetros de cada LLM API; `src/Fifa2026.V2.McpServer/` corretamente inexistente (forward dependency)

### Findings
- **Critical:** Nenhum.
- **Should-Fix:** Nenhum.
- **Pendência de design (NÃO reprova — dependência sinalizada):**
  - **Pinning do MCP SDK para @architect via ADE-002** (AC-6 + Task 2 marcada `[BLOQUEANTE para Task 3]` + Dev Notes `[DECISAO-PENDENTE]` + nota no header). Está **claramente marcada** e correta como sequência: a ADE-002 é pré-condição para a Task 3 do @dev. Isto é a forma EXEMPLAR de sinalizar uma dependência de design — modelada como gate interno explícito da própria story (Task 2 bloqueia Task 3), não como ambiguidade. Coerente com risco #4 do epic e com a decisão pendente "Pinning MCP SDK — S5 design — @architect" (EPIC-002 linha 171). Não bloqueia o Ready: o @dev sabe exatamente o que aguardar e de quem.
- **Nice-to-have:** AC-3/AC-4/AC-5 referenciam tabelas SQL "`purchases`, `ticket_categories` ou equivalente conforme schema real" — o "ou equivalente" dá pequena latitude; @dev resolve contra o schema real no design. Cosmético, não impacta testabilidade dos 3 smoke tests.

**Verdict 2.5: ✅ GO — 9/10 (HIGH).** -1 pela latitude "ou equivalente conforme schema real" nas tools (nice-to-have de precisão de schema). A pendência ADE-002 NÃO desconta — está modelada corretamente como gate sequencial.

---

## Story 2.6 — F6: Flow Visualizer com Correlation ID Animado em Tempo Real

### Ponto de atenção dedicado — Correção APIM → Gateway YARP

**Resultado: CORREÇÃO COMPLETA E CONSISTENTE. ✅**

Executei busca ativa (`grep`) por `APIM | API Management | External ID` em toda a 2.6. **Resultado: ZERO referências a APIM como componente ativo do fluxo v2.** Todas as ocorrências de "APIM" são de uma das duas categorias legítimas:
1. **Histórico/corretivo** — explicando que o draft v0.1 estava errado (linhas 6, 95, 189, 191, 282) e o Change Log v0.2.
2. **Instrução negativa** — "Gateway YARP — não APIM" / "confirmar nenhuma referência a APIM" (linhas 86, 103, 183, 264).

Verificações de consistência do fluxo de 6 nós:
- **Gateway YARP como nó zero** em TODAS as superfícies: AC-4 (tabela de hops, hop 0), AC-5 (diagrama), AC-6 (animação), AC-7 (smoke), Dev Notes (diagrama ASCII L210-216), Event types (`GATEWAY_YARP_RECEIVED`), Correlation matrix. ✅
- **Ordem dos 6 nós idêntica em todas as ocorrências:** Gateway YARP → Function Entry → Service Bus → Function Consumer → n8n → SQL. ✅
- **Fonte de verdade citada e verificada:** AC-4 hop 0 cita `src/Fifa2026.V2.Gateway/Program.cs` — `AddRequestTransform`; confirmei o trecho real no código (L52-66), incluindo o snippet reproduzido em Dev Notes (L196-205) que bate **literalmente** com o código. Sem invenção.
- **n8n como hop 5 (não APIM):** AC-4 hop 5 cita o payload do webhook F4 com `correlationId` — coerente com 2.4 AC-7.

A correção crítica está **completa, internamente consistente e rastreada à implementação real**. Nada residual.

### Checklist 10 pontos

| # | Critério | Verdict | Observação |
|---|---|---|---|
| 1 | Título claro | ✅ PASS | "F6: Flow Visualizer com Correlation ID Animado em Tempo Real" |
| 2 | Descrição completa | ✅ PASS | As a/I want/so that do aluno; "estrela didática" do workshop; rota `/flow` |
| 3 | AC testáveis | ✅ PASS | 13 ACs verificáveis (6 nós animados < 30s, TTI < 1.5s, 60fps, a11y sem violações críticas, tag v2.0.0) |
| 4 | Escopo bem definido | ✅ PASS | IN: SignalR + FlowEventsFunction + rota /flow; merge final → main com tag |
| 5 | Dependências mapeadas | ✅ PASS | Depends on 2.1-2.5 Done; branch parte de `phase-05-ai-mcp`; ADE-004 (nó zero) |
| 6 | Complexidade estimada | ✅ PASS | HIGH (CodeRabbit) + 8h (alinhado ao epic S6, fase longa final) |
| 7 | Valor de negócio/didático | ✅ PASS | Distributed tracing + correlation IDs + observabilidade; visualização final das 5 fases |
| 8 | Riscos documentados | ✅ PASS | Tabela troubleshooting (5 sintomas) + AC-13 anti-hallucination (diagrama = arquitetura REAL) |
| 9 | DoD claro | ✅ PASS | 13 ACs + 11 tasks + Testing (E2E, performance, a11y, regressão visual) |
| 10 | Alinhamento PRD/Epic + re-escopo | ✅ PASS | Branch `phase-06-flow-visualizer`, tag v2.0.0, 6 nós com Gateway YARP nó zero (não APIM) |

**Score checklist: 10/10**

### Validações estruturais
- **Template:** seções completas; zero placeholders de template. `<id>`, `<correlationId>` são placeholders de runtime legítimos.
- **Executor Assignment:** executor `@dev` ✅ · quality_gate `@architect` ✅ · ≠ ✅ · tools `[code-review, ui-validation, telemetry-validation]` (non-empty) ✅ · Type-to-executor: full-stack código → @dev com @architect ✅. Nota: `deployment_owner: @devops` para o merge/tag (AC-10) — coerente com autoridade exclusiva @devops.
- **AC coverage:** 100% (AC-1→T1, AC-2→T2, AC-4→T3, AC-3→T4, AC-5/6/7/8/9→T5, AC-7→T6, AC-8→T7, AC-9→T8, AC-10→T9, AC-11/12→T10, AC-13→T11)
- **UI/Frontend completeness:** `FlowDiagram` (6 nós SVG), `RecentPurchases`, `useSignalRConnection`, `motion.circle` framer-motion, fallback polling 2s, a11y (aria-label, prefers-reduced-motion, modo lista) — bem detalhado ✅
- **Security/observabilidade:** SignalR connection string como App Setting; App Insights SDK; Kusto query exemplo marcado "validar contra docs.microsoft.com" ✅
- **CodeRabbit (enabled:true):** seção completa (Frontend+Integration HIGH, @dev/@analyst primary, gates incl. "confirmar nenhuma referência a APIM", self-healing, focus areas correlation + a11y) — PASS
- **Anti-hallucination:** AC-13 + Task 11 validam App Insights/SignalR SDK contra docs Microsoft; `src/Fifa2026.V2.FlowEvents/` corretamente inexistente (forward dependency); snippet do gateway bate com código real

### Findings
- **Critical:** Nenhum.
- **Should-Fix (não bloqueiam GO):**
  1. **Mesma nuance da 2.4 — Application Properties vs corpo da mensagem.** AC-4 hop 2 e a Correlation matrix dizem que o `CorrelationId` é incluído nas `ApplicationProperties` da mensagem SB pelo PurchaseEntryFunction. A implementação real serializa o `CorrelationId` no **corpo** da mensagem (`PurchaseMessage`), via output binding, não em `ApplicationProperties`. Isso não afeta o tracing (App Insights captura via `BeginScope`/`customDimensions.CorrelationId`, que ESTÁ no código — Entry L98, Consumer L57), e o FlowEventsFunction consulta o App Insights, não as ApplicationProperties. Mas a tabela de propagação ficaria mais precisa descrevendo "corpo da `PurchaseMessage` + `BeginScope` para App Insights". Recomendo @sm alinhar. Baixo impacto: o caminho real de tracing (BeginScope → App Insights → Kusto) é o que a FlowEventsFunction usa, e esse está correto.
- **Nice-to-have:** AC-2 "Service Mode: Default (não Serverless)" está correto e bem justificado (precisa Hub clássico). Sem ação.

**Verdict 2.6: ✅ GO — 9/10 (HIGH).** -1 pela nuance Application Properties (Should-Fix 1). A correção APIM→YARP está completa e não desconta — está exemplar.

---

## Story 2.7 — Materiais Didáticos Transversais (36 artefatos)

### Ponto de atenção dedicado — Pré-condição BLOQUEANTE Azure SQL DB + tabela de nomenclatura

**Resultado: AMBOS PRESENTES E CORRETOS. ✅**

1. **Pré-condição Azure SQL Database (ADE-003) no pré-flight:** presente e tratada como **BLOQUEANTE** em múltiplas superfícies:
   - **AC-9** descreve explicitamente: "PRE-CONDIÇÃO FÍSICA OBRIGATÓRIA (ADE-003): Azure SQL Database ativo (não SQL em VM). Azure Functions em Consumption plan não estão em VNet e não alcançam SQL em VM — F1 não funciona sem Azure SQL DB."
   - **Task 6.2** cria seção "Pré-condição OBRIGATÓRIA — Azure SQL Database (ADE-003) — destacada como bloqueante para F1".
   - **Dev Notes** ("Pré-condição Azure SQL Database (ADE-003)") manda aparecer com callout ⚠️ BLOQUEANTE no PRE-WORKSHOP-CHECKLIST, no PORTAL-GUIDE de F1 e no SPEAKER-NOTES de F1.
   - Coerente com a pré-condição do epic (EPIC-002 linha 50: "EPIC-001 S4 concluído — Azure SQL Database ativa"). ✅
2. **Tabela de nomenclatura do re-escopo (YARP / App Registration):** presente e completa:
   - **AC-1** (nomenclatura no template), **AC-8** (glossário), **AC-11** (consistency check buscando APIM/External ID), **AC-14** (nomenclatura consistente).
   - **Dev Notes** tem a tabela "Nomenclatura editorial do re-escopo (OBRIGATÓRIA)" mapeando Gateway YARP (nunca APIM), App Registration workforce + MSAL.js (nunca External ID/CIAM), `oid`/`entra_oid` (nunca "GUID↔int mapping" nem "ADE-001"), incluindo as **notas culturais** (em produção o equivalente é APIM/External ID — aluno merece saber). ✅
   - **Task 8.6** adiciona busca ativa de referências obsoletas em todos os artefatos. ✅

### Checklist 10 pontos

| # | Critério | Verdict | Observação |
|---|---|---|---|
| 1 | Título claro | ✅ PASS | "Materiais Didáticos Transversais (READMEs + PORTAL-GUIDEs + SPEAKER-NOTES + slides + vídeos)" |
| 2 | Descrição completa | ✅ PASS | As a/I want/so that; objetivo "produto editorial único" de 40h |
| 3 | AC testáveis | ✅ PASS | 14 ACs verificáveis (36 artefatos, glossário, pre/post-workshop, nomenclatura grep-able, ~30-50 slides/fase) |
| 4 | Escopo bem definido | ✅ PASS | IN: template + padrões + glossário + checklists + 36 artefatos; inventário do que já existe (F1-F3) vs a produzir (F4-F6) |
| 5 | Dependências mapeadas | ✅ PASS | Paralela a S2.1-S2.6; revisão técnica gated por @dev quando cada fase Done; F1-F3 confirmados no repo |
| 6 | Complexidade estimada | ✅ PASS | MEDIUM (alto volume, baixa complexidade técnica) + 40h spread (epic S7) |
| 7 | Valor de negócio/didático | ✅ PASS | Consistência editorial entre fases; experiência homogênea instrutor/aluno |
| 8 | Riscos documentados | ✅ PASS | Pré-condição Azure SQL bloqueante; risco de nomenclatura obsoleta (Task 8.6); AC-12 anti-hallucination |
| 9 | DoD claro | ✅ PASS | 14 ACs + 10 tasks + Testing (markdown-lint, link-check, voice consistency, grep nomenclatura) |
| 10 | Alinhamento PRD/Epic + re-escopo | ✅ PASS | 36 artefatos = SC do epic; nomenclatura YARP/App Reg; pré-condição Azure SQL (ADE-003) |

**Score checklist: 10/10**

### Validações estruturais
- **Template:** seções completas; zero placeholders de template.
- **Executor Assignment:** executor `@analyst` ✅ · quality_gate `@pm` ✅ · ≠ ✅ · tools `[content-review, consistency-check, anti-hallucination-check]` (non-empty) ✅ · Type-to-executor: Research/conteúdo → @analyst com @pm ✅ (consistente com a matriz: conteúdo/research → @analyst/@pm). `review_collaboration: @po` e `technical_review_per_phase: @dev` coerentes.
- **AC coverage:** 100% (AC-1→T1, AC-2..6→T2, AC-7/11/14→T3+T4, AC-8→T5, AC-9→T6, AC-10→T7, AC-11/14→T8, AC-13→T9, AC-12→T10)
- **Anti-hallucination:** AC-12 rastreia claims a blueprint/source code/docs oficiais; inventário "Estado atual dos artefatos" verificado contra repo (15 arquivos F1-F3 confirmados via Glob — fiel ao Art. IV No Invention)
- **CodeRabbit (enabled:true):** seção completa (Documentation MEDIUM, @analyst/@pm primary, gates, "Not applicable" para self-healing com justificativa de content story, focus areas nomenclatura) — PASS. Nota: self-healing N/A é apropriado para story de conteúdo puro.

### Findings
- **Critical:** Nenhum.
- **Should-Fix:** Nenhum.
- **Nice-to-have:**
  1. **AC-13 (revisão técnica por @dev) e AC-9 (pré-condição):** a story atravessa fronteira de papéis (conteúdo @analyst, accuracy @dev por fase, estratégia @pm). A sequência está bem desenhada (Task 9 lista a revisão @dev por fase gated por Done). Sem ação — apenas registro de que o owner deve garantir o sequenciamento temporal (F4-F6 não podem ser tecnicamente revisados antes de S2.4-S2.6 Done).
  2. **Slides "Reveal.js OU markdown convertível" (AC-5):** o epic cita `slides.pdf`/Reveal.js; a story aceita markdown convertível "conforme padrão estabelecido em F1-F3". Os artefatos reais são `slides.md` (confirmado via Glob) — então a story está alinhada ao padrão de facto. Cosmético.

**Verdict 2.7: ✅ GO — 9/10 (HIGH).** -1 pela complexidade de coordenação inter-papéis (nice-to-have 1) — única dedução leve; conteúdo e ACs completos, pré-condição e nomenclatura exemplares.

---

## Confirmações solicitadas

1. **2.6 — Correção APIM → Gateway YARP:** ✅ **COMPLETA E CONSISTENTE.** Zero referências residuais a APIM como componente ativo (busca grep executada). Fluxo de 6 nós com Gateway YARP como nó zero em todas as superfícies (AC-4/5/6/7, diagrama, event types, correlation matrix). Fonte de verdade (`Program.cs` AddRequestTransform) verificada e o snippet em Dev Notes bate literalmente com o código.
2. **2.7 — Pré-condição BLOQUEANTE Azure SQL DB:** ✅ **PRESENTE no pré-flight** (AC-9 + Task 6.2 + Dev Notes com callout ⚠️ BLOQUEANTE em 3 superfícies: PRE-WORKSHOP-CHECKLIST, PORTAL-GUIDE F1, SPEAKER-NOTES F1). **Tabela de nomenclatura (YARP/App Registration):** ✅ **PRESENTE** em Dev Notes com notas culturais + AC-1/8/11/14 + Task 8.6 (busca ativa).
3. **Pendências de design (não reprovam):** ✅ Tag n8n (2.4) e ADE-002 pinning MCP SDK (2.5) estão **claramente marcadas** como decisões de fase de design e tratadas como dependências sinalizadas (a de 2.5 modelada como gate sequencial Task 2→Task 3, forma exemplar). Coerente com o tratamento dado ao AUTO-DECISION da 2.3. Não descontam score.

## Decisão final e transições

| Story | Verdict | Score | Status | Change Log |
|---|---|---|---|---|
| 2.4 | ✅ GO | 9/10 | Draft → **Ready** | v0.3.0 (entrada @po 2026-06-06) |
| 2.5 | ✅ GO | 9/10 | Draft → **Ready** | v0.3.0 (entrada @po 2026-06-06) |
| 2.6 | ✅ GO | 9/10 | Draft → **Ready** | v0.3.0 (entrada @po 2026-06-06) |
| 2.7 | ✅ GO | 9/10 | Draft → **Ready** | v0.3.0 (entrada @po 2026-06-06) |

**Reference:** `story-lifecycle.md` Phase 2 — Draft → Ready é responsabilidade @po; cumprida para as 4 stories. Transições registradas nos Change Logs.

## Next Steps
1. ✅ Status atualizado para Ready nas 4 stories.
2. ✅ Change Log entries adicionadas (v0.3.0).
3. ✅ Relatório consolidado salvo aqui.
4. Recomendação opcional ao @sm (não bloqueia dev start): alinhar a redação "Application Properties do SB" → "corpo da `PurchaseMessage` + BeginScope para App Insights" em 2.4 (AC-6/7) e 2.6 (AC-4/correlation matrix), para evitar que o @dev procure ApplicationProperties inexistentes. Incremento textual pequeno.
5. Cadeia cumulativa de dependências mantida: 2.4 (após 2.3 Done) → 2.5 (após 2.4) → 2.6 (após 2.5). 2.7 corre em paralelo com revisão técnica @dev gated por Done de cada fase.
6. @architect (quality gate de 2.4/2.5/2.6): confirmar no design a tag n8n (2.4) e produzir ADE-002 pinning MCP SDK antes da Task 3 de 2.5.
