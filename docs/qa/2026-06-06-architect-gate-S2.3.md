# Architect Quality Gate — Story 2.3 (F3: Identidade Moderna)

> **Gate:** @architect (Aria) — security-focused quality gate
> **Story:** 2.3 — F3 Identidade (App Registration workforce + MSAL.js + JWT no gateway YARP)
> **Date:** 2026-06-06
> **Branch:** `phase-03-identity`
> **Tools:** code-review · security-validation · auth-flow-validation
> **Tipo de story:** a mais sensível do EPIC-002 (OIDC/JWT)

---

## Verdict

```yaml
storyId: 2.3
verdict: PASS (GO)
securityCritical: ALL_MET
gateOwner: "@architect (Aria)"
testsRun:
  gateway: "11/11 PASS"
  functions: "25/25 PASS"
  frontend_tsc: "0 errors"
issues:
  - severity: low
    category: security
    id: L-1
    status: documented (não-bloqueante)
  - severity: low
    category: docs
    id: L-2
    status: documented (não-bloqueante)
```

**GO / PASS.** Todos os controles de segurança críticos (validação completa de JWT, fail-closed M-1, anti-spoofing do `X-Entra-OID`, não-vazamento de `oid`, confiança gateway→Function justificada) estão presentes, corretos e cobertos por teste. As duas observações são *low* e não-bloqueantes.

---

## Verificação executada (evidência)

| Suite | Comando | Resultado |
|---|---|---|
| Gateway (JWT, anti-spoof, rejeição, cache, rate-limit, routing) | `dotnet test src/Fifa2026.V2.Gateway.Tests -c Release` | **11/11 PASS** |
| Functions (X-Entra-OID → entra_oid, regressão, consumer) | `dotnet test src/Fifa2026.V2.Functions.Tests -c Release` | **25/25 PASS** |
| Frontend typecheck | `npx tsc --noEmit` (Lovable/World Cup Tickets Hub) | **0 errors** |
| Regressão v1 | `git diff phase-02-gateway HEAD -- Lovable/.../src/lib/api.ts` | **sem alteração (intocado)** |

---

## 1. Acceptance Criteria — código vs runtime

| AC | Tipo | Status | Evidência |
|---|---|---|---|
| AC-1 Branch + workflow | código | ✅ | branch `phase-03-identity`; `.github/workflows/deploy-phase-03.yml` (2 jobs, sem bug H-1 — `--no-build` só no Publish) |
| AC-2 App Reg SPA | runtime/Portal | ⏭️ instrutor | documentado em PORTAL-GUIDE; fora do escopo de código |
| AC-3 Social login | runtime/Portal | ⏭️ instrutor | documentado |
| AC-4 App Reg admin + App Roles | runtime/Portal | ⏭️ instrutor | documentado |
| AC-5 MSAL.js no front | código | ✅ | `authV2.ts` (PKCE, sessionStorage, acquireTokenSilent→popup), `apiV2.ts` (Bearer), `LoginV2Button.tsx` |
| AC-6 `AddJwtBearer` ativado | código | ✅ | `Program.cs` L195-246: `RequireAuthorization()` + `AddJwtBearer` Authority `/v2.0`, iss/aud/sig/exp |
| AC-7 propaga `oid`→`X-Entra-OID` | código | ✅ | `Program.cs` L77-99 (transform); teste `ValidToken_Returns200_And_Forwards_XEntraOid` |
| AC-8 schema delta | código | ✅ | `phase-03.sql` idempotente, `UNIQUEIDENTIFIER NULL`, índice filtrado NÃO-unique |
| AC-9 Function lê `X-Entra-OID` | código | ✅ | `PurchaseEntryFunction.cs` L87-95; `PurchaseRepository.cs` INSERT param `@EntraOid` |
| AC-10 comparação v1/v2 | doc | ✅ | README phase-03 (tabela) |
| AC-11 smoke ponta-a-ponta | runtime | ⏭️/✅ | smoke automatizado no workflow (401 sem token); smoke manual = instrutor |
| AC-12 segurança (401s) | código | ✅ | `JwtRejectionTests`: NoToken/Expired/WrongIssuer/WrongAudience → 401 |
| AC-13 6 artefatos | doc | ✅ | 5 em `docs/workshops/phase-03/` + branch/workflow |
| AC-14 anti-hallucination | código | ✅ | claims `oid`/`iss`/`aud`, URI fallback, APIs MSAL/`AddJwtBearer` reais |

**Conclusão:** todos os ACs de código atendidos. ACs runtime/Portal (2,3,4,11-manual) estão fora do escopo de código por design (executados pelo instrutor/aluno) e estão devidamente documentados.

---

## 2. Validação de JWT (CRÍTICO) — APROVADO

`Program.cs` L204-217 valida explicitamente os 4 pilares:

- **Issuer** — `ValidateIssuer = true` + `ValidIssuer = https://login.microsoftonline.com/{tenant}/v2.0` (explícito, não só inferido do Authority).
- **Audience** — `ValidateAudience = true` + `ValidAudiences = [clientId, api://clientId]` (cobre os dois formatos de `aud` que o Entra emite para SPA).
- **Assinatura (JWKS)** — `ValidateIssuerSigningKey = true`; `Authority` `/v2.0` + `RequireHttpsMetadata = true` → JWKS via discovery, RS256.
- **Expiração** — `ValidateLifetime = true` + `ClockSkew = TimeSpan.Zero` (sem tolerância: token expirado → 401 imediato).

**Não há falha silenciosa de validação.** A combinação `UseAuthentication`/`UseAuthorization` + `RequireAuthorization()` no `MapReverseProxy()` rejeita com 401 *antes* do proxy. Confirmado por teste para cada vetor (401 em ausência/expirado/issuer/aud).

### Fix M-1 (carry-forward do gate S2.2) — CONFIRMADO

| Requisito M-1 | Estado | Evidência |
|---|---|---|
| Sem default `"common"` | ✅ | `Program.cs` L161-175: tenant ausente OU `== "common"` → `InvalidOperationException` no startup (fail-closed) |
| `EntraClientId` obrigatório | ✅ | L177-183: ausente → exceção |
| `ValidIssuer` explícito | ✅ | L208 |
| `ValidAudiences` explícito | ✅ | L212 |
| `ClockSkew = 0` | ✅ | L216 |

Fail-closed reforçado em **profundidade**: o workflow (`deploy-phase-03.yml` L146-149) também aborta o deploy se `ENTRA_TENANT_ID`/`ENTRA_CLIENT_ID` faltarem. `appsettings.json` traz placeholders **vazios** (não `common`). Testes `WrongIssuer_Returns401` e `WrongAudience_Returns401` provam o comportamento.

---

## 3. Não-vazamento de `oid` + anti-spoofing — APROVADO (ambos CRÍTICOS)

### `oid` não vazado em logs

`grep` por `LogInformation|LogWarning|LogError|Console.Write` em todo `src/Fifa2026.V2.Gateway` → **nenhum log de token ou `oid`**. A Function loga apenas `hasEntraIdentity={bool}` (`entraOid.HasValue`), nunca o valor (`PurchaseEntryFunction.cs` L100-102). Comentários explícitos reforçando a regra (L96-97 gateway; L89 function).

### Anti-spoofing do `X-Entra-OID` — APROVADO

`Program.cs` L77-99: o transform **sempre remove** qualquer `X-Entra-OID` de entrada (`ProxyRequest.Headers.Remove(EntraOidHeader)` — L80) **antes** de injetar o valor derivado do token validado. O cliente não consegue forjar identidade. Coberto por teste `SpoofedXEntraOidHeader_FromClient_IsStripped`: o valor forjado (`ffff...`) nunca é encaminhado; só o `oid` real do token. **Esta era a brecha CRÍTICA de maior risco da story — está fechada e testada.**

---

## 4. Function confia no gateway (não revalida) — DECISÃO CONSCIENTE, APROVADA

`PurchaseEntryFunction.cs` L19-25 documenta explicitamente: a Function confia no `X-Entra-OID` porque o gateway é o guardião único; o cliente nunca chama a Function direto (URL real não exposta — ADE-004 Inv 1/5, ADE-005 Inv 4). Coerente com o desenho. A Function trata header ausente/inválido como `null` (degrada com segurança — `entra_oid` NULL), sem elevar privilégio.

> **Nota arquitetural (não é issue):** a garantia "Function não acessível direto pelo cliente" é runtime (rede/URL não publicada), não enforced em código. Para um workshop educacional isso é aceitável e está documentado nas invariantes. Em produção endurecida, o reforço natural seria mTLS/managed identity entre gateway e Function — registrar como evolução futura (F-level), fora do escopo desta story.

---

## 5. Aderência ADE-005 (Inv 1-5) e ADE-004 Inv 4

| Invariante | Estado | Nota |
|---|---|---|
| ADE-005 Inv 1 (workforce + App Reg, não External ID) | ✅ | authority `/{tenant}` (não `common`/`organizations` em prod); config fail-closed |
| ADE-005 Inv 2 (caminho b — MSAL.js + PKCE) | ✅ | `authV2.ts` SPA + PKCE, sem client secret no browser |
| ADE-005 Inv 3 (`oid` como chave; coluna `entra_oid`) | ✅ | `phase-03.sql`; AUTO-DECISION `purchases` confirmada (ver §6) |
| ADE-005 Inv 4 (YARP valida JWT, propaga `oid`) | ✅ | transform + AddJwtBearer; Function grava `entra_oid` |
| ADE-005 Inv 5 (contrato de setup) | ✅ | PORTAL-GUIDE + `vite-env.d.ts` (VITE_ENTRA_*) |
| **ADE-004 Inv 4** (placeholder F2 ativado) | ✅ | o placeholder JWT de F2 ganhou vida; default scheme corrigido para `"Entra"` (bug latente de F2 sanado — sem ele `RequireAuthorization()` falharia o challenge) |

---

## 6. Schema — APROVADO

`phase-03.sql`:
- `entra_oid UNIQUEIDENTIFIER NULL` (✅ **NULL, não NOT NULL** — compras v1/antigas não quebram).
- `IF NOT EXISTS` em coluna e índice → **idempotente**.
- Índice **filtrado e NÃO-unique** (`WHERE entra_oid IS NOT NULL`) — correto: um `oid` faz várias compras; UNIQUE quebraria a 2ª compra. Idempotência v2 já garantida por `UQ_purchases_correlation_id` (phase-01).
- Colunas do INSERT (`PurchaseRepository.cs`) conferidas contra `schema.sql` + phase-01 — todas existem.

**AUTO-DECISION `entra_oid` em `purchases` (não `users`):** confirmo a decisão. Alinha com ADE-000 Inv 1 (paridade v1/v2 na mesma tabela) e com o gravador existente (F1 já escreve em `purchases`). Mantida — sem ajuste para `users`.

---

## 7. Regressão v1 — APROVADO

`git diff phase-02-gateway HEAD -- Lovable/.../src/lib/api.ts` → **vazio**. Mudanças no front são puramente **aditivas**: `App.tsx` (MsalProvider envolvendo, AuthProvider intacto), `main.tsx` (init MSAL com `.catch` que NÃO derruba a app — degradação graciosa), `Navbar.tsx` (botão), + módulos novos (`authV2/apiV2/LoginV2Button`). O fluxo v1 (bcrypt+JWT HS256) segue funcional sem token Entra. `LoginV2Button` renderiza aviso discreto se `VITE_ENTRA_*` ausentes (não quebra).

---

## 8. Testes — APROVADO

- **401:** NoToken / Expired (ClockSkew=0) / WrongIssuer / WrongAudience — 4 vetores cobertos (`JwtRejectionTests`).
- **200 + X-Entra-OID:** `ValidToken_Returns200_And_Forwards_XEntraOid` filtra pelo `oid` único do teste (não usa `.Last()` — robusto ao log compartilhado).
- **Anti-spoof:** `SpoofedXEntraOidHeader_FromClient_IsStripped`.
- **entra_oid gravado:** `Reads_XEntraOid_Header_Into_Message`; regressão `NoHeader_Leaves_EntraOid_Null` + `InvalidGuid_Header_Is_Ignored`.
- `TestTokenFactory` usa RSA 2048 real + `GatewayTestFixture` substitui só o JWKS (mantém iss/aud/lifetime/signing validados — mesma postura de segurança do `Program.cs`). Boa fidelidade.
- Decisão de dividir JWT em 2 classes (≤5 POST/classe) por causa do rate limiter 5/min — não enfraquece nenhum AC.

---

## Issues (todas low, não-bloqueantes)

### L-1 (low / security) — Output cache reabilitado para requisições autenticadas

`XCacheOutputCachePolicy.CacheRequestAsync` força `AllowCacheLookup/Storage = true`, sobrepondo a proteção default do ASP.NET Core OutputCache (que desabilita cache para requisições com `Authorization`). Aplica-se **apenas ao GET `/purchase/{correlationId}`** (o POST não é cacheável por método). A chave de cache é o **path** (inclui o `correlationId`), e a resposta de status **não depende do usuário** — dois usuários consultando o mesmo `correlationId` recebem o mesmo status, o que é o design pretendido (`correlationId` é a chave opaca de consulta). **Risco prático: baixo.** O `correlationId` é um GUID não-enumerável e o conteúdo (status da compra) não contém PII do `oid`.

**Recomendação (não-bloqueante):** documentar no código que o cache de status é **intencionalmente per-correlationId e user-agnostic**; se no futuro a resposta de status passar a incluir dados específicos do usuário, adicionar `SetVaryByHeader`/key por `oid` ou desabilitar cache na rota. Hoje está seguro. Aceito como está para o escopo do workshop.

### L-2 (low / docs) — Garantia "Function não acessível direto" é runtime, não em código

Ver §4. A invariante "o cliente nunca chama a Function direto" depende de a URL real não ser publicada (config de rede/deploy), não de enforcement em código (a Function é `AuthorizationLevel.Anonymous`). Aceitável e documentado para workshop.

**Recomendação (não-bloqueante):** registrar como evolução de hardening (mTLS / managed identity gateway→Function) numa futura ADE/fase. Não afeta o gate.

---

## Decisão final

**GO / PASS.** Story 2.3 atende todos os critérios de segurança críticos com cobertura de teste real e verificada nesta sessão (11/11 + 25/25 + tsc 0). Fix M-1 confirmado e reforçado em profundidade. Anti-spoofing fechado e testado. Sem vazamento de `oid`. Aderência total a ADE-005 (Inv 1-5) e ADE-004 (Inv 4). Regressão v1 preservada. As 2 issues são *low* e documentadas.

Status: **InReview → Done**.

---

**Authority:** Aria (Architect) — security/auth-flow quality gate, EPIC-002 F3.
