@description('Resource ID of the SQL server to put behind a private endpoint')
param sqlServerResourceId string

@description('Resource ID of an existing VNet subnet (must have privateEndpointNetworkPolicies disabled)')
param subnetResourceId string

@description('Location for the private endpoint')
param location string = resourceGroup().location

@description('Name for the private endpoint resource')
param privateEndpointName string

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: privateEndpointName
  location: location
  properties: {
    subnet: { id: subnetResourceId }
    privateLinkServiceConnections: [
      {
        name: '${privateEndpointName}-connection'
        properties: {
          privateLinkServiceId: sqlServerResourceId
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.database.windows.net'
  location: 'global'
}

resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql-config'
        properties: { privateDnsZoneId: privateDnsZone.id }
      }
    ]
  }
}

output privateEndpointId string = privateEndpoint.id
