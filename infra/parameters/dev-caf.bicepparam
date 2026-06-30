// =====================================================
// Parameters — ambiente "dev" com nomenclatura CAF
// (Cloud Adoption Framework) na região brazilsouth.
// Tenant/subscription: TFTEC — Microsoft Partner - Produção.
// =====================================================
// Uso:
//   $env:SQL_ADMIN_PASSWORD = '...'
//   $env:JWT_SECRET = '...'
//   az deployment group create \
//     --resource-group rg-fifa2026-dev-brs-001 \
//     --template-file main.bicep \
//     --parameters parameters/dev-caf.bicepparam
// =====================================================
using '../main.bicep'

param location = 'brazilsouth'
param appServicePlanSku = 'B1'
param sqlDatabaseSku = 'Basic'
param sqlAdminLogin = 'fifa2026admin'
param sqlDatabaseName = 'FIFA2026Tickets'
param nodeVersion = '~18'

// ----- Nomenclatura CAF: <tipo>-<workload>-<ambiente>-<região>-<instância> -----
param planName = 'asp-fifa2026-dev-brs-001'
param webFrontName = 'app-fifa2026-web-dev-brs-001'
param webBackName = 'app-fifa2026-api-dev-brs-001'
param sqlServerName = 'sql-fifa2026-dev-brs-001'

// Senhas via variáveis de ambiente — não commitar valores!
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', 'TROQUE_AQUI_OU_VIA_ENV')
param jwtSecret = readEnvironmentVariable('JWT_SECRET', 'TROQUE_AQUI_OU_VIA_ENV')
