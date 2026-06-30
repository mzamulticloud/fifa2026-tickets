# Architect Quality Gate — Story 2.2 (F2: Gateway Profissional em Código com YARP)

> **Gate type:** Architecture + Code Quality + Security (quality_gate: @architect)
> **Date:** 2026-06-06
> **Reviewer:** Aria (Architect)
> **Story:** [2.2](../stories/2.2.story.md) — F2 Gateway-as-Code (YARP)
> **Decisões de arquitetura:** [ADE-004](../architecture/ade-004-gateway-yarp.md) (Inv 1-5), [ADE-005](../architecture/ade-005-identity-easy-auth.md) (Inv 4), [ADE-003](../architecture/ade-003-v2-infrastructure-baseline.md) (Inv 3), [ADE-000](../architecture/ade-000-microservice-parallel-pattern.md) (Inv 5)
> **Branch:** `phase-02-gateway`
> **Tools applied:** code-review, pattern-validation, security-validation, build+test execution

---

## Verdict: **GO (PASS) com CONCERNS**

```yaml
storyId: 2.2
verdict: PASS
adherence_ade_004: CONFIRMED (5/5 invariantes)
adherence_ade_003_inv3: CONFIRMED (sem SqlConnectionString no gateway)
adherence_ade_005_inv4: CONFIRMED (JWT placeholder, rotas anônimas)
adherence_ade_000_inv5: CONFIRMED (X-Correlation-ID GUID novo se ausente)
build: SUCCESS (0 warnings, 0 erros)
tests: 5/5 PASS (executado localmente — dotnet 10 SDK, target net8.0)
concerns: 4   # 0 HIGH · 3 MEDIUM · 1 LOW — nenhum bloqueante
blocking_issues: 0
```

**Síntese:** O padrão **gateway-as-code com YARP** está implementado com fidelidade arquitetural. As 5 invariantes da ADE-004 estão materializadas, a tabela de paridade APIM→YARP (Inv 3) está coberta 1:1, ADE-003 Inv 3 é respeitada (nenhum segredo de SQL no gateway), e o placeholder JWT (ADE-004/005 Inv 4) está corretamente configurado com rotas anônimas. A ordem do pipeline de middleware — o ponto de maior risco da fase — está correta. Build limpo e 5/5 testes passam (verificado por execução local). O bug H-1 do gate S2.1 (`--no-build` no `dotnet test`) **não se repete** — o workflow foi explicitamente corrigido. Os 4 concerns são de robustez incremental e de preparação para F3/F6, não de design — por isso **GO**.

---

## 1. Acceptance Criteria — atendidos vs runtime-only

| AC | Tipo | Status | Evidência |
|----|------|--------|-----------|
| AC-1 — Branch + workflow | código | ✅ ATENDIDO | branch `phase-02-gateway`; `.github/workflows/deploy-phase-02.yml` |
| AC-2 — Projeto YARP versionado | código | ✅ ATENDIDO | `src/Fifa2026.V2.Gateway/` + `Yarp.ReverseProxy` 2.2.0; routes/clusters em `appsettings.json` |
| AC-3 — Container App via Portal | runtime | ⏸️ RUNTIME-ONLY | Dockerfile + workflow prontos; provisão per-aluno é @devops/aluno; PORTAL-GUIDE cobre |
| AC-4 — Routing + path rewrite | código | ✅ ATENDIDO | `PathSet`/`PathPattern` `/purchase`→`/api/v2/purchase`; teste `PostPurchase_IsRewrittenTo_ApiV2Purchase`. **Rota real do backend confirmada** (`Route="v2/purchase"` + routePrefix `api`) |
| AC-5 — Rate limit 429 | código | ✅ ATENDIDO | `AddRateLimiter` fixed-window 5/min por IP; teste `SixthRequest_WithinWindow_Returns_429` PASS |
| AC-6 — Output cache + X-Cache | código | ✅ ATENDIDO | `AddOutputCache` 30s + `XCacheOutputCachePolicy`/`XCacheMiddleware`; teste HIT/MISS PASS (ver Concern M-2) |
| AC-7 — CORS restrito | código | ✅ ATENDIDO | `AddCors` origin `Gateway:FrontendOrigin` + `UseCors` (ver Concern M-3) |
| AC-8 — Transform X-Correlation-ID | código | ✅ ATENDIDO | `AddRequestTransform` GUID novo se ausente; teste `Gateway_Injects_XCorrelationId_Downstream_WhenAbsent` PASS |
| AC-9 — JWT placeholder | código | ✅ ATENDIDO | `AddJwtBearer("Entra")` configurado, SEM `RequireAuthorization()`; comentário `// F3:` presente |
| AC-10 — Smoke test E2E | runtime | ⏸️ RUNTIME-ONLY | encaminhamento validado por teste de integração; fluxo até SQL é runtime (@devops/aluno) |
| AC-11 — Tracing E2E (App Insights) | híbrido | 🟡 PARCIAL (wired ✅ / trace live) | `AddApplicationInsightsTelemetry` + X-Correlation-ID injetado/devolvido; visualização do trace é runtime |
| AC-12 — YARP substitui policies XML | código | ✅ ATENDIDO | nenhum `apim/policies/*.xml`; gateway é código auditável |
| AC-13 — 6 artefatos didáticos | doc | ✅ ATENDIDO | README, PORTAL-GUIDE, SPEAKER-NOTES, slides, intro-video-script + branch/workflow (6º artefato) |
| AC-14 — Anti-hallucination | código | ✅ ATENDIDO | packages e APIs validados; build 0-warnings confirma resolução real (ver §5) |

**Resumo:** 10 ACs de código totalmente atendidos (1,2,4,5,6,7,8,9,12,13,14). 1 híbrido parcial com parcela de código completa (11). 2 puramente runtime (3,10), legitimamente fora do escopo @dev/@architect. Nenhum AC de código não atendido.

**Runtime-only (legítimos — para @devops/aluno):** AC-3 (Portal Container App), AC-10 (E2E até SQL), AC-11 (trace live no App Insights).

---

## 2. ADE-004 — Conformidade com as 5 invariantes

| Inv | Invariante | Status | Avaliação |
|-----|-----------|--------|-----------|
| 1 | Gateway é YARP em código, não APIM | ✅ CONFIRMADO | `AddReverseProxy().LoadFromConfig(...)`; rotas/clusters em `appsettings.json`; zero XML de policy. Auditável. |
| 2 | Hosting em Container App dedicado | ✅ CONFIRMADO | Dockerfile multi-stage (sdk:8.0→aspnet:8.0, porta 8080); workflow faz `az containerapp update`. Per-aluno. |
| 3 | Paridade APIM→YARP (tabela) | ✅ CONFIRMADO (6/6) | rate-limit→429, cache→X-Cache, CORS, set-header X-Correlation-ID, path rewrite, JWT placeholder — todos presentes (ver tabela §3) |
| 4 | JWT é ponto de integração com identidade | ✅ CONFIRMADO | `AddJwtBearer` aponta para issuer Entra (`Jwt:TenantId`); F2 anônimo, F3 ativa. Paridade de faseamento com `<choose>` APIM. |
| 5 | Sem recurso Azure compartilhado | ✅ CONFIRMADO | gateway é per-aluno (workflow usa vars per-aluno); nenhum recurso compartilhado |

### Tabela de paridade APIM→YARP (Invariante 3) — verificação 1:1

| Capacidade APIM | Equivalente no código | Local | Status |
|---|---|---|---|
| `rate-limit-by-key` | `AddRateLimiter` fixed-window 5/min por IP → 429 | `Program.cs` 63-76 | ✅ |
| `cache-lookup`/`cache-store` | `AddOutputCache` 30s + `X-Cache` HIT/MISS | `Program.cs` 82-88 + Infrastructure | ✅ |
| `cors` | `AddCors` + `UseCors` origin restrito | `Program.cs` 93-101, 135 | ✅ |
| `set-header X-Correlation-ID` | `AddRequestTransform` (GUID novo se ausente) | `Program.cs` 42-56 | ✅ |
| `rewrite-uri` / path strip | `PathSet`/`PathPattern` | `appsettings.json` 30-42 | ✅ |
| `validate-jwt` (placeholder) | `AddJwtBearer("Entra")`, rotas anônimas | `Program.cs` 109-126 | ✅ |

---

## 3. ADE-003 Inv 3, ADE-005 Inv 4, ADE-000 Inv 5

- **ADE-003 Inv 3 (segredos onde são usados):** ✅ **CONFIRMADO.** Nenhuma `SqlConnectionString` no gateway. A única dependência externa é `FunctionAppF1Url`, lida via `IConfiguration` no `FunctionDestinationConfigFilter` e nunca hardcoded — `appsettings.json` mantém só `http://localhost:7071/` para dev, e a URL real entra por env do Container App. A connection string SQL permanece nas Functions.
- **ADE-005 Inv 4 (JWT no gateway, anônimo em F2):** ✅ **CONFIRMADO.** `AddJwtBearer("Entra")` aponta para `https://login.microsoftonline.com/{tenantId}/v2.0` com `ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true`. `MapReverseProxy` NÃO tem `.RequireAuthorization()` — rotas anônimas. Marcador `// F3: aplicar .RequireAuthorization()` presente. Paridade exata com o `<choose>` desabilitado do APIM.
- **ADE-000 Inv 5 (correlation propagation):** ✅ **CONFIRMADO.** O transform gera GUID novo quando o header está ausente, propaga downstream e devolve ao cliente. (Nota: a propagação `traceparent` W3C completa fica a cargo do `Activity` do .NET; o gateway garante o componente `X-Correlation-ID` exigido pelo AC-8.)

---

## 4. Code review — pipeline, segurança, env vars

### 4.1 Ordem do pipeline (ponto crítico da fase) — ✅ CORRETA

`Program.cs` 135-146: `UseCors → UseRateLimiter → XCacheMiddleware → UseOutputCache → UseAuthentication → UseAuthorization → MapReverseProxy`. Bate com ADE-004 / Task 2.6. Rate-limit antes do proxy (barra a chamada abusiva antes de gastar o backend); cache antes de auth/proxy; CORS no início. **Armadilha nº1 da fase evitada.**

### 4.2 Rate limiter escopo correto — ✅

`RequireRateLimiting` aplicado a `MapReverseProxy`, NÃO a `/health`. O health probe do Container App não é rate-limitado — design correto (senão o probe poderia se auto-bloquear).

### 4.3 X-Correlation-ID gerado como GUID novo — ✅

`string.IsNullOrWhiteSpace(incoming) ? Guid.NewGuid().ToString() : incoming` — gera novo só quando ausente, preserva o recebido caso contrário. Remove o header anterior antes de re-adicionar (evita duplicação — exatamente o foco secundário do CodeRabbit).

### 4.4 Env vars sem hardcode — ✅

`FunctionAppF1Url`, `Gateway:FrontendOrigin`, `Jwt:TenantId`, `Jwt:Audience` todos via `IConfiguration` com defaults seguros. Workflow documenta App Settings, não valores embutidos.

### 4.5 Segurança — observações

- CORS: `AllowAnyMethod()` + `AllowAnyHeader()` com origin restrito — aceitável para o escopo (ver Concern M-3).
- JWT: validação completa configurada, mas inativa em F2 (correto). Ponto de atenção para F3 em Concern M-1.

---

## 5. Anti-hallucination (AC-14 / Art. IV) — ✅

Build local com **0 warnings / 0 erros** prova resolução real de todos os símbolos:
- `Yarp.ReverseProxy` 2.2.0, `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.8, `Microsoft.ApplicationInsights.AspNetCore` 2.22.0 (gateway); `WireMock.Net` 1.25.0, `Microsoft.AspNetCore.Mvc.Testing` 8.0.8 (testes).
- APIs in-box reais: `AddRateLimiter`/`RateLimitPartition.GetFixedWindowLimiter`, `AddOutputCache`/`IOutputCachePolicy`, transforms `PathSet`/`PathPattern`/`AddRequestTransform`, `IProxyConfigFilter`. Nenhuma API inventada.

---

## 6. Testes — execução local

`dotnet test src/Fifa2026.V2.Gateway.Tests -c Release` → **Aprovado: 5, Com falha: 0** (829 ms).

| Teste | AC | Relevância |
|---|---|---|
| `PostPurchase_IsRewrittenTo_ApiV2Purchase_OnBackend` | AC-4 | path rewrite real validado contra WireMock |
| `Gateway_Injects_XCorrelationId_Downstream_WhenAbsent` | AC-8 | GUID novo + propagação downstream + devolução ao cliente |
| `Health_Endpoint_Returns_Ok` | AC-3 | health probe do Container App |
| `SixthRequest_WithinWindow_Returns_429` | AC-5 | rate-limit determinístico |
| `SecondIdenticalGet_Returns_CacheHit_WithoutHittingBackend` | AC-6 | cache HIT/MISS + zero hit extra no backend |

Cobertura dos 3 ACs de borda críticos (429, cache, routing/transform) é adequada e os mocks (WireMock) isolam do Azure real corretamente. Atende o requisito de "60% do pipeline de middleware". **Concern M-4:** falta cobertura para CORS preflight e para o comportamento do correlation ID no caminho de cache HIT.

---

## 7. CI/CD — fix do H-1 confirmado

`deploy-phase-02.yml` step `Test` (linha 57): `dotnet test ${{ env.TEST_PATH }} -c Release --verbosity normal` — **SEM `--no-build`**. Comentário explícito documenta a lição do gate S2.1. O step `Build` cobre só o projeto do gateway; deixar o `dotnet test` fazer restore+build do projeto de testes é o comportamento correto. **Bug H-1 não se repete.** ✅
Demais pontos do workflow: `set -euo pipefail` em todos os scripts, smoke test valida `.correlationId` E o header `X-Correlation-ID`, tags de imagem por `github.sha`, vars per-aluno. Boa higiene.

---

## 8. Documentação — fidelidade técnica

README de F2 verificado contra o código real: pipeline na ordem exata, X-Cache HIT/MISS, path rewrite `/purchase`→`/api/v2/purchase`, env `FunctionAppF1Url`/`Gateway:FrontendOrigin`/`Jwt:TenantId`, JWT `AddJwtBearer("Entra")` anônimo, porta 8080, packages com versões corretas. Tabela de paridade do README espelha a do ADE-004. **Fidelidade técnica: alta.** Continuidade pedagógica F1→F2 e ponte F2→F3 presentes.

---

## 9. Issues por severidade

```yaml
issues:
  - id: M-1
    severity: medium
    category: security
    description: >
      JWT placeholder usa Jwt:TenantId default "common" e não configura
      ValidIssuers explícito — confia na validação de issuer derivada do Authority.
      Inofensivo em F2 (rotas anônimas), mas com "common" a validação de issuer é
      efetivamente multi-tenant/permissiva. Quando F3 ativar RequireAuthorization(),
      um tenant default "common" aceitaria tokens de QUALQUER tenant Entra.
    recommendation: >
      Em F3 (S2.3), fixar Jwt:TenantId para o tenant workforce do aluno (ADE-005
      Inv 1) e considerar ValidIssuer/ValidAudiences explícitos. Documentar no
      PORTAL-GUIDE de F3 que "common" é placeholder, não valor de produção.
    blocking: false
    target_phase: F3 (S2.3)

  - id: M-2
    severity: medium
    category: code
    description: >
      O transform que injeta X-Correlation-ID roda dentro do MapReverseProxy, DEPOIS
      do OutputCache. Numa resposta de cache HIT (GET repetido em < 30s) o proxy não
      é invocado, então o X-Correlation-ID devolvido é o da PRIMEIRA requisição
      (cacheado junto da resposta), não um GUID novo. Defensável (a resposta cacheada
      pertence logicamente à chamada upstream original), mas pode confundir o trace
      no Flow Visualizer de F6 (dois GETs distintos com o mesmo correlation ID).
    recommendation: >
      Decisão consciente para F6: documentar que respostas de cache compartilham o
      correlation ID da chamada que populou o cache, OU mover a geração do
      X-Correlation-ID para um middleware antes do OutputCache se cada hop de cliente
      precisar de ID próprio. Não bloqueante em F2.
    blocking: false
    target_phase: F6 (S2.6)

  - id: M-3
    severity: medium
    category: security
    description: >
      CORS usa WithOrigins(origin) + AllowAnyMethod() + AllowAnyHeader(). Origin
      restrito está correto, mas AllowAnyHeader/AnyMethod é mais permissivo que o
      necessário para os contratos reais (POST/GET + Content-Type/Authorization).
    recommendation: >
      Aceitável para workshop. Em hardening (nota didática), restringir a
      WithMethods("GET","POST") e WithHeaders("Content-Type","Authorization").
      Mencionar em SPEAKER-NOTES como "em prod, restrinja métodos/headers".
    blocking: false
    target_phase: hardening (didático)

  - id: L-1
    severity: low
    category: tests
    description: >
      Cobertura de testes não inclui CORS preflight (OPTIONS) nem o comportamento
      do X-Correlation-ID no caminho de cache HIT (relacionado a M-2). 5/5 cobrem
      os caminhos críticos, mas esses dois cenários ficam sem rede de segurança.
    recommendation: >
      Adicionar (oportunístico) um teste de OPTIONS preflight e um que assercione o
      correlation ID em GET cacheado quando M-2 for decidido. Não bloqueante.
    blocking: false
    target_phase: oportunístico
```

---

## 10. Decisão final

**GO (PASS) com 4 CONCERNS** (0 HIGH, 3 MEDIUM, 1 LOW). Nenhum issue bloqueia o merge. O padrão gateway-as-code está correto, seguro e fiel às ADE-004/003/005/000. Build limpo, 5/5 testes passam, CI corrigido (H-1 não recorre). Os concerns M-1 e M-2 são direcionados a F3 e F6 respectivamente e devem ser carregados como carry-forward para essas stories; M-3 e L-1 são hardening didático opcional.

Story transita **InReview → Done**.

---

**Authority:** Aria (Architect) — quality_gate designado para S2.2 (code-review, pattern-validation, security-validation).
**Carry-forward:** M-1 → S2.3 (F3, fixar tenant + issuer no JWT ativo); M-2 → S2.6 (F6, decisão de correlation ID em cache).
