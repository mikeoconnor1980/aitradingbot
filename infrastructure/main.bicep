// ---------- Parameters ----------
@description('Environment name (e.g. prod, dev)')
@allowed(['dev', 'prod'])
param environmentName string = 'prod'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for all resources')
param appName string = 'tradepilot'

@description('Container image (e.g. ghcr.io/owner/tradepilot-api:latest)')
param containerImage string

@description('Azure SQL administrator login')
param sqlAdminLogin string = 'tradepilotadmin'

@secure()
@description('Azure SQL administrator password')
param sqlAdminPassword string

@description('Allowed CORS origin (Azure Static Web App URL)')
param corsAllowedOrigin string = ''

@description('Fixed public IPv4 address of the VPS-hosted Worker. Leave empty until available.')
param workerPublicIpAddress string = ''

@description('Deploy the API Container App after Key Vault secrets have been populated.')
param deployApi bool = true

// ---------- Modules ----------

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  params: {
    name: '${appName}-${environmentName}-logs'
    location: location
  }
}

module signalr 'modules/signalr.bicep' = {
  name: 'signalr'
  params: {
    name: '${appName}-${environmentName}-signalr'
    location: location
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    name: '${appName}-${environmentName}-insights'
    location: location
    workspaceId: logAnalytics.outputs.workspaceId
  }
}

module apiIdentity 'modules/managed-identity.bicep' = {
  name: 'api-identity'
  params: {
    name: '${appName}-${environmentName}-api-id'
    location: location
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: '${appName}-${environmentName}-kv'
    location: location
    apiIdentityResourceId: apiIdentity.outputs.resourceId
    apiIdentityPrincipalId: apiIdentity.outputs.principalId
  }
}

module sql 'modules/sql-server.bicep' = {
  name: 'sql'
  params: {
    serverName: '${appName}-${environmentName}-sql'
    databaseName: '${appName}-db'
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    workerPublicIpAddress: workerPublicIpAddress
  }
}

module storage 'modules/storage-account.bicep' = {
  name: 'storage'
  params: {
    name: replace('${appName}${environmentName}sa', '-', '')
    location: location
  }
}

module containerAppEnv 'modules/container-app-environment.bicep' = {
  name: 'container-app-env'
  params: {
    name: '${appName}-${environmentName}-env'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// Reference the deployed storage account to build connection string without exposing keys in module outputs

module containerApp 'modules/container-app.bicep' = if (deployApi) {
  name: 'container-app'
  params: {
    name: '${appName}-${environmentName}-api'
    location: location
    environmentId: containerAppEnv.outputs.environmentId
    containerImage: containerImage
    sqlServerFqdn: sql.outputs.serverFqdn
    databaseName: '${appName}-db'
    keyVaultUri: keyVault.outputs.vaultUri
    apiIdentityResourceId: apiIdentity.outputs.resourceId
    apiIdentityClientId: apiIdentity.outputs.clientId
    applicationInsightsConnectionString: monitoring.outputs.connectionString
    corsAllowedOrigin: corsAllowedOrigin
    installerBlobContainerName: storage.outputs.containerName
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  params: {
    name: '${appName}-${environmentName}-ui'
    location: 'westeurope' // SWA not available in uksouth
  }
}

// ---------- Outputs ----------

output apiUrl string = deployApi ? containerApp!.outputs.fqdn : ''
output staticWebAppUrl string = staticWebApp.outputs.defaultHostname
output signalRHostName string = signalr.outputs.hostName
output sqlServerFqdn string = sql.outputs.serverFqdn
output storageAccountName string = storage.outputs.storageAccountName
output keyVaultUri string = keyVault.outputs.vaultUri
output applicationInsightsConnectionString string = monitoring.outputs.connectionString
output apiIdentityClientId string = apiIdentity.outputs.clientId
