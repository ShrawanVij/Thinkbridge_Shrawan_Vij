@description('Name of the Container App')
param name string

@description('Location for the Container App')
param location string = resourceGroup().location

@description('Resource ID of the existing Container Apps environment')
param containerAppsEnvironmentResourceId string

@description('Container image to deploy')
param image string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Minimum number of replicas')
param minReplicas int

@description('Maximum number of replicas')
param maxReplicas int

@description('CPU cores per replica')
param cpu string

@description('Memory per replica')
param memory string

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  properties: {
    environmentId: containerAppsEnvironmentResourceId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          name: 'quotes-api'
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
