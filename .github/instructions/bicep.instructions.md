---
applyTo: "**/*.bicep,**/infrastructure/**"
---

# Azure Bicep Guidelines

Infrastructure-as-Code for Azure resources using Bicep templates.

## File Structure

<!-- DOT-CUSTOM-START:bicep-file-structure -->
```
pipelines/infrastructure/
├── azuredeploy.bicep          # Main template
└── parameters/
    ├── dev.parameters.json
    ├── qa.parameters.json
    ├── stg.parameters.json
    └── prod.parameters.json
```
<!-- DOT-CUSTOM-END:bicep-file-structure -->

## Parameter Definitions

Use appropriate decorators and constraints:

```bicep
@allowed([
  'loc'
  'dev'
  'qa'
  'stg'
  'prod'
])
param environmentName string

@description('The base URL for the application')
param baseUrl string

@secure()
param databaseAdminPassword string

param location string = resourceGroup().location

param logAnalyticsRetentionInDays int = 30
```

## Naming Conventions

Follow consistent naming patterns:

```bicep
var appName = 'dts-morph'

// Resources follow pattern: {appName}-{environment}-{resourceType}
resource serviceBus 'Microsoft.ServiceBus/namespaces@2017-04-01' = {
  name: '${appName}-${environmentName}-bus'
  // ...
}

resource keyVault 'Microsoft.KeyVault/vaults@2018-02-14' = {
  name: '${appName}-${environmentName}-kv'
  // ...
}

resource sqlServer 'Microsoft.Sql/servers@2020-11-01-preview' = {
  name: '${appName}-${environmentName}-sql'
  // ...
}
```

## Resource Tags

Apply consistent tags to all resources:

```bicep
var isProduction = (environmentName == 'prod' || endsWith(environmentName, '-prod'))

var resourceTags = {
  DTSEnvironment: environmentDescription
  MEMBERFIRM: 'UK'
  COUNTRY: 'UK'
  FUNCTION: 'Tax'
  SUBFUNCTION: 'DTS'
  'APPID Service ID': 'DTS.UKCT.Efiling'
  ENVIRONMENT: (isProduction ? 'Production' : 'Non-Prod')
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  tags: resourceTags
  name: '${appName}-${environmentName}-ai'
  location: location
  // ...
}
```

## Key Vault Secrets

Key Vault is used to store ALL configuration values for UK CT, not just sensitive ones:

```bicep
var keyVaultValues = [
  {
    name: 'Database--ConnectionString'
    value: databaseConnectionString
  }
  {
    name: 'IdentityServer--ClientId'
    value: identityServerAudience
  }
  {
    name: 'EventBus--PublishTopic'
    value: topicName
  }
]

resource keyVaultSecrets 'Microsoft.KeyVault/vaults/secrets@2018-02-14' = [
  for secret in keyVaultValues: {
    name: '${keyVault.name}/${secret.name}'
    properties: {
      value: secret.value
    }
  }
]
```

## Service Bus Configuration

```bicep
var topicName = 'events'

resource serviceBus 'Microsoft.ServiceBus/namespaces@2017-04-01' = {
  name: '${appName}-${environmentName}-bus'
  tags: resourceTags
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    minimalTlsVersion: '1.3'
  }
}

resource serviceBusTopic 'Microsoft.ServiceBus/namespaces/topics@2017-04-01' = {
  name: '${serviceBus.name}/${topicName}'
}
```

## Key Vault Network Rules

```bicep
resource keyVault 'Microsoft.KeyVault/vaults@2018-02-14' = {
  name: '${appName}-${environmentName}-kv'
  tags: resourceTags
  location: location
  properties: {
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    tenantId: subscription().tenantId
    accessPolicies: accessPolicies
    sku: {
      name: 'standard'
      family: 'A'
    }
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: [
        for subnetId in keyVaultVnetSubnets: {
          id: subnetId
        }
      ]
    }
  }
}
```

## Conditional Deployment

```bicep
var isLocal = (environmentName == 'loc')
var isSecondaryRegion = contains(environmentName, 'bcp')

// Skip database deployment for local
resource sqlServer 'Microsoft.Sql/servers@2020-11-01-preview' = if (!isLocal) {
  // ...
}

// Skip for secondary failover region
resource sqlDatabase 'Microsoft.Sql/servers/databases@2020-11-01-preview' = if (!isLocal && !isSecondaryRegion) {
  // ...
}
```

## Modules

Create reusable modules in `modules/` folder:

```bicep
// modules/azure-sql-failover.bicep
param sqlFailoverGroupName string
param sqlServerPrimaryName string
param sqlServerSecondaryId string
param databases array

resource failoverGroup 'Microsoft.Sql/servers/failoverGroups@2020-11-01-preview' = {
  name: '${sqlServerPrimaryName}/${sqlFailoverGroupName}'
  properties: {
    partnerServers: [
      {
        id: sqlServerSecondaryId
      }
    ]
    databases: [for db in databases: resourceId('Microsoft.Sql/servers/databases', sqlServerPrimaryName, db)]
    readWriteEndpoint: {
      failoverPolicy: 'Automatic'
      failoverWithDataLossGracePeriodMinutes: 60
    }
  }
}
```

## Best Practices

1. **Use @secure() for sensitive parameters** - passwords, connection strings, keys
2. **Apply tags to all resources** - for cost tracking and organization
3. **Use conditional deployment** - skip resources for specific environments
4. **Extract reusable patterns to modules** - for consistency
5. **Use environment suffixes** - `environment().suffixes.sqlServerHostname`
6. **Configure TLS 1.3 minimum** - for all services supporting it
7. **Set retention policies** - appropriate to environment (longer for prod)