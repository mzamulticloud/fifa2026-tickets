# EPIC-002 Living Lab Workshop — RUN-LOG

> **Pipeline:** `living-lab-workshop`
> **Epic:** [EPIC-002 — Living Lab Workshop Azure-Native](EPIC-002-living-lab-workshop.md)
> **Blueprint:** [docs/workshops/2026-blueprint-living-lab-azure.md](../workshops/2026-blueprint-living-lab-azure.md)
> **Owner:** Guilherme Prux Campos
> **Status:** 📝 DRAFT (S2.1 Ready+arch-validated, aguardando @dev start após review offline)

---

## ▶️ Resume Here — Próxima sessão

**Active handoff (ler primeiro):** `.aiox/handoffs/handoff-2026-05-25-architect-to-dev-living-lab.yaml`

**Comando sugerido:** `@dev *develop 2.1`

**Pré-condições verificadas:**
- ✅ EPIC-002 publicado e aprovado pelo PM
- ✅ Blueprint + ADE-000 + S2.1 v0.3.0 (Ready) lidos por PO e Architect
- ✅ Owner reviewou docs offline e compartilhou com equipe TFTEC
- ⏳ Owner ainda precisa autorizar @dev start (confirmar antes de codar)

**Critical constraints para @dev (do handoff):**
1. Idempotência via UNIQUE constraint + INSERT-catch (NÃO SELECT-then-INSERT) — ADE-000 Inv 4
2. Parameterized queries (Dapper ou Microsoft.Data.SqlClient) — sem string concat em SQL
3. Schema delta apenas aditivo (sem DROP, sem ALTER COLUMN, sem rename) — ADE-000 Inv 2
4. `ILogger.BeginScope(new { CorrelationId })` para W3C Trace Context — ADE-000 Inv 5
5. Backend Node original NÃO é modificado — ADE-000 Inv 1
6. Warmup 5min antes de cada bloco de aula (mitigation cold start)

**Decisões pendentes (não bloqueiam S2.1):**
- ADE-001 (Entra ↔ local mapping) — resolve em S2.3 design
- ADE-002 (MCP SDK pinning) — resolve em S2.5 design
- Calendário workshop (4×10h ou 5×8h) — pré-evento
- Custom domain APIM — pré-evento

---

## Histórico da pipeline (waves consolidadas)

### Wave 1 — Brainstorm + Co-design (2026-05-24)

**Agent:** @analyst (Atlas) · **Status:** ✅ DONE · **Effort:** ~90min

**Delivered:**
- `docs/workshops/2026-blueprint-living-lab-azure.md` (576 linhas, 12 seções)
- Pivô importante: brainstorm divergente → co-design estruturado de workshop 40h
- 10 decisões fechadas (n8n hosting, LLM padrão, número de fases, External ID escopo, MCP tools, Visualizer real-time, custo+APIM tier, pré-req aluno, audiência, materiais por fase)
- F1 detalhada em 9 sub-itens (objetivos, arquitetura, endpoints, schema, CI/CD, roteiro, DoD, troubleshooting, tempo)
- F2-F6 em esqueleto

**Decisões chave:**
- Lente: híbrido "produto-real-como-laboratório" (features reais + ensináveis)
- Horizonte: 4-8 semanas (Copa iminente)
- Stack mantido (Vite+React+Node+SQL+Azure) — apenas estende com microsserviços .NET
- Backend Node intocado; novos serviços em fluxo v2 paralelo

**Patches durante wave:**
- APIM Consumption → **Developer** (sem rate-limit-by-key no Consumption)
- Provisioning Portal-first sempre; Bicep como apêndice opcional
- SPEAKER-NOTES.md adicionado como 6º artefato obrigatório por fase

**Handoff:** `handoff-2026-05-24-analyst-to-pm-living-lab.yaml` → @pm

---

### Wave 2 — Epic Creation (2026-05-25)

**Agent:** @pm (Morgan) · **Status:** ✅ DONE · **Effort:** ~20min

**Delivered:**
- `docs/epics/EPIC-002-living-lab-workshop.md` (~280 linhas)
- 7 stories planejadas com executor + quality gate atribuídos
- Compatibility requirements + 10 riscos com mitigação
- Decisões fechadas (10) + carry-forward (4) documentadas

**Story executor mapping:**
- S2.1, S2.2, S2.3, S2.5, S2.6: @dev / @architect
- S2.4: @devops / @architect
- S2.7: @analyst / @pm

**Handoff:** `handoff-2026-05-25-pm-to-sm-living-lab.yaml` → @sm

---

### Wave 3 — Story Drafting (2026-05-25)

**Agent:** @sm (River) · **Status:** ✅ DONE · **Effort:** ~60min

**Delivered:**
- `docs/stories/2.1.story.md` — F1 detalhada (~13 ACs, 9 tasks)
- `docs/stories/2.2.story.md` — F2 APIM
- `docs/stories/2.3.story.md` — F3 Identity
- `docs/stories/2.4.story.md` — F4 n8n
- `docs/stories/2.5.story.md` — F5 MCP+AI
- `docs/stories/2.6.story.md` — F6 Flow Visualizer
- `docs/stories/2.7.story.md` — Materiais didáticos transversal

**Padrão homogêneo:** Status Draft, Story aluno-perspective, AC numeradas com anti-hallucination AC final, CodeRabbit Integration completa (config enabled), Tasks/Subtasks check-listadas, Dev Notes com arquitetura+schema+troubleshooting, Testing section, Change Log, placeholders Dev/QA.

**Handoff:** `handoff-2026-05-25-sm-to-po-living-lab.yaml` → @po

---

### Wave 4 — PO Validation S2.1 (piloto) (2026-05-25)

**Agent:** @po (Pax) · **Status:** ✅ DONE · **Effort:** ~30min

**Delivered:**
- `docs/qa/2026-05-25-po-validation-S2.1.md` (relatório completo)
- S2.1 Status transition: **Draft → Ready** (Change Log v0.2.0)

**Verdict:** ✅ GO 9/10 (HIGH confidence)
- 10/10 checklist clean
- 11 validações estruturais adicionais: PASS em todas
- 0 critical / 0 should-fix / 3 nice-to-have

**Modo:** Piloto — validar S2.1 primeiro para confirmar padrão editorial. Outras 6 stories (S2.2-S2.7) ficaram Draft aguardando decisão do owner.

**Handoff:** `handoff-2026-05-25-po-to-architect-living-lab.yaml` → @architect (review preventiva por solicitação do owner)

---

### Wave 5 — Architect Review S2.1 (preventiva) (2026-05-25)

**Agent:** @architect (Aria) · **Status:** ✅ DONE · **Effort:** ~45min

**Delivered:**
- `docs/architecture/ade-000-microservice-parallel-pattern.md` — **foundational pattern** (7 invariantes que regem F1-F6)
- `docs/qa/2026-05-25-architect-review-S2.1.md` — relatório de review
- S2.1 patched para v0.3.0 com 1 fix obrigatório + 2 recommendations

**Verdict:** ✅ GO ARCHITECTURE (com patch aplicado)

**Critical issue detectado e corrigido:**
- SELECT-then-INSERT é TOCTOU race condition → substituído por UNIQUE constraint + INSERT-catch (`SqlException ex when ex.Number == 2627`)
- 2 consumers paralelos não conseguem mais duplicar registros
- Patch propagado: schema delta + Task 4.2 + Dev Notes

**Recommendations aplicadas:**
- W3C Trace Context propagation pattern (Activity API + `ILogger.BeginScope`) documentado no Dev Notes
- "Use Dapper com parameterized queries" explícito em Task 4.2

**ADE-000 — 7 invariantes herdadas por F1-F6:**
1. Backend original intocado
2. Schema delta apenas aditivo e idempotente
3. Identificação de origem por linha (coluna `source`)
4. Idempotência robusta (UNIQUE constraint + INSERT-catch)
5. W3C Trace Context propagation
6. Recursos Azure por aluno (exceto APIM compartilhado)
7. Branching cumulativo + cherry-pick para hotfixes

**Handoff:** `handoff-2026-05-25-architect-to-dev-living-lab.yaml` → @dev (ativo, aguarda autorização do owner)

---

## Carry-forward decisions (a resolver nas próximas waves)

| Decisão | Resolve em | Responsável | Tipo |
|---|---|---|---|
| ADE-001 — Mapping IDs Entra (GUID) ↔ IDs locais (int) | S2.3 (F3) design | @architect | Arquitetural |
| ADE-002 — Pinning de versão MCP SDK | S2.5 (F5) design | @architect | Arquitetural |
| Calendário workshop (4 finais-de-semana × 10h OU 5 dias × 8h) | Pré-evento | @pm | Operacional |
| Custom domain APIM compartilhado (opcional) | Pré-evento | @devops | Operacional |
| Validação S2.2-S2.7 pelo @po | Próxima sessão (após review owner) | @po | Processo |
| Script `bin/propagate-hotfix.sh` cherry-pick automático | S2.7 ou futura ADE | @devops | Nice-to-have |

---

## Artefatos consolidados desta pipeline

| Categoria | Arquivo | Status |
|---|---|---|
| Estratégico | `docs/workshops/2026-blueprint-living-lab-azure.md` | ✅ Done |
| Estratégico | `docs/epics/EPIC-002-living-lab-workshop.md` | ✅ Done |
| Estratégico | `docs/architecture/ade-000-microservice-parallel-pattern.md` | ✅ Done |
| Story | `docs/stories/2.1.story.md` v0.3.0 | ✅ Ready (arch-validated) |
| Story | `docs/stories/2.2.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| Story | `docs/stories/2.3.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| Story | `docs/stories/2.4.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| Story | `docs/stories/2.5.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| Story | `docs/stories/2.6.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| Story | `docs/stories/2.7.story.md` v0.1.0 | 📝 Draft (aguarda PO) |
| QA Report | `docs/qa/2026-05-25-po-validation-S2.1.md` | ✅ Done |
| QA Report | `docs/qa/2026-05-25-architect-review-S2.1.md` | ✅ Done |
| RUN-LOG (este) | `docs/epics/EPIC-002-RUN-LOG.md` | ✅ Done |

---

## Handoffs (cadeia)

| # | Wave | Handoff | Status |
|---|---|---|---|
| 1 | W1→W2 | `handoff-2026-05-24-analyst-to-pm-living-lab.yaml` | ✅ archived |
| 2 | W2→W3 | `handoff-2026-05-25-pm-to-sm-living-lab.yaml` | ✅ archived |
| 3 | W3→W4 | `handoff-2026-05-25-sm-to-po-living-lab.yaml` | ✅ archived |
| 4 | W4→W5 | `handoff-2026-05-25-po-to-architect-living-lab.yaml` | ✅ archived |
| 5 | W5→W6 | `handoff-2026-05-25-architect-to-dev-living-lab.yaml` | 🟢 **ACTIVE** |

> Handoffs 1-4 movidos para `.aiox/handoffs/_archive/living-lab/` conforme regra de consolidation. Handoff 5 (ativo) permanece individual para próxima sessão ler primeiro.

---

## Next Wave preview (Wave 6 esperada)

**Agent:** @dev (Dex)
**Comando:** `*develop 2.1`
**Pré-req antes de start:** owner autoriza explicitamente após review offline
**Estimativa:** 8-12h (Tasks 1-6 + 8 + 9 caminho crítico; Task 7 paralela com @analyst)
**Self-healing CodeRabbit:** light mode (2 iter, CRITICAL/HIGH only)
**Saída esperada:**
- Branch `phase-01-servicebus-functions` populada com código + workflow CI/CD
- 6 artefatos didáticos em `docs/workshops/phase-01/`
- Smoke tests passando
- Story Status → InProgress → InReview ao final
- Handoff Wave 6→7 (@dev → @qa para QA gate)
