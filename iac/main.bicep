targetScope = 'resourceGroup'

@description('Short application name used in resource names.')
@minLength(2)
@maxLength(12)
param appName string = 'claims'

@description('Environment name used in resource names and tags.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string = 'prod'

@description('Region for all resources except the Static Web App. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('Region for the App Service plan and API web app. Empty means the same region as everything else.')
param appServiceLocation string = ''

@description('Region for the SQL server and database. Empty means the same region as everything else; set it when Azure SQL provisioning is restricted there for your subscription.')
param sqlLocation string = ''

@description('Region for the Static Web App, which is only offered in a few regions.')
@allowed([
  'westeurope'
  'eastus2'
  'centralus'
  'westus2'
  'eastasia'
])
param staticWebAppLocation string = 'eastus2'

@description('App Service plan SKU for the API.')
@allowed([
  'B1'
  'B2'
  'S1'
  'P0v3'
  'P1v3'
])
param appServicePlanSkuName string = 'B1'

@description('Azure SQL database SKU.')
@allowed([
  'Basic'
  'S0'
  'S1'
])
param sqlDatabaseSkuName string = 'Basic'

@description('Object id of the Entra user or group that administers the SQL server. SQL logins and passwords are disabled.')
@minLength(36)
@maxLength(36)
param sqlEntraAdminObjectId string

@description('Display name of the Entra SQL administrator (user principal name or group name).')
param sqlEntraAdminLogin string

@description('Whether the Entra SQL administrator is a group (recommended) or a single user.')
@allowed([
  'Group'
  'User'
])
param sqlEntraAdminPrincipalType string = 'Group'

@description('Optional public IP allowed through the SQL firewall (e.g. to run EF migrations). Leave empty to skip.')
param clientIpAddress string = ''

var suffix = take(uniqueString(resourceGroup().id), 6)
var effectiveSqlLocation = empty(sqlLocation) ? location : sqlLocation
var sqlSuffix = take(uniqueString(resourceGroup().id, effectiveSqlLocation), 6)
var baseName = toLower('${appName}-${environmentName}')
var tags = {
  application: appName
  environment: environmentName
}

module monitoring 'modules/monitoring.bicep' = {
  name: '${deployment().name}-monitoring'
  params: {
    location: location
    logAnalyticsWorkspaceName: 'log-${baseName}'
    applicationInsightsName: 'appi-${baseName}'
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: '${deployment().name}-storage'
  params: {
    location: location
    storageAccountName: toLower('st${take(replace(appName, '-', ''), 10)}${environmentName}${suffix}')
    documentsContainerName: 'claim-documents'
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: '${deployment().name}-sql'
  params: {
    location: effectiveSqlLocation
    sqlServerName: 'sql-${baseName}-${sqlSuffix}'
    sqlDatabaseName: 'sqldb-${baseName}'
    entraAdminObjectId: sqlEntraAdminObjectId
    entraAdminLogin: sqlEntraAdminLogin
    entraAdminPrincipalType: sqlEntraAdminPrincipalType
    databaseSkuName: sqlDatabaseSkuName
    clientIpAddress: clientIpAddress
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: '${deployment().name}-keyvault'
  params: {
    location: location
    keyVaultName: 'kv-${take(baseName, 14)}-${suffix}'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: tags
  }
}

module staticWebApp 'modules/staticwebapp.bicep' = {
  name: '${deployment().name}-staticwebapp'
  params: {
    location: staticWebAppLocation
    staticWebAppName: 'stapp-${baseName}-${suffix}'
    tags: tags
  }
}

module appService 'modules/appservice.bicep' = {
  name: '${deployment().name}-appservice'
  params: {
    location: empty(appServiceLocation) ? location : appServiceLocation
    appServicePlanName: 'asp-${baseName}'
    webAppName: 'app-${baseName}-api-${suffix}'
    appServicePlanSkuName: appServicePlanSkuName
    sqlConnectionString: 'Server=tcp:${sql.outputs.sqlServerFullyQualifiedDomainName},1433;Database=${sql.outputs.sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    storageBlobServiceUri: storage.outputs.blobServiceUri
    applicationInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
    frontendOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: tags
  }
}

module sqlFirewall 'modules/sqlfirewall.bicep' = {
  name: '${deployment().name}-sqlfirewall'
  params: {
    sqlServerName: sql.outputs.sqlServerName
    webAppOutboundIpAddresses: appService.outputs.possibleOutboundIpAddresses
  }
}

module roleAssignments 'modules/roleassignments.bicep' = {
  name: '${deployment().name}-roleassignments'
  params: {
    principalId: appService.outputs.principalId
    keyVaultName: keyVault.outputs.keyVaultName
    storageAccountName: storage.outputs.storageAccountName
  }
}

output apiUrl string = 'https://${appService.outputs.defaultHostname}'
output frontendUrl string = 'https://${staticWebApp.outputs.defaultHostname}'
output webAppName string = appService.outputs.webAppName
output webAppPrincipalId string = appService.outputs.principalId
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output sqlServerName string = sql.outputs.sqlServerName
output sqlServerFullyQualifiedDomainName string = sql.outputs.sqlServerFullyQualifiedDomainName
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output keyVaultName string = keyVault.outputs.keyVaultName
output storageAccountName string = storage.outputs.storageAccountName
