# =====================================================
# FIFA 2026 - Provisionamento via az CLI (PowerShell)
# =====================================================
# Equivalente Windows-friendly ao provision.sh.
#
# Uso:
#   $env:SQL_ADMIN_PASSWORD = 'SuaSenh@Forte!'
#   $env:JWT_SECRET = 'string-longa-aleatoria'
#   pwsh provision.ps1
# =====================================================
$ErrorActionPreference = 'Stop'

# ----- Parametros -----
$RG           = $env:RG           ?? 'fifa2026-rg'
$LOCATION     = $env:LOCATION     ?? 'eastus'
$PREFIX       = $env:PREFIX       ?? 'fifa2026'
$PLAN_SKU     = $env:PLAN_SKU     ?? 'B1'
$SQL_SKU      = $env:SQL_SKU      ?? 'Basic'
$SQL_ADMIN    = $env:SQL_ADMIN    ?? 'fifa2026admin'
$SQL_DB       = $env:SQL_DB       ?? 'FIFA2026Tickets'
$NODE_VERSION = $env:NODE_VERSION ?? '~18'

$PLAN      = "$PREFIX-plan"
$WEB_FRONT = "$PREFIX-web"
$WEB_BACK  = "$PREFIX-back"
$SQL_SERVER = "$PREFIX-sql"

if (-not $env:SQL_ADMIN_PASSWORD) { throw 'SQL_ADMIN_PASSWORD nao definido' }
if (-not $env:JWT_SECRET)         { throw 'JWT_SECRET nao definido' }

Write-Host '>> Login Azure (se necessario): az login'
az account show | Out-Null

# ----- 1. Resource Group -----
Write-Host ">> 1/6  Resource Group: $RG"
az group create --name $RG --location $LOCATION -o table

# ----- 2. App Service Plan -----
Write-Host ">> 2/6  App Service Plan: $PLAN ($PLAN_SKU)"
az appservice plan create `
  --resource-group $RG `
  --name $PLAN `
  --sku $PLAN_SKU `
  --is-linux false `
  -o table

# ----- 3. Azure SQL -----
Write-Host ">> 3/6  SQL Server: $SQL_SERVER + DB: $SQL_DB ($SQL_SKU)"
az sql server create `
  --resource-group $RG `
  --name $SQL_SERVER `
  --location $LOCATION `
  --admin-user $SQL_ADMIN `
  --admin-password $env:SQL_ADMIN_PASSWORD `
  --minimal-tls-version 1.2 `
  -o table

az sql db create `
  --resource-group $RG `
  --server $SQL_SERVER `
  --name $SQL_DB `
  --service-objective $SQL_SKU `
  --collation 'SQL_Latin1_General_CP1_CI_AS' `
  -o table

az sql server firewall-rule create `
  --resource-group $RG `
  --server $SQL_SERVER `
  --name AllowAllAzureServices `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0 `
  -o table

$SQL_FQDN = az sql server show -g $RG -n $SQL_SERVER --query fullyQualifiedDomainName -o tsv

# ----- 4. Web App Backend -----
Write-Host ">> 4/6  Web App Backend: $WEB_BACK"
az webapp create `
  --resource-group $RG `
  --plan $PLAN `
  --name $WEB_BACK `
  --runtime 'node:18LTS' `
  -o table

az webapp config set `
  --resource-group $RG `
  --name $WEB_BACK `
  --ftps-state Disabled `
  --min-tls-version 1.2 `
  --http20-enabled true `
  -o table

az webapp update -g $RG -n $WEB_BACK --https-only true -o table

az webapp config appsettings set `
  --resource-group $RG `
  --name $WEB_BACK `
  --settings `
    "DB_SERVER=$SQL_FQDN" `
    'DB_PORT=1433' `
    "DB_USER=$SQL_ADMIN" `
    "DB_PASSWORD=$env:SQL_ADMIN_PASSWORD" `
    "DB_NAME=$SQL_DB" `
    "JWT_SECRET=$env:JWT_SECRET" `
    'JWT_EXPIRES_IN=7d' `
    "FRONTEND_URL=https://$WEB_FRONT.azurewebsites.net" `
    "WEBSITE_NODE_DEFAULT_VERSION=$NODE_VERSION" `
  -o table

# ----- 5. Web App Frontend -----
Write-Host ">> 5/6  Web App Frontend: $WEB_FRONT"
az webapp create `
  --resource-group $RG `
  --plan $PLAN `
  --name $WEB_FRONT `
  --runtime 'node:20LTS' `
  -o table

az webapp config set `
  --resource-group $RG `
  --name $WEB_FRONT `
  --ftps-state Disabled `
  --min-tls-version 1.2 `
  --http20-enabled true `
  -o table

az webapp update -g $RG -n $WEB_FRONT --https-only true -o table

# ----- 6. Backend privado — DESABILITADO no B1 -----
# AVISO: no App Service B1 (sem VNet Integration) o reverse proxy /api do
# IIS/ARR nao funciona, entao o frontend embute VITE_API_URL e o BROWSER
# chama o backend DIRETO. Travar o backend por allowlist dos outbound IPs
# do frontend devolveria 403 ao usuario final e quebraria o app.
# A seguranca no B1 e CORS (FRONTEND_URL) + JWT. Ver DEPLOY.md (Cenario B).
# Para privacidade de rede real: Standard+ com Private Endpoint + VNet Integration.
Write-Host '>> 6/6  Backend privado: PULADO (incompativel com B1 + VITE_API_URL).'
Write-Host '        Seguranca do backend = CORS (FRONTEND_URL) + JWT. Ver DEPLOY.md.'

# ----- Outputs -----
Write-Host ''
Write-Host '============================================================'
Write-Host '  Provisionamento concluido'
Write-Host '============================================================'
Write-Host "  Resource Group : $RG"
Write-Host "  Frontend URL   : https://$WEB_FRONT.azurewebsites.net"
Write-Host "  Backend URL    : https://$WEB_BACK.azurewebsites.net"
Write-Host "  SQL Server     : $SQL_FQDN"
Write-Host "  SQL Database   : $SQL_DB"
Write-Host '============================================================'
