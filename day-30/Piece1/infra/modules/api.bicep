@description('Name of the Container App')
param name string

@description('Location for the Container App')
param location string = resourceGroup().location

@description('Resource ID of the existing Container Apps environment')
param containerAppsEnvironmentResourceId string

@description('Resource ID of the user-assigned identity this app runs as')
param identityResourceId string

@description('Client ID of the same user-assigned identity')
param identityClientId string

@description('Login server of the Azure Container Registry images are pulled from')
param registryLoginServer string

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

@description('SQL server FQDN — not a secret, no password in any of the four connection strings below (AAD managed-identity auth)')
param sqlServerFqdn string

@description('Database names, one per module: [quotes, identity, collections, engagement]')
param sqlDatabaseNames array

@description('Service Bus namespace host name — not a secret, no SAS connection string (AAD token auth)')
param serviceBusHostName string

@description('Key Vault URI of the JWT signing secret')
param jwtSecretUri string

@description('Entra ID (Azure AD) application (client) ID used for built-in Container Apps authentication')
param entraIdClientId string

// One SQL connection string per module database — same server, same
// managed-identity auth, different Database= only. No password anywhere:
// Authentication=Active Directory Managed Identity resolves via the
// container's own identity (User Id= pins it to this specific
// user-assigned identity rather than whichever the runtime finds first).
var quotesConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseNames[0]};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;'
var identityConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseNames[1]};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;'
var collectionsConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseNames[2]};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;'
var engagementConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseNames[3]};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;'

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  // Required for `azd deploy` to find this resource for the 'api' service
  // declared in azure.yaml — without this tag it has no way to match them up.
  tags: {
    'azd-service-name': 'api'
  }
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
      // No admin username/password — the registry has no admin user; pulls
      // authenticate as this Container App's own managed identity (granted
      // AcrPull on the registry in registry.bicep).
      registries: [
        {
          server: registryLoginServer
          identity: identityResourceId
        }
      ]
      // Key Vault reference: the secret's plaintext value never appears
      // here — only its URI, resolved at runtime via the identity above.
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
            {
              name: 'ConnectionStrings__Quotes'
              value: quotesConnectionString
            }
            {
              name: 'ConnectionStrings__Identity'
              value: identityConnectionString
            }
            {
              name: 'ConnectionStrings__Collections'
              value: collectionsConnectionString
            }
            {
              name: 'ConnectionStrings__Engagement'
              value: engagementConnectionString
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
