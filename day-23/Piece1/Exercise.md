# Day 23 — Bicep IaC — Submission

## main.bicep

```bicep
@description('Environment name, e.g. dev or prod — used to name resources')
param environmentName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Resource ID of the existing Container Apps environment')
param containerAppsEnvironmentResourceId string

@description('Container image for the API')
param apiImage string

@description('Minimum API replicas')
param apiMinReplicas int

@description('Maximum API replicas')
param apiMaxReplicas int

@description('API CPU cores per replica')
param apiCpu string

@description('API memory per replica')
param apiMemory string

@description('SQL admin login')
param sqlAdminLogin string

@description('SQL admin password')
@secure()
param sqlAdminPassword string

@description('SQL database SKU name')
param sqlSkuName string

@description('SQL database SKU tier')
param sqlSkuTier string

@description('SQL database max size in bytes')
param sqlMaxSizeBytes int

@description('Service Bus namespace SKU')
param serviceBusSkuName string

module api 'modules/api.bicep' = {
  name: 'api'
  params: {
    name: 'quotes-api-${environmentName}'
    location: location
    containerAppsEnvironmentResourceId: containerAppsEnvironmentResourceId
    image: apiImage
    minReplicas: apiMinReplicas
    maxReplicas: apiMaxReplicas
    cpu: apiCpu
    memory: apiMemory
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: 'sql-quotes-${environmentName}'
    databaseName: 'quotesdb'
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    skuName: sqlSkuName
    skuTier: sqlSkuTier
    maxSizeBytes: sqlMaxSizeBytes
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    namespaceName: 'sb-quotes-${environmentName}'
    queueName: 'quotes-events'
    location: location
    skuName: serviceBusSkuName
  }
}

output apiFqdn string = api.outputs.fqdn
output sqlServerFqdn string = sql.outputs.serverFqdn
output serviceBusHostName string = serviceBus.outputs.namespaceHostName
```

## modules/sql.bicep

```bicep
@description('Name of the SQL logical server')
param serverName string

@description('Name of the database')
param databaseName string

@description('Location for the SQL resources')
param location string = resourceGroup().location

@description('SQL admin login')
param adminLogin string

@description('SQL admin password')
@secure()
param adminPassword string

@description('Database SKU name, e.g. Basic (dev) or S1 (prod)')
param skuName string

@description('Database SKU tier, e.g. Basic (dev) or Standard (prod)')
param skuTier string

@description('Max database size in bytes')
param maxSizeBytes int

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    maxSizeBytes: maxSizeBytes
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
```

## main.parameters.dev.json

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": { "value": "dev" },
    "location": { "value": "centralindia" },
    "containerAppsEnvironmentResourceId": {
      "value": "/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env"
    },
    "apiImage": { "value": "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest" },
    "apiMinReplicas": { "value": 0 },
    "apiMaxReplicas": { "value": 1 },
    "apiCpu": { "value": "0.25" },
    "apiMemory": { "value": "0.5Gi" },
    "sqlAdminLogin": { "value": "quotesadmin" },
    "sqlSkuName": { "value": "Basic" },
    "sqlSkuTier": { "value": "Basic" },
    "sqlMaxSizeBytes": { "value": 2147483648 },
    "serviceBusSkuName": { "value": "Basic" }
  }
}
```

## main.parameters.prod.json

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": { "value": "prod" },
    "location": { "value": "centralindia" },
    "containerAppsEnvironmentResourceId": {
      "value": "/subscriptions/${AZURE_SUBSCRIPTION_ID}/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env"
    },
    "apiImage": { "value": "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest" },
    "apiMinReplicas": { "value": 2 },
    "apiMaxReplicas": { "value": 10 },
    "apiCpu": { "value": "1.0" },
    "apiMemory": { "value": "2.0Gi" },
    "sqlAdminLogin": { "value": "quotesadmin" },
    "sqlSkuName": { "value": "S1" },
    "sqlSkuTier": { "value": "Standard" },
    "sqlMaxSizeBytes": { "value": 10737418240 },
    "serviceBusSkuName": { "value": "Standard" }
  }
}
```

## Successful what-if output (dev)

```
az deployment group what-if \
  --resource-group thinkschool-rg \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json \
  --parameters containerAppsEnvironmentResourceId=<existing thinkschool-env id> \
  --parameters sqlAdminPassword=<supplied at deploy time>
```

```
Resource and property changes are indicated with these symbols:
  + Create
  * Ignore

Scope: /subscriptions/<sub>/resourceGroups/thinkschool-rg

  + Microsoft.App/containerApps/quotes-api-dev [2024-03-01]
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev [2022-10-01-preview]
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev/queues/quotes-events [2022-10-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev [2023-08-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev/databases/quotesdb [2023-08-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev/firewallRules/AllowAllAzureServices [2023-08-01-preview]
  * Microsoft.App/managedEnvironments/thinkschool-env

Resource changes: 6 to create, 1 to ignore.
```

Re-running with `main.parameters.prod.json` produces the same 6 resources named `*-prod` instead of `*-dev`, with `sql.sku.name: "S1"` and `properties.maxSizeBytes: 10737418240` — confirming the parameter files, not the modules, drive the environment difference.
