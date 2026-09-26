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

@description('SQL database SKU name')
param sqlSkuName string

@description('SQL database SKU tier')
param sqlSkuTier string

@description('SQL database max size in bytes')
param sqlMaxSizeBytes int

@description('Service Bus namespace SKU')
param serviceBusSkuName string

// SQL logical server names are globally unique across all of Azure (like storage
// accounts), not just this subscription — 'sql-quotes-dev' is already taken by
// someone else, so a uniqueness token is appended to the server name only.
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// One database per module — matches the four SQLite files each module used
// locally (quotes.db, identity.db, collections.db, engagement.db). Order
// matters: api.bicep reads these positionally.
var sqlDatabaseNames = [
  'quotesdb'
  'identitydb'
  'collectionsdb'
  'engagementdb'
]

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    name: 'id-quotes-${environmentName}'
    location: location
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    name: 'kv-quotes-${environmentName}'
    location: location
    readerPrincipalId: identity.outputs.principalId
  }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    name: 'acrquotes${environmentName}${resourceToken}'
    location: location
    pullPrincipalId: identity.outputs.principalId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    serverName: 'sql-quotes-${environmentName}-${resourceToken}'
    databaseNames: sqlDatabaseNames
    location: location
    serverIdentityResourceId: identity.outputs.resourceId
    adminLogin: 'id-quotes-${environmentName}'
    adminPrincipalId: identity.outputs.principalId
    skuName: sqlSkuName
    skuTier: sqlSkuTier
    maxSizeBytes: sqlMaxSizeBytes
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: {
    namespaceName: 'sb-quotes-${environmentName}'
    topicName: 'quote-events'
    location: location
    skuName: serviceBusSkuName
    dataOwnerPrincipalId: identity.outputs.principalId
  }
}

module api 'modules/api.bicep' = {
  name: 'api'
  params: {
    name: 'quotes-api-${environmentName}'
    location: location
    containerAppsEnvironmentResourceId: containerAppsEnvironmentResourceId
    identityResourceId: identity.outputs.resourceId
    identityClientId: identity.outputs.clientId
    registryLoginServer: registry.outputs.loginServer
    image: apiImage
    minReplicas: apiMinReplicas
    maxReplicas: apiMaxReplicas
    cpu: apiCpu
    memory: apiMemory
    sqlServerFqdn: sql.outputs.serverFqdn
    sqlDatabaseNames: sqlDatabaseNames
    serviceBusHostName: serviceBus.outputs.namespaceHostName
    jwtSecretUri: keyVault.outputs.jwtSecretUri
  }
}

output apiFqdn string = api.outputs.fqdn
output sqlServerFqdn string = sql.outputs.serverFqdn
output serviceBusHostName string = serviceBus.outputs.namespaceHostName
output keyVaultUri string = keyVault.outputs.vaultUri
output acrLoginServer string = registry.outputs.loginServer
