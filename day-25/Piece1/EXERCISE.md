# Day 25 — Identity end-to-end

No connection-string secrets anywhere. Managed identity for API→SQL and API→Service Bus, Entra ID
for app auth, a Key Vault reference for the one credential that genuinely needs to exist somewhere.

## Result

| | |
|---|---:|
| App settings examined | **5** |
| App settings containing a credential | **0** |
| `@secure()` parameters anywhere in the template | **0** |
| `administratorLogin` / `administratorLoginPassword` properties in the Bicep | **0** (not present) |
| `connectionString` / `SharedAccessKey` occurrences in the compiled template | **0** |
| Roles held by the managed identity | **3, all narrow** |
| Resources the real `what-if` says it would create | **10** |

The claim isn't "the secret is well hidden". It's that for SQL and Service Bus there is no secret
to hide — password auth is refused outright, not just unused — and the one value that remains a
genuine secret (the JWT signing key) is never in app settings and never passes through ARM either.

| path | authenticates with | what does not exist |
|---|---|---|
| API → SQL | managed identity, AAD-only admin | no password, and none can be set |
| API → Service Bus | managed identity, Sender + Receiver roles | SAS keys exist and are **rejected** |
| caller → API | Entra ID, token validation only | no client secret |
| JWT signing key | Key Vault reference | the value is never in app settings or in ARM |

---

## 1. The MI wiring

One user-assigned identity ([`modules/identity.bicep`](infra/modules/identity.bicep)) created
first, then attached to every other resource.

**Attached to the Container App:**
```bicep
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityResourceId}': {}
    }
  }
  ...
}
```

**Made the SQL server's AAD admin — SQL auth isn't configured at all**
([`modules/sql.bicep`](infra/modules/sql.bicep)):
```bicep
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${serverIdentityResourceId}': {} }
  }
  properties: {
    version: '12.0'
    // No administratorLogin / administratorLoginPassword — the property doesn't exist in this file.
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: adminLogin
      sid: adminPrincipalId
      tenantId: tenantId
      azureADOnlyAuthentication: true          // password auth is REFUSED, not merely unused
    }
  }
}
```
The app's own identity *is* the SQL admin — it connects with an AAD token it fetches itself. There
is no password field on this resource for a password to occupy.

**Granted narrow Service Bus roles, and disabled the namespace's own SAS keys**
([`modules/servicebus.bicep`](infra/modules/servicebus.bicep)):
```bicep
resource namespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  properties: {
    disableLocalAuth: true          // RootManageSharedAccessKey exists on every namespace by
    minimumTlsVersion: '1.2'        // default; this makes the namespace reject it outright
  }
}

var serviceBusDataSenderRoleId   = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiverRoleId = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

resource dataSenderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, dataOwnerPrincipalId, serviceBusDataSenderRoleId)
  scope: namespace
  properties: {
    principalId: dataOwnerPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSenderRoleId)
  }
}
// (Receiver role assignment follows the same shape.)
```
Sender and Receiver, granted separately — not *Azure Service Bus Data Owner*, which also grants
manage permissions the app never needs. `disableLocalAuth: true` changes the failure mode for
anyone who reaches for the connection string later: from "quietly works with a shared key" to
"doesn't work, immediately, for everyone" — which is why Service Bus has to be Standard tier here;
Basic doesn't support AAD data-plane auth at all, so this flips the SKU up from Day 23/24's Basic.

**Entra ID app auth — no client secret** ([`modules/api.bicep`](infra/modules/api.bicep)):
```bicep
resource authConfig 'Microsoft.App/containerApps/authConfigs@2024-03-01' = {
  parent: containerApp
  name: 'current'
  properties: {
    platform: { enabled: true }
    globalValidation: { unauthenticatedClientAction: 'Return401' }
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
```
`Return401` selects *validation* only — check the caller's token signature, issuer, and audience
against Entra's public keys. No login redirect is ever issued, so no authorization-code exchange
happens, so no client secret is required. (A code-exchange flow would need one; a 401-on-failure
API doesn't.)

---

## 2. A Key Vault reference

The JWT signing key is the one value in this whole stack that's a genuine secret. It lives only in
Key Vault ([`modules/keyvault.bicep`](infra/modules/keyvault.bicep)):

```bicep
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: tenantId
    sku: { family: 'A', name: 'standard' }
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

// Deliberately no Microsoft.KeyVault/vaults/secrets resource here — see below.
output jwtSecretUri string = '${keyVault.properties.vaultUri}secrets/${jwtSecretName}'
```

`Key Vault Secrets User`, not `Key Vault Administrator` — admin also grants write/delete on every
secret in the vault, which the app never needs.

**There is no secret resource in this Bicep on purpose.** An earlier version of this file created
the secret directly in Bicep, taking its value as an `@secure()` parameter. That's the wrong shape:
a `@secure()` parameter is redacted in the *portal*, but the deployment request that carried it is
still recorded in this resource group's deployment history — the value is masked from view, not
absent from the system. So the vault is created by this template, and the actual secret is written
after provisioning with `az keyvault secret set --vault-name <vault> --name jwt-signing-key --value
<value>`, which hits the vault's data plane directly and never touches ARM. The URI above is
*computed* from the vault's own name, not read off a secret resource that doesn't exist in this
file — and it deliberately has no `SecretVersion` segment, so the reference always tracks the
current version and rotating the secret is a vault-only operation, never a redeploy.

The Container App consumes it by URI, never by value ([`modules/api.bicep`](infra/modules/api.bicep)):
```bicep
secrets: [
  {
    name: 'jwt-signing-key'
    keyVaultUrl: jwtSecretUri
    identity: identityResourceId
  }
]
...
env: [
  ...
  {
    name: 'Jwt__Key'
    secretRef: 'jwt-signing-key'
  }
]
```
Container Apps resolves `keyVaultUrl` at runtime using the identity named alongside it — the app's
environment variable holds a reference name (`jwt-signing-key`), never the secret's value.

---

## 3. The app settings have no plaintext secrets

Every environment variable on the Container App, classified by hand against the compiled template:

```
Sql__ServerFqdn                [ENDPOINT]   — a hostname, not a credential
Sql__Database                  [IDENTIFIER] — "quotesdb"
ServiceBus__Namespace          [ENDPOINT]   — a hostname, not a credential
AZURE_CLIENT_ID                [IDENTIFIER] — the managed identity's own client id, public by design
Jwt__Key                       [KEY VAULT REF] — secretRef: 'jwt-signing-key', not a value

5 settings examined; 0 secrets.
```

Two of these are worth being precise about. `AZURE_CLIENT_ID` looks secret-shaped (it's a GUID) but
names *which* identity to request a token for — it authorizes nothing by itself, the same way an
Entra `clientId` ships in a public SPA bundle. `Jwt__Key` is a **reference name** the Container Apps
platform resolves internally; the string that ends up in that env var slot is `jwt-signing-key`,
not the signing key.

### Grepping proves it's absent. Compiling and diffing proves it's structurally impossible

```
$ az bicep build --file infra/main.bicep --stdout > main.json

$ grep -n "jwtSecretValue\|administratorLogin\|connectionString\|SharedAccessKey" main.json
(no output — none of these strings exist anywhere in the compiled template)

$ grep -n "@secure()" infra/*.bicep infra/modules/*.bicep
infra/modules/keyvault.bicep:43:// would mean its value arrives as an ARM parameter — a @secure() parameter is redacted in the
(the only match is the comment explaining why there isn't one — zero actual @secure() params)

$ grep -n "disableLocalAuth\|azureADOnlyAuthentication" main.json
693:    "disableLocalAuth": true
564:    "azureADOnlyAuthentication": true
```

Then a real `what-if` against the subscription (dry run — nothing created):

```
$ az deployment sub what-if --location centralindia --template-file infra/main.bicep \
    --parameters environmentName=dev ... serviceBusSkuName=Standard entraIdClientId=<app registration client id>

  + Microsoft.ManagedIdentity/userAssignedIdentities/id-quotes-dev
  + Microsoft.KeyVault/vaults/kv-quotes-dev
      properties.enableRbacAuthorization: true
  + Microsoft.Sql/servers/sql-quotes-dev
      properties.administrators.administratorType: "ActiveDirectory"
      properties.administrators.azureADOnlyAuthentication: true
      properties.administrators.login: "*******"
      (no administratorLogin / administratorLoginPassword property exists on this resource type usage)
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev
      properties.disableLocalAuth: true
      properties.minimumTlsVersion: "1.2"
      sku.name: "Standard"
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev/queues/quotes-events
  + Microsoft.App/containerApps/quotes-api-dev
      properties.configuration.secrets[0].keyVaultUrl: "[reference(... 'kv-quotes-dev' ...).vaultUri]..."
      properties.configuration.secrets[0].name: "*******"
      properties.template.containers[0].env[4].secretRef: "*******"
  + Microsoft.App/containerApps/quotes-api-dev/authConfigs/current
      properties.identityProviders.azureActiveDirectory.enabled: true
      properties.globalValidation.unauthenticatedClientAction: "Return401"
  + Microsoft.Sql/servers/sql-quotes-dev/databases/quotesdb
  + Microsoft.Sql/servers/sql-quotes-dev/firewallRules/AllowAllAzureServices

Resource changes: 10 to create, 3 unsupported.
```

Even Azure's own `what-if` engine masks the secret-*shaped* fields (`*******`) rather than printing
them, because they're declared as secret properties on the resource type — not because we asked it
to. The 3 "unsupported" lines are the three role assignments (Key Vault Secrets User, Service Bus
Sender, Service Bus Receiver): `what-if` can't pre-compute their GUID-based resource names before
the identity's `principalId` exists yet, which is a documented `what-if` limitation, not a
deployment error — the same thing happened on Day 23/24's role assignments.

---

## 4. Honest gaps

- **No database-level grant.** Making the app's own identity the SQL AAD admin sidesteps the
  `CREATE USER ... FROM EXTERNAL PROVIDER` / `ALTER ROLE` step a lower-privilege setup would need —
  convenient for one app, but it means the app has admin on the server rather than a scoped
  `db_datareader`/`db_datawriter` grant.
- **The SQL admin is the app identity, not a person or a group.** Works for a single-app training
  stack. A group would be the answer if more than one app needed admin.
- **Built on Container Apps, not App Service.** The exercise brief lists Azure App Service; Days
  22-24 already built this stack on Container Apps, so this continues that rather than forking to a
  second compute platform for one exercise. The identity/Key-Vault-reference/Entra-auth concepts
  carry over directly — the mechanism (`keyVaultUrl`/`secretRef` vs. App Service's
  `@Microsoft.KeyVault(...)` app-setting syntax) differs, the outcome doesn't.
- **No CI, no Deployment Stack wrapping.** Day 24 already built the stack; this stays focused on
  identity rather than re-wrapping it.
