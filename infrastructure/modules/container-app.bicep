@description('Container App name')
param name string

@description('Azure region')
param location string

@description('Container Apps Environment resource ID')
param environmentId string

@description('Container image reference')
param containerImage string

@description('Key Vault URI containing runtime secrets for the application')
param keyVaultUri string

@description('Allowed CORS origin')
param corsAllowedOrigin string = ''

@description('GitHub Container Registry username (GitHub actor)')
param registryUsername string

@secure()
@description('GitHub Container Registry password (PAT or GITHUB_TOKEN)')
param registryPassword string

@description('Blob container name for installer artifacts')
param installerBlobContainerName string = 'installers'

@description('Blob service URI for installer artifacts')
param installerBlobServiceUri string

@description('Use Key Vault-backed Container Apps secret references for runtime secrets')
param useKeyVaultSecretReferences bool = true

@secure()
@description('Bootstrap SQL connection string used only before Key Vault secrets are seeded')
param sqlConnectionString string = ''

@secure()
@description('Bootstrap SignalR connection string used only before Key Vault secrets are seeded')
param signalRConnectionString string = ''

@secure()
@description('Bootstrap JWT signing key used only before Key Vault secrets are seeded')
param jwtSecretKey string = ''

@secure()
@description('Bootstrap LLM API key used only before Key Vault secrets are seeded')
param llmApiKey string = ''

@secure()
@description('Bootstrap LLM review API key used only before Key Vault secrets are seeded')
param llmReviewApiKey string = ''

@secure()
@description('Bootstrap LLM context API key used only before Key Vault secrets are seeded')
param llmContextApiKey string = ''

@secure()
@description('Bootstrap Telegram bot token used only before Key Vault secrets are seeded')
param telegramBotToken string = ''

@description('Non-secret deployment stamp used to create a new revision when runtime secret source changes')
param runtimeConfigurationVersion string = ''

var keyVaultRuntimeSecrets = [
  { name: 'sql-connection-string', keyVaultUrl: '${keyVaultUri}secrets/connectionstrings--defaultconnection', identity: 'system' }
  { name: 'signalr-connection-string', keyVaultUrl: '${keyVaultUri}secrets/azure--signalr--connectionstring', identity: 'system' }
  { name: 'jwt-secret-key', keyVaultUrl: '${keyVaultUri}secrets/jwt--secret-key', identity: 'system' }
  { name: 'llm-api-key', keyVaultUrl: '${keyVaultUri}secrets/llm--api-key', identity: 'system' }
  { name: 'llm-review-api-key', keyVaultUrl: '${keyVaultUri}secrets/llm-review--api-key', identity: 'system' }
  { name: 'llm-context-api-key', keyVaultUrl: '${keyVaultUri}secrets/llm-context--api-key', identity: 'system' }
  { name: 'telegram-bot-token', keyVaultUrl: '${keyVaultUri}secrets/telegram--bot-token', identity: 'system' }
]

var bootstrapRuntimeSecrets = [
  { name: 'sql-connection-string', value: sqlConnectionString }
  { name: 'signalr-connection-string', value: signalRConnectionString }
  { name: 'jwt-secret-key', value: jwtSecretKey }
  { name: 'llm-api-key', value: llmApiKey }
  { name: 'llm-review-api-key', value: llmReviewApiKey }
  { name: 'llm-context-api-key', value: llmContextApiKey }
  { name: 'telegram-bot-token', value: telegramBotToken }
]

var runtimeSecrets = useKeyVaultSecretReferences ? keyVaultRuntimeSecrets : bootstrapRuntimeSecrets

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: 'ghcr.io'
          username: registryUsername
          passwordSecretRef: 'ghcr-password'
        }
      ]
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
      secrets: concat(runtimeSecrets, [
        { name: 'ghcr-password', value: registryPassword }
      ])
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
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'sql-connection-string' }
            { name: 'Azure__SignalR__ConnectionString', secretRef: 'signalr-connection-string' }
            { name: 'Jwt__SecretKey', secretRef: 'jwt-secret-key' }
            { name: 'Llm__ApiKey', secretRef: 'llm-api-key' }
            { name: 'LlmContext__ApiKey', secretRef: 'llm-context-api-key' }
            { name: 'LlmReview__ApiKey', secretRef: 'llm-review-api-key' }
            { name: 'Cors__AllowedOrigins__0', value: corsAllowedOrigin }
            { name: 'Telegram__BotToken', secretRef: 'telegram-bot-token' }
            { name: 'Installer__BlobServiceUri', value: installerBlobServiceUri }
            { name: 'Installer__BlobContainerName', value: installerBlobContainerName }
            { name: 'Deployment__RuntimeConfigurationVersion', value: runtimeConfigurationVersion }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 60
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
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
