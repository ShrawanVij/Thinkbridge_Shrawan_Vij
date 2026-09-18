@description('Name of the SQL logical server')
param serverName string

@description('Names of the databases to create — one per module, since no two modules share a database any more than they share a DbContext')
param databaseNames array

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

@description('Max size in bytes, per database')
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
    // Required whenever identity.type is UserAssigned, even with a single identity —
    // Azure SQL won't infer which one to use for the AAD admin lookup otherwise.
    primaryUserAssignedIdentityId: serverIdentityResourceId
    // No administratorLogin / administratorLoginPassword — SQL auth doesn't exist on this server.
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

// One server, one database per module — same physical-server-different-
// database split the local SQLite files use (quotes.db, identity.db,
// collections.db, engagement.db), not four servers, since these are cheap
// to run side by side on a single Basic/S1-tier server and there's no
// reason to pay for four.
resource databases 'Microsoft.Sql/servers/databases@2023-08-01-preview' = [
  for dbName in databaseNames: {
    parent: sqlServer
    name: dbName
    location: location
    sku: {
      name: skuName
      tier: skuTier
    }
    properties: {
      maxSizeBytes: maxSizeBytes
    }
  }
]

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
