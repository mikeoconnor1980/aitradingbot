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

@description('GitHub Container Registry username')
param registryUsername string

@secure()
@description('GitHub Container Registry password (PAT)')
param registryPassword string

@description('Use Key Vault-backed Container Apps secret references for runtime secrets')
param useKeyVaultSecretReferences bool = true

@secure()
@description('Bootstrap JWT signing key used only before Key Vault secrets are seeded')
param jwtSecretKey string = ''

@secure()
@description('Bootstrap Gemini API key used only before Key Vault secrets are seeded')
param llmApiKey string = ''

@secure()
@description('Bootstrap Telegram bot token used only before Key Vault secrets are seeded')
param telegramBotToken string = ''

@description('Non-secret deployment stamp used to create a new Container App revision')
param runtimeConfigurationVersion string = ''

@description('Optional object ID of the deployment principal that seeds Key Vault secrets')
param deploymentPrincipalObjectId string = ''

@description('Optional Microsoft Entra administrator object ID for Azure SQL passwordless access planning')
param sqlEntraAdminObjectId string = ''

@description('Optional Microsoft Entra administrator login name or group display name for Azure SQL passwordless access planning')
param sqlEntraAdminLogin string = ''

var resourceTags = {
  Environment: environmentName
  Application: appName
  ManagedBy: 'bicep'
}

var keyVaultName = '${appName}-${environmentName}-kv'
var apiContainerAppName = '${appName}-${environmentName}-api'
var signalRName = '${appName}-${environmentName}-signalr'
var storageAccountName = replace('${appName}${environmentName}sa', '-', '')

var keyVaultSecretsUserRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var keyVaultSecretsOfficerRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')
var storageBlobDataReaderRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1')
var storageBlobDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

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
    name: signalRName
    location: location
    allowedOrigins: empty(corsAllowedOrigin) ? [] : [corsAllowedOrigin]
    skuName: environmentName == 'prod' ? 'Standard_S1' : 'Standard_S1'
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
    entraAdminObjectId: sqlEntraAdminObjectId
    entraAdminLogin: sqlEntraAdminLogin
  }
}

module storage 'modules/storage-account.bicep' = {
  name: 'storage'
  params: {
    name: storageAccountName
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

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: keyVaultName
    location: location
    tags: resourceTags
  }
}

resource keyVaultResource 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

var bootstrapSqlConnectionString = useKeyVaultSecretReferences ? '' : 'Server=tcp:${sql.outputs.serverFqdn},1433;Database=${appName}-db;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var bootstrapSignalRConnectionString = useKeyVaultSecretReferences ? '' : listKeys(resourceId('Microsoft.SignalRService/signalR', signalRName), '2024-03-01').primaryConnectionString

module containerApp 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    name: apiContainerAppName
    location: location
    environmentId: containerAppEnv.outputs.environmentId
    containerImage: containerImage
    keyVaultUri: keyVault.outputs.vaultUri
    corsAllowedOrigin: corsAllowedOrigin
    registryUsername: registryUsername
    registryPassword: registryPassword
    installerBlobContainerName: storage.outputs.containerName
    installerBlobServiceUri: storage.outputs.blobServiceUri
    useKeyVaultSecretReferences: useKeyVaultSecretReferences
    sqlConnectionString: bootstrapSqlConnectionString
    signalRConnectionString: bootstrapSignalRConnectionString
    jwtSecretKey: useKeyVaultSecretReferences ? '' : jwtSecretKey
    llmApiKey: useKeyVaultSecretReferences ? '' : llmApiKey
    llmReviewApiKey: useKeyVaultSecretReferences ? '' : llmApiKey
    llmContextApiKey: useKeyVaultSecretReferences ? '' : llmApiKey
    telegramBotToken: useKeyVaultSecretReferences ? '' : telegramBotToken
    runtimeConfigurationVersion: runtimeConfigurationVersion
  }
}

resource storageAccountResource 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource apiKeyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVaultResource
  name: guid(keyVaultName, apiContainerAppName, keyVaultSecretsUserRoleDefinitionId)
  properties: {
    principalId: containerApp.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsUserRoleDefinitionId
  }
}

resource apiStorageBlobDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccountResource
  name: guid(storageAccountName, apiContainerAppName, storageBlobDataReaderRoleDefinitionId)
  properties: {
    principalId: containerApp.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataReaderRoleDefinitionId
  }
}

resource deploymentPrincipalSecretsOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deploymentPrincipalObjectId)) {
  scope: keyVaultResource
  name: guid(keyVaultName, deploymentPrincipalObjectId, keyVaultSecretsOfficerRoleDefinitionId)
  properties: {
    principalId: deploymentPrincipalObjectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: keyVaultSecretsOfficerRoleDefinitionId
  }
}

resource deploymentPrincipalStorageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deploymentPrincipalObjectId)) {
  scope: storageAccountResource
  name: guid(storageAccountName, deploymentPrincipalObjectId, storageBlobDataContributorRoleDefinitionId)
  properties: {
    principalId: deploymentPrincipalObjectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: storageBlobDataContributorRoleDefinitionId
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

output apiUrl string = containerApp.outputs.fqdn
output staticWebAppUrl string = staticWebApp.outputs.defaultHostname
output signalRName string = signalRName
output signalRHostName string = signalr.outputs.hostName
output sqlServerFqdn string = sql.outputs.serverFqdn
output storageAccountName string = storage.outputs.storageAccountName
output installerBlobServiceUri string = storage.outputs.blobServiceUri
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.vaultUri
output sqlEntraAdminConfigured bool = sql.outputs.entraAdminConfigured
