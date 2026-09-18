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

// No secret resource here on purpose — same reasoning as Day 25: a secret set
// through Bicep would ride along as an ARM parameter, recorded in this
// resource group's deployment history even as a @secure() parameter. Set it
// after provisioning with `az keyvault secret set`, straight to the vault's
// data plane. No ;SecretVersion= in the URI, so rotating it never needs a
// redeploy.
output vaultUri string = keyVault.properties.vaultUri
output jwtSecretUri string = '${keyVault.properties.vaultUri}secrets/${jwtSecretName}'
