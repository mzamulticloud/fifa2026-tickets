# Guia do Aluno — Quartas de Final (F2/F3: Gateway YARP + identidade dois-mundos) do zero

> **O que você vai construir nesta aula:** a **Fase 2/3 (F2/F3)** do FIFA 2026 Tickets — um **gateway YARP** (a porta da frente) que valida **dois mundos de login** com a mesma mecânica issuer-agnóstica: o **cliente** (comprador) entra por **Entra External ID (CIAM)** e o **admin** (operador) entra por **Entra ID (workforce)**. No fim você **migra** usuários antigos (senha bcrypt) para o CIAM **sem apagar nada**, provando que as duas identidades convivem na mesma linha do banco.
>
> **Importante (leia antes de começar):**
> - **Cada aluno cria TUDO no próprio Azure / GitHub**: seus tenants, suas App Registrations, seus recursos, com **seus próprios nomes**. Os valores deste guia são **genéricos** (`<sufixo>`, `<seu-tenant>`, `<client-id>`) — preencha os seus.
> - Você terá **DOIS tenants e DOIS client IDs**: o **CIAM** (cliente, `*.ciamlogin.com`) e o **workforce** (admin, `login.microsoftonline.com`). Anote os dois separadamente — é fácil confundir.
> - **O fork NÃO é o passo zero.** Toda a infra e a identidade são criadas **à mão** primeiro; o **fork + GitHub Actions é o ÚLTIMO passo de deploy** ([Fase 6](#fase-6--deploy-via-github-actions-o-último-passo)).

> ⚠️ **O engano que quebra o lab:** `ciamlogin.com` (cliente/CIAM) **≠** `microsoftonline.com` (admin/workforce). Login no host errado → `AADSTS50011`. E **nunca** use `b2clogin.com` (Azure AD B2C é legado) — este lab é **exclusivamente Entra External ID** (`ciamlogin.com`).

> **Referências:** Story [2.11](../stories/2.11.story.md) · [ADE-007 (identidade CIAM)](../architecture/ade-007-identity-external-id.md) · [ADE-004 (gateway issuer-agnóstico)](../architecture/ade-004-yarp-gateway.md) · Workflow [`lab-quartas-de-final.yml`](../../.github/workflows/lab-quartas-de-final.yml)

---

## Como as peças se encaixam

| | **Cliente (comprador)** | **Admin (operador)** |
|---|---|---|
| Produto | **Entra External ID** (CIAM) | **Entra ID** (workforce) |
| Host de login | `<seu-tenant>.ciamlogin.com` | `login.microsoftonline.com` |
| Como entra | cadastro self-service (Google / email+OTP) | conta corporativa que já existe |

Há **duas divisões de trabalho** bem distintas:

| O quê | Como é feito | Onde |
|---|---|---|
| **IDENTIDADE** (2 tenants, 2 App Registrations, App Role) | **À mão, no Entra admin center** | Entra (Fases 1–3) |
| **INFRA do gateway** (ACR, Environment, Container App, App Settings) | **À mão, no Portal do Azure** | Portal (Fase 4) |
| **CÓDIGO + MIGRATIONS + FRONTEND** | **GitHub Actions** (workflow único `Lab Quartas de Final`) | Seu fork (Fases 5–6) |

```
─── Tudo à mão (Entra / Portal) ────────────────────────────────
  Fase 1  Tenant CIAM (External ID) + Google IdP + user flow
  Fase 2  App Reg do CLIENTE  (SPA no CIAM)      ─┐ daqui saem os
  Fase 3  App Reg do ADMIN    (SPA no workforce) ─┘ 4 GUIDs Jwt__*
  Fase 4  Infra do gateway: ACR + Environment + Container App + App Settings
─── Só agora entra o fork (último passo de deploy) ─────────────
  Fase 5  GitHub: Variables + Secrets no SEU fork
  Fase 6  Fork (todas as branches) + PR lab-quartas→main + Actions: migrations → gateway → frontend
─── Validação no browser / SQL ─────────────────────────────────
  Fase 7  Login cliente (CIAM)   Fase 8  Login admin (workforce)
  Fase 9  Migração users v1 → CIAM (SQL — o clímax)
```

A regra de ouro: **Entra/Portal criam a infra e a identidade vazias; o Actions só publica código e schema.** Ele **não cria recurso Azure nenhum**.

---

## Convenção de nomes (preencha a SUA)

Este lab **reusa os recursos das Oitavas (F1)** e cria os **novos** das Quartas. Anote os **seus** valores aqui — todas as fases referenciam estes placeholders.

> 💡 **Sufixo:** escolha um sufixo curto e único (ex.: suas iniciais + número) e use-o nos recursos novos. **ACR** só aceita **letras e números** (sem hífen).

| Recurso | Convenção sugerida | Seu valor |
|---|---|---|
| Subscription | `<sua-subscription>` | ____________ |
| Subscription ID | (GUID) | ____________ |
| Região | `<sua-regiao>` | ____________ |
| Resource Group | `<seu-rg>` (reuse o das Oitavas) | ____________ |
| SQL Server / DB | `<seu-sql-server>` / `FIFA2026Tickets` | ____________ |
| Function App F1 | `<seu-func>` → `https://<seu-func>.azurewebsites.net` | ____________ |
| Frontend Web App | `<seu-frontend>` → `https://<seu-frontend>.azurewebsites.net` | ____________ |
| Backend v1 Web App | `<seu-backend>` (intocado — comparação didática) | ____________ |
| Container Registry (ACR) | `cr<sufixo>` → `cr<sufixo>.azurecr.io` (só letras/números) | ____________ |
| Container Apps Environment | `cae-<sufixo>` | ____________ |
| Container App (gateway) | `ca-gateway-<sufixo>` | ____________ |
| FQDN do gateway | `<gateway-fqdn>` (gerado pelo Azure) | ____________ |
| Tenant CIAM (nome / domínio) | `<seu-tenant>` / `<seu-tenant>.onmicrosoft.com` | ____________ |
| Host de login do cliente | `<seu-tenant>.ciamlogin.com` | ____________ |
| Tenant ID CIAM = `Jwt__CiamTenantId` | `<CiamTenantId>` | ____________ |
| App Reg SPA cliente = `Jwt__CiamClientId` | `student-<iniciais>-v2` | ____________ |
| App Reg admin = `Jwt__AdminClientId` | `student-<iniciais>-admin` | ____________ |
| Tenant ID workforce = `Jwt__AdminTenantId` | `<AdminTenantId>` | ____________ |

---

## Fase 1 — Tenant CIAM (Entra External ID) + Google IdP

Tudo aqui é no **Microsoft Entra admin center** ([entra.microsoft.com](https://entra.microsoft.com)) — **não** no `portal.azure.com`.

### 1.1 Criar o tenant External ID

1. Entra admin center → **Entra ID → Overview → Manage tenants → `Create`**.
2. Selecione **External** → **Continue**.
3. Escolha a forma de criar:
   - **30-day free trial** — não pede subscription; mais rápido (tenant descartável de aula).
   - **Use Azure Subscription** — se o trial não aparecer; escolha `<sua-subscription>` + `<seu-rg>` (free 50K MAU, não expira).
4. Preencha **Tenant Name** = `<seu-tenant>` e **Domain Name** = `<seu-tenant>` (vira `<seu-tenant>.onmicrosoft.com`) · selecione a **Location** → criar (pode levar até 30 min; acompanhe no sino **Notifications**).
5. Troque para o tenant novo: engrenagem **Settings → Directories + subscriptions →** localize `<seu-tenant>` → **Switch**.
6. **Tenant overview → Overview**: anote o **Tenant ID** (= `<CiamTenantId>`). O host de login do cliente é `<seu-tenant>.ciamlogin.com`.

> 💡 A **Location não muda depois** — escolha certo na criação. Se a criação falhar com erro de provider: `az provider register -n Microsoft.AzureActiveDirectory` (uma vez).

### 1.2 Criar o user flow (sign-up / sign-in)

1. **External Identities → User flows → `New user flow`**.
2. **Name:** `SignUpSignIn`.
3. **Identity providers:** marque **Email Accounts → Email one-time passcode (OTP)**.
4. **User attributes:** escolha o que coletar → **Create**.

### 1.3 Google como provedor de identidade (login social do cliente)

1. Crie o OAuth client no Google e pegue **Client ID + Client secret** — passo a passo no **[Apêndice A](#apêndice-a--google-oauth-os-7-redirect-uris)** (depende do `<CiamTenantId>` da 1.1).
2. **External Identities → All identity providers → Built-in → Google → Configure** → cole **Client ID** + **secret** → **Save**.
3. Ative no flow: **User flows →** `SignUpSignIn` **→ Settings → Identity providers →** marque **Google → Save**.

> 🟢 Sem tempo para o Google? O **Email OTP** (1.2) já cobre o lab inteiro — o Google é o login social opcional.

✅ **Checkpoint:** tenant CIAM criado e **selecionado** (canto superior mostra `<seu-tenant>`); **Tenant ID** anotado; user flow `SignUpSignIn` ativo (Email OTP, + Google se configurado).

---

## Fase 2 — App Registration do CLIENTE (SPA, no tenant CIAM)

Confirme no topo do Entra admin center que você está no tenant **CIAM** (`*.ciamlogin.com`).

### 2.1 Registrar a App SPA + Redirect URI

1. **Entra ID → App registrations → `New registration`**.
2. **Name:** `student-<iniciais>-v2` · **Supported account types:** single-tenant → **Register**.
3. Na **Overview**, anote **Application (client) ID** (= `Jwt__CiamClientId` e `VITE_CIAM_CLIENT_ID`).
4. **Authentication → Add a platform → Single-page application (SPA)** → em **Redirect URIs** adicione os **dois** (sem barra final, sem path):
   - `https://<seu-frontend>.azurewebsites.net`  ← frontend deployado
   - `http://localhost:5173`  ← `npm run dev` local
5. **NÃO** crie client secret (SPA é público, usa PKCE) → **Save**.

> ⚠️ Plataforma tem de ser **Single-page application (SPA)**, **não** *Web*. O frontend faz login com `redirectUri = window.location.origin` (sem path), por isso os dois URIs exatos acima. "Web" por engano → `AADSTS9002326`.

### 2.2 Expose an API → scope `purchase.write`

O SPA pede um access token com escopo `api://<ciam-client-id>/purchase.write`. Sem esse scope exposto, o login até funciona mas a **compra** falha.

1. No app `student-<iniciais>-v2` → **Expose an API**.
2. Em **Application ID URI** → **Add** → aceite o default **`api://<ciam-client-id>`** → **Save**.
3. **`+ Add a scope`**:
   - **Scope name:** `purchase.write`
   - **Who can consent:** **Admins and users**
   - preencha os display names / descriptions
   - **State:** Enabled → **Add scope**.

### 2.3 Vincular ao user flow

1. **External Identities → User flows →** `SignUpSignIn` **→ Applications → `Add application`** → selecione `student-<iniciais>-v2` → **Select**.

✅ **Checkpoint:** App Reg SPA no CIAM; **client ID** anotado; redirect SPA com os 2 URIs; **Application ID URI** `api://<ciam-client-id>` com o scope `purchase.write`; vínculo ao user flow feito. *(Há um app `b2c-extensions-app` na lista — não apague.)*

---

## Fase 3 — App Registration do ADMIN (SPA, no tenant workforce) + App Role

Troque para o tenant **workforce** (domínio `*.onmicrosoft.com`, **não** `ciamlogin.com`): engrenagem **Settings → Directories + subscriptions → Switch**.

### 3.1 Registrar a App SPA + Redirect URI

1. **Entra ID → App registrations → `New registration`** → **Name:** `student-<iniciais>-admin` · single-tenant → **Register**.
2. Anote **Application (client) ID** (= `Jwt__AdminClientId`) e **Directory (tenant) ID** (= `Jwt__AdminTenantId` = `<AdminTenantId>` — o workforce, **diferente** do CIAM).
3. **Authentication → Add a platform → Single-page application (SPA)** → adicione os **mesmos dois** redirect URIs (sem barra final, sem path):
   - `https://<seu-frontend>.azurewebsites.net`
   - `http://localhost:5173`

> ⚠️ O admin usa o **mesmo SPA** do cliente (`redirectUri = window.location.origin`), por isso esta App Reg workforce precisa **dos mesmos** redirect URIs SPA da Fase 2. Sem eles → `AADSTS50011`.

### 3.2 Expose an API (Application ID URI)

O front pede o token admin com o escopo `api://<admin-client-id>/.default`. Para isso o app precisa de um **Application ID URI**.

1. No app `student-<iniciais>-admin` → **Expose an API → Application ID URI → Add** → aceite **`api://<admin-client-id>`** → **Save**.
2. *(Opcional)* se quiser um scope nomeado em vez do `.default`, **`+ Add a scope`** (ex.: `access_as_admin`) e configure a Variable `VITE_ADMIN_SCOPE` na Fase 5.

### 3.3 Manifest → `requestedAccessTokenVersion = 2`

Por padrão, o access token deste app sai em **v1.0**. O gateway valida o issuer admin como `…/v2.0` — token v1 → **401**. Force a v2:

1. No app → **Manifest**.
2. No **Microsoft Graph App manifest**, dentro do objeto **`api`**, localize `"requestedAccessTokenVersion": null` e troque para **`2`**.
   *(No manifesto AAD legado, o campo equivalente é `"accessTokenAcceptedVersion": 2` no nível raiz.)*
3. **Save**.

### 3.4 App Role `Admin` + atribuir o usuário admin

1. No app → **App roles → `Create app role`**: **Display name** `Admin` · **Allowed member types** Users/Groups · **Value** **`Admin`** · habilitada → **Apply**.
2. Atribua o usuário: **Enterprise applications →** `student-<iniciais>-admin` **→ Users and groups → `Add user/group`** → escolha seu admin → role `Admin` → **Assign**.

✅ **Checkpoint:** App Reg admin no workforce com redirect SPA, **Application ID URI** `api://<admin-client-id>`, **Manifest com `requestedAccessTokenVersion = 2`**, e App Role `Admin` **criada e atribuída**. Agora você tem os **4 GUIDs reais** `Jwt__*` (CIAM tenant/client + admin tenant/client).

---

## Fase 4 — Infra do gateway no Portal (ACR + Environment + Container App)

Esta é a **única fonte** do provisionamento do gateway. Tudo à mão no **[portal.azure.com](https://portal.azure.com)** (⚠️ **não** no `entra.microsoft.com` desta vez). Confirme no topo que está na `<sua-subscription>` e no `<seu-rg>`.

### 4.1 Azure Container Registry (ACR)

1. Busca do topo → **Container registries → `+ Create`**.
2. **Subscription** `<sua-subscription>` · **Resource group** `<seu-rg>` · **Registry name** `cr<sufixo>` (só letras/números, único global) · **Location** `<sua-regiao>` · **SKU** **Basic**.
3. **Review + create → Create → Go to resource**.
4. **Settings → Access keys** → ligue **Admin user = Enabled**.
5. Anote **Login server** (`cr<sufixo>.azurecr.io`), **Username** e uma **password**.

### 4.2 Container Apps Environment

1. Busca do topo → **Container Apps → `+ Create`** (abre o assistente do Container App).
2. Aba **Basics**, em **Container Apps Environment → `Create new`**: **Name** `cae-<sufixo>` · **Region** `<sua-regiao>` → **Create**.

### 4.3 Container App do gateway

Suba com **imagem placeholder pública** — a imagem real vem pelo Actions (Fase 6).

1. **Basics:** **Container app name** `ca-gateway-<sufixo>` · **Environment** `cae-<sufixo>` → **Next: Container**.
2. **Container:** mantenha **Use quickstart image** (ACR ainda vazio); CPU/memória no menor preset → **Next: Ingress**.
3. **Ingress:** **Enabled** · **Ingress traffic** = **Accepting traffic from anywhere** · **Target port** = **`8080`**.
4. **Review + create → Create → Go to resource**.
5. Na **Overview**, copie a **Application Url** — é o seu `<gateway-fqdn>` (vira a Variable `GATEWAY_V2_URL`).

> ⚠️ **Target port = 8080** é crítico: a imagem do gateway expõe a porta **8080** (`Dockerfile`: `EXPOSE 8080` + `ASPNETCORE_URLS=http://+:8080`). Qualquer outro valor = **502** em tudo.

### 4.4 Conectar o ACR (Admin Credentials)

1. No Container App → **Settings → Registries → `+ Add`** → **Registry** = `cr<sufixo>.azurecr.io` → **Authentication** = **Admin Credentials** → **Save**.

### 4.5 App Settings de identidade (gateway é fail-closed)

O gateway **só sobe com as 4 `Jwt__*` presentes**. No Container App: **Application → Containers → `Edit and deploy`** → selecione o container → aba **Environment variables** → adicione as 8 (Source = Manual entry) → **Save → Create**:

| App Setting | Valor |
|---|---|
| `Jwt__CiamTenantId` | `<CiamTenantId>` (Fase 1.1) |
| `Jwt__CiamClientId` | client ID da App Reg SPA cliente (Fase 2) |
| `Jwt__AdminTenantId` | `<AdminTenantId>` workforce (Fase 3) |
| `Jwt__AdminClientId` | client ID da App Reg admin (Fase 3) |
| `FunctionAppF1Url` | `https://<seu-func>.azurewebsites.net` (sem ela `/purchase` dá 502) |
| `Gateway__FrontendOrigin` | `https://<seu-frontend>.azurewebsites.net` (CORS restrito ao front) |
| `BackendV1Url` | `https://<seu-backend>.azurewebsites.net` (rotas `/admin/*` → backend v1; **sem ela o admin dá 502**) |
| `Gateway__AdminSharedSecret` | **segredo forte que VOCÊ gera** (ex.: `openssl rand -hex 24`) — injetado como header `X-Gateway-Key`. Use o **MESMO valor** no Secret `GATEWAY_SHARED_SECRET` do fork (Fase 5), que o workflow aplica no backend ao rodar `acao=backend` |

> 🔒 **Duplo underscore:** `Jwt:CiamTenantId` na config vira `Jwt__CiamTenantId` em env var (`:` não é válido). A connection string do SQL **NÃO** vai no gateway (fica na Function). Para só testar a infra antes de ter os GUIDs reais, dá pra usar 4 GUIDs **placeholder** (válidos em forma): o gateway sobe e o `401` sem token já funciona; o fluxo com token real só fecha com os 4 GUIDs reais.

> 💡 **Alternativa CLI (Cloud Shell)** — toda a Fase 4 em um bloco (⚠️ não imprima a senha em logs compartilhados):
> ```bash
> az acr create -g <seu-rg> -n cr<sufixo> --sku Basic --admin-enabled true --location <sua-regiao> -o table
> az containerapp env create -g <seu-rg> -n cae-<sufixo> --location <sua-regiao> -o table
> az containerapp create -g <seu-rg> -n ca-gateway-<sufixo> \
>   --environment cae-<sufixo> --image mcr.microsoft.com/k8se/quickstart:latest \
>   --target-port 8080 --ingress external --min-replicas 0 --max-replicas 1 -o table
> ACR_USER=$(az acr credential show -g <seu-rg> -n cr<sufixo> --query username -o tsv)
> ACR_PASS=$(az acr credential show -g <seu-rg> -n cr<sufixo> --query "passwords[0].value" -o tsv)
> az containerapp registry set -g <seu-rg> -n ca-gateway-<sufixo> \
>   --server cr<sufixo>.azurecr.io --username "$ACR_USER" --password "$ACR_PASS" -o table
> az containerapp update -g <seu-rg> -n ca-gateway-<sufixo> --set-env-vars \
>   "Jwt__CiamTenantId=<CiamTenantId>" "Jwt__CiamClientId=<CLIENT_ID_SPA_CIAM>" \
>   "Jwt__AdminTenantId=<AdminTenantId>" "Jwt__AdminClientId=<CLIENT_ID_ADMIN>" \
>   "FunctionAppF1Url=https://<seu-func>.azurewebsites.net" \
>   "Gateway__FrontendOrigin=https://<seu-frontend>.azurewebsites.net" \
>   "BackendV1Url=https://<seu-backend>.azurewebsites.net" \
>   "Gateway__AdminSharedSecret=<seu-segredo-forte>" -o table
> ```

✅ **Checkpoint:** ACR `cr<sufixo>` (Admin Enabled), Environment `cae-<sufixo>`, Container App `ca-gateway-<sufixo>` rodando (placeholder) com **ingress externo na porta 8080**, **Application Url** anotada (= `<gateway-fqdn>`), **ACR conectado** em Registries e as **8 App Settings** presentes (4 `Jwt__*` reais + `BackendV1Url` + `Gateway__AdminSharedSecret`).

> 🔒 **Handshake admin (gateway ↔ backend):** o gateway injeta `X-Gateway-Key` = `Gateway__AdminSharedSecret`; o backend confia quando bate com o `GATEWAY_SHARED_SECRET` dele. O **workflow** `acao=backend` (Fase 6) deploya o backend (com o `gatewayTrust.js`) e aplica esse `GATEWAY_SHARED_SECRET` a partir do Secret do fork — por isso o valor tem de ser o **mesmo** aqui (gateway) e no Secret (Fase 5). O segredo nunca vai pro código/repositório.

---

## Fase 5 — GitHub: Variables + Secrets (no SEU fork)

No **seu fork** → **Settings → Secrets and variables → Actions**. Os **nomes** abaixo são **fixos** (iguais para todos); os **valores** são os **seus** (placeholders da convenção de nomes).

### Variables

| Nome EXATO | Valor (seu) | Usada em (ação) |
|---|---|---|
| `SQL_SERVER` | `<seu-sql-server>` | migrations |
| `RESOURCE_GROUP` | `<seu-rg>` | migrations |
| `ACR_LOGIN_SERVER` | `cr<sufixo>.azurecr.io` | gateway |
| `PHASE04_CONTAINERAPP_NAME` | `ca-gateway-<sufixo>` | gateway |
| `PHASE04_RESOURCE_GROUP` | `<seu-rg>` | gateway |
| `FRONTEND_APP_NAME` | `<seu-frontend>` | frontend |
| `BACKEND_APP_NAME` | `<seu-backend>` | backend |
| `GATEWAY_V2_URL` | `https://<gateway-fqdn>` | frontend (→ `VITE_GATEWAY_V2_URL`) |
| `BACKEND_URL` | `https://<seu-backend>.azurewebsites.net` | frontend |
| `FUNCTION_V2_URL` | `https://<seu-func>.azurewebsites.net` | frontend (→ `VITE_FUNCTION_V2_URL`) |
| `VITE_CIAM_AUTHORITY` | `https://<seu-tenant>.ciamlogin.com/` | frontend |
| `VITE_CIAM_CLIENT_ID` | client ID da App Reg SPA cliente (Fase 2) | frontend |
| `VITE_ADMIN_TENANT_ID` | `<AdminTenantId>` workforce (Fase 3) | frontend |
| `VITE_ADMIN_CLIENT_ID` | client ID da App Reg admin (Fase 3) | frontend |
| `VITE_ADMIN_SCOPE` *(opcional)* | `api://<admin-client-id>/.default` *(default do código; só defina para um scope nomeado)* | frontend |

> ⚠️ As vars do gateway têm prefixo **`PHASE04_`** (`PHASE04_CONTAINERAPP_NAME`, `PHASE04_RESOURCE_GROUP`) — é exatamente o que o YAML lê. `SQL_SERVER`/`RESOURCE_GROUP` têm fallback para defaults internos do YAML, mas mantê-las explícitas é mais claro.
> ⚠️ **Login do admin precisa de `VITE_ADMIN_TENANT_ID` + `VITE_ADMIN_CLIENT_ID`.** Se faltarem, o build sai sem o mundo admin (login workforce desabilitado) e a [Fase 8](#fase-8--login-do-admin-workforce--app-role) não funciona.

### Secrets

| Nome EXATO | Conteúdo | Usada em (ação) |
|---|---|---|
| `AZURE_CREDENTIALS` | JSON do Service Principal com acesso ao RG | migrations + backend + gateway |
| `SQL_CONNECTION_STRING` | connection string ADO.NET do `FIFA2026Tickets` | migrations |
| `AZURE_FRONTEND_PUBLISH_PROFILE` | publish profile do `<seu-frontend>` | frontend |
| `AZURE_BACKEND_PUBLISH_PROFILE` | publish profile do `<seu-backend>` | backend |
| `GATEWAY_SHARED_SECRET` | **mesmo** valor do `Gateway__AdminSharedSecret` (Fase 4.5) — o workflow aplica no backend | backend |

> **Montar a `SQL_CONNECTION_STRING` (Cloud Shell PowerShell):**
> ```powershell
> $server = "<seu-sql-server>"; $senha = "<senha-adminsql>"
> "Server=$server.database.windows.net,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=$senha;Encrypt=true;TrustServerCertificate=true"
> ```

> 📌 **Sobras da F1 (Oitavas) — NÃO confunda:** `FUNCTION_APP_NAME` (Variable) e `FUNCTION_PUBLISH_PROFILE` (Secret) existem no fork mas o workflow das Quartas **NÃO lê** — deixe-as quietas. *(Atenção: `BACKEND_APP_NAME` e `AZURE_BACKEND_PUBLISH_PROFILE`, antes inertes, agora SÃO usadas pelo `acao=backend`.)*

✅ **Checkpoint:** as 15 Variables (3 com default) e os 5 Secrets criados no fork, com os nomes EXATOS acima.

---

## Fase 6 — Deploy via GitHub Actions (o ÚLTIMO passo)

Toda a infra acima foi criada **à mão**. Este é o **último bloco de deploy**: o Actions só **constrói e publica código** (schema + imagens). Precisa do **fork** porque é nele que ficam o workflow e os Secrets/Vars.

### 6.1 Preparar o fork (tudo pela web do GitHub)

A branch do lab no repositório do evento (org **TFTEC**) chama-se **`lab-quartas-de-final`** — ela traz o workflow `lab-quartas-de-final.yml` + o código e as migrations das Quartas.

1. **Faça um fork NOVO** do repo do evento, **com TODAS as branches** — na tela de fork, **desmarque** *Copy the `main` branch only* → **Create fork**. Assim a `lab-quartas-de-final` (e a `lab-oitavas-de-final`) vêm junto.
   > ⚠️ **Não reuse o fork das Oitavas.** O botão **Sync fork** do GitHub só atualiza a `main` e **não traz branches novas** — a `lab-quartas-de-final` não apareceria. Forkar de novo é o caminho limpo (e 100% pela web).
2. **Habilite o workflow na `main` do seu fork:** no seu fork, abra um **Pull Request `lab-quartas-de-final` → `main`** (base = `main`, compare = `lab-quartas-de-final`) e faça o **merge**. Como a `main` ainda não tem o `lab-quartas-de-final.yml`, é esse PR que faz o workflow aparecer no Actions — e ele é o próprio **"exercício"** da aula. (Você nunca dá PR no repo da TFTEC, só no SEU fork.)

### 6.2 Rodar o workflow — nesta ordem

Sempre em **Actions → "Lab Quartas de Final" → Run workflow → branch `main`** (já com o workflow após o merge da 6.1), variando o `acao`:

1. **`acao = migrations`** — aplica `phase-01`, `phase-03` e a nova **`phase-04-ciam-link.sql`** (cria `users.entra_oid` **vazia** + índice `UQ_users_entra_oid`). O workflow abre/reverte acesso temporário ao SQL privado (idempotente; pode repetir). O **preenchimento** da coluna é o hands-on da [Fase 9](#fase-9--migração-users-v1--ciam-sql--o-clímax) — de propósito **não** roda aqui.
2. **`acao = backend`** — deploya o backend v1 (com o `gatewayTrust.js` do **admin 100% workforce**) e aplica o `GATEWAY_SHARED_SECRET` (do Secret do fork). Pré-req: **SCM Basic Auth `On`** no Web App do backend e o `AZURE_BACKEND_PUBLISH_PROFILE` capturado **depois** disso. O job **abre/reverte** o acesso público do backend (que é privado) durante o deploy.
3. **`acao = gateway`** — `dotnet build/test`, **build & push** da imagem no ACR (`cr<sufixo>.azurecr.io/gateway:<sha>`), `az containerapp update --image` (troca o placeholder pela imagem real) + smoke. Re-rode só quando trocar o código.
4. **`acao = frontend`** — antes, garanta **SCM Basic Auth `On`** no Web App do frontend e capture o `AZURE_FRONTEND_PUBLISH_PROFILE` **depois** disso. O job faz `npm ci` + `vite build` (com `VITE_CIAM_*` e `VITE_ADMIN_*`) + deploy.

> 🖱️ **Disparo manual apenas:** o workflow só tem `workflow_dispatch` — nada roda até você clicar em **Run workflow** e escolher a ação.

### 6.3 Smoke do gateway

```bash
FQDN="<gateway-fqdn>"
sleep 20   # cold start: min-replicas=0
curl -fsS "https://${FQDN}/health"
# → {"status":"healthy","service":"gateway-yarp"}   (rota anônima)
curl -s -o /dev/null -w '%{http_code}\n' -X POST "https://${FQDN}/purchase" \
  -H "Content-Type: application/json" -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# → 401   (fail-closed: sem token o gateway recusa)
```

✅ **Checkpoint:** três jobs verdes; `/health` = 200; `POST /purchase` sem token = **401** (AC-6); a revisão ativa do Container App aponta para `cr<sufixo>.azurecr.io/gateway:<sha>` (não mais o placeholder); frontend publicado com as authorities CIAM e admin embutidas.

---

## Fase 7 — Login do cliente (CIAM) e2e

1. Abra o frontend → **Entrar (v2)** → redireciona para `<seu-tenant>.ciamlogin.com`.
2. **Sign-up self-service:** "Continuar com Google" **ou** email + **OTP**.
3. Faça uma compra — o SPA envia `Authorization: Bearer <token-CIAM>` ao gateway.
4. Confirme no SQL:
   ```sql
   SELECT TOP 5 id, user_id, entra_oid, status, created_at
   FROM dbo.purchases WHERE entra_oid IS NOT NULL ORDER BY id DESC;
   ```

> ⚠️ **GATE de runtime:** cole o access token em [jwt.ms](https://jwt.ms) e confira o `iss` (termina em `…/v2.0`), o `aud` (= seu client ID CIAM) e o claim `oid`. É aqui que você trava o formato do issuer CIAM e o `knownAuthorities`. Em sala é trial descartável — nunca cole tokens de produção.

✅ **Checkpoint (AC-11):** login CIAM → gateway valida → `X-Entra-OID` propagado → `purchases.entra_oid` (origem CIAM) gravado ao lado de registros v1.

---

## Fase 8 — Login do admin (workforce) + App Role

Pré-condição: Fase 3 feita, os `Jwt__Admin*` + `BackendV1Url` + `Gateway__AdminSharedSecret` no gateway (Fase 4.5), `VITE_ADMIN_*` + `GATEWAY_SHARED_SECRET` no fork (Fase 5), e o **backend já deployado com `acao=backend`** (Fase 6) — é ele que traz o `gatewayTrust.js` + o segredo pro backend confiar no gateway.

1. Logue como admin (authority `https://login.microsoftonline.com/<AdminTenantId>`). Em [jwt.ms](https://jwt.ms): `iss = login.microsoftonline.com/.../v2.0` e `roles: ["Admin"]`.
2. Teste a separação: um **cliente CIAM válido** numa rota admin recebe **403** (autenticado, sem a role) — **não** 401.

✅ **Checkpoint (AC-13):** login admin via workforce com `roles:["Admin"]`, separado do cliente CIAM. Dois mundos coexistindo, validados pela **mesma** mecânica issuer-agnóstica.

---

## Fase 9 — Migração `users` v1 → CIAM (SQL — o clímax)

A coluna `users.entra_oid` já existe **vazia** (Fase 6.2) — você a **preenche** aqui, de forma aditiva.

**9.1 Listar os alvos**
```sql
SELECT id, name, email, entra_oid FROM dbo.users WHERE entra_oid IS NULL ORDER BY id;
```

**9.2 Sign-up no CIAM com o MESMO email do v1** — para cada conta, faça sign-up self-service no CIAM (Fase 7) com **o email idêntico** ao de `users`. A senha bcrypt **NÃO vai** pro CIAM; o `users.password` fica **intacto** no caminho v1.

**9.3 Capturar o `oid` emitido pelo CIAM** — via app (token em jwt.ms/DevTools) **ou** via Portal (Entra admin center → tenant CIAM → **Users** → o usuário → **Object ID**).

**9.4 Vincular o `oid` ao registro v1 (idempotente)**
```sql
UPDATE dbo.users
SET    entra_oid = @oid       -- oid do 9.3
WHERE  email = @email         -- MESMO email do v1
  AND  entra_oid IS NULL;     -- guard de idempotência
```

**9.5 Provar a coexistência (o clímax)**
```sql
SELECT u.id AS user_id_v1, u.email,
       CASE WHEN u.password LIKE '$2%' THEN 'bcrypt-presente' ELSE 'sem-bcrypt' END AS credencial_v1,
       u.entra_oid AS oid_ciam_v2,
       CASE WHEN u.password IS NOT NULL AND u.entra_oid IS NOT NULL
            THEN 'COEXISTE (v1 bcrypt + v2 CIAM)'
            WHEN u.entra_oid IS NULL THEN 'so v1 (nao migrou)'
            ELSE 'estado inesperado' END AS status_migracao
FROM dbo.users u WHERE u.email = @email;
```
Esperado: `status_migracao = COEXISTE (v1 bcrypt + v2 CIAM)`.

> 💡 **Rollback (aditivo ⇒ trivial):** desfazer um vínculo = `UPDATE dbo.users SET entra_oid = NULL WHERE email = @email;`. Reverter a migration = `DROP INDEX UQ_users_entra_oid ON dbo.users; ALTER TABLE dbo.users DROP COLUMN entra_oid;`. **Nunca** crie backup table no SQL (regra do projeto).

✅ **Checkpoint (AC-16):** uma linha de `users` com as **duas identidades** vivas lado a lado. Modernização sem destruição, provada em banco.

---

## Resumo do que você criou nesta aula

| Camada | Recursos / artefatos |
|---|---|
| Identidade (cliente) | Tenant CIAM `<seu-tenant>` + App Reg SPA `student-<iniciais>-v2` (scope `purchase.write`) + user flow (+Google) |
| Identidade (admin) | App Reg `student-<iniciais>-admin` no workforce (App ID URI + token v2 + App Role `Admin` atribuída) |
| Infra do gateway | ACR `cr<sufixo>` + Environment `cae-<sufixo>` + Container App `ca-gateway-<sufixo>` (ingress externo 8080) |
| Banco | `users.entra_oid` (migration aditiva) + vínculo v1→CIAM hands-on |
| Automação | Fork: Variables + Secrets + workflow único `Lab Quartas de Final` (`migrations`/`gateway`/`frontend`/`tudo`) |

---

## Apêndice A — Google OAuth (os 7 redirect URIs)

> 🟢 Só faça se for oferecer login social do Google (o **Email OTP** da Fase 1.2 já cobre o lab). Faça **depois** de ter o `<CiamTenantId>` (Fase 1.1) — os redirect URIs dependem dele.

A interface atual chama-se **Google Auth Platform** (rótulos legados *APIs & services → OAuth consent screen* entre parênteses).

1. [console.cloud.google.com](https://console.cloud.google.com) (conta do lab) → **New Project** (ex.: `fifa2026-ciam-lab`) → **Create** → selecione-o.
2. **☰ → Google Auth Platform → Branding** → wizard **Get started**: App name + User support email (Gmail do lab); **Audience = External**; contato → Save.
3. **Audience:** confirme **Publishing status = Testing** e adicione o Gmail do lab em **Test users**.
4. **Branding → Authorized domains:** adicione **`ciamlogin.com`** e **`microsoftonline.com`**.
5. **Clients → Create client → Web application** → em **Authorized redirect URIs** cole **os 7** abaixo, trocando `<tenant-ID>` pelo seu `<CiamTenantId>` e `<tenant-subdomain>` pelo seu `<seu-tenant>`:

```text
https://login.microsoftonline.com
https://login.microsoftonline.com/te/<tenant-ID>/oauth2/authresp
https://login.microsoftonline.com/te/<tenant-subdomain>.onmicrosoft.com/oauth2/authresp
https://<tenant-ID>.ciamlogin.com/<tenant-ID>/federation/oidc/accounts.google.com
https://<tenant-ID>.ciamlogin.com/<tenant-subdomain>.onmicrosoft.com/federation/oidc/accounts.google.com
https://<tenant-subdomain>.ciamlogin.com/<tenant-ID>/federation/oauth2
https://<tenant-subdomain>.ciamlogin.com/<tenant-subdomain>.onmicrosoft.com/federation/oauth2
```

> Cadastre os 7 ou o login falha com `redirect_uri_mismatch`. Ref.: https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-google-federation-customers

6. **Create** → copie **Client ID** e **Client secret** (o secret só aparece agora por inteiro) → leve para a **Fase 1.3**.

---

## Apêndice B — Gemini key (adiantamento p/ o último lab)

Adiantamento para o **último lab** (chatbot LLM). Como você cria a conta Google de qualquer jeito (Apêndice A), já deixe a key pronta.

1. Crie/abra uma conta **Gmail exclusiva do lab** (ex.: `fifa2026.lab.<iniciais>@gmail.com`), em janela anônima.
2. Acesse **https://aistudio.google.com/apikey** logado nessa conta → aceite os termos.
3. **Create API key → Create API key in new project** → copie e guarde como `GEMINI_API_KEY` (nunca no código). Modelo do lab: `gemini-2.5-flash`.

---

## Apêndice C — Troubleshooting

| Sintoma | Causa provável | Mitigação |
|---|---|---|
| **502** em toda chamada | targetPort do ingress ≠ **8080** | ingress targetPort = **8080** (Fase 4.3) |
| **502** só em `/purchase` | `FunctionAppF1Url` ausente/errada | apontar p/ `https://<seu-func>.azurewebsites.net` (Fase 4.5) |
| **502** nas rotas `/admin/*` | `BackendV1Url` ausente no gateway | definir `BackendV1Url` no gateway (Fase 4.5) |
| Admin **401/403** com token válido (`roles:["Admin"]`) | `Gateway__AdminSharedSecret` (gateway) ≠ `GATEWAY_SHARED_SECRET` (backend), ou `acao=backend` não rodou | usar o MESMO valor nos dois (App Setting Fase 4.5 = Secret Fase 5) e rodar `acao=backend` (Fase 6) |
| Container App `Failed`/CrashLoop | `Jwt__*` ausente/vazia/`"common"` (fail-closed) | 4 `Jwt__*` presentes; placeholder serve p/ subir e fazer o 401 |
| `/purchase` dá **200** sem token | gateway não fail-closed (config errada) | revisar `AddJwtBearer`/`Jwt__*`; deveria ser **401** |
| `/health` não responde no 1º hit | cold start (`min-replicas=0`) | aguardar ~20s e repetir |
| `AADSTS50011` no login do cliente | authority com `microsoftonline.com` | `VITE_CIAM_AUTHORITY` = `https://<seu-tenant>.ciamlogin.com/` (Fase 5) |
| `AADSTS50011` no login do admin | redirect URI faltando na App Reg workforce | adicionar `https://<seu-frontend>.azurewebsites.net` + `http://localhost:5173` como SPA (Fase 3.1) |
| `AADSTS9002326` (cross-origin) | plataforma cadastrada como **Web** | recriar a plataforma como **SPA** (Fases 2.1 / 3.1) |
| **401 "Invalid issuer"** no token admin | token saiu em **v1.0** | Manifest `requestedAccessTokenVersion = 2` (Fase 3.3) |
| **401 "Invalid issuer"** (cliente/admin) | `Jwt__*` placeholder/errado | trocar pelos 4 GUIDs reais (Fase 4.5) |
| Login do cliente OK mas **compra falha** | scope `purchase.write` não exposto | Expose an API + scope `purchase.write` (Fase 2.2) |
| MSAL recusa authority "não confiável" | falta `knownAuthorities` | derivado de `VITE_CIAM_AUTHORITY` (já no `authV2.ts`) |
| `roles` ausente no token admin | role não atribuída | Enterprise applications → atribuir `Admin` (Fase 3.4) |
| Cliente CIAM em rota admin dá 401 (esperava 403) | esquema fixado na policy | `AdminOnly` só `RequireRole("Admin")` (já no código) |
| Mundo **admin não aparece** no front | `VITE_ADMIN_TENANT_ID`/`VITE_ADMIN_CLIENT_ID` ausentes | criar as duas Variables (Fase 5) e re-rodar `acao=frontend` |
| Vars do gateway "não encontradas" | esqueceu o prefixo `PHASE04_` | usar `PHASE04_CONTAINERAPP_NAME` / `PHASE04_RESOURCE_GROUP` (Fase 5) |
| `redirect_uri_mismatch` (Google) | redirect URI ≠ callback do Entra | cadastrar **todos** os 7 URIs (Apêndice A) |
| Migrations falham por firewall | SQL privado; runner sem regra | o workflow abre/reverte acesso temporário (já tratado no YAML) |
| `lab-quartas-de-final` não aparece no fork | fork feito "só com a main" (Sync fork não traz branches novas) | **fork NOVO** com TODAS as branches — desmarque *Copy the `main` branch only* (Fase 6.1) |
| Workflow "Lab Quartas de Final" não aparece no Actions | `main` do fork ainda sem o `lab-quartas-de-final.yml` | abra o **PR `lab-quartas-de-final` → `main`** no seu fork e faça o merge (Fase 6.1) |
| Deploy do frontend `Publish profile is invalid` | basic-auth Off ao capturar o profile | ligar **SCM Basic Auth On**, recapturar o profile, atualizar o secret (Fase 6.2) |
| Usuário não migra / `so v1` | UPDATE não rodou / email divergente | re-executar o UPDATE idempotente (Fase 9.4) |
| Só "Use Azure Subscription" (sem trial) | trial 30d quase nunca é ofertado | seguir por **Use Azure Subscription** — free 50K MAU, não expira (Fase 1.1) |
| "Insufficient privileges" ao criar o tenant | conta sem Owner / RP não registrado | conta **Global Admin + Owner** + `az provider register -n Microsoft.AzureActiveDirectory` |
