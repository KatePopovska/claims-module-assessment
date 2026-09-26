@description('Logical SQL server to add the firewall rules to.')
param sqlServerName string

@description('Comma-separated outbound IP addresses of the web app (possibleOutboundIpAddresses).')
param webAppOutboundIpAddresses string

var outboundIpAddresses = union(split(webAppOutboundIpAddresses, ','), [])

resource sqlServer 'Microsoft.Sql/servers@2025-01-01' existing = {
  name: sqlServerName
}

resource webAppOutboundRules 'Microsoft.Sql/servers/firewallRules@2025-01-01' = [
  for ipAddress in outboundIpAddresses: {
    parent: sqlServer
    name: 'AllowWebApp-${replace(trim(ipAddress), '.', '-')}'
    properties: {
      startIpAddress: trim(ipAddress)
      endIpAddress: trim(ipAddress)
    }
  }
]
