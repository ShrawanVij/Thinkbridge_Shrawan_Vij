@description('Name of the SQL logical server')
param serverName string

@description('Name of the database')
param databaseName string

@description('Location for the SQL resources')
param location string = resourceGroup().location

@description('Resource ID of the user-assigned identity to use for AAD admin lookup')
param serverIdentityResourceId string

@description('Display name (login) of the AAD principal that becomes SQL admin — the API own managed identity')
param adminLogin string

@description('Object (principal) ID of the API managed identity — becomes the SQL AAD admin, no SQL password exists')
param adminPrincipalId string

@description('Tenant ID for the AAD admin')
param tenantId string = subscription().tenantId

@description('Database SKU name, e.g. Basic (dev) or S1 (prod)')
param skuName string

@description('Database SKU tier, e.g. Basic (dev) or Standard (prod)')
param skuTier string

@description('Max database size in bytes')
param maxSizeBytes int

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${serverIdentityResourceId}': {}
    }
  }
  properties: {
    version: '12.0'
    // No administratorLogin / administratorLoginPassword — SQL auth is not configured at all.
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: adminLogin
      sid: adminPrincipalId
      tenantId: tenantId
      azureADOnlyAuthentication: true
    }
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
