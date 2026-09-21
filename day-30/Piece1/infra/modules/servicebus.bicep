@description('Name of the Service Bus namespace')
param namespaceName string

@description('Name of the topic the app actually uses (quote-events)')
param topicName string

@description('Location for the Service Bus namespace')
param location string = resourceGroup().location

@description('Namespace SKU — must be Standard or Premium; disableLocalAuth/RBAC data-plane auth is not supported on Basic')
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
    // The RootManageSharedAccessKey SAS rule exists on every namespace
    // whether it's used or not — disableLocalAuth makes the namespace
    // reject those keys outright.
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

// A topic with two subscriptions, not a queue — this is what the app
// actually does: Quotes publishes one QuoteCreated event, and Engagement's
// two independent workers (NotifySubscriptionWorker, AuditSubscriptionWorker)
// each get their own full copy of it via their own subscription (fan-out),
// with MaxConcurrentCalls inside each subscription as the competing-consumer
// half.
resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: namespace
  name: topicName
}

resource notifySubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topic
  name: 'notify-sub'
  properties: {
    maxDeliveryCount: 10
  }
}

resource auditSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topic
  name: 'audit-sub'
  properties: {
    maxDeliveryCount: 10
  }
}

// Managed identity wiring: no SAS connection string issued or stored
// anywhere — Sender/Receiver granted separately (not Data Owner, which also
// grants manage), same least-privilege reasoning as the Key Vault role.
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
