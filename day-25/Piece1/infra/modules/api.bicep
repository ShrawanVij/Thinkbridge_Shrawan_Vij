@description('Name of the Container App')
param name string

@description('Location for the Container App')
param location string = resourceGroup().location

@description('Resource ID of the existing Container Apps environment')
param containerAppsEnvironmentResourceId string

@description('Resource ID of the user-assigned identity this app runs as')
param identityResourceId string

@description('Client ID of the same user-assigned identity — passed through so the app can target it explicitly')
param identityClientId string

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

@description('SQL server FQDN — not a secret, no password in the connection string (AAD token auth)')
param sqlServerFqdn string

@description('SQL database name')
param sqlDatabaseName string

@description('Service Bus namespace host name — not a secret, no SAS connection string (AAD token auth)')
param serviceBusHostName string

@description('Key Vault URI of the JWT signing secret')
param jwtSecretUri string

@description('Entra ID (Azure AD) application (client) ID used for built-in Container Apps authentication')
param entraIdClientId string

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityResourceId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentResourceId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      // Key Vault reference: the secret's plaintext value never appears here — only its URI,
      // resolved at runtime via the user-assigned identity above.
      secrets: [
        {
          name: 'jwt-signing-key'
          keyVaultUrl: jwtSecretUri
          identity: identityResourceId
        }
      ]
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
          env: [
            // Connection info only — no password, no SAS key, no connection string with embedded
            // credentials. The API's own managed identity authenticates to SQL and Service Bus.
            {
              name: 'Sql__ServerFqdn'
              value: sqlServerFqdn
            }
            {
              name: 'Sql__Database'
              value: sqlDatabaseName
            }
            {
              name: 'ServiceBus__Namespace'
              value: serviceBusHostName
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: identityClientId
            }
            {
              name: 'Jwt__Key'
              secretRef: 'jwt-signing-key'
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

// Entra ID app auth: validates callers against the tenant's Entra ID, no custom auth secret needed.
resource authConfig 'Microsoft.App/containerApps/authConfigs@2024-03-01' = {
  parent: containerApp
  name: 'current'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      unauthenticatedClientAction: 'Return401'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entraIdClientId
          openIdIssuer: '${environment().authentication.loginEndpoint}${subscription().tenantId}/v2.0'
        }
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
