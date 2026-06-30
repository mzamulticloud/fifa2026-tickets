# PO Validation — Story 2.9 (Fase B: `criar_alerta_ingresso` + workflow n8n AI Agent)

> **Validador:** Pax (@po) · **Data:** 2026-06-10 · **Branch:** `phase-06-flow-visualizer`
> **Story:** `docs/stories/2.9.story.md` (draft commit f1ec7d2) · **Source:** ADE-006 v1.1 (Fases B.1/B.2/B.3 + Inv 1/2/4/7)
> **Task:** `validate-next-story.md` (10-point checklist) · **Modo:** YOLO (autônomo)

---

## Veredito

| | |
|---|---|
| **Decisão** | **GO** |
| **Implementation Readiness Score** | **9 / 10** |
| **Confidence Level** | **High** |
| **Status transition** | Draft → **Ready** (aplicado) |
| **Should-fixes aplicados nesta validação** | 1 (Risco 2 — accuracy do accessor de OID) |

---

## 10-Point Checklist (story-lifecycle.md Fase 2)

| # | Critério | Verdict | Nota |
|---|---|---|---|
| 1 | Título claro e objetivo | PASS | Título nomeia Fase B, a tool e o padrão dos dois agentes. |
| 2 | Descrição completa (problema/necessidade) | PASS | Story user-voice + rationale didático (dois agentes cooperando). |
| 3 | ACs testáveis | PASS | 12 ACs, cada um com discriminador verificável (contagem tools, ReadOnly, asserts de teste). AC-1/AC-7/AC-8/AC-11 são de verificação em instância viva — corretamente marcados como pendência runtime/smoke. |
| 4 | Escopo bem definido (IN/OUT) | PASS | Seção "Fora do escopo" explícita (Fase C/2.10, sem SQL, sem mudança no gateway/Functions/front). |
| 5 | Dependências mapeadas | PASS | Depends on S2.8 Done + S2.4 Done; Task 0 bloqueante mapeia o pré-requisito de verificação V-1..V-4. |
| 6 | Estimativa de complexidade | PASS | HIGH declarada na seção CodeRabbit, com justificativa (2 executores, nova abstração, ReadOnly=false pela 1ª vez). |
| 7 | Valor de negócio | PASS | Valor pedagógico claro (materializa split sentidos/mãos + dois agentes). |
| 8 | Riscos documentados | PASS | 7 gotchas + tabela de troubleshooting; ReadOnly=false, fire-and-forget, recursão do agente, tag `latest` cobertos. |
| 9 | Definition of Done | PASS | Quality gates Pre-Commit/Pre-PR/Pre-Deployment + contagem de testes alvo (>=67) + asserts de discriminador. |
| 10 | Alinhamento com PRD/Epic/ADE | PASS (após fix) | Cada AC rastreia a ADE-006 v1.1 / S2.4 / código real. Único desvio (accessor de OID inexistente) corrigido nesta validação. |

**Subtotal:** 10/10 PASS após o should-fix. Score de readiness = **9/10** (descontado 1 ponto pela pendência inerente de Task 0/AC-7/AC-8 ser verificável só na instância viva — risco de sprint legítimo, não defeito da story).

---

## Validações estruturais complementares (validate-next-story.md)

- **Template completeness (passo 1):** PASS. Todas as seções do molde S2.8 presentes (Executor Assignment, Story, ACs, Fora do escopo, CodeRabbit Integration, Tasks/Subtasks, Dev Notes, Troubleshooting, Testing, Change Log, Dev Agent Record, QA Results). Sem placeholders não preenchidos.
- **Executor Assignment (passo 1.1):** PASS. `executor: @dev` ≠ `quality_gate: @architect`; ambos agentes conhecidos; tools de gate apropriadas (code-review, mcp-spec-validation, webhook-contract-validation). Multi-executor (@devops para workflow n8n) declarado com rationale — coerente com trabalho de infra/config n8n.
- **File structure / source tree (passo 2):** PASS. Paths reais e precisos (`src/Fifa2026.V2.McpServer/Data/`, `Tools/FifaTickerTools.cs`, `Program.cs`); arquivos novos vs. estendidos claramente distinguidos na "Arquitetura delta".
- **AC satisfaction (passo 4):** PASS. Task-AC mapping explícito em cada task (`(AC: 3, 5)` etc.).
- **Testing instructions (passo 5):** PASS. 5 cenários unit + 1 integração DI + asserts de contagem; arquivos de teste reais referenciados.
- **Security (passo 6):** PASS. `entraOid` nunca em log raw (Task 9.4), App Setting via IConfiguration (Task 9.5), sem SQL no notifier (Task 9.2), fire-and-forget sem re-throw.
- **Task sequencing (passo 7):** PASS. Task 0 bloqueante → DTOs → notifier → tool → testes → build → workflow n8n. Ordem lógica e dependências corretas.
- **CodeRabbit (passo 8):** PASS. `coderabbit_integration.enabled: true` no core-config; seção completa (tipo, agentes, gates, self-healing light/2-iter/@dev, focus areas).
- **Anti-hallucination (passo 9):** PASS após fix — ver adjudicação Risco 2.
- **Dev readiness (passo 10):** PASS. Story self-contained: code samples de referência (notifier + tool), schema do payload, invariantes herdadas em tabela.

---

## Adjudicação dos 4 riscos sinalizados pelo @sm

### Risco 1 — Task 0 bloqueante (V-1..V-4 na instância n8n viva) → ADEQUADAMENTE TRATADO

A story deixa claro e inequívoco que **nenhuma codificação começa antes da Task 0**:
- AC-1 é rotulado "TASK 0 (BLOQUEANTE)" e exige registro dos achados **na própria story** ("seção Achados Task 0 abaixo do Change Log, ou comentário inline", Task 0.6) **antes** de Task 1.
- Task 0 é a primeira task, com 6 subtasks (0.1–0.6) cobrindo V-1 (nó AI Agent), V-2 (MCP Client Tool + transporte), V-3 (credencial Gemini), V-4 (FQDN interno do McpServer).
- ACs 7/8 declaram dependência explícita dos achados de V-1..V-4.

**Registro como risco de sprint:** se a `latest` do `n8nio/n8n` instalada **não** dispuser de AI Agent node / MCP Client Tool (ou com transporte incompatível com o ingress interno do McpServer), o escopo do @devops (AC-7/AC-8/Task 6) é **revisado** — o workflow agêntico não é construível como desenhado e a Fase B regride para a Opção 1 (n8n determinístico) ou aguarda atualização da imagem. Este é um risco real e aceito (consistente com Gotcha 4 e ADE-006 B.3/Inv anti-aluc. 7). A mitigação correta já está na story: verificar antes de codar. **Sem fix necessário.**

### Risco 2 — `EntraOidContext` accessor para o OID raw → SHOULD-FIX APLICADO

**Achado:** a story (em Dev Note L305, Gotcha 2, code sample L292, Task 1.1 e schema) referenciava `EntraOidContext.GetOidOrNull(): Guid?` como método **a possivelmente adicionar**. Verificação do código real (`src/Fifa2026.V2.McpServer/Tools/EntraOidContext.cs`):

- A classe **já expõe** `public string? GetRawOid()` (linha 30) — o OID raw já é acessível, **nenhum método novo é necessário**.
- O OID é tratado como **`string`** em todo o McpServer (valor bruto do header `X-Entra-OID`); **não há parsing para `Guid`**. Logo `GetOidOrNull(): Guid?` é duplamente impreciso: nome inexistente **e** tipo errado.
- (Contraste verificado: o `N8nWebhookPayload` de F4 usa `EntraOid (Guid?)` porque a mensagem Service Bus carrega Guid; o McpServer **não** — `EntraOidContext` só dá string.)

**Adjudicação:** concordo com a leitura do @sm de que o uso do OID raw no payload **é autorizado pelo ADE-006 Inv 7 v1.1** ("o `entraOid` chega ao n8n como campo do payload do webhook... é um *fato de contexto*, não um Bearer token"). Portanto usar o accessor não-mascarado para alimentar o payload é **rastreável ao ADE, não invenção (Art. IV)** — não é motivo de NO-GO. Mas a story afirmava uma API inexistente, o que **é** um defeito de accuracy que induziria o @dev a criar código desnecessário (e com tipo errado).

**Fix aplicado (story atualizada):**
1. AC-3 `entraOid`: usar `EntraOidContext.GetRawOid()` (`string?`, já existente); tipo no DTO = `string?`.
2. Task 1.1: `AlertWebhookPayload.EntraOid` = **`string?`** (não `Guid?`), com nota do porquê (McpServer não faz parse).
3. Code sample: `EntraOid = oidContext.GetRawOid()` (era `GetOidOrNull()`).
4. Dev Note (ex-L305) e Gotcha 2 reescritos: `GetRawOid()` já existe; **não criar** método novo; **log SEMPRE mascarado** via `GetMaskedOidForLog()` — valor raw nunca em log (Task 9.4 mantém a verificação).
5. Schema do webhook: `"entraOid": "string | null"`.

A invariante de segurança ("log sempre mascarado, raw só no payload") está fixada na story em 3 pontos.

### Risco 3 — Recursão do AI Agent (action tool visível ao próprio agente n8n) → ADEQUADAMENTE TRATADO

A story trata explicitamente em **Gotcha 6**: como o MCP Client Tool conecta ao McpServer interno que lista as 8 tools, o AI Agent **enxerga** `criar_alerta_ingresso`. A mitigação está definida:
- @devops **DEVE verificar** no n8n UI que o AI Agent **não invoca** `criar_alerta_ingresso` durante o workflow `chat-alert-ingresso` (sem recursão).
- Alternativa mais segura registrada: **limitar o subset de tools** do AI Agent via configuração do MCP Client Tool (se o n8n permitir seleção).
- A decisão deve ser registrada no AC-7 após verificação.

Isso é reforçado por AC-8 (regra de ouro no Agente 2) e Task 6.3 ("Confirmar que o agente enxerga apenas as tools `ReadOnly=true`"). **Tratamento adequado — sem fix necessário.** Observação nice-to-have: a melhor prática (restringir subset) e a verificação comportamental (não-invocação) coexistem; a story permite ambas — boa flexibilidade dado que o suporte do n8n a subset depende de V-1/V-2 (a confirmar na Task 0).

### Risco 4 — Nome do App Setting `N8N_ALERT_WEBHOOK_URL` → ALINHADO À CONVENÇÃO (sem fix)

Verificação da convenção real de F4 (`src/Fifa2026.V2.Functions/Data/N8nWebhookNotifier.cs` L22 + `deploy-phase-04.yml` + workshop docs):
- F4 usa `public const string WebhookUrlSetting = "N8N_WEBHOOK_URL"` — convenção: `N8N_*_URL`, UPPER_SNAKE, lido por `IConfiguration[...]`.
- A story usa `N8N_ALERT_WEBHOOK_URL` (Gotcha 1, code sample L222, Task 2.2/5.2/9.5) — **mesma convenção** (`N8N_` + qualificador + `_URL`).

A divergência de nome é **intencional e corretamente justificada** (Gotcha 1): F4 e a action tool apontam para **workflows n8n distintos** (`post-purchase-notification` vs `chat-alert-ingresso`); reusar `N8N_WEBHOOK_URL` faria a compra acionar o alerta e vice-versa. O nome distinto é a decisão certa e segue o padrão do projeto. **Sem fix necessário** — o nome está alinhado à convenção e o `_ALERT_` é o diferenciador semântico correto.

---

## Art. IV (No Invention) — verificação de rastreabilidade

| Claim da story | Fonte | Veredito |
|---|---|---|
| `criar_alerta_ingresso` ReadOnly=false (mão) | ADE-006 Inv 2, B.1 | Rastreável |
| Payload `{correlationId, entraOid, matchId, matchDescription, categoria, requestedAt}` | ADE-006 B.1/Inv 4 | Rastreável |
| Webhook fire-and-forget, timeout 5s, no-op se vazio | F4 `N8nWebhookNotifier.cs` (código real) | Rastreável |
| `CategoryLabelMapper.ToDbLabel()` rótulos reais ou null | ADE-002 decisão C / Inv anti-aluc. 4 | Rastreável |
| n8n → McpServer leste-oeste (bypass gateway) | ADE-006 Inv 7 v1.1 | Rastreável |
| Nomes de nós n8n (AI Agent / MCP Client Tool) **NÃO afirmados como fato** | V-1/V-2 a verificar (Task 0) | Correto — marcados como verificação, não afirmação |
| `EntraOidContext.GetOidOrNull(): Guid?` | **nenhuma** (método inexistente) | **Corrigido → `GetRawOid(): string?`** |
| `N8N_ALERT_WEBHOOK_URL` | convenção F4 + divergência justificada | Rastreável |

Único caso de "invenção" detectado (accessor inexistente) — corrigido. Nomes de nós n8n estão corretamente tratados como itens de verificação (V-1..V-4), não como fato — conformidade exemplar com Inv anti-alucinação 7.

---

## Issues classificadas

### Critical (Must Fix — bloqueia)
Nenhuma.

### Should-Fix (aplicadas nesta validação)
1. **[APLICADO]** Accessor de OID: `GetOidOrNull(): Guid?` (inexistente) → `GetRawOid(): string?` (real); DTO `EntraOid` de `Guid?` → `string?`. 5 pontos corrigidos na story.

### Nice-to-Have (não bloqueia; para @dev/@devops considerarem)
1. Na Task 6.3 (Risco 3), preferir **restringir o subset de tools** no MCP Client Tool (se V-1/V-2 confirmarem suporte) sobre depender só da verificação comportamental de não-invocação — defesa estrutural > comportamental.
2. AC-1/Task 0.6: padronizar o local de registro dos achados V-1..V-4 (a story oferece "seção abaixo do Change Log OU comentário inline" — sugerir a seção dedicada para auditabilidade consistente com o padrão de handoff).

---

## Anti-Hallucination Findings
- 1 claim não rastreável detectado e corrigido (accessor de OID). Demais claims verificados contra ADE-006 v1.1, código real (EntraOidContext.cs, N8nWebhookNotifier.cs, N8nWebhookPayload.cs, FifaTickerTools.cs, Program.cs) e S2.4. Sem bibliotecas/padrões inventados.

## CodeRabbit Integration Findings
- Seção presente e completa (enabled: true). Tipo (API+Integration), agentes (@dev primário, @devops, @architect gate, @analyst), gates (Pre-Commit/Pre-PR/Pre-Deployment), self-healing (light/2-iter/@dev), focus areas — todos coerentes com o escopo. PASS.

---

## Decisão final

**GO — Story 2.9 está Ready para implementação.** Score 9/10, confiança alta. Status atualizado Draft → Ready; should-fix do Risco 2 aplicado na story; Change Log v0.2 registrado.

**Handoff:** @dev `*develop 2.9` — **começar obrigatoriamente pela Task 0 (verificação V-1..V-4 na instância n8n viva)** e registrar os achados na story antes de qualquer codificação. @devops executa Task 6 (workflow n8n) com os achados de Task 0.

**Risco de sprint a monitorar:** disponibilidade de AI Agent node + MCP Client Tool na `n8nio/n8n:latest` instalada (Risco 1). Se ausente/incompatível, AC-7/AC-8/Task 6 são re-escopados.
