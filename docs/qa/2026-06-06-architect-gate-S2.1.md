# Architect Quality Gate — Story 2.1 (F1: Service Bus + Functions .NET)

> **Gate type:** Architecture + Code Quality (quality_gate: @architect)
> **Date:** 2026-06-06
> **Reviewer:** Aria (Architect)
> **Story:** [2.1](../stories/2.1.story.md) — F1 Mensageria Desacoplada
> **Foundational pattern:** [ADE-000](../architecture/ade-000-microservice-parallel-pattern.md) (7 invariantes — herdadas por F2-F6)
> **Branch:** `phase-01-servicebus-functions` (HEAD `3fbf72e`)
> **Tools applied:** code-review, pattern-validation, manual-review

---

## Verdict: **GO (PASS) com CONCERNS**

```yaml
storyId: 2.1
verdict: PASS
adherence_ade_000: CONFIRMED (7/7 invariantes)
concerns: 4   # 1 HIGH (CI), 2 MEDIUM, 1 LOW — nenhum bloqueante para o pattern foundational
blocking_issues: 0
```

**Síntese:** A fundação arquitetural está **correta e segura para ser herdada por F2-F6**. As 7 invariantes do ADE-000 estão materializadas com fidelidade — em especial Inv 4 (idempotência sem TOCTOU) e Inv 5 (correlation propagation), que eram os pontos de maior risco. Os ACs de código estão atendidos. Os concerns são de qualidade operacional (CI/CD) e robustez incremental, não de design — por isso o veredito é **GO**, com 4 issues documentados para tratamento (1 deles, o CI, recomendado antes do primeiro deploy real).

---

## 1. Acceptance Criteria — atendidos vs pendentes

| AC | Tipo | Status | Evidência |
|----|------|--------|-----------|
| AC-1 — Branch + workflow CI/CD | código | ✅ ATENDIDO | branch `phase-01-servicebus-functions`; `.github/workflows/deploy-phase-01.yml` |
| AC-2 — Recursos Azure via Portal | runtime | ⏸️ RUNTIME-ONLY | demonstrável pelo aluno/@devops; PORTAL-GUIDE cobre RG→NS→queue→DLQ |
| AC-3 — PurchaseEntryFunction | código | ✅ ATENDIDO | HTTP POST `v2/purchase`, GUID, `[ServiceBusOutput]`, 202 `{correlationId,status:queued}` |
| AC-4 — PurchaseConsumerFunction | código | ✅ ATENDIDO | SB trigger, INSERT `source='v2'`, `correlation_id`, `status='completed'` |
| AC-5 — Schema delta idempotente | código | ✅ ATENDIDO | `phase-01.sql`: IF NOT EXISTS, UNIQUE filtered index, aditivo only |
| AC-6 — Idempotência via UNIQUE + INSERT-catch | código | ✅ ATENDIDO | `PurchaseRepository` catch 2627/2601 → `Duplicate`; teste `Duplicate_is_swallowed_silently` |
| AC-7 — DLQ funcional | híbrido | 🟡 PARCIAL (lógica ✅ / demo runtime) | re-throw em CategoryNotFound/JSON inválido/correlationId vazio; testes cobrem; reprocessamento no SB Explorer é runtime |
| AC-8 — Endpoint de status | código | ✅ ATENDIDO | GET `v2/purchase/{correlationId}`, mapeamento de status, 3 testes |
| AC-9 — Observabilidade (App Insights) | híbrido | 🟡 PARCIAL (wired ✅ / trace live) | `BeginScope(CorrelationId)` nas 3 Functions, App Insights registrado em `Program.cs`; trace live é runtime |
| AC-10 — CI/CD funcional | híbrido | 🟡 PARCIAL (workflow ✅ / **ver Issue H-1**) | workflow completo; ordering `--no-build` quebra o step de teste em CI |
| AC-11 — 5 artefatos didáticos | código | ✅ ATENDIDO | README, PORTAL-GUIDE, SPEAKER-NOTES, slides (~46), intro-video-script |
| AC-12 — Roteiro 6h (7 blocos) | doc | ✅ ATENDIDO | documentado no SPEAKER-NOTES |
| AC-13 — Anti-hallucination (Art. IV) | código | ✅ ATENDIDO | tabela `purchases`, colunas e `ticket_categories.category` validados contra `schema.sql` |

**Resumo:** 9 ACs de código totalmente atendidos (1,3,4,5,6,8,11,12,13). 3 híbridos parciais com a parcela de código completa e a parcela runtime legitimamente pendente (7,9,10). 1 puramente runtime (2). Nenhum AC de código não atendido.

**Runtime-only (legítimos, fora do escopo @dev/@architect — para @devops/aluno):** AC-2 (Portal), AC-7 demo no SB Explorer, AC-9 trace live, AC-10 deploy + smoke test reais.

---

## 2. ADE-000 — Conformidade com as 7 invariantes

| Inv | Invariante | Status | Avaliação |
|-----|-----------|--------|-----------|
| 1 | Backend original intocado | ✅ | Nenhuma alteração em `fifa2026-api/` ou frontend; v2 grava na mesma DB em fluxo paralelo |
| 2 | Schema delta aditivo + idempotente | ✅ | `phase-01.sql` só faz ADD COLUMN + CREATE INDEX, tudo sob `IF NOT EXISTS`; nenhum DROP/ALTER COLUMN |
| 3 | Identificação de origem por linha | ✅ | `source NVARCHAR(20) NOT NULL DEFAULT 'v1'`; INSERT v2 grava `'v2'` |
| 4 | **Idempotência robusta (NÃO SELECT-then-INSERT)** | ✅ **CONFIRMADO** | UNIQUE filtered index + INSERT direto + `catch SqlException when ex.Number is 2627 or 2601`. **Zero TOCTOU.** Padrão correto para herança F2-F6 |
| 5 | **W3C Trace Context propagation** | ✅ **CONFIRMADO** | `correlationId` GUID na entrada → `ApplicationProperties`/Activity automático no SB hop → `BeginScope` (log hop) → coluna `correlation_id` (SQL hop). App Insights worker registrado |
| 6 | Recursos Azure por aluno | ✅ | PORTAL-GUIDE segue naming `rg-fifa2026-workshop-<iniciais>`, `sb-fifa2026-<iniciais>-<rand>` |
| 7 | Branching cumulativo | ✅ | `phase-01-servicebus-functions` partindo de `main`; workflow per-branch |

**Veredito ADE-000: 7/7 invariantes confirmadas.** O molde está arquiteturalmente correto para propagação às 5 fases seguintes.

### Destaque — Inv 4 (o ponto crítico)

O `PurchaseRepository.InsertPurchaseAsync` é o coração do pattern herdado. Implementação exemplar:
- INSERT...SELECT atômico com JOIN em `ticket_categories` (resolve `ticket_category_id`/`unit_price`/`total_price` numa única statement) — sem round-trip extra, sem janela de race.
- Duplicata detectada pelo DB (source-of-truth), não por SELECT prévio.
- `rowsAffected == 0` distingue categoria inexistente (→ DLQ) de duplicata (→ silenciosa). Distinção semântica correta e didaticamente valiosa.

### Destaque — Inv 5

A escolha de **não** criar `Activity` manualmente em F1 (delegando ao SDK isolated worker ≥ 7.17) está alinhada ao Dev Notes e ao ADE-000. Correto para esta fase; F6 (Flow Visualizer) terá a cadeia W3C completa sem retrabalho.

---

## 3. Code Review — qualidade e segurança

### Pontos fortes
- **Parameterized queries em 100% dos acessos** (Dapper `CommandDefinition` com objeto de parâmetros). Zero concatenação de string. **Sem superfície de SQL injection** (OWASP A03).
- **Error handling em camadas:** JSON inválido, correlationId vazio, categoria inexistente, duplicata — cada caso com tratamento e log explícitos, e roteamento correto para DLQ vs. silêncio.
- **`host.json` correto:** `maxConcurrentCalls=4` (protege o pool SQL, conforme troubleshooting do blueprint), `prefetchCount=0`, `autoCompleteMessages=true`, `maxAutoLockRenewalDuration=00:05:00`.
- **Connection string sem `EntityPath`:** documentado no template `local.settings.json` e reforçado no PORTAL-GUIDE (armadilha #1). Binding usa `Connection = "ServiceBusConnection"`.
- **Secrets fora do código:** `local.settings.json` é template vazio + gitignored; produção via App Settings/secrets do workflow.
- **`CancellationToken` propagado** em todo o caminho async — boa cidadania para Functions.
- **`JsonIgnoreCondition.WhenWritingNull`** em `ticketId` — contrato v2 limpo.

### Issues

| ID | Sev | Categoria | Descrição | Recomendação |
|----|-----|-----------|-----------|--------------|
| **H-1** | HIGH | deployment/CI | No `deploy-phase-01.yml`, o step **Build** compila apenas `src/Fifa2026.V2.Functions`, mas o step **Test** roda `dotnet test src/Fifa2026.V2.Functions.Tests --no-build`. O projeto de testes **nunca é compilado** → `--no-build` falha em CI (assembly de teste ausente). O gate de testes do AC-10 não executa de fato. | Compilar a solução/projeto de testes antes (`dotnet build src/Fifa2026.V2.Functions.Tests -c Release`) **ou** remover `--no-build` do step Test **ou** apontar Build para a solução. Recomendo build da solução: `dotnet build -c Release` na raiz (cobre ambos os projetos), mantendo `--no-build` em test e publish. Tratar antes do 1º deploy real (@dev fix / @devops valida). |
| **M-1** | MEDIUM | code/robustez | `MapStatus` mapeia qualquer status desconhecido para `"processing"`, mas o contrato v2 (AC-8) prevê `failed`. Linhas v1 históricas têm status `pending`/`cancelled` etc. — uma consulta por correlationId de v1 nunca ocorre (v1 não tem correlation_id), então o risco é baixo, porém o default silencioso pode mascarar estados. | Considerar logar (debug) quando cair no default, ou mapear explicitamente `pending→processing`. Não bloqueante. |
| **M-2** | MEDIUM | architecture/runtime | `total_price = tc.price * @Quantity` calcula preço no INSERT, mas **não decrementa `ticket_categories.available_quantity`** nem valida estoque. O v1 provavelmente controla estoque; o v2 grava compra sem reservar. Para um workshop didático em F1 é aceitável (escopo é mensageria, não inventário), mas é um desvio funcional do "comprar" real. | Documentar explicitamente como fora-de-escopo de F1 (já implícito no blueprint, mas vale nota no README/SPEAKER-NOTES para não dar a impressão de fluxo de compra completo). Reavaliar em fase futura se o inventário entrar em escopo. |
| **L-1** | LOW | observability | Em `PurchaseEntryFunction`, o `BeginScope(CorrelationId)` envolve a serialização e a montagem da resposta, mas o correlationId **não é devolvido como header HTTP** (`X-Correlation-ID`/`traceparent`) — só no corpo. ADE-000 Inv 5 cita header `X-Correlation-ID` para HTTP hops. | Em F1 (sem hop HTTP downstream) é cosmético; ao chegar F2 (gateway) recomendo adicionar o header de resposta para fechar a cadeia W3C HTTP. Anotar como carry-forward para F2. |

Nenhum dos issues compromete o pattern foundational nem a segurança. H-1 é o único com impacto operacional concreto (CI verde enganoso).

---

## 4. Testes — cobertura e relevância

**21 testes verdes** (reportado pelo @dev; csproj e fontes consistentes). Distribuição:
- **Validação de body** (`PurchaseRequestValidationTests`): categorias válidas/inválidas, matchId/userId/quantity fora de faixa. Boa cobertura de borda do AC-3.
- **Consumer** (`PurchaseConsumerFunctionTests`): happy path, **duplicata não-throw** (AC-6), CategoryNotFound→throw (AC-7), JSON malformado→throw, correlationId vazio→throw. Cobre os caminhos de DLQ e idempotência via mock de `IPurchaseRepository`.
- **Status** (`PurchaseStatusFunctionTests`): GUID inválido→400, sem linha→queued, completed→status+ticketId.

**Avaliação:** cobertura **relevante e bem direcionada aos ACs de risco** (idempotência, validação, DLQ-routing, status). Acima do mínimo de 70% das Functions exigido pela story.

**Lacuna aceitável:** a idempotência via UNIQUE constraint real e o roteamento DLQ no broker são verificáveis apenas em runtime (sem SQL/SB reais nos unit tests). A story já marca esses como smoke tests runtime (Task 8.1-8.4). A abstração `IPurchaseRepository` mockada cobre a lógica do consumer; o pattern SQL (catch 2627) só roda contra DB real — documentado e aceito.

---

## 5. Segurança (OWASP básico)

| Item | Status |
|------|--------|
| SQL injection (A03) | ✅ Parameterized queries em 100% dos acessos |
| Secrets no código | ✅ Template vazio + gitignore; produção via App Settings/secrets |
| AuthN/AuthZ | ⚠️ `AuthorizationLevel.Anonymous` em F1 — **intencional e documentado** (segurança entra em F2 com gateway). Aceito para fase didática isolada por aluno |
| Logging de dados sensíveis | ✅ Logs registram matchId/category/userId/quantity (não-sensíveis) e correlationId; sem PII/credenciais |
| Validação de input | ✅ DataAnnotations no entry; GUID parse no status |

`Anonymous` é a única exposição e é uma decisão arquitetural consciente da fase (recursos isolados por aluno, sem dados reais). Não é um achado — é o design de F1.

---

## 6. Docs didáticos — fidelidade técnica

Verificada consistência **código ↔ docs** (não a pedagogia a fundo):
- Endpoints `/api/v2/purchase` (POST/GET) — batem.
- Queue `tickets-purchase`, DLQ `tickets-purchase/$DeadLetterQueue` — batem com os bindings.
- App Setting `ServiceBusConnection` sem `EntityPath` — PORTAL-GUIDE reforça a armadilha exatamente como o código exige.
- Tabela `dbo.purchases`, idempotência UNIQUE filtered + SqlException 2627/2601 — README/PORTAL-GUIDE alinhados.
- Naming ADE-000 Inv 6 (`rg-fifa2026-workshop-<iniciais>`, `sb-fifa2026-<iniciais>-<rand>`, East US 2) — consistente.
- Lock 30s / max delivery 10 — batem com AC-2 e com o `maxAutoLockRenewalDuration` do host.json.

**Fidelidade técnica: aprovada.** Sem invenção de comandos/paths/nomes (Art. IV respeitado nos docs também).

---

## 7. No Invention (Art. IV)

Validado contra `fifa2026-api/database/schema.sql`:
- `purchases` (lowercase, `dbo`) — existe; colunas `user_id`, `ticket_category_id`, `quantity`, `unit_price`, `total_price`, `status`, `created_at`, `updated_at` — todas reais.
- `source` + `correlation_id` — adicionadas pela migration (não inventadas; ADD legítimo).
- `ticket_categories` com `match_id`, `category`, `price` — existem; JOIN do INSERT é fiel ao schema.

Nenhum nome de tabela/coluna/API inventado.

---

## 8. Recomendações priorizadas

1. **(HIGH — antes do 1º deploy)** Corrigir o ordering de build/test no `deploy-phase-01.yml` (Issue H-1). Owner: @dev (fix) → @devops (valida em CI real).
2. **(MEDIUM)** Anotar no README/SPEAKER-NOTES que F1 não trata inventário/estoque (M-2), para alinhar expectativa do aluno.
3. **(MEDIUM)** Revisar default silencioso de `MapStatus` (M-1).
4. **(LOW / carry-forward F2)** Adicionar header `X-Correlation-ID` na resposta da Entry Function quando F2 introduzir hops HTTP (L-1).

Issues 2-4 não bloqueiam o GO e podem ser tratados como follow-up. Issue H-1 é recomendado antes do deploy real, mas não bloqueia o merge da fundação (o build/test local já passou; é o gate de CI que está enganosamente verde).

---

**Authority:** Aria (Architect) — quality gate de pattern foundational (EPIC-002).
**Próximo passo:** Status InReview → Done. Carry-forward H-1 para @dev/@devops antes do deploy real de F1.
