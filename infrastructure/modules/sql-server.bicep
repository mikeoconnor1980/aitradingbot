@description('SQL Server name')
param serverName string

@description('Database name')
param databaseName string

@description('Azure region')
param location string

@description('SQL admin login')
param adminLogin string

@secure()
@description('SQL admin password')
param adminPassword string

@description('Optional Microsoft Entra administrator object ID for SQL passwordless access planning')
param entraAdminObjectId string = ''

@description('Optional Microsoft Entra administrator login name or group display name')
param entraAdminLogin string = ''

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
  }
}

resource sqlServerEntraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = if (!empty(entraAdminObjectId) && !empty(entraAdminLogin)) {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: entraAdminLogin
    sid: entraAdminObjectId
    tenantId: subscription().tenantId
  }
}

// Allow Azure services (Container Apps) to access the database
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2 GB
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output entraAdminConfigured bool = !empty(entraAdminObjectId) && !empty(entraAdminLogin)
