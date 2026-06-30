# Architecture Review — Story 2.1 (F1: Service Bus + Functions)

**Reviewer:** Aria (Architect)
**Date:** 2026-05-25
**Story:** `docs/stories/2.1.story.md`
**Parent epic:** `docs/epics/EPIC-002-living-lab-workshop.md`
**Triggered by:** Owner request (preventive review before @dev start — F1 é molde de F2-F6)
**Verdict:** ✅ **GO ARCHITECTURE (com 1 patch obrigatório aplicado em S2.1)**

---

## 1. Scope of Review

S2.1 foi previamente validada pelo @po com verdict GO 9/10. Owner solicitou esta revisão arquitetural ANTES de @dev iniciar implementação porque **F1 é o MOLDE herdado por F2-F6**. Pattern errado aqui propaga em 5 fases adicionais.

Foco da review:
1. Pattern de microsserviço .NET paralelo (v2 coexistindo com Node v1)
2. Idempotência no consumer (risk de TOCTOU race condition)
3. Schema delta strategy (aditivo idempotente)
4. Connection string → Managed Identity transition (preparar F3)
5. Correlation ID propagation (preparar F6 Flow Visualizer)
6. Branching cumulativo + cherry-pick policy
7. CI/CD por fase: slot vs RG dedicado
8. Naming + isolation por aluno

---

## 2. Findings by Focus Area

### 2.1 Pattern de microsserviço paralelo

✅ **Sound.** Backend Node intocado; novos Functions .NET gravam na mesma DB com flag `source='v2'`. Schema delta é aditivo only. Risco de conflito entre escritores: ZERO (cada compra é uma linha; `source` é apenas tag).

### 2.2 Idempotência no consumer

⚠️ **REQUIRED FIX.** S2.1 Task 4.2 atual diz "Validar idempotência via SELECT antes do INSERT". Isso é **TOCTOU race condition**: 2 consumers paralelos podem ambos não encontrar registro no SELECT e ambos fazer INSERT → duplicação.

**Fix necessário:** trocar para `UNIQUE constraint on correlation_id + INSERT com TRY/CATCH para violação 2627`. Documentado em **ADE-000 Invariante 4**.

**Impacto da mudança:**
- Migration `phase-01.sql` precisa adicionar `UNIQUE CONSTRAINT UQ_purchases_correlation_id (correlation_id)` filtered (`WHERE correlation_id IS NOT NULL`)
- Function Consumer .NET código: substituir `SELECT` por `try { INSERT } catch (SqlException ex) when (ex.Number == 2627)`
- AC-6 mantida (outcome é o mesmo)

### 2.3 Schema delta strategy

✅ **Sound** (com adição da UNIQUE constraint acima). `ALTER TABLE ADD COLUMN ... NOT NULL DEFAULT 'v1'` funciona em Azure SQL Standard tier (escreve default em rows existentes). Idempotência garantida por `IF NOT EXISTS`. Backward compat: Node v1 não precisa saber das colunas.

### 2.4 Connection string → MI transition

✅ **Smooth.** F1 usa connection string (debuggable, simples). F3 introduz Entra naturalmente → MI vira tema. Transição: App Setting + RBAC + binding attribute change. Bem demarcada.

### 2.5 Correlation ID propagation

⚠️ **RECOMMENDED IMPROVEMENT.** S2.1 AC-9 menciona App Insights + correlationId, mas não detalha pattern de propagation. Should referenciar:

- W3C Trace Context (`traceparent` header)
- .NET `Activity` API (auto-propagation com SDKs recentes)
- Service Bus `ApplicationProperties["CorrelationId"]`
- `ILogger.BeginScope(new { CorrelationId })`

**Patch S2.1:** adicionar bullet ao Dev Notes seção "Source files referenciados" + nota em Task 3.6 e 4.5.

### 2.6 Branching cumulativo

✅ **Sound** (com adição de política cherry-pick). Risk de drift se hotfix em F1 durante F3 já existe — mas é mitigado por:
- Main congelada pré-workshop
- Política cherry-pick documentada em ADE-000 Invariante 7

### 2.7 CI/CD por fase

✅ **Sound** com **recommendation:** RG dedicado por fase (`rg-fifa2026-workshop-<iniciais>-phase-NN`) > slot dedicado. Mais didático (mostra cleanup, custos isolados, IaC parametrizado). Já alinhado com EPIC-002.

### 2.8 Naming + isolation por aluno

✅ **Sound.** Recursos por aluno (Function App, SB, App Insights) + APIM compartilhado (Cenário A). Free trial $200 por aluno cobre os custos individuais. Documentado em ADE-000 Invariante 6.

---

## 3. Critical Issues — Architecture-blocking

| # | Issue | Severity | Action |
|---|---|---|---|
| 1 | Idempotência via SELECT-then-INSERT (TOCTOU) | **HIGH** | **REQUIRED FIX:** Substituir por UNIQUE constraint + INSERT-catch (ADE-000 Inv 4). **Patch aplicado em S2.1 nesta sessão.** |

## 4. Recommended Improvements (não bloqueantes)

| # | Improvement | Action |
|---|---|---|
| 1 | Detalhar correlation propagation com W3C Trace Context + Activity API | Patch S2.1 Dev Notes (aplicado nesta sessão) |
| 2 | Adicionar nota explícita "Dapper com parameterized queries" (nice-to-have do @po) | Patch S2.1 Dev Notes (aplicado nesta sessão) |
| 3 | RG dedicado por fase no naming | Já alinhado com EPIC-002; reforçado em ADE-000 Inv 6 |

## 5. Nice-to-Have (futuras iterações)

| # | Item | Quando |
|---|---|---|
| 1 | Script `bin/propagate-hotfix.sh` para cherry-pick automático | S2.7 (transversal) ou ADE futura |
| 2 | OTel instrumentation além de App Insights nativo | Opcional em F6 (bônus didático) |
| 3 | Feature flag para fluxo v2 (toggle no frontend) | S2.6 ou pós-workshop |

---

## 6. Documents Produced in This Session

| Document | Path | Purpose |
|---|---|---|
| **ADE-000** | `docs/architecture/ade-000-microservice-parallel-pattern.md` | Foundational pattern (7 invariantes) que rege F1-F6 |
| **Architecture review report** (este) | `docs/qa/2026-05-25-architect-review-S2.1.md` | Findings e verdict |
| **S2.1 patch** | `docs/stories/2.1.story.md` (versão 0.3.0) | Fix obrigatório + recommendations |

---

## 7. Final Assessment

| Métrica | Valor |
|---|---|
| **Verdict** | ✅ **GO ARCHITECTURE** (com patch aplicado) |
| **Critical fixes applied** | 1 (idempotência robusta) |
| **Recommendations applied** | 2 (W3C Trace Context, Dapper) |
| **Nice-to-haves deferred** | 3 |
| **ADE produced** | ADE-000 (foundational) |
| **Story status** | Ready (mantido) |
| **Implementation confidence** | HIGH |

S2.1 está pronta para `@dev *develop 2.1`. Pattern arquitetural está sólido para herança por F2-F6.

---

## 8. Next Steps

### Imediato
1. ✅ ADE-000 publicada
2. ✅ S2.1 patcheada (Task 4.2, Dev Notes, schema delta)
3. ✅ Architecture review report salvo
4. ➡️ **Handoff para @dev** começar implementação de S2.1

### Futuro (durante implementação)
- @dev consulta ADE-000 antes de qualquer dúvida arquitetural
- @architect (você, eu mesmo, Aria) será chamado novamente em S2.3 (ADE-001) e S2.5 (ADE-002)
- @qa valida cumprimento das 7 invariantes na review de cada fase

---

**Authority:** Aria (Architect) — design authority per `.claude/rules/agent-authority.md`.
**Anti-hallucination check:** ✅ Todas claims rastreáveis ao blueprint, EPIC-002, schema.sql real, docs SQL Server / .NET / Service Bus oficiais.
