@description('Name of the Azure Container Registry — alphanumeric only, no hyphens allowed by ACR')
param name string

@description('Location for the registry')
param location string = resourceGroup().location

@description('Principal ID of the user-assigned identity that pulls images at runtime')
param pullPrincipalId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    // No admin user / shared keys — pulls happen via the Container App's own
    // managed identity (AcrPull below); pushes happen via whichever human
    // account azd deploy runs as, granted AcrPush out-of-band.
    adminUserEnabled: false
  }
}

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullPrincipalId, 'AcrPull')
  scope: registry
  properties: {
    principalId: pullPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

output loginServer string = registry.properties.loginServer
output id string = registry.id
