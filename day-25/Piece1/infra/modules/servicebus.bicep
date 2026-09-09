@description('Name of the Service Bus namespace')
param namespaceName string

@description('Name of the queue')
param queueName string

@description('Location for the Service Bus namespace')
param location string = resourceGroup().location

@description('Namespace SKU. Must be Standard or Premium — disableLocalAuth/RBAC data-plane auth is not supported on Basic.')
param skuName string

@description('Principal ID of the identity that should send/receive on this namespace')
param dataOwnerPrincipalId string

var serviceBusDataSenderRoleId = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // The RootManageSharedAccessKey SAS rule exists on every namespace whether it's used or not.
    // disableLocalAuth makes the namespace reject those keys outright, instead of relying on the
    // convention of nobody reading them out of the portal.
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: namespace
  name: queueName
  properties: {
    maxDeliveryCount: 10
  }
}

// Managed Identity wiring: no SAS connection string is issued or stored anywhere — the API
// authenticates with an AAD token. Sender and Receiver granted separately (not Data Owner, which
// also grants manage) — the same least-privilege reasoning as the Key Vault role below.
resource dataSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, dataOwnerPrincipalId, serviceBusDataSenderRoleId)
  scope: namespace
  properties: {
    principalId: dataOwnerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
  }
}

resource dataReceiverRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, dataOwnerPrincipalId, serviceBusDataReceiverRoleId)
  scope: namespace
  properties: {
    principalId: dataOwnerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiverRoleId)
  }
}

output namespaceHostName string = '${namespace.name}.servicebus.windows.net'
