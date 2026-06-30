// =====================================================
// FIFA 2026 Tickets — Azure Infrastructure (main)
// =====================================================
// Provisiona:
//   - 1 App Service Plan (Windows)
//   - 1 Web App público (frontend, fifa2026-web)
//   - 1 Web App privado (backend, fifa2026-back)
//   - 1 Azure SQL Server + Database (FIFA2026Tickets)
//
// Outputs:
//   - frontendUrl       — URL pública do front (https://...)
//   - backendUrl        — URL do back (input do BACKEND_URL no build do front)
//   - sqlServerFqdn     — input do DB_SERVER no App Settings do back
//   - frontendOutboundIps — usar para criar Access Restriction no back
// =====================================================

targetScope = 'resourceGroup'

// ----------------------------------------------------
// Parameters
// ----------------------------------------------------
@description('Prefixo usado em todos os nomes de recurso.')
param namingPrefix string = 'fifa2026'

@description('Região Azure.')
param location string = 'eastus'

@description('SKU do App Service Plan. B1 = Basic (~$13/mês).')
@allowed([ 'B1', 'B2', 'S1', 'S2', 'P1v3' ])
param appServicePlanSku string = 'B1'

@description('SKU da Azure SQL Database.')
@allowed([ 'Basic', 'S0', 'S1', 'S2' ])
param sqlDatabaseSku string = 'Basic'

@description('Login admin do SQL Server.')
param sqlAdminLogin string = 'fifa2026admin'

@description('Senha do admin do SQL Server. Use Key Vault em produção.')
@secure()
param sqlAdminPassword string

@description('Nome do banco que será criado.')
param sqlDatabaseName string = 'FIFA2026Tickets'

@description('JWT secret para o backend.')
@secure()
param jwtSecret string

@description('Versão do Node no Web App (formato: ~18, ~20).')
param nodeVersion string = '~18'

// ----------------------------------------------------
// Naming
// ----------------------------------------------------
// Os nomes são params com defaults no padrão legado (`${namingPrefix}-x`)
// para manter compatibilidade. Para Cloud Adoption Framework (CAF),
// faça override via bicepparam (ver parameters/dev-caf.bicepparam).
@description('Nome do App Service Plan. Default = padrão legado.')
param planName string = '${namingPrefix}-plan'

@description('Nome do Web App frontend. Default = padrão legado.')
param webFrontName string = '${namingPrefix}-web'

@description('Nome do Web App backend. Default = padrão legado.')
param webBackName string = '${namingPrefix}-back'

@description('Nome do SQL Server. Default = padrão legado.')
param sqlServerName string = '${namingPrefix}-sql'

// ----------------------------------------------------
// Modules
// ----------------------------------------------------
module plan 'modules/app-service-plan.bicep' = {
  name: 'planDeploy'
  params: {
    name: planName
    location: location
    sku: appServicePlanSku
  }
}

module sql 'modules/sql-database.bicep' = {
  name: 'sqlDeploy'
  params: {
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    skuName: sqlDatabaseSku
  }
}

module backend 'modules/web-app-backend.bicep' = {
  name: 'backendDeploy'
  params: {
    name: webBackName
    location: location
    planId: plan.outputs.planId
    nodeVersion: nodeVersion
    appSettings: [
      { name: 'DB_SERVER',                  value: sql.outputs.serverFqdn }
      { name: 'DB_PORT',                    value: '1433' }
      { name: 'DB_USER',                    value: sqlAdminLogin }
      { name: 'DB_PASSWORD',                value: sqlAdminPassword }
      { name: 'DB_NAME',                    value: sqlDatabaseName }
      { name: 'JWT_SECRET',                 value: jwtSecret }
      { name: 'JWT_EXPIRES_IN',             value: '7d' }
      { name: 'FRONTEND_URL',               value: 'https://${webFrontName}.azurewebsites.net' }
      { name: 'WEBSITE_NODE_DEFAULT_VERSION', value: nodeVersion }
    ]
  }
}

module frontend 'modules/web-app-frontend.bicep' = {
  name: 'frontendDeploy'
  params: {
    name: webFrontName
    location: location
    planId: plan.outputs.planId
  }
}

// ----------------------------------------------------
// Outputs
// ----------------------------------------------------
output frontendUrl string = 'https://${frontend.outputs.defaultHostName}'
output backendUrl string = 'https://${backend.outputs.defaultHostName}'
output sqlServerFqdn string = sql.outputs.serverFqdn
output frontendOutboundIps string = frontend.outputs.possibleOutboundIps
output resourceGroupName string = resourceGroup().name
