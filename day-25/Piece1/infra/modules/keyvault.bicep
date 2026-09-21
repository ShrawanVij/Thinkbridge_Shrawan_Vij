@description('Name of the Key Vault')
param name string

@description('Location for the Key Vault')
param location string = resourceGroup().location

@description('Tenant ID for RBAC')
param tenantId string = subscription().tenantId

@description('Principal ID of the identity that should be able to read secrets')
param readerPrincipalId string

@description('Name of the JWT signing key secret — created out-of-band, never through this template')
param jwtSecretName string = 'jwt-signing-key'

var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

resource secretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, readerPrincipalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

// Deliberately no `Microsoft.KeyVault/vaults/secrets` resource here. Creating the secret in Bicep
// would mean its value arrives as an ARM parameter — a @secure() parameter is redacted in the
// portal, but the request that carried it is still recorded in this resource group's deployment
// history. The secret is written after provisioning via `az keyvault secret set`, which hits the
// vault's data plane directly and never touches ARM. The URI below is computed, not read off a
// resource, and deliberately carries no ;SecretVersion= — omitting it means the reference always
// follows the current version, so rotating the secret never requires a redeploy.
output vaultUri string = keyVault.properties.vaultUri
output jwtSecretUri string = '${keyVault.properties.vaultUri}secrets/${jwtSecretName}'
