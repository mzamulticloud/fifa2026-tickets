---
title: "F3 — Identidade Moderna: App Registration + MSAL.js + JWT no Gateway"
subtitle: "Workshop Living Lab Azure-Native · Fase 3 de 6"
theme: black
revealOptions:
  transition: slide
---

# F3 — Identidade Moderna

## OIDC com Entra + MSAL.js + JWT no gateway YARP

Workshop **Living Lab Azure-Native** · Fase 3 de 6

`Login v2 (PKCE)` → `Bearer token` → [Gateway valida] → `X-Entra-OID`

---

## O dia em 7 blocos · 6h

1. Conceitos: OIDC/SAML, Auth Code + PKCE, claims, App Reg vs External ID — 50min
2. Provisioning App Registration via Portal — 45min
3. MSAL.js no frontend (live coding) — 60min
4. ☕ Coffee — 15min
5. `AddJwtBearer` no gateway YARP (live coding) — 60min
6. Lab: cenários de rejeição (401) — 40min
7. CI/CD + smoke ponta-a-ponta — 45min · Retro + comparação v1/v2 — 45min

---

## A frase do dia

# "O gateway é o <br/> guardião único da identidade."

<small>Não é o APIM. Não é a Function. É o seu código C# no YARP.</small>

---

## De onde viemos (F2)

```
[Browser] → [Gateway YARP] → [Function F1]
              ├─ rate limit, cache, CORS
              └─ AddJwtBearer CONFIGURADO, rotas ANÔNIMAS
```

- O gateway tinha uma porta de segurança **instalada**
- ...mas **destrancada** (sem `RequireAuthorization()`)
- Identidade era o tema da **F3**

<small>Hoje a gente vira a chave.</small>

---

## Bloco 1 — Conceitos

### OIDC · PKCE · claims · App Registration vs External ID

---

## O problema: autenticação local (v1)

O backend v1 (Node/Express) faz tudo na mão:

- Guarda senha (**bcrypt** em `users.password_hash`)
- Emite **JWT HS256** com `JWT_SECRET` próprio
- Valida com `jwt.verify(token, JWT_SECRET)` — mesma chave

**Você é dono do hotel inteiro:** recepção, cofre, segurança, MFA, reset de senha. Dá trabalho e risco.

---

## A solução: identidade federada (v2)

Você **delega** o login ao Entra (e Google/GitHub) e só **confere o crachá**.

- Não guarda senha
- Não emite token
- Não implementa MFA/refresh/reset
- Só **valida** o token que o Entra emitiu

<small>Menos código de segurança. Mais segurança.</small>

---

## OAuth2 vs OIDC

- **OAuth2** = autorização delegada → "o que você **pode fazer**"
- **OIDC** = camada de identidade sobre OAuth2 → "**quem você é**"

> "Login com Google" é OIDC rodando por baixo.

A chamada à API usa o **access token** (OAuth2);
o `aud` dele aponta para a sua API.

---

## OIDC vs SAML

| | SAML | OIDC |
|---|---|---|
| Formato | XML | JSON / JWT |
| Época | SSO corporativo legado | Moderno (sobre OAuth2) |
| Casa bem | Web tradicional | APIs, SPAs, mobile |

<small>Aplicação nova / SPA → **OIDC**. SAML aparece em IdP corporativo antigo.</small>

---

## Authorization Code Flow + PKCE

O fluxo OIDC **recomendado para SPAs**. O MSAL.js implementa isso.

> Um SPA roda **no browser** — não tem onde esconder um secret.

Então como o app prova quem é? **PKCE.**

---

## PKCE: a prova sem secret

1. App gera **code_verifier** (secreto) + **code_challenge** (hash dele)
2. Manda o usuário ao Entra **com o challenge**
3. Usuário loga → Entra devolve um **authorization code**
4. App troca o code **+ verifier original** por tokens
5. Entra confere: hash(verifier) == challenge? → libera

> Interceptaram o code? Sem o verifier (que nunca saiu do browser), **não dá para trocar por tokens**.

---

## O fluxo desenhado

```
[SPA + MSAL.js]
  1. gera verifier+challenge
  2. loginPopup() → Entra (challenge)
[Entra workforce]
  3. login (Microsoft/Google/GitHub)
  4. authorization code → redirect URI
[SPA]
  5. troca code+verifier → access token
  6. POST /purchase + Bearer <token>
[Gateway] 7. valida JWT → oid → X-Entra-OID
[Function] 8. grava entra_oid em SQL
```

---

## Claims: o que vem no token

| Claim | O que é | Uso |
|---|---|---|
| **`oid`** | GUID estável do usuário no tenant | Chave de identidade → `X-Entra-OID` |
| **`iss`** | Issuer (quem emitiu) | Gateway valida = seu tenant |
| **`aud`** | Audience (para qual app) | Gateway valida = sua App Reg |

<small>`oid` é estável: não muda com e-mail/nome. Por isso é a chave (ADE-005 Inv 3).</small>

---

## Scopes e App Roles

- **Scope** = permissão granular no token (`purchase.write`)
  - você **expõe** na App Reg; o MSAL **solicita** no login
  - faz o `aud` apontar para a sua API
- **App Role** = papel do usuário (`Admin`/`Operator`/`Viewer`)
  - chega como claim `roles`
  - App Registration admin separada

---

## App Registration vs Entra External ID

- **Tenant workforce** (usamos): já vem com a subscription; App Registration e pronto
- **Entra External ID** (CIAM): tenant separado, B2C, user flows, branding

> **Nota honesta:** em produto B2C real, o certo é **External ID**.
> Aqui usamos workforce para **reduzir atrito** — os conceitos de OIDC são **idênticos**.

<small>(ADE-005 Consequências)</small>

---

## A grande comparação: v1 vs v2

| Aspecto | v1 (local) | v2 (federado) |
|---|---|---|
| Senha | bcrypt em `users` | não armazenada |
| Token | JWT **HS256** local | JWT **RS256** do Entra |
| Chave | **simétrica** (assina = valida) | **assimétrica** (priv. assina, pub. valida) |
| Validação | middleware Express | **`AddJwtBearer` no YARP** |
| Social / MFA / refresh | não | **sim** |
| Identidade entra | em cada serviço | **um lugar:** o gateway |

---

## HS256 vs RS256 (por que importa)

- **v1 / HS256:** quem valida conhece a **mesma chave** que assina → **pode forjar** tokens
- **v2 / RS256:** o Entra assina com chave **privada**; você valida com a **pública** (JWKS) → você **não consegue forjar**

> O gateway baixa as chaves públicas do `.well-known/openid-configuration`.

---

## Bloco 2 — App Registration via Portal

### SPA · redirect URIs · scope · social · App Roles

<small>Siga o PORTAL-GUIDE.md, Steps 0-7.</small>

---

## Os passos no Portal

1. **Tenant ID** (Entra ID → Overview)
2. **App Registration SPA** `student-<iniciais>-v2` (single tenant)
3. **Redirect URIs:** `http://localhost:5173` + prod
4. **Expose an API** → scope `purchase.write`
5. **Social login** (Google ou GitHub federado)
6. **App Reg admin** → App Roles (Admin/Operator/Viewer)
7. **`VITE_ENTRA_*`** no `.env` local + `EntraTenantId`/`EntraClientId` no gateway

---

## As variáveis VITE_ENTRA_*

```bash
VITE_ENTRA_CLIENT_ID=<client-id da App Reg SPA>
VITE_ENTRA_TENANT_ID=<tenant-id>
VITE_ENTRA_SCOPE=api://<client-id>/purchase.write
VITE_ENTRA_REDIRECT_URI=http://localhost:5173
VITE_GATEWAY_V2_URL=https://gateway-<iniciais>....
```

<small>Tipadas em `src/vite-env.d.ts`. `.env.example` não vai no repo (regra de `.env`).</small>

---

## ⚠️ Armadilha nº1 da fase

### AADSTS50011 — redirect URI mismatch

- `http://localhost:5173` **≠** `http://localhost:5173/`
- `http` ≠ `https`, porta importa

> O URI no Portal precisa bater **caractere a caractere** com o do MSAL.

---

## Bloco 3 — MSAL.js no frontend

### `PublicClientApplication` · `loginPopup` · `acquireTokenSilent`

<small>`src/lib/authV2.ts`, `apiV2.ts`, `LoginV2Button.tsx`</small>

---

## A config do MSAL (authV2.ts)

```ts
const msalConfig: Configuration = {
  auth: {
    clientId,                         // VITE_ENTRA_CLIENT_ID
    authority,                        // .../<tenant>  (NUNCA common)
    redirectUri,
  },
  cache: { cacheLocation: 'sessionStorage' },
};
export const msalInstance =
  new PublicClientApplication(msalConfig);
```

<small>"Public" = cliente sem secret (SPA). sessionStorage = some ao fechar a aba.</small>

---

## Login + token (loginPopup / silent)

```ts
// LoginV2Button.tsx — PKCE sem secret
await instance.loginPopup(loginRequest);   // scopes: [purchase.write]

// authV2.ts — renova silenciosamente
const r = await msalInstance.acquireTokenSilent({ ...loginRequest, account });
// se exigir interação → acquireTokenPopup
```

<small>O MSAL gera o code_verifier/challenge por baixo. Você não escreve criptografia.</small>

---

## A chamada com Bearer (apiV2.ts)

```ts
const token = await getV2AccessToken();
fetch(`${GATEWAY_V2_URL}/purchase`, {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}` },
  body: JSON.stringify(body),
});
// 401 → "token Entra ausente, expirado ou inválido"
```

<small>v1 (`api.ts` + `AuthProvider`) **intocado**. Os dois logins convivem.</small>

---

## Bloco 4 — `AddJwtBearer` no gateway

### Virar a chave: `RequireAuthorization()`

<small>`src/Fifa2026.V2.Gateway/Program.cs`</small>

---

## Virar a chave (a linha-desfecho)

```csharp
app.MapReverseProxy()
   .RequireRateLimiting(RateLimiterPolicy)
   .CacheOutput(OutputCachePolicy)
   .RequireAuthorization();   // ← F3 ATIVA o placeholder de F2
```

> Em F2: configurado, anônimo. Em F3: **token obrigatório.**

---

## Fail-closed (carry-forward M-1)

```csharp
if (string.IsNullOrWhiteSpace(entraTenantId) ||
    entraTenantId == "common")
    throw new InvalidOperationException(...); // a app NÃO sobe
```

- `EntraTenantId`/`EntraClientId` = **config obrigatória**
- **`common` proibido** → aceitaria tokens de **qualquer** tenant

<small>Melhor não subir do que subir inseguro.</small>

---

## Validação EXPLÍCITA do token

```csharp
TokenValidationParameters {
  ValidIssuer    = "https://login.microsoftonline.com/<tenant>/v2.0",
  ValidAudiences = [ clientId, $"api://{clientId}" ],
  ValidateLifetime = true,
  ClockSkew = TimeSpan.Zero,   // expirado → 401 na hora
}
```

<small>Assinatura RS256 via JWKS (do `.well-known/...`). iss/aud explícitos, não só inferidos.</small>

---

## Propagar o oid (anti-spoofing)

```csharp
// 1. ANTI-SPOOFING: remove o que o cliente mandou
proxyReq.Headers.Remove("X-Entra-OID");
// 2. extrai do token validado
var oid = user.FindFirst("oid")?.Value
       ?? user.FindFirst(OidClaimUri)?.Value;
// 3. injeta o valor REAL
if (oid != null) proxyReq.Headers.Add("X-Entra-OID", oid);
```

> O cliente **não consegue forjar a própria identidade**. E nunca logamos o oid (PII).

---

## A Function confia no gateway

```csharp
// PurchaseEntryFunction.cs — NÃO revalida o token
var hdr = req.Headers["X-Entra-OID"].ToString();
Guid? entraOid = Guid.TryParse(hdr, out var p) ? p : null;
// grava entra_oid em purchases
```

- Cliente nunca alcança a Function direto (URL não exposta)
- Gateway já removeu headers forjados
- **Guardião único** → revalidar seria redundante

---

## Bloco 6 — Lab: cenários de rejeição (401)

| Token | Falha | → |
|---|---|---|
| Sem header | `RequireAuthorization` | 401 |
| Expirado | `ValidateLifetime` (skew 0) | 401 |
| Issuer errado | `ValidIssuer` | 401 |
| Aud errado | `ValidAudiences` | 401 |
| `X-Entra-OID` forjado | anti-spoofing | ignorado |

<small>Cada 401 tem causa específica. Conecte ao `TokenValidationParameters`.</small>

---

## "Eu viro outra pessoa se forjar o header?"

```
Cliente manda:  X-Entra-OID: <oid-de-outra-pessoa>
Gateway:        Headers.Remove("X-Entra-OID")  ← descarta
Gateway:        injeta o oid do SEU token validado
```

> **Não.** A identidade que chega à Function é a do **seu token**, não a forjada.

---

## Bloco 7 — CI/CD + smoke ponta-a-ponta

- `deploy-phase-03.yml` → 2 jobs (Function + Gateway)
- `--no-build` só no Publish, **nunca no Test** (lição S2.1/H-1)
- **Smoke:** `POST /purchase` **sem token → espera 401**

> Em F2 era 202 anônimo. Agora sem token é **401**. Prova de que a chave foi virada.

---

## Schema delta: purchases.entra_oid

```sql
ALTER TABLE purchases ADD entra_oid UNIQUEIDENTIFIER NULL;
CREATE INDEX IX_purchases_entra_oid
  ON purchases(entra_oid) WHERE entra_oid IS NOT NULL;
```

- **NULL** (não NOT NULL): compras v1/anônimas continuam válidas
- Índice **NÃO-unique**: um usuário faz várias compras (oid repete)
- Roda **pré-workshop**, idempotente (ADE-000 Inv 2)

---

## Smoke manual (AC-11)

```
1. "Login v2" no front (MSAL)
2. POST /purchase v2 → 202
3. SELECT entra_oid FROM purchases → preenchido ✅
4. App Insights: hasEntraIdentity=True (sem imprimir o oid)
```

---

## DoD da F3

- [ ] App Registration SPA no tenant workforce (sem External ID)
- [ ] Login OIDC + social funcionando (MSAL.js)
- [ ] Gateway valida JWT (iss/aud/assinatura/exp) — guardião único
- [ ] `X-Entra-OID` propagado; `entra_oid` gravado em SQL
- [ ] Rejeição (expirado/issuer/aud → 401) entendida
- [ ] Smoke: login → SQL `entra_oid` preenchido

---

## A grande lição da fase

> **Identidade federada é MENOS código de segurança, não mais.**

Você parou de guardar senhas, emitir tokens, implementar MFA —
e ficou **mais seguro**.

E tudo é validado em **um só lugar**: o gateway que vocês construíram na F2.

---

## Carry-over para a F4

- O **`X-Entra-OID`** agora viaja junto com o **`X-Correlation-ID`**
- F4: automação de negócio (n8n) sobre o fluxo
- F5/F6: chatbot e Flow Visualizer usam o `oid` para saber **quem** comprou

> A identidade que vocês ligaram hoje dá nome e rosto a tudo que vem.

---

# Obrigado!

## Dúvidas?

Próxima: **F4 — Automação de processos de negócio**

<small>Workshop Living Lab Azure-Native · Fase 3 concluída</small>
</content>
