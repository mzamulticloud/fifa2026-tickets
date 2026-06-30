# GitHub Actions — FIFA 2026 Tickets

Dois workflows que publicam frontend e backend em **Azure Web App for Windows**.

| Workflow | Arquivo | Web App alvo (default) |
|---|---|---|
| Frontend | `deploy-frontend.yml` | `fifa2026-web` |
| Backend  | `deploy-backend.yml`  | `fifa2026-back` |

Ambos disparam por **push em `main`** (filtrando por path) ou manualmente via **Actions → Run workflow**.

---

## Pré-requisitos no Azure

### 1. Criar dois Web Apps (Windows)

```bash
# Resource group + plano
az group create --name fifa2026-rg --location brazilsouth
az appservice plan create \
  --name fifa2026-plan \
  --resource-group fifa2026-rg \
  --is-linux false \
  --sku B1

# Frontend (estático servido pelo IIS)
az webapp create \
  --name fifa2026-web \
  --resource-group fifa2026-rg \
  --plan fifa2026-plan \
  --runtime "node:20LTS"

# Backend (Node + iisnode)
az webapp create \
  --name fifa2026-back \
  --resource-group fifa2026-rg \
  --plan fifa2026-plan \
  --runtime "node:18LTS"
```

### 2. App Settings do backend (`fifa2026-back`)

```bash
az webapp config appsettings set \
  --resource-group fifa2026-rg \
  --name fifa2026-back \
  --settings \
    DB_SERVER='<server>.database.windows.net' \
    DB_PORT=1433 \
    DB_USER='<sql-user>' \
    DB_PASSWORD='<senha>' \
    DB_NAME='FIFA2026Tickets' \
    JWT_SECRET='<string-longa>' \
    JWT_EXPIRES_IN='7d' \
    FRONTEND_URL='https://fifa2026-web.azurewebsites.net' \
    WEBSITE_NODE_DEFAULT_VERSION='~18'
```

> O frontend **não precisa** de App Settings — `BACKEND_URL` é gravado no `web.config` em build time.

### 3. Segurança do backend (B1 = público)

> ⚠️ No App Service **B1** o backend fica **público** e protegido por **CORS** (`FRONTEND_URL`) + **JWT** — **não** o trave por allowlist de IPs do frontend. O proxy `/api` do IIS/ARR não funciona no B1, então o browser chama o backend direto (`VITE_API_URL`); travar por IP do frontend devolveria 403 ao usuário final. Veja [`../../DEPLOY.md`](../../DEPLOY.md) → Cenário B e [`../../infra/README.md`](../../infra/README.md).

Para privacidade de rede real, migre para Standard+ com Private Endpoint + VNet Integration.

---

## Configurando os secrets do GitHub

Os workflows usam **publish profile** — abordagem mais simples para workshops.

### 1. Baixar publish profile

```bash
az webapp deployment list-publishing-profiles \
  --resource-group fifa2026-rg \
  --name fifa2026-web \
  --xml > frontend.PublishSettings

az webapp deployment list-publishing-profiles \
  --resource-group fifa2026-rg \
  --name fifa2026-back \
  --xml > backend.PublishSettings
```

### 2. Adicionar como secret no GitHub

Repo → **Settings → Secrets and variables → Actions → New repository secret**:

| Nome do secret | Valor |
|---|---|
| `AZURE_FRONTEND_PUBLISH_PROFILE` | conteúdo de `frontend.PublishSettings` |
| `AZURE_BACKEND_PUBLISH_PROFILE`  | conteúdo de `backend.PublishSettings` |

### 3. Apagar os arquivos locais

```bash
rm frontend.PublishSettings backend.PublishSettings
```

---

## Disparando um deploy manual

**Frontend (escolhe o backend que ele vai chamar):**
1. Actions → **Deploy Frontend (FIFA 2026 Web)** → Run workflow
2. `backend_url`: `https://fifa2026-back.azurewebsites.net` (ou outro)
3. `app_name`: `fifa2026-web`

**Backend:**
1. Actions → **Deploy Backend (FIFA 2026 API)** → Run workflow
2. `app_name`: `fifa2026-back`

---

## Alternativa: OIDC (sem secret de longa duração)

Se for usar em produção real, prefira OIDC com Federated Credentials. Substitua o passo de publish profile por:

```yaml
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

- uses: azure/webapps-deploy@v3
  with:
    app-name: ...
    package: ...
    # sem publish-profile — usa o login acima
```

E adicione:

```yaml
permissions:
  id-token: write
  contents: read
```

Setup do App Registration + Federated Credential está na [doc oficial do Azure Login Action](https://github.com/Azure/login#login-with-openid-connect-oidc-recommended).

---

## Como o build do frontend descobre o backend

```
push em main / workflow_dispatch
       │
       ▼
npm ci  →  vite build  →  dist/ gerado
                                 │
                                 ▼
              scripts/set-backend-url.mjs
              substitui __BACKEND_URL__ em dist/web.config
              usando env BACKEND_URL (input do workflow)
                                 │
                                 ▼
              azure/webapps-deploy@v3 envia dist/ para o Web App
```

A regra do IIS no Web App lê o `web.config` deployado e faz proxy reverso para `${BACKEND_URL}/api/*`.

## Como o backend é deployado

```
push em main / workflow_dispatch
       │
       ▼
npm ci --omit=dev  →  remove logs/, .env  →  upload da pasta inteira
       │
       ▼
azure/webapps-deploy@v3 envia para fifa2026-back
       │
       ▼
iisnode usa fifa2026-api/web.config para iniciar src/index.js
       │
       ▼
backend lê App Settings (DB_SERVER, JWT_SECRET, FRONTEND_URL etc.)
```
