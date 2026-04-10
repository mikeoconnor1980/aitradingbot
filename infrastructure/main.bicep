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

@secure()
@description('JWT signing key for API authentication')
param jwtSecretKey string

@secure()
@description('Gemini API key for LLM context provider')
param llmApiKey string = ''

@description('Allowed CORS origin (Azure Static Web App URL)')
param corsAllowedOrigin string = ''

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

module sql 'modules/sql-server.bicep' = {
  name: 'sql'
  params: {
    serverName: '${appName}-${environmentName}-sql'
    databaseName: '${appName}-db'
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
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

module containerApp 'modules/container-app.bicep' = {
  name: 'container-app'
  params: {
    name: '${appName}-${environmentName}-api'
    location: location
    environmentId: containerAppEnv.outputs.environmentId
    containerImage: containerImage
    sqlConnectionString: sql.outputs.connectionString
    signalRConnectionString: signalr.outputs.connectionString
    jwtSecretKey: jwtSecretKey
    llmApiKey: llmApiKey
    corsAllowedOrigin: corsAllowedOrigin
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  params: {
    name: '${appName}-${environmentName}-ui'
    location: location
  }
}

// ---------- Outputs ----------

output apiUrl string = containerApp.outputs.fqdn
output staticWebAppUrl string = staticWebApp.outputs.defaultHostname
output signalRHostName string = signalr.outputs.hostName
output sqlServerFqdn string = sql.outputs.serverFqdn
