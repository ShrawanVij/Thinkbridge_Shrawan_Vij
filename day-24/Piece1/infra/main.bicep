targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

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

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-quotes-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    environmentName: environmentName
    location: location
    containerAppsEnvironmentResourceId: containerAppsEnvironmentResourceId
    apiImage: apiImage
    apiMinReplicas: apiMinReplicas
    apiMaxReplicas: apiMaxReplicas
    apiCpu: apiCpu
    apiMemory: apiMemory
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    sqlSkuName: sqlSkuName
    sqlSkuTier: sqlSkuTier
    sqlMaxSizeBytes: sqlMaxSizeBytes
    serviceBusSkuName: serviceBusSkuName
  }
}

output API_FQDN string = resources.outputs.apiFqdn
output SQL_SERVER_FQDN string = resources.outputs.sqlServerFqdn
output SERVICE_BUS_HOST_NAME string = resources.outputs.serviceBusHostName
