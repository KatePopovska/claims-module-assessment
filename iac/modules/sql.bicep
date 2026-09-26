@description('Azure region for the SQL server.')
param location string

@description('Globally unique logical SQL server name.')
param sqlServerName string

@description('Database name.')
param sqlDatabaseName string

@description('Object id of the Entra user or group that administers the SQL server.')
@minLength(36)
@maxLength(36)
param entraAdminObjectId string

@description('Display name of the Entra SQL administrator (user principal name or group name).')
param entraAdminLogin string

@description('Whether the Entra SQL administrator is a group or a single user.')
@allowed([
  'Group'
  'User'
])
param entraAdminPrincipalType string

@description('Database SKU: Basic (5 DTU), S0 or S1.')
@allowed([
  'Basic'
  'S0'
  'S1'
])
param databaseSkuName string

@description('Optional public IP allowed through the SQL firewall, e.g. to run EF migrations from a workstation. Leave empty to skip.')
param clientIpAddress string

@description('Log Analytics workspace that receives the SQL audit log.')
param logAnalyticsWorkspaceId string

param tags object

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: entraAdminLogin
      sid: entraAdminObjectId
      tenantId: subscription().tenantId
      principalType: entraAdminPrincipalType
    }
  }
}

resource entraAdministrator 'Microsoft.Sql/servers/administrators@2025-01-01' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: entraAdminLogin
    sid: entraAdminObjectId
    tenantId: subscription().tenantId
  }
}

resource entraOnlyAuthentication 'Microsoft.Sql/servers/azureADOnlyAuthentications@2025-01-01' = {
  parent: sqlServer
  name: 'Default'
  properties: {
    azureADOnlyAuthentication: true
  }
  dependsOn: [
    entraAdministrator
  ]
}

resource allowClientIp 'Microsoft.Sql/servers/firewallRules@2025-01-01' = if (!empty(clientIpAddress)) {
  parent: sqlServer
  name: 'AllowDeploymentClient'
  properties: {
    startIpAddress: clientIpAddress
    endIpAddress: clientIpAddress
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2025-01-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: databaseSkuName
    tier: databaseSkuName == 'Basic' ? 'Basic' : 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}

resource masterDatabase 'Microsoft.Sql/servers/databases@2025-01-01' existing = {
  parent: sqlServer
  name: 'master'
}

#disable-next-line use-recent-api-versions
resource auditDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'sql-security-audit'
  scope: masterDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
    ]
  }
}

resource auditingSettings 'Microsoft.Sql/servers/auditingSettings@2025-01-01' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
  dependsOn: [
    auditDiagnostics
  ]
}

output sqlServerName string = sqlServer.name
output sqlServerFullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
