# PORTAL GUIDE — F3: App Registration + Social Login + App Roles (+ Easy Auth opcional)

> **Bloco 2 do roteiro (45min)** · Demo guiada: o instrutor projeta, você replica.
> **Objetivo:** sair daqui com uma **App Registration SPA** no seu tenant Entra workforce, com **redirect URIs**, um **scope exposto** (`purchase.write`), **social login** (Google ou GitHub), uma **App Registration admin** com App Roles, e as **variáveis `VITE_ENTRA_*`** do frontend preenchidas.
> **Story:** [2.3](../../stories/2.3.story.md) (AC-2, AC-3, AC-4) · **Decisão:** [ADE-005](../../architecture/ade-005-identity-easy-auth.md) (Invariantes 1, 2 e 5)

---

## Pré-requisitos

- Subscription Azure ativa (a mesma da F1/F2) — ela traz um **tenant Entra ID workforce**
- Login em **portal.azure.com**
- Suas **iniciais** definidas (ex.: `jds`) — usamos em todos os nomes
- Seu **frontend Vite/React** rodando localmente em `http://localhost:5173` (dev) e/ou o App Service de prod da F1/EPIC-001
- A **URL pública do seu gateway YARP** da F2 (`VITE_GATEWAY_V2_URL`)

> **Sem tenant External ID.** Você usa o tenant Entra que **já existe** na sua subscription ([ADE-005 Inv 1](../../architecture/ade-005-identity-easy-auth.md)). Nada de criar tenant CIAM separado nem configurar user flows.

| Recurso | Padrão de nome | Exemplo |
|---|---|---|
| App Registration (SPA, usuário final) | `student-<iniciais>-v2` | `student-jds-v2` |
| App Registration (admin, App Roles) | `student-<iniciais>-admin` | `student-jds-admin` |
| Scope exposto | `purchase.write` | `api://<client-id>/purchase.write` |

---

## Step 0 — Onde fica o Entra ID e como achar o Tenant ID (3min)

1. No Portal, na busca do topo, digite **"Microsoft Entra ID"** e abra.
2. Na página **Overview**, localize e **anote**:
   - **Tenant ID** (um GUID) → vira o seu `VITE_ENTRA_TENANT_ID` e o `EntraTenantId` do gateway.
   - O nome do tenant (ex.: `seunome.onmicrosoft.com`).

> `[PRINT 1: Overview do Entra ID com o Tenant ID destacado]`

✅ **Checkpoint:** você sabe onde fica o Entra ID e anotou o **Tenant ID**.

> ⚠️ **Armadilha:** não confunda **Tenant ID** (do diretório) com **Subscription ID** (da assinatura). São GUIDs diferentes. O que o login OIDC usa é o **Tenant ID**.

---

## Step 1 — Criar a App Registration SPA `student-<iniciais>-v2` (10min)

Esta é a aplicação que o seu **frontend** usa para login. Tipo **SPA** → habilita Authorization Code Flow + **PKCE** (sem client secret no browser).

1. Em **Microsoft Entra ID → App registrations**, clique **`+ New registration`**.
2. **Name:** `student-<iniciais>-v2`.
3. **Supported account types:** **"Accounts in this organizational directory only"** (single tenant — o seu workforce).
4. **Redirect URI:** selecione a plataforma **Single-page application (SPA)** e informe **`http://localhost:5173`** (dev).
5. Clique **`Register`**.

> `[PRINT 2: formulário New registration com Name, Single tenant e platform SPA]`

6. Na página da App Registration (**Overview**), **anote**:
   - **Application (client) ID** (GUID) → vira `VITE_ENTRA_CLIENT_ID` e o `EntraClientId` do gateway.
   - **Directory (tenant) ID** → confirma o Tenant ID do Step 0.

✅ **Checkpoint:** App Registration SPA criada; **Client ID** anotado.

> ⚠️ **Armadilha (single vs multi-tenant):** se você escolher "qualquer diretório organizacional" sem necessidade, o token pode vir de outro tenant e o gateway (fail-closed, sem `common`) o rejeitará com 401. Para o workshop, **single tenant** é o correto ([ADE-005 Inv 1](../../architecture/ade-005-identity-easy-auth.md)).

---

## Step 2 — Adicionar os Redirect URIs (dev + prod) (5min)

O Entra só devolve o login para URIs **registradas**. Cadastre dev e prod.

1. Na App Registration, menu lateral → **Authentication**.
2. Em **Platform configurations → Single-page application**, confirme **`http://localhost:5173`** (dev).
3. Clique **`Add URI`** e adicione a URL de **produção** do front (ex.: **`https://fifa2026-web.azurewebsites.net`**).
4. (Opcional) Em **Implicit grant and hybrid flows**, **deixe tudo DESmarcado** — SPA moderno usa Authorization Code + PKCE, **não** implicit flow.
5. **`Save`**.

> `[PRINT 3: Authentication com os dois Redirect URIs (localhost:5173 + prod) sob SPA]`

✅ **Checkpoint:** dois redirect URIs registrados; implicit flow desligado.

> ⚠️ **Armadilha nº1 da fase (AADSTS50011):** "redirect URI mismatch". O URI no Portal precisa bater **exatamente** com o que o MSAL.js usa — incluindo `http` vs `https`, porta e ausência de barra final. `http://localhost:5173` ≠ `http://localhost:5173/`. Confira caractere a caractere.

---

## Step 3 — Expor um scope (`purchase.write`) (7min)

O access token precisa de um `aud` que aponte para a **sua API**, para o gateway validar. Isso vem de um scope exposto.

1. Na App Registration, menu lateral → **Expose an API**.
2. No topo, ao lado de **Application ID URI**, clique **`Add`** e aceite o padrão **`api://<client-id>`** → **`Save`**.
3. Clique **`+ Add a scope`**:
   - **Scope name:** `purchase.write`
   - **Who can consent:** **Admins and users**
   - **Admin consent display name:** "Criar compras v2"
   - **Admin consent description:** "Permite criar compras pelo fluxo v2 via gateway."
   - **State:** **Enabled**
4. **`Add scope`**.

> `[PRINT 4: Expose an API com Application ID URI api://<client-id> e o scope purchase.write Enabled]`

5. **Anote o scope completo:** `api://<client-id>/purchase.write` → vira `VITE_ENTRA_SCOPE`.

✅ **Checkpoint:** Application ID URI definido; scope `purchase.write` criado e habilitado.

> ⚠️ **Armadilha (AADSTS65001 — consent):** se o login pedir um scope que ninguém consentiu, dá erro de consentimento. "Admins and users" permite que o próprio usuário consinta no 1º login. O MSAL.js solicita exatamente `api://<client-id>/purchase.write` (veja `authV2.ts`).

---

## Step 4 — Configurar social login (Google OU GitHub) (8min)

Objetivo: provar OIDC federado — o usuário entra com conta **Google** ou **GitHub**, e o Entra emite o token. Você precisa do **Client ID/Secret** do provedor (criado no console do Google/GitHub).

> Em sala, demonstramos **um** provedor (escolha Google **ou** GitHub). Os passos abaixo usam o Portal do Entra ID. A configuração exata de cada provedor externo pode evoluir no Portal; siga os rótulos atuais e os links oficiais que o próprio Portal exibe.

1. Em **Microsoft Entra ID**, menu lateral → **External Identities → All identity providers** (ou **Identity providers**).

> 💡 **Não confunda:** "External Identities" aqui é o **nome do blade** do Portal para **federar** provedores sociais (Google/GitHub) no seu tenant workforce — **não** é o produto "Entra External ID" (CIAM). Você segue sem nenhum tenant External ID, como dito no início desta página.
2. Clique **`+ Google`** (ou **`+ GitHub`**).
3. Cole o **Client ID** e o **Client secret** obtidos no console do provedor (Google Cloud Console / GitHub OAuth Apps).
4. **`Save`**.

> `[PRINT 5: All identity providers com Google (ou GitHub) adicionado]`

5. **No provedor externo**, cadastre o **redirect URI do Entra** que o próprio painel mostra (algo como `https://login.microsoftonline.com/te/<tenant>/oauth2/authresp`). Copie do Portal o valor exato.

✅ **Checkpoint:** ao fazer "Login v2" no front, a tela do Entra oferece a opção de entrar com Google/GitHub, e o fluxo conclui no browser.

> ⚠️ **Armadilha:** o redirect URI do **provedor externo** (Google/GitHub) é diferente do redirect URI do **seu SPA** (Step 2). O do provedor aponta para o Entra; o do SPA aponta para o seu front. Não troque um pelo outro.

---

## Step 5 — App Registration admin com App Roles (7min)

A camada admin usa uma App Registration **separada** com App Roles ([ADE-005 Inv 1 e 5](../../architecture/ade-005-identity-easy-auth.md)).

1. Volte em **App registrations → `+ New registration`** → **Name:** `student-<iniciais>-admin` → single tenant → **`Register`**.
2. Na nova App Registration, menu lateral → **App roles → `+ Create app role`**. Crie os **três**:

| Display name | Allowed member types | Value | Description |
|---|---|---|---|
| Admin | Users/Groups | `Admin` | Acesso administrativo total |
| Operator | Users/Groups | `Operator` | Operações de compra/gestão |
| Viewer | Users/Groups | `Viewer` | Somente leitura |

3. **`Apply`** em cada role.

> `[PRINT 6: App roles com Admin, Operator e Viewer criados]`

4. (Atribuição de usuários a roles é feita em **Enterprise applications → <a app> → Users and groups** — demonstre rapidamente; uso efetivo dos roles em endpoints é evolução para fases seguintes.)

✅ **Checkpoint:** App Registration admin com os 3 App Roles (`Admin`/`Operator`/`Viewer`).

---

## Step 6 — Preencher as variáveis `VITE_ENTRA_*` do frontend (5min)

> **Por que aqui e não num `.env.example`?** O `.env.example` do frontend **não pôde ser versionado** (regra de proteção de arquivos `.env` no repo). As variáveis estão **tipadas** em `Lovable/World Cup Tickets Hub/src/vite-env.d.ts`. Use os valores que você anotou nos Steps acima para criar o seu **`.env` local** (não versionado) na raiz do frontend.

Crie `Lovable/World Cup Tickets Hub/.env` (ou `.env.local`) com:

```bash
# F3 — Identidade v2 (MSAL.js / Entra workforce). NÃO versionar este arquivo.
# Valores obtidos no Portal (Steps 0-3 deste guia).

# Application (client) ID da App Registration SPA student-<iniciais>-v2 (Step 1).
VITE_ENTRA_CLIENT_ID=<client-id-da-app-registration-spa>

# GUID do seu tenant Entra workforce (Step 0 — Entra ID → Overview → Tenant ID).
VITE_ENTRA_TENANT_ID=<tenant-id>

# Scope exposto pela API (Step 3). Formato: api://<client-id>/purchase.write
VITE_ENTRA_SCOPE=api://<client-id>/purchase.write

# Redirect URI registrada (Step 2). Em dev: http://localhost:5173
VITE_ENTRA_REDIRECT_URI=http://localhost:5173

# URL pública do gateway YARP v2 (Container App da F2).
VITE_GATEWAY_V2_URL=https://gateway-<iniciais>.azurecontainerapps.io

# (v1, comparação didática — intocado; mantenha o valor que já usava na F1.)
VITE_API_URL=https://<sua-api-v1>
```

Tabela de referência (origem de cada valor):

| Variável | De onde vem | Usada por |
|---|---|---|
| `VITE_ENTRA_CLIENT_ID` | App Registration SPA → Overview → Application (client) ID (Step 1) | `authV2.ts` (`clientId`, `authority`) |
| `VITE_ENTRA_TENANT_ID` | Entra ID → Overview → Tenant ID (Step 0) | `authV2.ts` (`authority`) |
| `VITE_ENTRA_SCOPE` | Expose an API → scope completo (Step 3) | `authV2.ts` (`loginRequest.scopes`) |
| `VITE_ENTRA_REDIRECT_URI` | Authentication → Redirect URI SPA (Step 2) | `authV2.ts` (`redirectUri`) |
| `VITE_GATEWAY_V2_URL` | URL do Container App do gateway (F2) | `apiV2.ts` (base URL) |

✅ **Checkpoint:** `.env` local preenchido; ao rodar `npm run dev`, o botão **"Login v2"** aparece **habilitado** (não mostra mais "não configurado").

> ⚠️ **Armadilha:** variável Vite **precisa** começar com `VITE_` para chegar ao browser; e o servidor de dev **precisa ser reiniciado** após editar o `.env` (o Vite lê o `.env` no boot). Se o botão continuar "não configurado", confira `VITE_ENTRA_CLIENT_ID` + `VITE_ENTRA_TENANT_ID` (são os dois que `isEntraConfigured()` checa).

---

## Step 7 — Configurar o gateway (App Settings de identidade) (5min)

O gateway YARP da F2 precisa saber **qual tenant** e **qual audience** validar. Em **fail-closed** — sem esses valores, o gateway **não sobe** (carry-forward M-1 do gate S2.2). Configure no **Container App** do gateway:

1. No Portal → seu **Container App** do gateway (`gateway-<iniciais>`) → **Settings → Containers → Environment variables** (ou via revisão).
2. Adicione:

| App Setting | Valor | Observação |
|---|---|---|
| `EntraTenantId` | `<tenant-id>` (GUID, Step 0) | **Nunca `common`** — o gateway recusa subir com `common` (aceitaria qualquer tenant). |
| `EntraClientId` | `<client-id>` da App Registration SPA (Step 1) | É o `aud` esperado do access token. |

3. **`Save`** (cria uma nova revisão).

> `[PRINT 7: Environment variables do Container App com EntraTenantId e EntraClientId]`

✅ **Checkpoint:** gateway com `EntraTenantId` + `EntraClientId` configurados; ele sobe e passa a validar o JWT (`https://login.microsoftonline.com/<tenant>/v2.0`).

> ⚠️ **Armadilha (fail-closed):** se você esquecer `EntraTenantId` ou `EntraClientId`, o gateway lança `InvalidOperationException` no startup e **não inicia** — isso é **proposital** (segurança). Veja a mensagem de erro real no `Program.cs`. Configure os dois antes de testar.

---

## Step 8 (OPCIONAL) — Easy Auth no App Service do front (camada complementar) (8min)

> **Quando fazer:** se você quer que **ninguém acesse o front sem estar logado** no Entra, mesmo antes de tocar no botão "Login v2". É uma camada de proteção **complementar**, não obrigatória ([ADE-005 Inv 2](../../architecture/ade-005-identity-easy-auth.md)).

**Importante (Task 7.2 / [ADE-005 Inv 2](../../architecture/ade-005-identity-easy-auth.md)):** o Easy Auth **NÃO substitui** a validação de JWT no gateway. No caminho recomendado (b — MSAL.js), a **autorização das chamadas à API** continua sendo feita pelo **Bearer token validado no gateway YARP**. O Easy Auth aqui só protege o **App Service que serve o SPA** — é "tranca a porta do prédio do front", enquanto o gateway continua sendo "o guardião de cada chamada à API". São camadas diferentes.

1. No Portal → **App Service** do frontend → menu lateral → **Authentication**.
2. **`Add identity provider`** → **Microsoft** (Entra ID).
3. **App registration:** use uma App Registration (pode ser uma dedicada ao App Service) — o Easy Auth gerencia o **client secret** dela (armazenado pelo App Service, não no repo; pode referenciar Key Vault — coerente com [ADE-003 Inv 3](../../architecture/ade-003-v2-infrastructure-baseline.md)).
4. **Restrict access:** **"Require authentication"** (redireciona quem não está logado para o login do Entra).
5. **Unauthenticated requests:** **HTTP 302 redirect** para o provedor.
6. **`Add`**.

> `[PRINT 8: Authentication do App Service com provider Microsoft e Require authentication]`

7. Endpoints que o Easy Auth passa a expor (úteis de conhecer):
   - **`/.auth/login/aad`** — inicia o login Microsoft (callback em **`/.auth/login/aad/callback`**)
   - **`/.auth/me`** — retorna os claims do usuário logado (inclui `oid`)
   - **`/.auth/logout`** — encerra a sessão
   - Header server-side **`X-MS-CLIENT-PRINCIPAL`** (Base64 com claims) — disponível em chamadas server-side.

8. (Opcional) Adicione **Google/GitHub** também como identity providers no painel Authentication do App Service.

✅ **Checkpoint (se feito):** acessar o front sem login redireciona ao Entra; após login, o SPA carrega e o botão "Login v2" segue funcionando para as chamadas à API.

> ⚠️ **Armadilha (dupla proteção confusa):** com Easy Auth ligado **e** MSAL.js, o usuário pode logar duas vezes (uma para entrar no front, outra para o token da API). É aceitável e didático — mas explique à turma que são **duas camadas distintas**: Easy Auth = porta do front; gateway JWT = guardião da API. O caminho (b) mantém o gateway como ponto único de **validação da API**.

---

## Apêndice — Mapa de troubleshooting (consulta rápida em sala)

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **AADSTS50011** (redirect URI mismatch) | URI no Portal ≠ URI do MSAL (http/https, porta, barra final) | Conferir Step 2: `http://localhost:5173` (dev) e URL prod exatas, sem barra final extra |
| **401 "Invalid audience"** | `EntraClientId` do gateway ≠ `aud` do token | `EntraClientId` = Client ID da App Registration SPA (Step 1/7); o `aud` do SPA é o Client ID |
| **401 "Lifetime validation failed"** | Token expirado (1h); MSAL não renovou | Esperado/didático (AC-12). `acquireTokenSilent` renova; relogue se preciso |
| **401 "issuer invalid"** | `EntraTenantId` errado, ou app multi-tenant emitindo de outro tenant | Conferir `EntraTenantId` (Step 7) = Tenant ID (Step 0); App Reg **single tenant** (Step 1) |
| **AADSTS65001** (consent) | Scope `purchase.write` não exposto/consentido | Step 3: Expose an API → scope Enabled; "Admins and users" para consent |
| **Botão "Login v2" mostra "não configurado"** | `VITE_ENTRA_CLIENT_ID`/`VITE_ENTRA_TENANT_ID` ausentes ou Vite não reiniciado | Step 6: preencher `.env` e **reiniciar `npm run dev`** |
| **Gateway não sobe (InvalidOperationException no startup)** | `EntraTenantId`/`EntraClientId` ausente, ou tenant = `common` | Step 7: configurar ambos; nunca usar `common` (fail-closed proposital) |
| **`X-Entra-OID` chega null na Function** | Token de conta pessoal (MSA) sem `oid`, ou claim não extraído | Conta organizacional do tenant; o gateway usa `oid` e o fallback URI `objectidentifier` (`Program.cs`) |
| **CORS bloqueando o fluxo** | Origin do front ≠ `Gateway:FrontendOrigin` do gateway | Ajustar a origem no gateway (F2); o login MSAL vai direto ao Entra, não passa pelo gateway |

Próximo: na aula, abrimos o código (`authV2.ts`, `apiV2.ts`, `Program.cs`) nos Blocos 3 e 4. Veja as [`SPEAKER-NOTES.md`](./SPEAKER-NOTES.md).
</content>
