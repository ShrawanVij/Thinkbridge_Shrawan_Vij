@description('Name of the user-assigned managed identity')
param name string

@description('Location for the identity')
param location string = resourceGroup().location

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
}

output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
output resourceId string = identity.id
