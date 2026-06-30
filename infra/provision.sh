#!/usr/bin/env bash
# =====================================================
# FIFA 2026 — Provisionamento via az CLI (didático)
# =====================================================
# Equivalente imperativo ao main.bicep. Mais fácil de
# acompanhar passo-a-passo durante o evento.
#
# Uso:
#   export SQL_ADMIN_PASSWORD='SuaSenh@Forte!'
#   export JWT_SECRET='string-longa-aleatoria'
#   bash provision.sh
# =====================================================
set -euo pipefail

# ----- Parâmetros (override via env) -----
RG=${RG:-fifa2026-rg}
LOCATION=${LOCATION:-eastus}
PREFIX=${PREFIX:-fifa2026}
PLAN_SKU=${PLAN_SKU:-B1}
SQL_SKU=${SQL_SKU:-Basic}
SQL_ADMIN=${SQL_ADMIN:-fifa2026admin}
SQL_DB=${SQL_DB:-FIFA2026Tickets}
NODE_VERSION=${NODE_VERSION:-~18}

PLAN="${PREFIX}-plan"
WEB_FRONT="${PREFIX}-web"
WEB_BACK="${PREFIX}-back"
SQL_SERVER="${PREFIX}-sql"

# ----- Validações -----
: "${SQL_ADMIN_PASSWORD:?SQL_ADMIN_PASSWORD não definido}"
: "${JWT_SECRET:?JWT_SECRET não definido}"

echo ">> Login Azure (se necessário): az login"
az account show >/dev/null

# ----- 1. Resource Group -----
echo ">> 1/6  Resource Group: $RG"
az group create --name "$RG" --location "$LOCATION" -o table

# ----- 2. App Service Plan -----
echo ">> 2/6  App Service Plan: $PLAN ($PLAN_SKU)"
az appservice plan create \
  --resource-group "$RG" \
  --name "$PLAN" \
  --sku "$PLAN_SKU" \
  --is-linux false \
  -o table

# ----- 3. Azure SQL -----
echo ">> 3/6  SQL Server: $SQL_SERVER + DB: $SQL_DB ($SQL_SKU)"
az sql server create \
  --resource-group "$RG" \
  --name "$SQL_SERVER" \
  --location "$LOCATION" \
  --admin-user "$SQL_ADMIN" \
  --admin-password "$SQL_ADMIN_PASSWORD" \
  --minimal-tls-version 1.2 \
  -o table

az sql db create \
  --resource-group "$RG" \
  --server "$SQL_SERVER" \
  --name "$SQL_DB" \
  --service-objective "$SQL_SKU" \
  --collation 'SQL_Latin1_General_CP1_CI_AS' \
  -o table

# Permite Web Apps Azure acessarem o SQL
az sql server firewall-rule create \
  --resource-group "$RG" \
  --server "$SQL_SERVER" \
  --name AllowAllAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  -o table

SQL_FQDN=$(az sql server show -g "$RG" -n "$SQL_SERVER" --query fullyQualifiedDomainName -o tsv)

# ----- 4. Web App Backend -----
echo ">> 4/6  Web App Backend: $WEB_BACK"
az webapp create \
  --resource-group "$RG" \
  --plan "$PLAN" \
  --name "$WEB_BACK" \
  --runtime "node:18LTS" \
  -o table

az webapp config set \
  --resource-group "$RG" \
  --name "$WEB_BACK" \
  --ftps-state Disabled \
  --min-tls-version 1.2 \
  --http20-enabled true \
  -o table

az webapp update -g "$RG" -n "$WEB_BACK" --https-only true -o table

az webapp config appsettings set \
  --resource-group "$RG" \
  --name "$WEB_BACK" \
  --settings \
    DB_SERVER="$SQL_FQDN" \
    DB_PORT=1433 \
    DB_USER="$SQL_ADMIN" \
    DB_PASSWORD="$SQL_ADMIN_PASSWORD" \
    DB_NAME="$SQL_DB" \
    JWT_SECRET="$JWT_SECRET" \
    JWT_EXPIRES_IN=7d \
    FRONTEND_URL="https://${WEB_FRONT}.azurewebsites.net" \
    WEBSITE_NODE_DEFAULT_VERSION="$NODE_VERSION" \
  -o table

# ----- 5. Web App Frontend -----
echo ">> 5/6  Web App Frontend: $WEB_FRONT"
az webapp create \
  --resource-group "$RG" \
  --plan "$PLAN" \
  --name "$WEB_FRONT" \
  --runtime "node:20LTS" \
  -o table

az webapp config set \
  --resource-group "$RG" \
  --name "$WEB_FRONT" \
  --ftps-state Disabled \
  --min-tls-version 1.2 \
  --http20-enabled true \
  -o table

az webapp update -g "$RG" -n "$WEB_FRONT" --https-only true -o table

# ----- 6. Backend privado — DESABILITADO no B1 -----
# AVISO: no App Service B1 (sem VNet Integration) o reverse proxy /api do
# IIS/ARR não funciona, então o frontend embute VITE_API_URL e o BROWSER
# chama o backend DIRETO. Travar o backend por allowlist dos outbound IPs
# do frontend devolveria 403 ao usuário final e quebraria o app.
# Segurança no B1 = CORS (FRONTEND_URL) + JWT. Ver DEPLOY.md (Cenário B).
# Privacidade de rede real: Standard+ com Private Endpoint + VNet Integration.
echo ">> 6/6  Backend privado: PULADO (incompatível com B1 + VITE_API_URL)."
echo "        Segurança do backend = CORS (FRONTEND_URL) + JWT. Ver DEPLOY.md."

# ----- Outputs -----
echo
echo "============================================================"
echo "  Provisionamento concluído"
echo "============================================================"
echo "  Resource Group : $RG"
echo "  Frontend URL   : https://${WEB_FRONT}.azurewebsites.net"
echo "  Backend URL    : https://${WEB_BACK}.azurewebsites.net"
echo "  SQL Server     : $SQL_FQDN"
echo "  SQL Database   : $SQL_DB"
echo "============================================================"
echo
echo "Próximos passos:"
echo "  1. Importar FIFA2026Tickets.bacpac no Azure SQL"
echo "  2. cd Lovable/World\\ Cup\\ Tickets\\ Hub && VITE_API_URL=https://${WEB_BACK}.azurewebsites.net/api BACKEND_URL=https://${WEB_BACK}.azurewebsites.net npm run build"
echo "  3. Deploy do dist/ no $WEB_FRONT (via GitHub Actions ou az webapp deploy)"
echo "  4. Deploy do fifa2026-api/ no $WEB_BACK (via GitHub Actions)"
