# SPEAKER NOTES — F3: Identidade Moderna (App Registration + MSAL.js + JWT no Gateway)

> **Notas do facilitador** · 7 blocos · 6h (360min) · Workshop "Living Lab Azure-Native"
> **Use junto com:** [`slides.md`](./slides.md) (Bloco 1), [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Bloco 2), código em `src/Fifa2026.V2.Gateway/Program.cs` e `Lovable/.../src/lib/authV2.ts` + `apiV2.ts` (Blocos 3-4).
> **Story:** [2.3](../../stories/2.3.story.md) · **Decisão:** [ADE-005](../../architecture/ade-005-identity-easy-auth.md) + [ADE-004](../../architecture/ade-004-gateway-yarp.md) Inv 4

---

## Visão geral do dia (cole no flip chart)

| # | Bloco | Tempo | Modo | Marco do aluno |
|---|---|---|---|---|
| 1 | Conceitos: OIDC vs SAML, Auth Code + PKCE, claims (`oid`/`iss`/`aud`), App Registration vs External ID | 50min | Expositivo + Q&A | Sabe o que é identidade federada e como difere do v1 |
| 2 | Provisioning App Registration SPA via Portal (redirect URI, scope, social, App Roles) | 45min | Demo guiada (PORTAL-GUIDE) | App Registration no ar; `VITE_ENTRA_*` preenchidas |
| 3 | MSAL.js no frontend: `PublicClientApplication`, `loginPopup`, `acquireTokenSilent`, botão "Login v2" | 60min | Live coding | Login OIDC funciona no browser; token anexado às chamadas |
| ☕ | Coffee break | 15min | — | — |
| 4 | `AddJwtBearer` no gateway YARP: `RequireAuthorization()`, validar iss/aud/assinatura, propagar `X-Entra-OID` | 60min | Live coding | Gateway valida o JWT; vira a chave da F2 |
| 5 | Lab de cenários de rejeição: expirado → 401, issuer errado → 401, `aud` errado → 401 | 40min | Lab investigativo | Entende por que cada 401 acontece |
| 6 | CI/CD + smoke test ponta-a-ponta (MSAL → gateway → Function → SQL, `entra_oid` gravado) | 45min | Hands-on | Fluxo completo validado; `entra_oid` na tabela |
| 7 | Retro + Q&A: comparação v1 (bcrypt+JWT HS256) vs v2 (OIDC RS256) + carry-over para F4 | 45min | Conversa | Decide local vs federado; pronto para F4 |

**Mindset do facilitador:** a turma vem da F2 com o gateway pronto e o **JWT como placeholder destrancado**. O ouro didático da F3 é **virar a chave**: ligar a validação de verdade e mostrar que **identidade federada é menos código de segurança, não mais**. O segundo fio condutor é a comparação **local (v1) vs federado (v2)**, que coexistem no projeto — abra com ela (Bloco 1) e feche com ela (Bloco 7).

**A frase âncora do dia:** "O gateway é o guardião único da identidade." Repita. (Em F2 era "o gateway faz por dentro o que você escreve em C#"; em F3 ele ganha a função de portaria de identidade.)

**Pré-checagem (antes de começar):** confirme em voz alta "todo mundo tem o gateway F2 no ar e a Function F1 funcionando?" e "todo mundo consegue abrir o Entra ID no Portal e ver o Tenant ID?". Sem isso, os Blocos 2 e 4 travam.

---

## BLOCO 1 — Conceitos (50min · slides + Q&A)

**Objetivo:** ao fim, o aluno sabe a diferença entre **autenticação local e identidade federada**, entende **OIDC vs OAuth2 vs SAML**, o **Authorization Code Flow + PKCE**, e os claims `oid`/`iss`/`aud`.

### Pontos a enfatizar
- **Local (v1) vs federado (v2):** no v1 você é dono do hotel inteiro (senhas, tokens, MFA). No v2 você terceiriza a recepção e o cofre ao Entra e só **confere o crachá**. Use essa analogia o dia todo.
- **OAuth2 = "o que pode fazer"; OIDC = "quem é".** OIDC é uma camada fina sobre OAuth2. "Login com Google" é OIDC rodando.
- **PKCE é o coração técnico.** Um SPA não tem onde esconder um secret → PKCE gera uma prova descartável a cada login. Mesmo que interceptem o authorization code, sem o code verifier ninguém troca por tokens.
- **Claims:** `oid` (chave estável de identidade — vira `X-Entra-OID`), `iss` (issuer — o tenant), `aud` (audience — a App Registration). O gateway valida todos.
- **App Registration vs External ID:** usamos o **tenant workforce** (já existe) para reduzir atrito. External ID é o equivalente B2C/CIAM real. **Conceitos idênticos**, só muda a topologia de tenant.

### Perguntas pra turma (as quatro perguntas-chave da fase)

**1. "Quando usar tenant workforce vs External ID?"**
> Resposta-guia: **workforce** = funcionários/usuários internos que já têm conta no diretório da organização; **External ID** (CIAM) = clientes externos em massa (B2C), com branding, user flows e cadastro self-service. No workshop usamos workforce para evitar criar um tenant CIAM separado e configurar user flows — mas os **conceitos de OIDC são idênticos**. Em produção B2C real, a escolha seria External ID. ([ADE-005 Consequências](../../architecture/ade-005-identity-easy-auth.md).)

**2. "OIDC vs SAML?"**
> Resposta-guia: ambos fazem federação/SSO. **SAML** é mais antigo, baseado em **XML** (assertions assinadas), forte em SSO corporativo legado. **OIDC** é moderno, baseado em **JSON/JWT**, sobre OAuth2, pensado para APIs/SPAs/mobile. Para aplicações novas — e especialmente SPAs como a nossa — **OIDC é o padrão**. SAML aparece quando você integra com um IdP corporativo antigo.

**3. "Por que PKCE sem secret?"**
> Resposta-guia: porque o SPA roda **no browser** — tudo que ele guarda, o usuário (e atacantes) também veem. Não há onde esconder um client secret. PKCE substitui o secret fixo por um par **code_verifier/code_challenge** gerado **a cada login**: o desafio (hash) vai no início, o verifier (original) só na troca do code por tokens. Interceptar o code não basta — falta o verifier, que nunca saiu do browser legítimo.

**4. "Quem valida o JWT — APIM ou YARP?"**
> Resposta-guia: **o gateway YARP** ([ADE-004 Inv 4](../../architecture/ade-004-gateway-yarp.md) / [ADE-005 Inv 4](../../architecture/ade-005-identity-easy-auth.md)). Não há APIM no nosso stack (foi substituído por YARP em código na F2). A validação é o `AddJwtBearer` do `Program.cs` — código C# que vocês leem. **Um lugar só** valida identidade. A Function **não** revalida; confia no `X-Entra-OID` propagado pelo gateway (que removeu qualquer header forjado antes).

### Armadilhas (a evitar como instrutor)
- ⚠️ Não diga "o v2 é mais seguro porque tem mais código". É o contrário: o v2 é mais seguro porque **delega** — menos código de auth, menos responsabilidade (senha, MFA, refresh).
- ⚠️ Não confunda **ID token** (identidade, OIDC) com **access token** (autorização, OAuth2). A chamada à API usa o **access token**; o `aud` dele aponta para a sua API.
- ⚠️ Não prometa External ID — fomos honestos: usamos workforce. Faça a nota didática.

### Se sobrar tempo (+10min)
- Desenhe o Authorization Code Flow + PKCE no quadro, passo a passo, e pergunte "onde está o secret?" (não há — é o ponto).
- Mostre um JWT decodificado (jwt.ms ou jwt.io) e localize `oid`/`iss`/`aud` ao vivo.

### Se faltar tempo (-10min)
- Corte SAML para uma frase ("padrão XML mais antigo; OIDC é o sucessor JSON").
- Foque PKCE + os 3 claims; é o que sustenta os Blocos 4-5.

### Transição → Bloco 2
"Conceito na cabeça. Agora criamos a identidade de verdade — uma App Registration no seu tenant. Abram o `PORTAL-GUIDE.md`."

---

## BLOCO 2 — Provisioning App Registration via Portal (45min · demo guiada)

**Objetivo:** turma sai com **App Registration SPA** (`student-<iniciais>-v2`), redirect URIs (localhost + prod), scope `purchase.write`, **social login** (Google ou GitHub), **App Registration admin** com App Roles, e as variáveis **`VITE_ENTRA_*`** preenchidas no `.env` local.

> Conduza pelo [`PORTAL-GUIDE.md`](./PORTAL-GUIDE.md) (Steps 0-7; Step 8 Easy Auth é opcional). Projete a tela; aguarde a turma em cada checkpoint.

### Pontos a enfatizar
- **Tenant ID ≠ Subscription ID.** O login OIDC usa o **Tenant ID** (Entra ID → Overview).
- **Platform = SPA** (não Web). É o que habilita Authorization Code + PKCE sem secret.
- **Single tenant.** "Accounts in this organizational directory only" — combina com o gateway fail-closed (sem `common`).
- **Redirect URI exato** (Step 2): `http://localhost:5173` (dev) e a URL prod. Sem barra final extra. **Armadilha nº1 (AADSTS50011).**
- **Expose an API → scope `purchase.write`** (Step 3): é isso que coloca o `aud` da sua API no token. Anote o scope completo `api://<client-id>/purchase.write` → vira `VITE_ENTRA_SCOPE`.
- **Social login** (Step 4): demonstre **um** provedor. O redirect URI do **provedor** (aponta ao Entra) é diferente do redirect URI do **SPA** (aponta ao front).
- **App Roles** (Step 5): App Registration **admin separada** com `Admin`/`Operator`/`Viewer`.
- **`VITE_ENTRA_*`** (Step 6): explique que o `.env.example` não está no repo (regra de proteção de `.env`); as variáveis estão **tipadas** em `src/vite-env.d.ts`. Cada aluno cria o seu `.env` local.

### Perguntas pra turma
- "Por que SPA e não Web na plataforma?" (SPA → PKCE sem secret; Web pressupõe um secret de servidor).
- "Qual a diferença entre o `aud` e o `iss` do token?" (aud = para qual app; iss = qual tenant emitiu).
- "Por que duas App Registrations (v2 e admin)?" (separar a identidade do usuário final da camada de papéis admin — ADE-005 Inv 1).

### Armadilhas (acompanhar a turma)
- ⚠️ **Redirect URI com barra final** → AADSTS50011. Confira caractere a caractere.
- ⚠️ **Platform Web em vez de SPA** → MSAL.js reclama / fluxo errado.
- ⚠️ **Esquecer "Add" no Application ID URI** antes de criar o scope (Step 3).
- ⚠️ **`VITE_*` sem reiniciar o `npm run dev`** → botão fica "não configurado". O Vite lê `.env` no boot.

### Se sobrar tempo (+15min)
- Decodifique no jwt.ms um token real recém-emitido e localize `oid`/`iss`/`aud`/`scp`.
- Mostre a atribuição de um usuário a um App Role em Enterprise applications.

### Se faltar tempo (-10min)
- Faça a App Registration SPA + scope ao vivo; deixe a admin (App Roles) e o social como "façam depois com o guia".

### Transição → Bloco 3
"Identidade criada no Portal. Agora vamos **conectar o front** a ela — o botão 'Login v2' com MSAL.js. Abram o `authV2.ts`."

---

## BLOCO 3 — MSAL.js no frontend (60min · live coding)

**Objetivo:** o aluno demonstra login OIDC no browser via "Login v2" e vê o access token sendo anexado às chamadas v2.

> Código: `Lovable/World Cup Tickets Hub/src/lib/authV2.ts` (config + `getV2AccessToken`), `src/lib/apiV2.ts` (Bearer nas chamadas), `src/components/LoginV2Button.tsx` (botão). O `main.tsx` faz `msalInstance.initialize()`; o `App.tsx` envolve em `MsalProvider`.

### Pontos a enfatizar
- **`PublicClientApplication`** (`authV2.ts`): "public" porque é um cliente **sem secret** (SPA). Config: `clientId`, `authority` (do tenant — **não `common`**), `redirectUri`. Cache em **`sessionStorage`** (não persiste entre abas — mais seguro p/ SPA).
- **`loginRequest.scopes`**: pede `api://<client-id>/purchase.write` (de `VITE_ENTRA_SCOPE`). É isso que faz o `aud` do token apontar para a sua API.
- **`loginPopup`** (`LoginV2Button.tsx`): faz o Authorization Code Flow **com PKCE** — sem secret. O MSAL.js gera o code_verifier/challenge por baixo; você não escreve criptografia.
- **`acquireTokenSilent`** (`getV2AccessToken`): tenta renovar o token **silenciosamente**; se a sessão exigir interação (`InteractionRequiredAuthError`), cai para `acquireTokenPopup`. É o padrão de mercado para manter o usuário logado sem reabrir popup toda hora.
- **`apiV2.ts`**: toda chamada v2 anexa `Authorization: Bearer <token>`. Sem token (não fez "Login v2") → erro local **antes** da chamada. 401 do gateway é tratado com mensagem clara (cenário AC-12).
- **Coexistência v1/v2 (decisão didática):** `authV2.ts`/`apiV2.ts`/`LoginV2Button.tsx` são **módulos paralelos**. O v1 (`src/lib/api.ts` + `AuthProvider`) está **intocado**. Os dois logins convivem na Navbar.
- **Degradação elegante:** se `VITE_ENTRA_*` faltam, `isEntraConfigured()` retorna false e o botão mostra "Login v2 (Entra) não configurado" em vez de quebrar.

### Perguntas pra turma
- "Por que `PublicClientApplication` e não `ConfidentialClientApplication`?" (SPA não tem secret seguro → cliente público + PKCE).
- "Por que `acquireTokenSilent` antes de `acquireTokenPopup`?" (UX: renova sem incomodar; só abre popup se precisar mesmo).
- "Onde o token fica guardado e por quê sessionStorage?" (sessionStorage some ao fechar a aba — menor janela de exposição que localStorage).

### Armadilhas
- ⚠️ **`authority` com `common`** → o gateway (fail-closed) rejeita. Use o tenant. (No código, o fallback é `organizations`, nunca `common`.)
- ⚠️ **Esquecer `await msalInstance.initialize()`** antes do 1º uso → MSAL v3 exige init. Já feito no `main.tsx`.
- ⚠️ **Pedir só `openid` e esperar chamar a API** → sem o scope da API, o `aud` não aponta para sua API → gateway dá 401. Peça `api://<client-id>/purchase.write`.
- ⚠️ **Misturar o cliente v1 com o v2** "pra economizar" → quebra a comparação didática. Mantenha paralelos.

### Demonstração (faça ao vivo)
```
1. npm run dev → abre http://localhost:5173
2. Clicar "Login v2" → popup do Entra → login (Microsoft ou Google/GitHub)
3. Navbar mostra "v2: <nome da conta>" (useIsAuthenticated true)
4. DevTools → Network: a chamada POST /purchase ao gateway carrega Authorization: Bearer eyJ...
5. (cole o token no jwt.ms e mostre oid/iss/aud) → "esse oid é a identidade que o gateway vai propagar"
```

### Se sobrar tempo (+10min)
- Mostre o `acquireTokenSilent` renovando após um refresh da página (conta persiste, token renova).
- Decodifique o token e relacione cada claim ao que o gateway vai checar no Bloco 4.

### Se faltar tempo (-15min)
- Use os módulos prontos; foque em explicar `loginPopup` (PKCE) e `acquireTokenSilent` + o Bearer no `apiV2.ts`.

### Transição → Coffee → Bloco 4
"O front já tem o token. Depois do café, vamos ao **guardião**: ligar o `RequireAuthorization()` no gateway e fazer o `AddJwtBearer` validar esse token de verdade."

---

## ☕ Coffee break (15min)
Avise: "voltamos para virar a chave da segurança — o `AddJwtBearer` que dormia desde a F2 vai começar a validar. E vamos quebrar de propósito com tokens ruins."

---

## BLOCO 4 — `AddJwtBearer` no gateway YARP (60min · live coding)

**Objetivo:** o aluno liga `RequireAuthorization()`, entende a validação `iss`/`aud`/assinatura/exp e vê o gateway propagar `X-Entra-OID`.

> Código: `src/Fifa2026.V2.Gateway/Program.cs` (bloco AddAuthentication/AddJwtBearer, fail-closed M-1, transform do `X-Entra-OID`, `MapReverseProxy().RequireAuthorization()`).

### Pontos a enfatizar (o coração da F3)
- **"Virar a chave":** em F2, `AddJwtBearer` existia mas as rotas eram anônimas. Em F3, `MapReverseProxy()...RequireAuthorization()` torna o token **obrigatório**. Mostre essa linha — é o desfecho do placeholder.
- **Fail-closed (carry-forward M-1 do gate S2.2):** `EntraTenantId` e `EntraClientId` são **config obrigatória**. Ausência → `InvalidOperationException` no startup (a app **não sobe**). E **`common` é proibido** — aceitaria qualquer tenant (multi-tenant) = brecha. Mostre o `throw` no código.
- **Validação EXPLÍCITA:** `TokenValidationParameters` com `ValidIssuer` (= `https://login.microsoftonline.com/<tenant>/v2.0`) e `ValidAudiences` (= `[clientId, api://clientId]`), não só inferidos do Authority. `ClockSkew = TimeSpan.Zero` (sem tolerância → expirado dá 401 na hora).
- **Assinatura RS256 via JWKS:** o `Authority` aponta para o discovery (`.well-known/openid-configuration`), de onde o handler baixa as chaves **públicas** do Entra. Você não tem a privada → não forja token. **Contraste com o v1 (HS256, mesma chave assina e valida).**
- **Transform do `X-Entra-OID`** (o momento-chave): após a validação, extrai o claim `oid` (`user.FindFirst("oid")`, com fallback para a URI `objectidentifier`) e injeta `X-Entra-OID` downstream. **Anti-spoofing:** **remove** qualquer `X-Entra-OID` que veio do cliente **antes** de injetar o valor real. Mostre o `Headers.Remove(EntraOidHeader)`.
- **Não logar o oid/token:** PII de identidade nunca em log de aplicação (CodeRabbit focus / AC-12). Aponte o comentário no código.
- **Ordem do pipeline** (revisão da F2): `UseAuthentication → UseAuthorization → MapReverseProxy`. Auth antes do proxy → sem token válido nem chega ao backend.

### Perguntas pra turma
- "Por que o gateway recusa subir sem `EntraTenantId`?" (fail-closed: melhor não subir do que subir inseguro).
- "Por que `common` é proibido?" (aceitaria tokens de **qualquer** tenant Entra — qualquer pessoa do mundo com conta Microsoft entraria).
- "Por que o gateway remove o `X-Entra-OID` do cliente antes de injetar o seu?" (anti-spoofing: o cliente não pode forjar a própria identidade).
- "A Function valida o token também?" (**Não** — confia no header do gateway; o gateway é o guardião único, e o cliente nunca alcança a Function direto).

### Armadilhas
- ⚠️ **Deixar `common`/sem tenant** → o gateway nem sobe (proposital). Configure `EntraTenantId`/`EntraClientId` antes.
- ⚠️ **`Authority` sem `/v2.0`** → o discovery/claim `oid` ficam errados. Deve ser `.../v2.0`.
- ⚠️ **`aud` = Client ID, não App ID URI sozinho** → o gateway aceita ambos (`[clientId, api://clientId]`), mas se você mexer e deixar só um, pode dar 401. Mostre o `ValidAudiences`.
- ⚠️ **Confiar no `X-Entra-OID` do cliente** (sem o `Remove`) → buraco de spoofing. O código já remove; explique por quê.
- ⚠️ **Logar o token/oid** "pra debugar" → vaza PII. Use `hasEntraIdentity` (booleano) como o código faz, nunca o valor.

### Demonstração (faça ao vivo)
```
1. Com o token do Bloco 3, POST /purchase no gateway → 202 (passou na validação)
2. Mostrar nos logs da Function: hasEntraIdentity=True (sem imprimir o oid)
3. (prepara o Bloco 6) verificar depois no SQL: purchases.entra_oid preenchido
```

### Se sobrar tempo (+10min)
- Abra o `JwtValidationTests`/`JwtRejectionTests` e mostre como geram tokens de teste com chave conhecida (TestTokenFactory) — prepara o Bloco 5.
- Mostre o discovery endpoint real do tenant no browser (`https://login.microsoftonline.com/<tenant>/v2.0/.well-known/openid-configuration`).

### Se faltar tempo (-15min)
- Use o `Program.cs` pronto; foque no `RequireAuthorization()`, no fail-closed e no transform anti-spoofing.

### Transição → Bloco 5
"O caminho feliz funciona. Agora a parte mais didática: vamos **quebrar de propósito** — token expirado, issuer errado, audience errado — e ver cada 401 acontecer."

---

## BLOCO 5 — Lab de cenários de rejeição (40min · lab investigativo)

**Objetivo:** o aluno entende **por que** cada token ruim resulta em 401, conectando o erro à validação específica que falhou (AC-12).

> Apoio: `src/Fifa2026.V2.Gateway.Tests/JwtRejectionTests.cs` (cenários automatizados), `TestTokenFactory.cs` (gera tokens controlados). Em sala, combine os testes com tentativas manuais.

### Os três cenários (cada um → 401, por motivo diferente)

| Cenário | O que falha | Mensagem típica |
|---|---|---|
| **Token expirado** | `ValidateLifetime` (com `ClockSkew=0`) | "Lifetime validation failed" |
| **Issuer errado** (outro tenant) | `ValidIssuer` | "issuer invalid" |
| **Audience errado** (outra app) | `ValidAudiences` | "audience invalid" |
| (bônus) **Sem header** | `RequireAuthorization` | 401 antes da validação |
| (bônus) **`X-Entra-OID` forjado** | anti-spoofing (removido) | passa, mas o header forjado é ignorado |

### Pontos a enfatizar
- **Cada 401 tem uma causa específica.** Não é "deu erro" — é "a checagem X reprovou". Conecte cada falha à linha de `TokenValidationParameters`.
- **`ClockSkew = TimeSpan.Zero`** é por que o expirado dá 401 imediato (sem a tolerância padrão de 5min). Decisão didática.
- **Sem `common`** é por que o issuer de outro tenant reprova. Reforce o fail-closed.
- **O forjado é ignorado, não rejeitado:** a requisição até passa (o token real é válido), mas o `X-Entra-OID` que o cliente tentou injetar foi **removido** — a identidade que chega à Function é a do **token**, não a forjada.

### Perguntas pra turma
- "Recebi 401 mas meu login funcionou ontem — o que provavelmente é?" (token expirado; relogar/renovar).
- "Como o gateway sabe que o token é de outro tenant?" (compara o `iss` com `ValidIssuer` do tenant configurado).
- "Se eu mandar `X-Entra-OID: <oid-de-outra-pessoa>`, eu viro outra pessoa?" (**não** — o gateway remove o header forjado e usa o `oid` do **seu** token).

### Demonstração (faça ao vivo)
```bash
# 1. Sem token → 401
curl -s -o /dev/null -w "%{http_code}\n" -X POST $GATEWAY/purchase \
  -H "Content-Type: application/json" -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# Esperado: 401

# 2. Token expirado / de outro tenant / aud errado → 401
#    (use o TestTokenFactory ou um token velho copiado do DevTools)
curl -s -o /dev/null -w "%{http_code}\n" -X POST $GATEWAY/purchase \
  -H "Authorization: Bearer <token-expirado>" -H "Content-Type: application/json" \
  -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# Esperado: 401
```
Depois rode `dotnet test src/Fifa2026.V2.Gateway.Tests` e mostre `WrongIssuer_Returns401` / `WrongAudience_Returns401` verdes. "O que vocês fizeram na mão, os testes garantem em todo deploy."

### Se sobrar tempo (+10min)
- Mostre o `TestTokenFactory` gerando um token com chave de teste e como o fixture sobrescreve a validação para usar essa chave pública conhecida.
- Discuta: "em produção, como você monitoraria a taxa de 401?" (App Insights / métricas de auth).

### Se faltar tempo (-10min)
- Faça só "sem token → 401" e "expirado → 401"; descreva issuer/aud verbalmente apontando o `ValidIssuer`/`ValidAudiences`.

### Transição → Bloco 6
"Sabemos validar e rejeitar. Agora vamos ver o fluxo **inteiro** rodar e o `entra_oid` aparecer no banco — e automatizar isso no CI."

---

## BLOCO 6 — CI/CD + smoke test ponta-a-ponta (45min · hands-on)

**Objetivo:** entender o `deploy-phase-03.yml`; ver o fluxo MSAL → gateway → Function → SQL com `entra_oid` gravado; validar os ACs.

> Arquivo: `.github/workflows/deploy-phase-03.yml` (2 jobs: Function + Gateway). **Push real / secrets é do @devops.** Em sala, foque em ler e entender o pipeline e fazer o smoke manual.

### Pontos a enfatizar
- **2 jobs:** deploy da **Function F1** (que passou a ler `X-Entra-OID`) + update do **Container App do gateway** (com a config JWT). Cumulativo sobre F1/F2.
- **`--no-build` só no Publish, nunca no Test** (lição do gate S2.1/H-1): o `dotnet test` faz restore+build; usar `--no-build` ali quebraria.
- **Smoke automatizado:** `POST /purchase` **sem token → espera 401**. Isso valida que a segurança está **ligada** (em F2 era 202 anônimo; agora sem token é 401). É a prova automatizada de que a chave foi virada.
- **Schema delta (`phase-03.sql`):** roda **pré-workshop** (não em aula), idempotente, adiciona `purchases.entra_oid UNIQUEIDENTIFIER NULL` + índice filtrado **não-unique** (um usuário faz várias compras → `oid` repete; UNIQUE quebraria a 2ª compra). NÃO aplicar do @dev — é do @devops/instrutor.
- **A Function lê `X-Entra-OID`** (`PurchaseEntryFunction.cs`): parseia para `Guid?`, propaga na `PurchaseMessage.EntraOid`, e o repositório grava `entra_oid`. Sem header → `null` (compras v1/anônimas continuam válidas).

### Perguntas pra turma
- "Por que o smoke test espera **401** sem token, e não 202?" (porque a segurança agora está **ligada** — é a prova de que a F3 funcionou).
- "Por que o índice de `entra_oid` é **não-unique**?" (um usuário Entra faz várias compras → o `oid` repete legitimamente entre linhas).
- "Por que `entra_oid` é **NULL** e não NOT NULL?" (compras v1 e v2 antigas/anônimas não têm `oid` — NOT NULL quebraria).

### Validação dos ACs em sala (smoke manual — AC-11)
1. **Login real** via "Login v2" no front (MSAL).
2. **POST /purchase** pelo fluxo v2 → 202.
3. **No SQL:** `SELECT TOP 5 correlation_id, entra_oid FROM purchases ORDER BY ...` → `entra_oid` **preenchido** na compra recém-feita.
4. **App Insights** (se conn string no Container App): trace de borda com o `X-Correlation-ID` (Gateway → Function), `hasEntraIdentity=True`. **Sem imprimir o oid.**

### Armadilhas
- ⚠️ Esperar deploy real sem secrets (`AZURE_CREDENTIALS`, nomes de Container App/RG) — é etapa do @devops.
- ⚠️ Aplicar `phase-03.sql` em aula no banco real — não; roda **pré-workshop**.
- ⚠️ `entra_oid` null mesmo com login — provavelmente conta sem `oid` (MSA pessoal) ou gateway sem a config; reveja Bloco 4.

### Se sobrar tempo (+10min)
- Abra a aba Actions e percorra um run dos 2 jobs.
- Mostre o trace completo no App Insights.

### Se faltar tempo (-15min)
- Leia o YAML em conjunto; faça só o smoke manual (login → SQL).

### Transição → Bloco 7
"Fluxo completo, identidade gravada. Vamos fechar comparando os dois mundos que convivem no projeto — local (v1) e federado (v2) — e plantar a F4."

---

## BLOCO 7 — Retro + comparação v1 vs v2 + carry-over F4 (45min · conversa)

**Objetivo:** consolidar a diferença **local vs federado**, revisitar o DoD, e plantar a ponte para a F4.

### Roteiro da conversa
1. **A tabela comparativa (slide):** retome v1 (bcrypt+JWT HS256, você é dono da identidade) vs v2 (OIDC Entra RS256, você delega e só valida). **Frase de fechamento:** "Identidade federada é **menos** código de segurança, não mais. Você parou de guardar senhas, emitir tokens e implementar MFA — e ficou mais seguro." Os dois fluxos **convivem** no projeto: é a comparação viva.
2. **Revisitar o DoD do aluno:**
   - App Registration SPA no tenant workforce (sem External ID)
   - Login OIDC (incl. social) funcionando no browser via MSAL.js
   - Gateway valida o JWT (iss/aud/assinatura/exp) — **guardião único**
   - `X-Entra-OID` propagado; Function grava `entra_oid` em `purchases`
   - Cenários de rejeição (expirado/issuer/aud → 401) entendidos
   - Smoke ponta-a-ponta: login → SQL `entra_oid` preenchido
3. **Reforço da chave virada:** "Em F2 a porta de identidade estava instalada e destrancada. Hoje vocês viraram a chave: `RequireAuthorization()` + `AddJwtBearer` validando de verdade."

### Perguntas pra turma (reflexão)
- "Em que cenário real você usaria autenticação local (v1) em vez de federada?" (raros: sistemas isolados sem IdP; quase sempre federar é melhor).
- "O que você deixou de implementar ao delegar ao Entra?" (storage de senha, reset, MFA, refresh, social — tudo de graça).
- "Quando migraríamos do tenant workforce para o Entra External ID?" (produto B2C real com clientes externos em massa).

### Armadilhas (de fechamento)
- ⚠️ Não deixe ninguém achar que "workforce é gambiarra". É uma escolha consciente de reduzir atrito; os conceitos são idênticos ao External ID.
- ⚠️ Reforce que a Function **confiar** no header não é insegurança — é porque o cliente nunca a alcança direto e o gateway é o guardião único.

### Carry-over para F4 (plante a curiosidade)
"Hoje o `X-Entra-OID` — a identidade do usuário — começou a viajar junto com o `X-Correlation-ID` (que nasceu na borda em F2) por todo o fluxo. Na **F4** a gente automatiza processos de negócio (n8n) sobre esse fluxo; nas **F5/F6**, o chatbot e o Flow Visualizer vão usar exatamente esse `oid` para saber **quem** iniciou cada compra. A identidade que vocês ligaram hoje é o que dá nome e rosto a tudo que vem."

---

## Apêndice — Mapa de troubleshooting (consulta rápida em sala)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **AADSTS50011** (redirect URI mismatch) | URI no Portal ≠ URI do MSAL (http/https, porta, barra final) | App Reg → Authentication → Redirect URIs: `http://localhost:5173` (dev) + prod, exatos |
| **401 "Invalid audience"** | `EntraClientId`/`opt.Audience` ≠ `clientId` da App Registration SPA | `EntraClientId` = Client ID da App Reg SPA (o `aud` do SPA é o Client ID) |
| **401 "Lifetime validation failed"** | Token expirado (1h); MSAL não renovou | `acquireTokenSilent` (forceRefresh false); expirado é didático (AC-12) |
| **401 "issuer invalid" / signature failed** | `EntraTenantId`/Authority errado; falta `/v2.0` | `Authority = https://login.microsoftonline.com/<tenant>/v2.0` (com `/v2.0`) |
| **Gateway não sobe** | `EntraTenantId`/`EntraClientId` ausente, ou `common` | Config obrigatória fail-closed; nunca `common` (M-1) |
| **CORS no fluxo MSAL** | Origin do front bloqueado | Gateway `AddCors` permite a origem; o login MSAL vai direto ao Entra (não passa pelo gateway) |
| **`X-Entra-OID`/`entra_oid` null** | Conta pessoal (MSA) sem `oid`, ou claim não extraído | Conta organizacional do tenant; gateway usa `oid` + fallback URI `objectidentifier` |
| **AADSTS65001** (consent) | Scope `purchase.write` não exposto/consentido | Expose an API → scope Enabled; MSAL pede `api://<client-id>/purchase.write` |

---

## Lembretes finais para o facilitador
- **Volte sempre à analogia do hotel/portaria** e ao "gateway é o guardião único da identidade". São os fios condutores.
- **PKCE sem secret** é o conceito técnico mais elegante da fase — dê tempo a ele (Bloco 1).
- **Local vs federado** é a comparação que a fase quer plantar — abra (Bloco 1), feche (Bloco 7), com honestidade (workforce ≠ External ID, mas conceitos idênticos).
- **Fail-closed (sem `common`)** é segurança por design, não chatice — explique o "por quê".
- O **JWT que era placeholder em F2** ganha vida hoje. Conecte explicitamente: "a chave que vocês deixaram instalada na F2 é a que viramos agora."
- Tom: prático, honesto sobre trade-offs, sem hype. A turma é técnica e respeita transparência — igual a F1/F2.
</content>
