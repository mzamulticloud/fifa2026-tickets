# Quartas de Final (F2) — Referência do INSTRUTOR (demo HML)

> 🔒 **Uso interno do instrutor — valores reais da demo HML. NÃO distribuir aos alunos.**
> Os alunos usam o guia genérico [`quartas-f2-portal-guide.md`](./quartas-f2-portal-guide.md) (cada um tem o próprio fork / subscription / nomes de recurso). Este doc existe só para **nós** reproduzirmos e validarmos a demo de referência.

> **Story:** [2.11](../stories/2.11.story.md) · **Workflow:** [`lab-quartas-de-final.yml`](../../.github/workflows/lab-quartas-de-final.yml) · **Passo-a-passo conceitual:** [guia do aluno](./quartas-f2-portal-guide.md)

---

## Status board (estado real da nossa simulação)

Única fonte de estado da demo. Tudo que **não** depende do tenant CIAM já está pronto na sessão `SUBS - HML`.

| Bloco | Estado | O que cobre |
|---|---|---|
| **Infra + Gateway** | ✅ FEITO | ACR, Container Apps Environment, Container App (ingress externo, targetPort 8080), imagem real deployada, smoke `/health` 200 + `POST /purchase` sem token = 401 — validado ao vivo |
| **Migrations** | ✅ FEITO | phase-01 + phase-03 + phase-04-ciam-link → `users.entra_oid` criada (vazia) + índice `UQ_users_entra_oid` |
| **GitHub Vars/Secrets** | ✅ FEITO | ver [tabela de Vars/Secrets](#github-varssecrets-valores-reais-do-fork) — `VITE_CIAM_*` setadas em 2026-06-29 |
| **Identidade** | ✅ FEITO | tenant CIAM + App Reg SPA (`app-ciam`) + App Reg admin (`app-workforce`) → 4 GUIDs reais `Jwt__*` plugados no gateway em 2026-06-29 |
| **Runtime e2e** | 🔄 EM ANDAMENTO | 4 GUIDs reais no gateway ✅ (rev `--0000003`, `/health` 200 + `/purchase` 401 revalidados); `acao=frontend` disparado; falta cadastrar redirect URIs SPA + login CIAM/admin ao vivo + migração `users` v1→CIAM |

> 🔑 **GUIDs placeholder:** o gateway é **fail-closed** (não sobe sem as 4 `Jwt__*`). Na demo ele subiu com **GUIDs placeholder** — válidos em forma, não são tenants reais. Isso já prova a infra (o `401` sem token funciona). O fluxo com **token real** só fecha depois de trocar os GUIDs reais (ver [comandos `az`](#comandos-az-já-executados-na-demo)).

---

## Tabela de valores reais — recursos Azure (`SUBS - HML`)

| Recurso | Valor real | Origem |
|---|---|---|
| Subscription | **SUBS - HML** (`d970133e-865f-40db-b9f2-6fedb919714e`) | — |
| Resource Group | **rg-hml-tik-cin-001** | reusa da F1 (Oitavas) |
| Região | **centralindia** | mesma da F1 |
| SQL Server / DB | **sql-dev-tk-cin-001** / **FIFA2026Tickets** | reusa (privado — `publicNetworkAccess Disabled`) |
| Function App F1 | **func-dev-tk-cin-001** (`https://func-dev-tk-cin-001.azurewebsites.net`) | reusa |
| Frontend Web App | **app-dev-tk-fend-cin-001** (`https://app-dev-tk-fend-cin-001.azurewebsites.net`) | reusa |
| Backend v1 Web App | **app-dev-tk-bend-cin-001** | reusa (comparação didática — intocado) |
| Container Registry (ACR) | **crdevtkcin001** → **crdevtkcin001.azurecr.io** | **NOVO** F2 ✅ |
| Container Apps Environment | **cae-dev-tk-cin-001** | **NOVO** F2 ✅ |
| Container App (gateway) | **ca-gateway-dev-tk-cin-001** | **NOVO** F2 ✅ |
| FQDN do gateway | **ca-gateway-dev-tk-cin-001.blueplant-f8efd93e.centralindia.azurecontainerapps.io** | gerado pelo Azure ✅ |

### Identidade (dois mundos) — valores reais

| Campo | Valor real (CIAM) | Estado |
|---|---|---|
| Nome do tenant | **copa-external-id** | ✅ |
| Domínio inicial | **copadomundoazurehml.onmicrosoft.com** | ✅ |
| Login do cliente | **copadomundoazurehml.ciamlogin.com** | ✅ |
| **Tenant ID (CIAM)** = `Jwt__CiamTenantId` | **`872736f9-b166-4e47-b881-52e4aa3fb3e6`** | ✅ |
| App Reg SPA (cliente) = `Jwt__CiamClientId` | **`app-ciam`** → **`123ce4f0-d930-4b6a-ab34-b78b6c7012eb`** | ✅ |
| App Reg admin (workforce) = `Jwt__AdminClientId` | **`app-workforce`** → **`a827c1e2-028f-4de6-8404-c8c28033d335`** | ✅ |
| Tenant ID workforce = `Jwt__AdminTenantId` | **`7de09cb8-6a1e-4646-8df8-70a9bc2ba649`** | ✅ |

> 💡 São **DOIS** tenants e **DOIS** client IDs: o **CIAM** (cliente, `*.ciamlogin.com`) e o **workforce** (admin). Anote os dois separadamente.

---

## GitHub Vars/Secrets (valores reais do fork)

Fork `tftec-guilherme/fifa2026-tickets-dev` → **Settings → Secrets and variables → Actions**.

### Variables

| Nome EXATO | Valor real | Usada em (ação) | Estado |
|---|---|---|---|
| `SQL_SERVER` | `sql-dev-tk-cin-001` | migrations | ✅ existe |
| `RESOURCE_GROUP` | `rg-hml-tik-cin-001` | migrations | ✅ existe |
| `ACR_LOGIN_SERVER` | `crdevtkcin001.azurecr.io` | gateway | ✅ existe |
| `PHASE04_CONTAINERAPP_NAME` | `ca-gateway-dev-tk-cin-001` | gateway | ✅ existe |
| `PHASE04_RESOURCE_GROUP` | `rg-hml-tik-cin-001` | gateway | ✅ existe |
| `FRONTEND_APP_NAME` | `app-dev-tk-fend-cin-001` | frontend | ✅ existe |
| `GATEWAY_V2_URL` | `https://ca-gateway-dev-tk-cin-001.blueplant-f8efd93e.centralindia.azurecontainerapps.io` | frontend (→ `VITE_GATEWAY_V2_URL`) | ✅ existe |
| `BACKEND_URL` | `https://app-dev-tk-bend-cin-001.azurewebsites.net` | frontend | ✅ existe |
| `FUNCTION_V2_URL` | `https://func-dev-tk-cin-001.azurewebsites.net` | frontend (→ `VITE_FUNCTION_V2_URL`) | ✅ existe |
| `VITE_CIAM_AUTHORITY` | `https://copadomundoazurehml.ciamlogin.com/` | frontend | ✅ setada (2026-06-29) |
| `VITE_CIAM_CLIENT_ID` | `123ce4f0-d930-4b6a-ab34-b78b6c7012eb` (App Reg `app-ciam`) | frontend | ✅ setada (2026-06-29) |

> ⚠️ As vars do gateway têm prefixo **`PHASE04_`** — é exatamente o que o YAML lê. `SQL_SERVER`/`RESOURCE_GROUP` têm fallback para defaults internos do YAML, mas mantê-las explícitas é mais claro.

### Secrets

| Nome EXATO | Conteúdo | Usada em (ação) | Estado |
|---|---|---|---|
| `AZURE_CREDENTIALS` | JSON do Service Principal com acesso ao RG | migrations + gateway | ✅ existe |
| `SQL_CONNECTION_STRING` | connection string ADO.NET do `FIFA2026Tickets` | migrations | ✅ existe |
| `AZURE_FRONTEND_PUBLISH_PROFILE` | publish profile do `app-dev-tk-fend-cin-001` | frontend | ✅ existe |

> **Montar a `SQL_CONNECTION_STRING` (Cloud Shell PowerShell):**
> ```powershell
> $server = "sql-dev-tk-cin-001"; $senha = "<senha-adminsql>"
> "Server=$server.database.windows.net,1433;Database=FIFA2026Tickets;User Id=adminsql;Password=$senha;Encrypt=true;TrustServerCertificate=true"
> ```

> 📌 **Sobras da F1 (Oitavas) — NÃO confunda:** existem no fork mas o workflow das Quartas **NÃO lê**. Deixe-as quietas.
> - Variables inertes: `BACKEND_APP_NAME`, `FUNCTION_APP_NAME`
> - Secrets inertes: `AZURE_BACKEND_PUBLISH_PROFILE`, `FUNCTION_PUBLISH_PROFILE`

---

## App Settings reais do Container App (gateway)

As 6 env vars do Container App `ca-gateway-dev-tk-cin-001` na demo:

| App Setting | Valor REAL (plugado 2026-06-29, rev `--0000003`) | Origem |
|---|---|---|
| `Jwt__CiamTenantId` | `872736f9-b166-4e47-b881-52e4aa3fb3e6` | tenant CIAM `copa-external-id` |
| `Jwt__CiamClientId` | `123ce4f0-d930-4b6a-ab34-b78b6c7012eb` | App Reg SPA **`app-ciam`** |
| `Jwt__AdminTenantId` | `7de09cb8-6a1e-4646-8df8-70a9bc2ba649` | tenant workforce |
| `Jwt__AdminClientId` | `a827c1e2-028f-4de6-8404-c8c28033d335` | App Reg admin **`app-workforce`** |
| `FunctionAppF1Url` | `https://func-dev-tk-cin-001.azurewebsites.net` | (mantida — sem ela `/purchase` dá 502) |
| `Gateway__FrontendOrigin` | `https://app-dev-tk-fend-cin-001.azurewebsites.net` | (mantida — CORS restrito ao front) |

> ✅ **GUIDs reais plugados (2026-06-29):** os 4 placeholders foram trocados pelos GUIDs reais via `az containerapp update` (mantendo `FunctionAppF1Url` e `Gateway__FrontendOrigin`). Nova revisão `ca-gateway-dev-tk-cin-001--0000003` (`provisioningState=Succeeded`); revalidado ao vivo: `/health` → **200**, `POST /purchase` sem token → **401** (gateway continua subindo fail-closed com os GUIDs reais).

> 🔒 **Duplo underscore:** `Jwt:CiamTenantId` em variável de ambiente vira `Jwt__CiamTenantId`. A connection string do SQL **NÃO** vai no gateway (fica na Function).
> **Discovery que o gateway monta:** authority `https://copadomundoazurehml.ciamlogin.com/872736f9-b166-4e47-b881-52e4aa3fb3e6`, issuer `…/v2.0`; admin `https://login.microsoftonline.com/<AdminTenantId>/v2.0`. Validação fail-closed, `ClockSkew=0`, `ValidIssuer`/`ValidAudiences` explícitos.

### Trocar os 4 GUIDs placeholder pelos reais (após Fases 1–3)

```bash
az containerapp update -g rg-hml-tik-cin-001 -n ca-gateway-dev-tk-cin-001 --set-env-vars \
  "Jwt__CiamTenantId=872736f9-b166-4e47-b881-52e4aa3fb3e6" \
  "Jwt__CiamClientId=<CLIENT_ID_SPA_CIAM>" \
  "Jwt__AdminTenantId=<TENANT_ID_WORKFORCE>" \
  "Jwt__AdminClientId=<CLIENT_ID_ADMIN>" -o table
```

---

## Comandos `az` já executados na demo

A infra do gateway já foi provisionada na demo HML. Estes comandos são **referência** (já rodaram); repita-os só para refazer do zero.

```bash
# ACR (Basic, admin-enabled) → crdevtkcin001.azurecr.io
az acr create -g rg-hml-tik-cin-001 -n crdevtkcin001 --sku Basic --admin-enabled true --location centralindia -o table

# Container Apps Environment
az containerapp env create -g rg-hml-tik-cin-001 -n cae-dev-tk-cin-001 --location centralindia -o table

# Container App do gateway (ingress externo, targetPort 8080 — errar aqui = 502)
az containerapp create -g rg-hml-tik-cin-001 -n ca-gateway-dev-tk-cin-001 \
  --environment cae-dev-tk-cin-001 \
  --image mcr.microsoft.com/k8se/quickstart:latest \
  --target-port 8080 --ingress external --min-replicas 0 --max-replicas 1 -o table

# Conectar o ACR (admin creds) p/ puxar a imagem real (NÃO imprima ACR_PASS em logs compartilhados)
ACR_USER=$(az acr credential show -g rg-hml-tik-cin-001 -n crdevtkcin001 --query username -o tsv)
ACR_PASS=$(az acr credential show -g rg-hml-tik-cin-001 -n crdevtkcin001 --query "passwords[0].value" -o tsv)
az containerapp registry set -g rg-hml-tik-cin-001 -n ca-gateway-dev-tk-cin-001 \
  --server crdevtkcin001.azurecr.io --username "$ACR_USER" --password "$ACR_PASS" -o table
```

> A imagem do gateway expõe a **porta 8080** (`src/Fifa2026.V2.Gateway/Dockerfile`: `EXPOSE 8080` + `ASPNETCORE_URLS=http://+:8080`); por isso o **targetPort do ingress = 8080**. O Container App subiu primeiro com imagem placeholder; o workflow `acao=gateway` depois trocou pela imagem real SHA-tagged (`crdevtkcin001.azurecr.io/gateway:<sha>`).

### Smoke test (validado ao vivo)

```bash
FQDN="ca-gateway-dev-tk-cin-001.blueplant-f8efd93e.centralindia.azurecontainerapps.io"
sleep 20   # cold start: min-replicas=0
curl -fsS "https://${FQDN}/health"
# → {"status":"healthy","service":"gateway-yarp"}   (rota anônima)
curl -s -o /dev/null -w '%{http_code}\n' -X POST "https://${FQDN}/purchase" \
  -H "Content-Type: application/json" -d '{"matchId":1,"category":"VIP","userId":1,"quantity":1}'
# → 401   (fail-closed: sem token o gateway recusa)
```

---

## Redirect URIs da App Reg SPA `app-ciam` (CRÍTICO p/ o login MSAL)

O MSAL (`src/lib/authV2.ts`) usa `redirectUri = import.meta.env.VITE_ENTRA_REDIRECT_URI ?? window.location.origin`. Como o workflow `lab-quartas-de-final.yml` **NÃO** define `VITE_ENTRA_REDIRECT_URI` no build do frontend, o redirect cai em **`window.location.origin`** (origem da página, sem path nem barra final).

Cadastre na App Reg **`app-ciam`** → **Authentication → Platform → Single-page application (SPA)** EXATAMENTE estas duas URIs:

```text
https://app-dev-tk-fend-cin-001.azurewebsites.net
http://localhost:5173
```

> ⚠️ **Plataforma SPA, não Web.** MSAL.js (PKCE, browser) exige a plataforma **SPA**; cadastrar como "Web" causa `AADSTS9002326` (cross-origin token redemption). Sem barra final e sem path — `window.location.origin` nunca inclui path. A primeira atende o frontend deployado (`app-dev-tk-fend-cin-001.azurewebsites.net`); a segunda atende `npm run dev` local (Vite na 5173).
> 💡 Se algum dia o frontend rodar em outra origem (ex.: domínio custom), basta adicionar essa nova origem aqui — ou setar a var `VITE_ENTRA_REDIRECT_URI` no build com a URL completa.

---

## 7 redirect URIs do Google (valores reais do tenant CIAM da demo)

Cole **os 7** em **Authorized redirect URIs** do OAuth client do Google (já com Tenant ID `872736f9-...` e subdomínio `copadomundoazurehml`):

```text
https://login.microsoftonline.com
https://login.microsoftonline.com/te/872736f9-b166-4e47-b881-52e4aa3fb3e6/oauth2/authresp
https://login.microsoftonline.com/te/copadomundoazurehml.onmicrosoft.com/oauth2/authresp
https://872736f9-b166-4e47-b881-52e4aa3fb3e6.ciamlogin.com/872736f9-b166-4e47-b881-52e4aa3fb3e6/federation/oidc/accounts.google.com
https://872736f9-b166-4e47-b881-52e4aa3fb3e6.ciamlogin.com/copadomundoazurehml.onmicrosoft.com/federation/oidc/accounts.google.com
https://copadomundoazurehml.ciamlogin.com/872736f9-b166-4e47-b881-52e4aa3fb3e6/federation/oauth2
https://copadomundoazurehml.ciamlogin.com/copadomundoazurehml.onmicrosoft.com/federation/oauth2
```

> Cadastre os 7 ou o login falha com `redirect_uri_mismatch`. Referência: https://learn.microsoft.com/en-us/entra/external-id/customers/how-to-google-federation-customers

---

## Re-executar as fases (referência rápida)

- **Migrations:** Actions → "Lab Quartas de Final" → Run workflow → `acao = migrations` → branch `phase-04-quartas`. O workflow abre acesso público + firewall temporário ao SQL (privado), aplica as 3 migrations e **reverte** o acesso ao final (mesmo em falha). Idempotente.
- **Gateway (código):** `acao = gateway` — `dotnet build/test` (suíte 17/17) + build & push no ACR + `az containerapp update --image` + smoke. Re-rode só se trocar o código.
- **Frontend:** `acao = frontend` — exige `VITE_CIAM_*`. `npm ci` + `vite build` + deploy no Web App.

> 📝 O cache de borda (AC-6) foi reescrito no fix `142bc92`: o `OutputCache` nativo não captura respostas proxied pelo YARP, então virou um `XCacheMiddleware`/`IMemoryCache`. Suíte: **17/17**.

O **passo-a-passo conceitual completo** (conceitos, fases de identidade, SQL da migração) está no [guia do aluno](./quartas-f2-portal-guide.md) — este doc só carrega os valores reais que não vão para os alunos.
