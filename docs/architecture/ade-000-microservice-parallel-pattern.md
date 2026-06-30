# ADE-000 — Microsserviço Paralelo: Pattern Foundational do Workshop "Living Lab"

> **Tipo:** Architecture Decision Entry (foundational pattern)
> **Status:** ✅ Accepted
> **Date:** 2026-05-25
> **Author:** Aria (Architect)
> **Scope:** EPIC-002 (todas as 6 fases F1-F6)
> **Supersedes:** N/A (foundational)
> **Related:** ADE-001 (Entra ID mapping — pendente S2.3), ADE-002 (MCP SDK pinning — pendente S2.5)

---

## Context

O workshop "Living Lab Azure-Native" (EPIC-002) evolui o aplicativo FIFA 2026 Tickets em 6 fases cumulativas, introduzindo microsserviços .NET Azure-native em coexistência com o backend Node original. Esta ADE define o **pattern arquitetural foundational** que rege todas as 6 fases — qualquer desvio precisa de ADE específica que o supersede.

Esta ADE é particularmente crítica porque **S2.1 (F1) é o molde herdado por F2-F6** — erro de pattern aqui se propaga em 5 fases adicionais.

## Decision

Adotamos o pattern **"Parallel Microservice with Additive Schema"** com 7 invariantes:

### Invariante 1: Backend original intocado

O backend Node/Express + frontend Vite/React + tabelas SQL existentes **NÃO são modificados** em sua superfície de API/contratos. Novos microsserviços .NET (Functions) coexistem em fluxo v2 paralelo, gravando na mesma base de dados.

### Invariante 2: Schema delta apenas aditivo e idempotente

Mudanças de schema durante o workshop são SOMENTE:
- `ALTER TABLE ADD COLUMN` (com NOT NULL DEFAULT OU NULL)
- `CREATE INDEX`
- `CREATE TABLE` (novas tabelas auxiliares)

PROIBIDO durante o workshop:
- `DROP COLUMN`, `DROP TABLE`, `ALTER COLUMN ... ALTER DATATYPE`
- Renomear colunas/tabelas
- Mudar constraints existentes (PK/FK/UNIQUE)

Toda migration deve ser idempotente (`IF NOT EXISTS ... BEGIN ALTER TABLE ADD ...`).

### Invariante 3: Identificação de origem por linha

Tabelas com escrita por múltiplos microsserviços ganham coluna `source NVARCHAR(20) NOT NULL DEFAULT 'v1'`. Função: rastreabilidade do produtor de cada linha.

### Invariante 4: Idempotência robusta no consumer (NÃO SELECT-then-INSERT)

Mensageria com `at-least-once delivery` (Service Bus default) obriga idempotência no consumer. Pattern **mandatório**:

```sql
-- Pattern A (recomendado para F1-F6): UNIQUE constraint + INSERT com TRY/CATCH
ALTER TABLE purchases ADD CONSTRAINT UQ_purchases_correlation_id
    UNIQUE (correlation_id);
-- Consumer code (.NET):
try { await db.ExecuteAsync("INSERT INTO purchases ..."); }
catch (SqlException ex) when (ex.Number == 2627 /* unique violation */) { /* dup — ignore */ }

-- Pattern B (alternativo): MERGE atomic
MERGE INTO purchases AS target
USING (SELECT @correlation_id AS correlation_id) AS source
ON target.correlation_id = source.correlation_id
WHEN NOT MATCHED THEN INSERT (...) VALUES (...);
```

**PROIBIDO:** `SELECT ... WHERE correlation_id = @id; IF NOT FOUND INSERT ...` — sujeito a race condition (TOCTOU).

### Invariante 5: W3C Trace Context propagation

Correlation ID é propagado via:
- **HTTP hops:** header `traceparent` (W3C) + header `X-Correlation-ID` (compat) — ambos.
- **Service Bus hops:** `ApplicationProperties["CorrelationId"]` + .NET `Activity.Current.TraceId` automaticamente.
- **Log hops:** `ILogger.BeginScope(new { CorrelationId = id })`.
- **SQL hops:** coluna `correlation_id UNIQUEIDENTIFIER` (já em invariante 4).
- **Webhook (n8n) hops:** body JSON `{ correlationId: "...", ... }`.

Functions usam `Activity` API do .NET (`System.Diagnostics.Activity`) — propagação automática para chamadas downstream desde que SDKs sejam recentes (Microsoft.ApplicationInsights 2.22+, Azure.Messaging.ServiceBus 7.17+).

### Invariante 6: Recursos Azure por aluno (exceto APIM)

Cada aluno provisiona seus próprios recursos isolados:
- Resource Group: `rg-fifa2026-workshop-<iniciais>-phase-NN` (ou `rg-fifa2026-workshop-<iniciais>` único se aluno preferir reuso)
- Function App: `fifa2026-v2-functions-<iniciais>` (compartilhado entre fases) OU `fifa2026-v2-functions-<iniciais>-phase-NN` (por fase — recomendado para isolamento didático)
- Service Bus namespace: `sb-fifa2026-<iniciais>-<rand>`
- App Insights: `appi-fifa2026-<iniciais>`

ÚNICO recurso compartilhado: APIM `apim-fifa2026-workshop` (cenário A do blueprint, decisão de custo).

### Invariante 7: Branching cumulativo + cherry-pick para hotfixes

Branches são linhas-do-tempo cumulativas (não features paralelas):
```
main → phase-01 → phase-02 → phase-03 → phase-04 → phase-05 → phase-06 → merge → main
```

Se hotfix necessário em fase já entregue (e.g., bug encontrado em F1 durante F3):
1. Aplicar hotfix em `phase-01-servicebus-functions`
2. `git cherry-pick` para `phase-02-apim`, `phase-03-identity`, ..., `phase-06-flow-visualizer`
3. Re-deploy cada fase via CI/CD per branch
4. Não rebase (preserva história linear didática)

`main` é congelada pré-workshop. Pós-workshop merge F6 → main com tag `v2.0.0`.

---

## Rationale

### Por que microsserviço paralelo (vs reescrita)?

- **Pedagógico:** aluno vê comparação direta v1 vs v2 lado-a-lado ("antes vs depois" é ouro didático)
- **Reduz risco:** backend Node permanece como rede de segurança; se v2 falhar, fluxo v1 continua funcional
- **Atende restrição firme do owner:** "manter stack atual"
- **Realista:** modernização incremental é o pattern dominante em empresas reais (ninguém reescreve from scratch)

### Por que schema delta aditivo apenas?

- Idempotência garantida (migrations podem rodar N vezes)
- Backward compat: Node v1 não precisa saber das colunas novas (NULL ou DEFAULT cobrem)
- Reduz fragilidade durante 40h de workshop
- Permite rollback trivial (drop the new columns)

### Por que UNIQUE constraint + INSERT-catch (vs SELECT-then-INSERT)?

- **SELECT-then-INSERT é TOCTOU** (Time-Of-Check, Time-Of-Use): 2 consumers paralelos podem ambos não encontrar registro no SELECT e ambos fazer INSERT → 2 registros duplicados.
- Service Bus Standard com `prefetchCount > 0` ou múltiplas instâncias do Function App causa esse cenário ocasionalmente.
- UNIQUE constraint força o DB a ser o source-of-truth para idempotência. INSERT-catch é o pattern atomicamente correto.
- **Custo pedagógico:** explicar TOCTOU é didaticamente valioso (ensina concorrência distribuída).

### Por que W3C Trace Context (vs solução proprietária)?

- Padrão da indústria (https://www.w3.org/TR/trace-context/)
- Suportado nativamente por .NET 8 `Activity` API + Application Insights
- Compat com OpenTelemetry (F6 Flow Visualizer pode usar OTel queries no App Insights)
- Não inventa formato proprietário

### Por que recursos por aluno (vs compartilhado)?

- **Isolation:** aluno A não afeta aluno B (debug fica simples)
- **Free trial $200:** cada aluno tem sua subscription independente
- **Teardown:** cleanup ao final é per-aluno (script `teardown.ps1` destrói seu RG)
- **APIM compartilhado é a exceção justificada:** US$50 vs US$50/aluno (cenário A do cost model)

### Por que branching cumulativo (vs features branches)?

- **Espelha a jornada didática:** cada branch = um marco da aprendizagem
- **CI/CD por fase é viável:** workflow dedicado por branch
- **Rollback didático:** aluno pode checkout `phase-03` para revisitar F3
- **Drift mitigado:** main congelado + cherry-pick policy

---

## Consequences

### Positivas

- ✅ Workshop entrega 6 fases progressivas com baixíssimo risco de propagar bugs entre fases
- ✅ Backend original protegido; alunos podem comparar implementações
- ✅ Idempotência robusta evita "bugs de concorrência" que matam workshops em hands-on
- ✅ Observabilidade preparada para F6 (Flow Visualizer) desde F1
- ✅ Custo total previsível (Cenário A do blueprint: US$50-95 compartilhado + US$5-15/aluno)
- ✅ Pattern espelha modernização incremental do mundo real (transferível para vida pós-workshop)

### Negativas / Trade-offs aceitos

- ⚠️ **Duplicação de fluxos:** v1 e v2 fazem essencialmente a mesma coisa — ineficiente em produção (mas didático em workshop)
- ⚠️ **Schema acoplado:** v1 e v2 compartilham tabela `purchases` — mudança schema afeta ambos (mitigado por invariante 2: aditivo only)
- ⚠️ **APIM compartilhado:** rate-limit-by-key isola alunos, mas falha do APIM derruba o workshop inteiro (mitigado: APIM Developer tem 99.95% SLA implícito; ter procedimento de fallback "direto na Function" como backup)
- ⚠️ **Cold start de Functions:** primeiro request de cada bloco hands-on terá 5-10s de latency (mitigado: warmup automático 5min antes — Invariante 6 do EPIC-002)

---

## Alternatives Considered (rejeitadas)

### Alt 1: Reescrever backend Node em .NET

- **Rejected porque:** quebra restrição firme do owner ("manter stack atual"); 40h não comporta reescrita completa; perde a comparação didática v1/v2.

### Alt 2: Schema completamente novo (tabelas paralelas `purchases_v2`)

- **Rejected porque:** dobra complexidade de query (Visualizer F6 teria que UNION); v1 não enxerga compras v2 (perde paralelismo didático); fragmenta dados sem benefício técnico.

### Alt 3: Service Bus Premium ao invés de Standard

- **Rejected porque:** custo (~US$700/mês vs US$10/mês); workshop não precisa de partições nem VNet; Standard suporta topics que são suficientes para F1-F6.

### Alt 4: AKS para n8n ao invés de Container Apps

- **Rejected porque:** overkill pedagógico (Kubernetes ensina cluster ops, não workflow orchestration); custo (US$70+/mês para cluster vs US$5-15 Container Apps); operação mais complexa que assusta polyglot devs.

### Alt 5: Selct-then-INSERT para idempotência (pattern simples)

- **Rejected porque:** TOCTOU race condition (documentado em "Por que UNIQUE constraint + INSERT-catch"); pode causar duplicações ocasionais durante workshops com alunos paralelos.

### Alt 6: Trace ID proprietário ao invés de W3C Trace Context

- **Rejected porque:** padrão industrial existe e é suportado; inventar formato custom seria anti-padrão (Constitution Article IV — No Invention).

### Alt 7: Branches feature-paralelas (não cumulativas)

- **Rejected porque:** quebra jornada didática (aluno teria que fazer merge mental entre branches); CI/CD multiplicar; perde o "checkout phase-N para revisitar" simples.

---

## Validation

Este pattern é considerado **validado** quando:

- [ ] S2.1 (F1) implementa todas as 7 invariantes
- [ ] S2.2-S2.6 herdam as invariantes sem quebra
- [ ] Smoke tests de cada fase passam em CI/CD
- [ ] App Insights mostra correlation propagation end-to-end (testado em F6)
- [ ] Custo total do evento ≤ orçamento (Budget Alert + Cost Management report final)

## Impact on EPIC-002

Stories S2.1-S2.6 devem referenciar esta ADE em seus Dev Notes ("conforme ADE-000 Invariante N"). S2.1 será patcheada para:

- Trocar Task 4.2 de SELECT-then-INSERT para UNIQUE constraint + INSERT-catch (Invariante 4)
- Adicionar referência a W3C Trace Context + Activity API no Dev Notes (Invariante 5)
- Adicionar UNIQUE constraint à migration `phase-01.sql` (schema delta evolução)

---

**Authority:** Aria (Architect) — designado por @aiox-master para foundational patterns.
**Review cycle:** Imutável durante EPIC-002. Mudanças → nova ADE que a supersede.
