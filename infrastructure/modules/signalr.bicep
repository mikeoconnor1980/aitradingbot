@description('SignalR Service name')
param name string

@description('Azure region')
param location string

@description('Allowed CORS origins for the SignalR service')
param allowedOrigins array = []

@description('SignalR SKU name')
@allowed([
  'Free_F1'
  'Standard_S1'
])
param skuName string = 'Standard_S1'

var skuTier = skuName == 'Free_F1' ? 'Free' : 'Standard'

resource signalr 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: name
  location: location
  sku: {
    name: skuName
    tier: skuTier
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
      allowedOrigins: length(allowedOrigins) == 0 ? ['http://localhost:4200'] : allowedOrigins
    }
  }
}

output id string = signalr.id
output hostName string = signalr.properties.hostName
