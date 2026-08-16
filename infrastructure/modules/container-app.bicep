@description('Container App name')
param name string

@description('Azure region')
param location string

@description('Container Apps Environment resource ID')
param environmentId string

@description('Container image reference')
param containerImage string

@description('Key Vault URI containing production secrets')
param keyVaultUri string

@description('User-assigned managed identity resource ID')
param apiIdentityResourceId string

@description('User-assigned managed identity client ID')
param apiIdentityClientId string

@description('Azure SQL server fully qualified domain name')
param sqlServerFqdn string

@description('Azure SQL database name')
param databaseName string

@description('Application Insights connection string')
param applicationInsightsConnectionString string

@description('Allowed CORS origin')
param corsAllowedOrigin string = ''

@description('Blob container name for installer artifacts')
param installerBlobContainerName string = 'installers'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentityResourceId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        corsPolicy: {
          allowedOrigins: empty(corsAllowedOrigin) ? ['http://localhost:4200'] : [corsAllowedOrigin, 'http://localhost:4200']
          allowedMethods: ['*']
          allowedHeaders: ['*']
          allowCredentials: true
        }
      }
      secrets: [
        { name: 'jwt-secret-key', keyVaultUrl: '${keyVaultUri}secrets/jwt-secret-key', identity: apiIdentityResourceId }
        { name: 'llm-api-key', keyVaultUrl: '${keyVaultUri}secrets/llm-api-key', identity: apiIdentityResourceId }
        { name: 'signalr-connection-string', keyVaultUrl: '${keyVaultUri}secrets/signalr-connection-string', identity: apiIdentityResourceId }
        { name: 'installer-blob-connection', keyVaultUrl: '${keyVaultUri}secrets/installer-blob-connection', identity: apiIdentityResourceId }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__DefaultConnection', value: 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${databaseName};Authentication=Active Directory Managed Identity;User Id=${apiIdentityClientId};Encrypt=True;TrustServerCertificate=False;' }
            { name: 'AZURE_CLIENT_ID', value: apiIdentityClientId }
            { name: 'KeyVault__Uri', value: keyVaultUri }
            { name: 'ApplicationInsights__ConnectionString', value: applicationInsightsConnectionString }
            { name: 'Azure__SignalR__ConnectionString', secretRef: 'signalr-connection-string' }
            { name: 'Jwt__SecretKey', secretRef: 'jwt-secret-key' }
            { name: 'Llm__ApiKey', secretRef: 'llm-api-key' }
            { name: 'LlmContext__ApiKey', secretRef: 'llm-api-key' }
            { name: 'LlmReview__ApiKey', secretRef: 'llm-api-key' }
            { name: 'Cors__AllowedOrigins__0', value: corsAllowedOrigin }
            { name: 'Installer__BlobConnectionString', secretRef: 'installer-blob-connection' }
            { name: 'Installer__BlobContainerName', value: installerBlobContainerName }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 60
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output principalId string = containerApp.identity.principalId
