@description('Container App name')
param name string

@description('Azure region')
param location string

@description('Container Apps Environment resource ID')
param environmentId string

@description('Container image reference')
param containerImage string

@secure()
@description('Azure SQL connection string')
param sqlConnectionString string

@secure()
@description('Azure SignalR connection string')
param signalRConnectionString string

@secure()
@description('JWT signing key')
param jwtSecretKey string

@secure()
@description('LLM API key (Gemini)')
param llmApiKey string = ''

@description('Allowed CORS origin')
param corsAllowedOrigin string = ''

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
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
        { name: 'sql-connection-string', value: sqlConnectionString }
        { name: 'signalr-connection-string', value: signalRConnectionString }
        { name: 'jwt-secret-key', value: jwtSecretKey }
        { name: 'llm-api-key', value: llmApiKey }
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
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'sql-connection-string' }
            { name: 'Azure__SignalR__ConnectionString', secretRef: 'signalr-connection-string' }
            { name: 'Jwt__SecretKey', secretRef: 'jwt-secret-key' }
            { name: 'LlmContext__ApiKey', secretRef: 'llm-api-key' }
            { name: 'LlmReview__ApiKey', secretRef: 'llm-api-key' }
            { name: 'Cors__AllowedOrigins__0', value: corsAllowedOrigin }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/healthz'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
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
