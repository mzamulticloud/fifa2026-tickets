# F3 — Identidade Moderna: App Registration (tenant workforce) + MSAL.js + JWT no Gateway

> **Leitura prévia obrigatória** · Workshop "Living Lab Azure-Native" (40h) · Fase 3 de 6
> **Tempo estimado de leitura:** 30-40 min · **Faça ANTES da aula.**
> **Story:** [2.3](../../stories/2.3.story.md) · **Decisão de arquitetura:** [ADE-005](../../architecture/ade-005-identity-easy-auth.md) (Invariantes 1-5) + [ADE-004](../../architecture/ade-004-gateway-yarp.md) (Inv 4 — JWT no YARP)
> **Continuidade:** parte cumulativa da [F1](../phase-01/README.md) e da [F2](../phase-02/README.md) — em F2 você deixou o JWT como **placeholder destrancado**; em F3 você **vira a chave**.

---

## 0. Por que você está lendo isto antes da aula

No fim da Fase 2, o seu gateway YARP estava pronto: roteamento, rate limit, cache, CORS, `X-Correlation-ID`. E havia uma porta de segurança **instalada, mas destrancada** — o `AddJwtBearer` estava configurado, mas as rotas eram **anônimas** (sem `RequireAuthorization()`). Foi de propósito: identidade era o tema da F3, não da F2.

Agora chegou a hora. A Fase 3 faz três coisas que se encaixam como engrenagens:

1. Você cria uma **App Registration** no seu próprio tenant Entra ID (o "workforce", o que já vem com a sua subscription Azure) — sem provisionar nenhum tenant separado.
2. Você integra o **MSAL.js** no frontend SPA: um botão "Login v2" que faz login OIDC moderno (**Authorization Code Flow + PKCE**), obtém um **access token** do Entra e o envia como `Authorization: Bearer <token>`.
3. Você **vira a chave** no gateway: liga o `RequireAuthorization()`, e o `AddJwtBearer` que dormia desde a F2 começa a **validar o token de verdade** — assinatura, issuer, audience, expiração. O gateway extrai o claim `oid` (a identidade do usuário) e o propaga downstream como `X-Entra-OID`.

> **A frase âncora da fase:** "O gateway é o guardião único da identidade." A validação do JWT acontece em **um só lugar** — o gateway YARP que você construiu na F2. Não é o APIM, não é a Function. É o seu código C#.

E há um segundo fio condutor, igualmente importante para a sua carreira: a diferença entre **autenticação local** (o que o v1 faz — senha no banco, token assinado por você) e **identidade federada** (o que o v2 faz — você delega o login ao Entra, ao Google, ao GitHub, e só **valida** o token que eles emitem). Os dois fluxos vivem **lado a lado** no projeto, de propósito, para você comparar.

Esta leitura cobre:

1. O problema da autenticação local (v1) e por que o mundo migrou para identidade federada
2. **OIDC e OAuth2**: a diferença, e por que você quase nunca implementa login do zero hoje
3. O **Authorization Code Flow + PKCE** explicado passo a passo (e por que PKCE dispensa o client secret)
4. **Claims, scopes e App Roles**: o conteúdo de um token e como o gateway o valida
5. **App Registration vs Entra External ID** (a nota didática honesta sobre o que estamos simplificando)
6. A **tabela comparativa v1 (bcrypt + JWT HS256) vs v2 (OIDC Entra RS256)** — o coração da comparação
7. O que vamos construir (arquitetura delta sobre a F2) e os **contratos exatos**
8. Glossário e checklist de pré-aula

> **Pré-requisitos de conhecimento:** você fez a F1 (fluxo `POST /api/v2/purchase` → fila → SQL) e a F2 (gateway YARP na frente da Function). Você programa em qualquer linguagem; não exigimos experiência prévia com OAuth2/OIDC nem com .NET. Se você já usou "Login com Google" em algum app, já viveu OIDC como usuário — aqui você vai vê-lo por dentro.

---

## 1. O problema: autenticação local (o que o v1 faz)

O backend v1 (`fifa2026-api/`, Node/Express) faz autenticação **local e manual**, do jeito clássico:

- A senha do usuário é guardada como **hash bcrypt** na coluna `users.password_hash`.
- No login, o servidor confere a senha e emite um **JWT assinado por ele mesmo** com uma chave secreta compartilhada (`JWT_SECRET`), usando o algoritmo **HS256** (simétrico).
- Em cada chamada protegida, um middleware faz `jwt.verify(token, JWT_SECRET)` — a **mesma chave** que assinou é a que valida.

Funciona. Mas carrega responsabilidades pesadas que crescem com o sistema:

- **Você guarda senhas.** Mesmo com bcrypt, você é o guardião de credenciais — superfície de ataque, compliance, vazamento de banco = vazamento de senhas.
- **Você é a autoridade de identidade.** Esqueceu a senha? Você implementa o fluxo. Quer MFA? Você implementa. Quer login social? Você integra cada provedor na mão.
- **A chave é simétrica (HS256).** Quem valida o token precisa conhecer o **mesmo segredo** que o assinou. Se três serviços precisam validar, os três compartilham o segredo — quem tem o segredo pode também **forjar** tokens.
- **Sem refresh, sem revogação fácil.** O token vale até expirar; controlar sessão de verdade exige mais infra.

> **A intuição:** no v1, você é **dono do hotel inteiro** — recepção, cofre de chaves, segurança, lista de hóspedes. Dá trabalho e risco. A identidade federada (v2) é como terceirizar a recepção e o cofre para uma empresa especializada (o Entra), e a sua aplicação só **confere o crachá** que ela emitiu.

---

## 2. OIDC e OAuth2 (e por que você não implementa login do zero)

### 2.1 OAuth2: autorização delegada

**OAuth2** é o padrão de **autorização delegada**: ele permite que um app obtenha um **access token** para chamar uma API **em nome do usuário**, sem que o app veja a senha do usuário. O usuário se autentica no provedor (Entra, Google...), e o app recebe um token que diz "este portador pode fazer X".

### 2.2 OIDC: a camada de identidade em cima do OAuth2

OAuth2 sozinho fala de **autorização** ("pode fazer X"), não de **identidade** ("quem é você"). O **OpenID Connect (OIDC)** é uma fina camada **sobre o OAuth2** que adiciona identidade: além do access token, o provedor emite um **ID token** com **claims** sobre o usuário (quem é, qual o `oid`, e-mail, nome). 

> **A regra prática:** OAuth2 = "o que você pode fazer". OIDC = "quem você é". Quando alguém diz "Login com Google", é **OIDC** rodando por baixo.

### 2.3 OIDC vs SAML (você vai ouvir os dois)

Antes do OIDC existir, o padrão corporativo de federação era o **SAML** — baseado em **XML**, troca de "assertions" assinadas via redirects de browser. SAML ainda é muito usado em SSO corporativo legado. O **OIDC** é o sucessor moderno: baseado em **JSON e JWT**, leve, pensado para APIs, mobile e SPAs. Para aplicações novas e especialmente para SPAs/APIs, **OIDC é a escolha padrão**; SAML aparece quando você integra com um identity provider corporativo antigo.

| Eixo | SAML | OIDC |
|---|---|---|
| Formato | XML (assertions) | JSON / JWT |
| Época | Padrão dos anos 2000, SSO corporativo | Moderno (sobre OAuth2) |
| Casa bem com | Web tradicional, SSO empresarial legado | APIs, SPAs, mobile, microsserviços |
| Token | SAML assertion (XML assinado) | ID token + access token (JWT) |

> **No nosso workshop usamos OIDC** — é o que o Entra emite para SPAs via MSAL.js, e o que o gateway valida como JWT.

---

## 3. O Authorization Code Flow + PKCE (passo a passo)

Este é o coração técnico da fase. É o fluxo OIDC/OAuth2 **recomendado para SPAs** (single-page applications, como o nosso frontend Vite/React). O MSAL.js implementa exatamente isto.

### 3.1 Por que SPA é um caso especial

Um SPA roda **inteiramente no browser do usuário**. Tudo que ele tem, o usuário (e qualquer um com DevTools) também tem. Isso significa: **um SPA não pode guardar um client secret** — não há onde escondê-lo. Os fluxos OAuth2 antigos dependiam de um client secret para o app provar sua identidade ao provedor. Como o SPA não tem segredo seguro, precisamos de outra prova. Essa prova é o **PKCE**.

### 3.2 PKCE: a prova sem segredo

**PKCE** (Proof Key for Code Exchange, pronuncia-se "pixie") resolve o problema assim:

1. Antes de iniciar o login, o app gera um valor aleatório secreto, o **code verifier**, e calcula um hash dele, o **code challenge**.
2. O app manda o usuário ao Entra para login, **junto com o code challenge** (o hash).
3. O usuário se autentica (com Microsoft, Google ou GitHub). O Entra devolve um **authorization code** (um código de uso único) ao redirect URI do app.
4. O app troca esse code por tokens — e nessa troca envia o **code verifier original** (o valor sem hash).
5. O Entra confere: o hash do verifier bate com o challenge enviado no passo 2? Se sim, libera os tokens.

> **Por que isso é seguro sem secret?** Mesmo que um atacante intercepte o authorization code (passo 3), ele **não consegue trocá-lo por tokens** — falta o code verifier, que nunca saiu do browser legítimo. O PKCE substitui o "segredo fixo do app" por um "segredo descartável gerado a cada login". É a razão pela qual um SPA público pode fazer OAuth2 com segurança.

### 3.3 O fluxo desenhado

```
[SPA Vite/React + MSAL.js]
   │ 1. gera code_verifier + code_challenge (hash)
   │ 2. loginPopup() → redireciona ao Entra com o code_challenge
   ▼
[Microsoft Entra ID — tenant workforce]
   │ 3. usuário faz login (Microsoft / Google / GitHub federado)
   │ 4. devolve authorization code ao redirect URI (localhost:5173 / prod)
   ▼
[SPA] 5. troca o code + code_verifier por tokens (access token + id token)
   │
   │ 6. POST /purchase  +  Authorization: Bearer <access_token>
   ▼
[Gateway YARP] 7. valida o JWT (assinatura/iss/aud/exp) → extrai oid → X-Entra-OID
   │
   ▼
[Function F1] 8. lê X-Entra-OID → grava entra_oid em SQL
```

No nosso código (`Lovable/.../src/lib/authV2.ts`), o MSAL.js cuida dos passos 1-5: `loginPopup(loginRequest)` faz o login com PKCE, e `acquireTokenSilent` renova o access token silenciosamente quando preciso (caindo para `acquireTokenPopup` se a sessão exigir interação). Você **não escreve a criptografia do PKCE** — o MSAL.js faz por você. Mas agora você sabe **o que ele faz por baixo**.

---

## 4. Claims, scopes e App Roles (o conteúdo do token e como o gateway o valida)

### 4.1 Claims: os dados dentro do token

Um JWT é, em essência, um JSON assinado com três partes (header.payload.signature). O payload carrega **claims** — afirmações sobre o usuário e o token. Os que importam para nós (validados contra a documentação oficial do Microsoft Identity Platform — AC-14):

| Claim | O que é | Como usamos |
|---|---|---|
| **`oid`** | Object ID: GUID **estável e único** do usuário no tenant Entra | É a **chave de identidade** do v2. O gateway extrai e propaga como `X-Entra-OID`; a Function grava em `purchases.entra_oid` ([ADE-005 Inv 3](../../architecture/ade-005-identity-easy-auth.md)). |
| **`iss`** | Issuer: quem emitiu e assinou o token | O gateway valida que é **exatamente** `https://login.microsoftonline.com/<tenant-id>/v2.0` — token de outro tenant → 401. |
| **`aud`** | Audience: para qual aplicação o token foi emitido | O gateway valida que é o **Client ID** da sua App Registration (ou `api://<client-id>`) — token destinado a outra app → 401. |

> **Por que `oid` e não e-mail ou nome?** Porque `oid` é **estável**: não muda se o usuário trocar de e-mail ou nome. É o identificador canônico de uma pessoa dentro do tenant. ([ADE-005 Inv 3](../../architecture/ade-005-identity-easy-auth.md) escolheu `oid` como chave exatamente por isso — sem necessidade de tabela de mapping.)

### 4.2 Scopes: o que o token autoriza

Um **scope** é uma permissão granular que o access token carrega — "este token pode escrever compras". Na sua App Registration você **expõe** um scope (`purchase.write`), e o MSAL.js o **solicita** no login (`scopes: ["api://<client-id>/purchase.write"]`). O `aud` do token resultante aponta para a sua API, e o gateway valida que o token foi emitido para ela.

### 4.3 App Roles: o que o usuário é

**App Roles** são papéis atribuídos ao usuário dentro de uma aplicação — `Admin`, `Operator`, `Viewer`. Eles chegam ao token como claim `roles` e permitem autorização baseada em papel (ex.: só `Admin` pode cancelar uma compra). Na F3 você cria uma App Registration admin com esses três roles ([ADE-005 Inv 1 e 5](../../architecture/ade-005-identity-easy-auth.md)); usá-los em endpoints específicos é evolução natural para fases seguintes.

### 4.4 Como o gateway valida (o que o `AddJwtBearer` faz)

Quando o token chega ao gateway (`src/Fifa2026.V2.Gateway/Program.cs`), o `AddJwtBearer` faz quatro checagens — e **qualquer falha resulta em 401**:

1. **Assinatura (RS256):** baixa as chaves públicas do Entra (do `.well-known/openid-configuration`, o JWKS) e verifica que o token foi de fato assinado pela chave privada do Entra. Você **não tem** essa chave privada — não consegue forjar token. (Contraste com o v1: lá a chave que assina é a mesma que valida.)
2. **Issuer (`iss`):** confere que é o issuer do **seu tenant** — explicitamente, via `ValidIssuer`. **Sem `common`** (que aceitaria qualquer tenant) — isso é fail-closed por design de segurança.
3. **Audience (`aud`):** confere que o token foi emitido para a **sua App Registration** — `ValidAudiences = [clientId, api://clientId]`.
4. **Expiração (`exp`):** confere que o token não expirou — com `ClockSkew = TimeSpan.Zero` (sem tolerância extra), então um token expirado dá 401 na hora (cenário didático AC-12).

> **Anti-spoofing (defense-in-depth):** antes de injetar o `X-Entra-OID` derivado do token, o gateway **remove** qualquer `X-Entra-OID` que o cliente tenha mandado. O cliente **não consegue forjar a própria identidade** — só o gateway, depois de validar o token, escreve esse header. Veja o transform real em `Program.cs`.

---

## 5. App Registration vs Entra External ID (a nota didática honesta)

Você vai ouvir dois termos da família Entra e é importante saber a diferença:

- **Tenant workforce** (o que usamos): é o tenant Entra ID que **já vem** com a sua subscription Azure. Foi pensado para **funcionários** de uma organização (workforce = força de trabalho). Você cria uma **App Registration** nele e pronto — login Microsoft + social (Google/GitHub federados) funcionam sem infraestrutura extra.
- **Entra External ID** (o equivalente "real" de B2C): é um tenant **separado**, do tipo **CIAM** (Customer Identity and Access Management), pensado para **clientes externos** (milhões de usuários consumidores). Tem branding customizado, user flows de sign-up/sign-in, atributos de cadastro — toda a cerimônia de um produto B2C real.

> **Nota didática (importante guardar):** Em produtos B2C reais, com milhões de usuários externos, o provedor correto é o **Microsoft Entra External ID** (CIAM, tenant separado) — ele **é** o equivalente do que antigamente se chamava "Azure AD B2C". Aqui no workshop usamos o **tenant workforce** para **reduzir o atrito de setup**: sem criar tenant separado, sem configurar user flows. **Os conceitos que você aprende são idênticos** — OIDC, Authorization Code + PKCE, claims, scopes, App Roles. Só a topologia de tenant muda. Quem dominar o fluxo aqui sabe migrar para o External ID em produção sem reaprender nada conceitual. ([ADE-005 Consequências](../../architecture/ade-005-identity-easy-auth.md) registra esse trade-off explicitamente.)

Por que essa decisão? O blueprint original pedia Entra External ID, mas ele introduz **atrito desproporcional** para um workshop gratuito de 6h/fase: exige tenant CIAM separado (pré-criado e gerido) + user flows. Trocar pelo tenant workforce + App Registration entrega o mesmo aprendizado de OIDC/social login com **custo US$0 e setup mínimo** ([ADE-005 Decisão e Rationale](../../architecture/ade-005-identity-easy-auth.md)).

---

## 6. A tabela comparativa: v1 (local) vs v2 (federado) — o coração da fase

Esta é a comparação que dá nome à fase. O v1 (Node/Express, **intocado**) e o v2 (Entra OIDC) coexistem no mesmo projeto, de propósito, para você ver lado a lado a diferença entre **ser dono da identidade** e **delegar a identidade**. (Adaptada de [ADE-005](../../architecture/ade-005-identity-easy-auth.md) — confira o `fifa2026-api/src/middleware/auth.js` para o v1 e o `Program.cs` do gateway para o v2.)

| Aspecto | v1 (intocado — autenticação local) | v2 (F3 — identidade federada) |
|---|---|---|
| **Storage de senha** | bcrypt (10 rounds) em `users.password_hash` — você guarda credenciais | **Não armazenado** — o provider externo (Entra/Google/GitHub) cuida disso |
| **Quem é a autoridade** | A sua API (você emite e valida) | O **Entra ID** (você só valida o que ele emite) |
| **Token** | JWT **HS256** local (Express `jsonwebtoken`) | JWT **RS256** emitido pelo Entra workforce |
| **Chave de assinatura** | **Simétrica** — `JWT_SECRET` compartilhado (quem valida pode forjar) | **Assimétrica** — Entra assina com chave **privada**; você valida com a **pública** (JWKS), não consegue forjar |
| **Issuer (`iss`)** | `fifa2026-api` local | `https://login.microsoftonline.com/<tenant-id>/v2.0` |
| **Validação** | Middleware Express manual (`jwt.verify(token, JWT_SECRET)`) | **`AddJwtBearer` no gateway YARP** (assinatura/iss/aud/exp) |
| **Login social** | Não | **Sim** — Google/GitHub federados via Entra |
| **MFA** | Não (você teria que implementar) | **Sim** — configurável no tenant, de graça |
| **Refresh token** | Não | **Sim** — OAuth2 refresh via MSAL (`acquireTokenSilent`) |
| **Onde a identidade entra no fluxo** | Em cada serviço, repetido | **Um lugar só:** o gateway YARP (guardião único) |
| **Mapping de ID** | `users.id` (int) | `purchases.entra_oid` (GUID, claim `oid`) |

> **A leitura desta tabela:** o v2 **não é "mais código de segurança"** — é **menos**. Você deixa de guardar senhas, de emitir tokens, de implementar MFA/refresh/reset. Você delega tudo isso ao Entra e fica responsável só por **validar** o token (em um lugar: o gateway) e **confiar** na identidade propagada. Menos responsabilidade, mais segurança. Esse é o ganho da identidade federada.

---

## 7. O que vamos construir (arquitetura delta sobre a F2)

### 7.1 Diagrama

```
[Browser SPA — Vite/React + MSAL.js]
   │ "Login v2" → Authorization Code Flow + PKCE → access token (Entra workforce)
   │ POST /purchase  +  Authorization: Bearer <token>
   ▼
[Container App: gateway-<iniciais>  (ASP.NET Core + YARP — da sua F2)]
   ├─ RequireAuthorization()           ── F3 LIGA a chave (era anônimo em F2)
   ├─ AddJwtBearer("Entra")            ── valida assinatura(RS256)/iss/aud/exp → 401 se falhar
   ├─ transform: extrai claim oid       ── injeta X-Entra-OID downstream (remove o forjado antes)
   └─ MapReverseProxy                   ── encaminha para a Function F1 (path rewrite da F2 mantido)
            │
            ▼
     [Function PurchaseEntryFunction (da sua F1)]
            ├─ lê header X-Entra-OID (confia no gateway — NÃO revalida o token)
            ├─ INSERT purchases com entra_oid = <oid do token>
            └─ continua o fluxo F1 (Service Bus → Consumer → SQL)
```

> **O que NÃO tocamos:** o **fluxo v1 inteiro** (`fifa2026-api/`, login bcrypt+JWT, `src/lib/api.ts` no front, `AuthProvider`) fica **intocado** — é o lado de comparação. O fluxo F1 (Service Bus → Consumer → SQL) e o gateway da F2 (rate limit, cache, CORS, `X-Correlation-ID`) continuam funcionando. A F3 **adiciona** identidade; não remove nada.

### 7.2 O gateway é o guardião único (e a Function confia nele)

Repare numa decisão deliberada ([ADE-005 Inv 4](../../architecture/ade-005-identity-easy-auth.md)): a **Function NÃO revalida o token**. Ela lê o header `X-Entra-OID` e confia nele. Por quê isso é seguro?

- O cliente **nunca chama a Function diretamente** — a URL real dela não é exposta; só o gateway a conhece ([ADE-004 Inv 1/5](../../architecture/ade-004-gateway-yarp.md)).
- O gateway **remove qualquer `X-Entra-OID` forjado** pelo cliente antes de injetar o valor real derivado do token validado (anti-spoofing).
- Portanto, todo `X-Entra-OID` que chega à Function **passou pela validação do gateway**. Validar de novo seria redundante e violaria o princípio "um guardião só".

> **A intuição:** o gateway é a portaria que confere o documento na entrada e dá um crachá oficial (`X-Entra-OID`). Lá dentro, os escritórios (Functions) confiam no crachá — não pedem o documento de novo. Mas ninguém entra sem passar pela portaria (a Function não tem porta para a rua).

### 7.3 Os contratos exatos

**POST `/purchase`** (no gateway) com Bearer válido:

```jsonc
// Request ao gateway (MSAL.js anexa o Authorization)
POST https://gateway-<iniciais>.azurecontainerapps.io/purchase
Authorization: Bearer <access_token_entra>
{ "matchId": 1, "category": "VIP", "userId": 1, "quantity": 1 }

// Response: 202 Accepted (mesmo contrato da F1/F2)
{ "correlationId": "3fa85f64-...", "status": "queued" }
// O gateway, por trás, injetou X-Entra-OID: <oid> na chamada à Function.
```

**Cenários de rejeição (todos → HTTP 401 — AC-12):**

```text
Sem Authorization header          → 401 (RequireAuthorization rejeita)
Token expirado                    → 401 ("Lifetime validation failed", ClockSkew=0)
Token de outro tenant (iss errado)→ 401 ("issuer invalid")
Token com aud errado              → 401 ("audience invalid")
X-Entra-OID forjado pelo cliente  → ignorado (removido antes da validação)
```

### 7.4 As variáveis de ambiente do frontend (`VITE_ENTRA_*`)

O frontend precisa saber qual App Registration usar. Esses valores vêm de variáveis Vite (tipadas em `src/vite-env.d.ts`) — **nunca hardcoded** ([ADE-005 Inv 5](../../architecture/ade-005-identity-easy-auth.md)). O passo-a-passo de onde obter cada valor no Portal está no [PORTAL-GUIDE](./PORTAL-GUIDE.md); aqui ficam os nomes para você reconhecê-los:

| Variável | O que é |
|---|---|
| `VITE_ENTRA_CLIENT_ID` | Application (client) ID da App Registration SPA |
| `VITE_ENTRA_TENANT_ID` | GUID do seu tenant Entra workforce |
| `VITE_ENTRA_SCOPE` | Scope exposto pela API (ex.: `api://<client-id>/purchase.write`) |
| `VITE_ENTRA_REDIRECT_URI` | Redirect URI registrada (dev: `http://localhost:5173`) |
| `VITE_GATEWAY_V2_URL` | URL pública do gateway YARP (Container App) |

> Sem `VITE_ENTRA_CLIENT_ID` + `VITE_ENTRA_TENANT_ID`, o botão "Login v2" mostra um aviso discreto "Login v2 (Entra) não configurado" (veja `LoginV2Button.tsx`) — ou seja, o front degrada com elegância em vez de quebrar.

---

## 8. Glossário rápido

| Termo | Significado curtíssimo |
|---|---|
| **OAuth2** | Padrão de autorização delegada: app obtém token para agir em nome do usuário, sem ver a senha. |
| **OIDC** | OpenID Connect: camada de **identidade** sobre o OAuth2 ("quem você é", via ID token + claims). |
| **SAML** | Padrão de federação anterior, baseado em XML; SSO corporativo legado. OIDC é o sucessor moderno. |
| **JWT** | JSON Web Token: JSON assinado (header.payload.signature) que prova identidade/autorização. |
| **HS256 / RS256** | Algoritmos de assinatura: HS256 = **simétrico** (mesma chave assina e valida); RS256 = **assimétrico** (chave privada assina, pública valida). |
| **PKCE** | Proof Key for Code Exchange: prova descartável que permite OAuth2 seguro **sem client secret** (essencial para SPA). |
| **Authorization Code Flow** | Fluxo OAuth2/OIDC recomendado: login → authorization code → troca por tokens. Com PKCE para SPAs. |
| **Claim** | Afirmação dentro de um token (ex.: `oid`, `iss`, `aud`, `roles`). |
| **`oid`** | Object ID: GUID estável e único do usuário no tenant — a chave de identidade do v2. |
| **`iss` / `aud`** | Issuer (quem emitiu) / Audience (para qual app). O gateway valida ambos. |
| **Scope** | Permissão granular que o token carrega (ex.: `purchase.write`). |
| **App Role** | Papel do usuário numa aplicação (`Admin`/`Operator`/`Viewer`), entregue como claim `roles`. |
| **App Registration** | O registro da sua aplicação no Entra ID; define redirect URIs, scopes, App Roles. |
| **Tenant workforce** | O tenant Entra que já vem com a subscription (para funcionários). Usamos ele no workshop. |
| **Entra External ID** | Tenant CIAM separado para clientes externos (B2C); o equivalente "real" que simplificamos. |
| **MSAL.js** | Microsoft Authentication Library para browser; implementa o login OIDC + PKCE no SPA. |
| **`AddJwtBearer`** | Middleware ASP.NET Core que valida o JWT no gateway (assinatura/iss/aud/exp). |
| **JWKS** | JSON Web Key Set: as chaves **públicas** do Entra (do `.well-known/...`) que validam a assinatura. |
| **X-Entra-OID** | Header com o `oid` que o gateway injeta downstream após validar o token (a Function confia nele). |
| **Easy Auth** | App Service Authentication: protege o App Service do front com login Entra, sem código. |

---

## 9. Checklist antes de entrar na aula

- [ ] Entendi a diferença entre **autenticação local (v1)** e **identidade federada (v2)** (seções 1 e 6)
- [ ] Sei a diferença entre **OAuth2** ("o que pode fazer") e **OIDC** ("quem é") (seção 2)
- [ ] Consigo explicar o **Authorization Code Flow + PKCE** e por que **PKCE dispensa o client secret** (seção 3) ← conceito-chave
- [ ] Conheço os claims **`oid`, `iss`, `aud`** e o que o gateway valida em cada um (seção 4)
- [ ] Entendi que o **gateway YARP é o guardião único do JWT** — não é o APIM, não é a Function (seções 4.4 e 7.2)
- [ ] Sei a diferença entre **tenant workforce** e **Entra External ID** e por que usamos o workforce no workshop (seção 5)
- [ ] Consigo ler a **tabela comparativa v1 vs v2** e explicar por que o v2 é "menos código de segurança, mais segurança" (seção 6)
- [ ] Lembro que o **JWT da F2 era placeholder** (anônimo) e a **F3 vira a chave** (`RequireAuthorization()`) (seção 0)
- [ ] Tenho minha Function F1 e meu gateway F2 no ar, e login em portal.azure.com com uma subscription que tem tenant Entra

Nos vemos na aula. Próximo artefato que você vai usar: [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md), no Bloco 2 (criar a App Registration).
</content>
</invoke>
