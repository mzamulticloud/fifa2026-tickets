# Infra Azure — FIFA 2026 Tickets

Provisiona App Service Plan + 2 Web Apps (Windows) + Azure SQL Database. Mesma topologia descrita em [`../DEPLOY.md`](../DEPLOY.md), implementada de duas formas:

| Arquivo | Quando usar |
|---|---|
| `main.bicep` + `parameters/dev.bicepparam` | Deploy declarativo, idempotente. Use no dia-a-dia. |
| `provision.sh` / `provision.ps1` | Scripts imperativos `az` CLI passo-a-passo. Use no material didático do evento. |

Ambos produzem a **mesma infra**: você escolhe pela ergonomia.

---

## Pré-requisitos

```bash
az login
az account set --subscription "<sua-subscription>"
az bicep upgrade   # opcional, mas recomendado
```

---

## Deploy via Bicep (recomendado)

```bash
# 1. Definir secrets (não vão para git)
export SQL_ADMIN_PASSWORD='SuaSenh@F0rte!'
export JWT_SECRET="$(openssl rand -hex 32)"

# 2. Criar resource group
az group create --name fifa2026-rg --location eastus

# 3. (Opcional) preview do que vai mudar
az deployment group what-if \
  --resource-group fifa2026-rg \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam

# 4. Deploy
az deployment group create \
  --resource-group fifa2026-rg \
  --template-file main.bicep \
  --parameters parameters/dev.bicepparam \
  --name fifa2026-initial

# 5. Capturar outputs
az deployment group show \
  --resource-group fifa2026-rg \
  --name fifa2026-initial \
  --query properties.outputs
```

> **Nota:** O Bicep cria o backend com `ipSecurityRestrictionsDefaultAction: 'Allow'` (público). No B1 isso é o estado **final** — veja abaixo.

### Segurança do backend no B1 — NÃO travar por IP

> ⚠️ **NÃO** aplique allowlist de outbound IPs do frontend + default Deny no backend rodando em **B1**. O reverse proxy `/api` do IIS/ARR **não funciona** no B1 (retorna 404), então o frontend embute `VITE_API_URL` absoluto e o **browser chama o backend direto** — as requisições partem do IP do **usuário final**, não do frontend. Travar por IP do frontend devolve **403** ao usuário e quebra o app.
>
> No B1 a segurança do backend é **CORS** (`FRONTEND_URL`) + **JWT**, não rede. (Detalhes em [`../DEPLOY.md`](../DEPLOY.md) → Cenário B.)

O snippet de allowlist/Deny que existia aqui (e ainda existe em `provision.sh`/`provision.ps1`) só é válido no **Cenário A (VMs)** ou em App Service **Standard+** com VNet Integration, onde o proxy server-side funciona. Para privacidade real no B1, migre para Standard+ com Private Endpoint + VNet Integration.

---

## Deploy via az CLI (didático)

```bash
export SQL_ADMIN_PASSWORD='SuaSenh@F0rte!'
export JWT_SECRET="$(openssl rand -hex 32)"
bash provision.sh
```

Ou no Windows:

```powershell
$env:SQL_ADMIN_PASSWORD = 'SuaSenh@F0rte!'
$env:JWT_SECRET = 'string-longa-aleatoria'
pwsh provision.ps1
```

O script imprime os outputs ao final.

---

## Estrutura

```
infra/
├── main.bicep                       # Entry point declarativo
├── modules/
│   ├── app-service-plan.bicep       # 1 plano Windows
│   ├── web-app-frontend.bicep       # fifa2026-web (público)
│   ├── web-app-backend.bicep        # fifa2026-back (Access Restriction pós-deploy)
│   └── sql-database.bicep           # Azure SQL + firewall AllowAllAzureServices
├── parameters/
│   └── dev.bicepparam               # Defaults: B1, Basic, eastus
├── provision.sh                     # az CLI (Linux/macOS)
├── provision.ps1                    # az CLI (Windows / PowerShell)
└── README.md
```

---

## Parâmetros

| Parâmetro | Default | Onde |
|---|---|---|
| `namingPrefix` | `fifa2026` | Bicep |
| `location` | `eastus` | Bicep |
| `appServicePlanSku` | `B1` (Basic, ~$13/mês) | Bicep |
| `sqlDatabaseSku` | `Basic` (~$5/mês) | Bicep |
| `sqlAdminLogin` | `fifa2026admin` | Bicep |
| `sqlAdminPassword` | env `SQL_ADMIN_PASSWORD` | Bicep + scripts |
| `jwtSecret` | env `JWT_SECRET` | Bicep + scripts |
| `nodeVersion` | `~18` | Bicep |

---

## Outputs

| Output | Uso |
|---|---|
| `frontendUrl` | URL pública (`https://fifa2026-web.azurewebsites.net`) |
| `backendUrl` | Input do `BACKEND_URL` no build do frontend |
| `sqlServerFqdn` | App Setting `DB_SERVER` no backend |
| `frontendOutboundIps` | Configurar Access Restriction no backend (passo 6) |

---

## Importar o `.bacpac` para o SQL provisionado

Depois do deploy, o banco está vazio. Importar o bacpac:

```bash
# Upload do bacpac para um Storage Account temporário
STORAGE_ACCT=fifa2026sa$RANDOM
az storage account create \
  -g fifa2026-rg -n $STORAGE_ACCT -l eastus --sku Standard_LRS
KEY=$(az storage account keys list -g fifa2026-rg -n $STORAGE_ACCT --query [0].value -o tsv)
az storage container create --name bacpac --account-name $STORAGE_ACCT --account-key $KEY
az storage blob upload \
  --account-name $STORAGE_ACCT --account-key $KEY \
  --container-name bacpac \
  --file ../FIFA2026Tickets.bacpac \
  --name FIFA2026Tickets.bacpac

# Importar
az sql db import \
  -g fifa2026-rg -s fifa2026-sql -n FIFA2026Tickets \
  --storage-key-type StorageAccessKey \
  --storage-key $KEY \
  --storage-uri "https://$STORAGE_ACCT.blob.core.windows.net/bacpac/FIFA2026Tickets.bacpac" \
  --admin-user fifa2026admin \
  --admin-password "$SQL_ADMIN_PASSWORD"

# Limpar
az storage account delete -g fifa2026-rg -n $STORAGE_ACCT --yes
```

---

## Destruir tudo

```bash
az group delete --name fifa2026-rg --yes --no-wait
```
