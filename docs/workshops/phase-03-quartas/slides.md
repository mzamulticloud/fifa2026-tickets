---
title: "Quartas de Final — Identidade dois-mundos (CIAM + workforce + migração)"
subtitle: "Workshop Living Lab Azure-Native · Lab Quartas de Final"
theme: black
revealOptions:
  transition: slide
---

# Quartas de Final

## Identidade dois-mundos

Cliente no **External ID** (CIAM) · Admin no **workforce** · Migração v1→CIAM

Workshop **Living Lab Azure-Native**

`ciamlogin.com` (cliente) + `login.microsoftonline.com` (admin) → [Gateway YARP]

---

## O lab em 4 blocos · sessão estendida (~7,5–9,5h)

0. Conceitos: **desambiguação** + OIDC/PKCE + B2C canônico — 40min
1. Gateway YARP policies (revisão F2) — 60–75min
2. **Cliente CIAM** (App Reg SPA + authority + sign-up/Google) — 90–120min
3. ☕ **Pausa natural**
4. Admin workforce + App Role `Admin` — 75–90min
5. **Migração v1→CIAM** + prova de coexistência — 60–90min
6. Retro — 30min

---

## A frase do dia

# "O cliente entra pelo External ID;<br/>o funcionário pelo workforce;<br/>o gateway valida os dois —<br/>só muda a string da authority."

<small>Dois mundos de identidade, uma mecânica de validação.</small>

---

## De onde viemos (F2)

```
[Browser] → [Gateway YARP] → [Function F1]
             AddJwtBearer CONFIGURADO
             mas rotas ANÔNIMAS (placeholder)
```

- Na F2 a porta de identidade ficou **instalada e destrancada**
- Nas **Quartas** ela ganha a chave — e a chave tem **duas fechaduras**

<small>Cliente (CIAM) e admin (workforce) — dois públicos, dois produtos.</small>

---

## Bloco 0 — Conceitos

### Desambiguação · OIDC/PKCE · B2C canônico

---

## ⚠️ SLIDE DE DESAMBIGUAÇÃO (decore!)

| Produto | É | Login | Usamos? |
|---|---|---|---|
| **Entra Connect** | sync AD on-prem → nuvem | *(não é login)* | ❌ só citar |
| **Entra ID** (workforce) | crachá de funcionário (B2B) | `login.microsoftonline.com` | ✅ admin |
| **Entra External ID** | cadastro de cliente (CIAM/B2C) | `<tenant>.ciamlogin.com` | ✅ cliente |

<small>Vê `ciamlogin` → pensa **cliente**. Vê `microsoftonline` → pensa **funcionário**.</small>

---

## A analogia: o estádio

- **Cliente** compra na **bilheteria pública** → qualquer um chega, se cadastra, compra → **External ID**
- **Funcionário** entra pela **portaria de serviço** com o **crachá** da empresa → **workforce (Entra ID)**

<small>Duas portas, dois públicos, dois produtos. Misturar é erro de arquitetura.</small>

---

## O desenho canônico B2C

```
[Cliente — torcedor]              [Admin — operador]
 cadastro self-service             conta corporativa
       │                                 │
       ▼                                 ▼
[Entra External ID]              [Entra ID workforce]
 <tenant>.ciamlogin.com           login.microsoftonline.com
       └──────────────┬────────────────┘
                      ▼
        [Gateway YARP — guardião do JWT]
         valida AMBOS por discovery
```

---

## OIDC + PKCE: SPA não guarda segredo

- **SPA** = JavaScript no browser → qualquer client secret é **visível** no DevTools
- **OAuth 2.0** = autorização · **OIDC** = autenticação (emite `oid`/`iss`/`aud`)
- **Authorization Code Flow** = recebe um **código**, depois troca por token
- **PKCE** = segredo **temporário e descartável** por login → torna o flow seguro **sem** client secret

<small>O **MSAL** faz todo o PKCE por você. Você só configura authority + clientId.</small>

---

## O fluxo PKCE (Bloco 2)

```
1. SPA gera code_verifier (segredo) + code_challenge (hash)
2. redireciona → External ID (ciamlogin.com), leva o challenge
3. usuário autentica (Google / email+OTP)
4. External ID devolve um CÓDIGO (ainda não o token)
5. SPA troca o código revelando o code_verifier
6. confere: hash bate? → emite os TOKENS
7. SPA envia Authorization: Bearer <token> ao gateway
```

<small>Quem interceptar o código no meio não tem o code_verifier → não troca por token.</small>

---

## External ID vs Azure AD B2C legado

| | **Entra External ID** | **Azure AD B2C** (legado) |
|---|---|---|
| Status | ✅ atual | ⚠️ **depreciado** |
| URL | `<tenant>.ciamlogin.com` | `<tenant>.b2clogin.com` |
| Usamos? | ✅ exclusivamente | ❌ **nunca** (AC-17) |

> Vê `b2clogin.com`? **Pare e revise.** É o legado. O lab usa **só** `ciamlogin.com`.

---

## Bloco 1 — Gateway YARP policies

### Revisão da F2 · a borda já existe

```
UseCors → UseRateLimiter → XCacheMiddleware
        → UseOutputCache → UseAuthentication
        → UseAuthorization → MapReverseProxy
```

- Rate-limit 5/min → **429** · Output cache 30s (`X-Cache`) · CORS restrito
- `AddJwtBearer` **já valida** (da F3) — nas Quartas **só muda a authority**

---

## O gateway é issuer-agnóstico

```csharp
// já existe (F3) — valida workforce por discovery, fail-closed
.AddJwtBearer("Entra", o => {
    o.Authority = "https://login.microsoftonline.com/{tenantId}/v2.0";
    // ValidIssuer/ValidAudiences explícitos, ClockSkew=Zero
});
```

> Validar por **discovery** = aceitar um novo emissor é **config, não código**.

<small>Plante: "pra aceitar o cliente CIAM, a gente muda **uma string**".</small>

---

## Bloco 2 — Cliente CIAM

### App Reg SPA + authority + sign-up · o clímax do lab

Siga o **`PORTAL-GUIDE.md`** (Steps 2.1–2.4).

---

## A ÚNICA mudança de código de identidade

```
[authV2.ts]  authority: <tenant>.ciamlogin.com   ← MUDA (era microsoftonline)
[Program.cs] AddJwtBearer authority: ciamlogin    ← MUDA (era microsoftonline)

[Function]   X-Entra-OID → entra_oid              ← NÃO muda
[oid pipeline]                                     ← NÃO muda
```

<small>"Só muda a string da authority." É a prova do issuer-agnóstico.</small>

---

## Authority CIAM no MSAL (authV2.ts)

```ts
const msalConfig = {
  auth: {
    clientId: import.meta.env.VITE_CIAM_CLIENT_ID,
    authority: import.meta.env.VITE_CIAM_AUTHORITY,  // https://<tenant>.ciamlogin.com/
    knownAuthorities: ['<tenant>.ciamlogin.com'],    // authority non-AAD
    redirectUri: 'http://localhost:5173',
  },
  cache: { cacheLocation: 'sessionStorage' },
};
```

<small>`ciamlogin.com`, NÃO `microsoftonline.com`. PublicClientApplication/acquireTokenSilent não mudam.</small>

---

## O gateway valida o JWT do CIAM

```
Authority : https://<tenant>.ciamlogin.com/<tenantId>
Issuer    : https://<tenant>.ciamlogin.com/<tenantId>/v2.0
Discovery : https://<tenant>.ciamlogin.com/<tenantId>/v2.0
            /.well-known/openid-configuration
```

App Settings: `Jwt__CiamTenantId`, `Jwt__CiamClientId`

<small>Do discovery o gateway pega `jwks_uri` (chaves RS256) e `issuer`. Tudo automático.</small>

---

## O encontro Gateway × Identidade

# "O gateway é o guardião:<br/>valida o JWT que o **CIAM emitiu**<br/>antes de deixar passar."

<small>O `AddJwtBearer` → discovery CIAM é onde os dois temas das Quartas se encontram.</small>

---

## oid → X-Entra-OID → entra_oid (intacto)

```csharp
// gateway: extrai oid do token CIAM, anti-spoofing, injeta downstream
transformContext.ProxyRequest.Headers.Remove("X-Entra-OID"); // anti-spoof
var oid = user.FindFirst("oid")?.Value
       ?? user.FindFirst(".../objectidentifier")?.Value;       // fallback URI
ProxyRequest.Headers.TryAddWithoutValidation("X-Entra-OID", oid);
```

<small>A Function grava `purchases.entra_oid` igual à F3. A coluna não sabe de qual tenant veio o GUID.</small>

---

## Dois eixos de entra_oid

| Coluna | Eixo | Significa | Schema |
|---|---|---|---|
| `purchases.entra_oid` | **compra** | oid de quem fez ESTA compra v2 | já existe (NÃO-unique) |
| `users.entra_oid` | **cadastro** | oid CIAM vinculado a ESTE usuário | DDL nova (UNIQUE) |

<small>Bloco 2 grava no eixo **compra**. O eixo **cadastro** aparece no Bloco 4.</small>

---

## Demo: login CIAM ponta-a-ponta

```
SPA → "Entrar v2" → ciamlogin.com → sign-up (Google ou email+OTP)
    → access token (JWT CIAM) → Authorization: Bearer
    → gateway valida → X-Entra-OID → Function → purchases.entra_oid
```

```sql
SELECT TOP 5 id, user_id, entra_oid FROM dbo.purchases
WHERE entra_oid IS NOT NULL ORDER BY id DESC;
```

<small>No token (jwt.ms): `oid`, `iss` (...ciamlogin.com/.../v2.0), `aud` (= client ID).</small>

---

## ☕ PONTO DE PAUSA NATURAL

> Cliente CIAM **validado pelo gateway**, `entra_oid` no SQL.

- Turmas que dividem em 2 encontros **encerram aqui**
- É um clímax fechado — uma aula completa por si

<small>O que vem (admin + migração) é uma camada por cima de um cliente CIAM já funcionando.</small>

---

## Bloco 3 — Admin workforce + App Role

### O segundo mundo de identidade

---

## ⚠️ DESAMBIGUAÇÃO (de novo!)

Saindo de `ciamlogin.com` (**cliente**) → entrando em `login.microsoftonline.com` (**funcionário**).

| | Cliente | Admin |
|---|---|---|
| Tenant | CIAM (`*.ciamlogin.com`) | workforce (`*.onmicrosoft.com`) |
| Produto | External ID | Entra ID |

<small>Tenants diferentes, App Regs diferentes. Maior risco de confusão — cuidado.</small>

---

## App Registration admin (workforce)

```
Entra admin center (tenant WORKFORCE)
→ App registrations → New registration
   Name: student-<iniciais>-admin
   Account types: this directory only (single-tenant, nunca "common")
→ anote Application (client) ID + Directory (tenant) ID
```

<small>Single-tenant = fail-closed (herdado da F3). Tenant workforce ≠ tenant CIAM.</small>

---

## App Role `Admin` (uma só)

```
App roles → Create app role
   Display name : Admin
   Member types : Users/Groups
   Value        : Admin     ← aparece no claim "roles"
→ Enterprise applications → Users and groups
   → atribuir a role Admin ao usuário
```

<small>Decisão do owner: **uma** role. Criar não basta — tem que **atribuir**.</small>

---

## Gateway aceita o 2º emissor

```csharp
// segundo AddJwtBearer — mesma mecânica, authority diferente
.AddJwtBearer("Admin", o => {
    o.Authority = "https://login.microsoftonline.com/<AdminTenantId>/v2.0";
    // ValidIssuer = .../v2.0 ; ValidAudiences = <AdminClientId>
});
```

App Settings: `Jwt__AdminTenantId`, `Jwt__AdminClientId`

<small>Cliente e admin = emissores diferentes, validados igual. A prova do issuer-agnóstico.</small>

---

## Login admin separado do cliente

```
authority admin = login.microsoftonline.com/<AdminTenantId>
                  (workforce — NÃO ciamlogin.com)
```

No token (jwt.ms): `iss` = `...microsoftonline.com/.../v2.0` · `roles: ["Admin"]`

<small>Dois mundos coexistindo. O gateway valida ambos com o mesmo mecanismo.</small>

---

## Bloco 4 — Migração v1 → CIAM

### Modernização sem destruição · o ápice

---

## A migração é ADITIVA (vincula, não apaga)

```
v1 (homegrown):  users.id + users.password (bcrypt)   ← INTOCADO
v2 (CIAM):       users.entra_oid (GUID)               ← ADICIONADO

Mecanismo (Opção C):
  sign-up CIAM (MESMO email) + UPDATE users.entra_oid
```

<small>Os dois caminhos permanecem vivos. A migração demonstra **convivência**, não substituição.</small>

---

## A LIÇÃO: a senha bcrypt NÃO viaja

> O External ID **não importa** hash bcrypt.<br/>A senha v1 **não vai** para o CIAM.

- O usuário cria **credencial nova** no CIAM (Google/OTP)
- O `users.password` (bcrypt) permanece **intacto** no v1
- **Mesmo humano, duas credenciais independentes**

<small>"No mundo gerenciado, a Microsoft cuida da credencial; você só guarda o `oid`."</small>

---

## Passo-a-passo (Bloco 4)

```sql
-- 1. listar alvos
SELECT id, email, entra_oid FROM dbo.users WHERE entra_oid IS NULL;
```
```
-- 2. sign-up no CIAM com o MESMO email do v1 (Google ou OTP)
-- 3. capturar o oid (app/jwt.ms ou Portal → Users → Object ID)
```
```sql
-- 4. vincular (idempotente)
UPDATE dbo.users SET entra_oid = @oid
WHERE email = @email AND entra_oid IS NULL;
```

---

## A prova de coexistência (o clímax)

```sql
SELECT u.email,
  CASE WHEN u.password LIKE '$2%' THEN 'bcrypt-presente'
       ELSE 'sem-bcrypt' END AS credencial_v1,
  u.entra_oid AS oid_ciam_v2,
  CASE WHEN u.password IS NOT NULL AND u.entra_oid IS NOT NULL
       THEN 'COEXISTE (v1 bcrypt + v2 CIAM)' ... END AS status_migracao
FROM dbo.users u WHERE u.email = @email;
```

> Esperado: **`COEXISTE (v1 bcrypt + v2 CIAM)`**

---

## Uma linha, duas identidades

# bcrypt v1 ∥ oid CIAM v2<br/>na MESMA linha de `users`

<small>Dois paradigmas — homegrown e gerenciado — vivos lado a lado. Modernizar não exige destruir.</small>

---

## Idempotência & rollback

```sql
-- idempotente: WHERE entra_oid IS NULL + índice UNIQUE filtrado
-- rollback de um vínculo:
UPDATE dbo.users SET entra_oid = NULL WHERE email = @email;
-- rollback total (raro): DROP INDEX + DROP COLUMN (aditivo ⇒ seguro)
```

<small>NUNCA backup table no SQL. Trial CIAM é descartável.</small>

---

## Bloco 5 — Retro & DoD

### Você completou as Quartas se...

- ✅ App Reg SPA no CIAM + authority `ciamlogin.com` + login (Google/OTP)
- ✅ Gateway valida JWT CIAM → `X-Entra-OID` → `purchases.entra_oid`
- ✅ App Reg admin no workforce + App Role `Admin` + login separado
- ✅ Gateway aceita o 2º emissor (workforce)
- ✅ Migração v1→CIAM + `COEXISTE` na mesma linha de `users`
- ✅ Nenhum recurso **Azure AD B2C** provisionado

---

## Comparação v1 vs v2

| Aspecto | v1 (homegrown) | v2 (CIAM) |
|---|---|---|
| Senha | bcrypt em `users.password` | Microsoft gerencia |
| Token | JWT HS256 local | JWT RS256 External ID |
| Issuer | `fifa2026-api` | `<tenant>.ciamlogin.com/v2.0` |
| Social/MFA/reset | você gerencia | Microsoft gerencia |

<small>Não trocaram um pelo outro — fizeram **coexistir**. Modernização incremental.</small>

---

## Carry-over → F4

Hoje: a identidade nasceu na **borda** (gateway valida cliente CIAM / admin workforce).

**F4 — Orquestração:**
- o `oid` / `X-Entra-OID` anda **leste-oeste** (n8n, McpServer)
- a identidade da borda diz **"quem"** disparou cada passo do fluxo interno

<small>A identidade que vocês construíram hoje é a base da rastreabilidade da orquestração.</small>

---

## Perguntas de checagem (revisão)

- Qual a URL de **discovery** do CIAM?
- Por que o gateway é **issuer-agnóstico**?
- O que a migração faz com o **bcrypt**?
- Por que **`users.entra_oid`** e não `purchases.entra_oid` para o cadastro?

<small>`<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration` · valida por discovery · nada (intacto) · cadastro durável precisa sobreviver sem compra v2.</small>

---

# Obrigado!

## Dúvidas?

Próxima fase: **F4 — Orquestração (n8n)**

`phase-04-quartas` → próxima etapa do mundial
