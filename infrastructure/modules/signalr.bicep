@description('SignalR Service name')
param name string

@description('Azure region')
param location string

resource signalr 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: name
  location: location
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  kind: 'SignalR'
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'True'
      }
    ]
    cors: {
      allowedOrigins: ['*']
    }
  }
}

output hostName string = signalr.properties.hostName

#disable-next-line outputs-should-not-contain-secrets
output connectionString string = signalr.listKeys().primaryConnectionString
