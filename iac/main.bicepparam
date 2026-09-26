using './main.bicep'

param appName = 'claims'
param environmentName = 'prod'
param appServiceLocation = 'centralus'
param appServicePlanSkuName = 'B1'
param sqlLocation = 'centralus'
param sqlDatabaseSkuName = 'Basic'
param staticWebAppLocation = 'eastus2'
param sqlEntraAdminObjectId = readEnvironmentVariable('SQL_ENTRA_ADMIN_OBJECT_ID')
param sqlEntraAdminLogin = readEnvironmentVariable('SQL_ENTRA_ADMIN_LOGIN')
param sqlEntraAdminPrincipalType = 'Group'
param clientIpAddress = readEnvironmentVariable('CLIENT_IP_ADDRESS', '')
