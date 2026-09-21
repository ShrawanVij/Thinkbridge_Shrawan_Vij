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
