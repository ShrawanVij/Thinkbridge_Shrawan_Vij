@description('Name of the Service Bus namespace')
param namespaceName string

@description('Name of the queue')
param queueName string

@description('Location for the Service Bus namespace')
param location string = resourceGroup().location

@description('Namespace SKU, e.g. Basic (dev) or Standard (prod)')
param skuName string

resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: namespace
  name: queueName
  properties: {
    maxDeliveryCount: 10
  }
}

output namespaceHostName string = '${namespace.name}.servicebus.windows.net'
