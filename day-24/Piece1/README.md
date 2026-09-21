# Day 24 — Deployment Stacks + azd

## Objective
Deploy the full stack (Container App API, Azure SQL, Service Bus) with Azure Deployment Stacks — driven by `azd` — so teardown is clean and drift is detectable. Deploy to `dev`, then promote to `prod`.

Built on top of Day 23's Bicep (same `main.bicep` → `resources.bicep` → 3 modules shape), restructured to subscription scope so `azd` can create a dedicated resource group per environment (`rg-quotes-dev`, `rg-quotes-prod`), and wired to `azd`'s native Deployment Stacks support.

## azd config — azure.yaml

```yaml
name: quotes-stack
metadata:
  template: quotes-stack@0.0.1

infra:
  provider: bicep
  path: infra
  module: main
  deploymentStacks:
    denySettings:
      mode: denyDelete
    bypassStackOutOfSyncError: false
    actionOnUnmanage:
      resources: delete
      resourceGroups: delete
```

- `denySettings.mode: denyDelete` — once provisioned, nobody (no principal, no portal click) can delete a resource the stack manages except through the stack itself.
- `actionOnUnmanage` — if a resource is removed from the Bicep and re-provisioned, the stack deletes it (and the resource group, if the whole stack goes) instead of orphaning it — this is what makes teardown clean.

`deploymentStacks` in `azure.yaml` only takes effect if the `deployment.stacks` alpha feature is turned on for the `azd` CLI (`azd config set alpha.deployment.stacks on`); without it, `azd provision` silently falls back to a plain `az deployment sub create` and the deny-delete/unmanage settings above are never actually applied, with no error raised.

Per-environment values (SKU tiers, replica counts, container image, the existing Container Apps environment ID, `SQL_ADMIN_PASSWORD`) are supplied through `azd env set` per environment, referenced in `infra/main.parameters.json` as `${VAR_NAME}` placeholders — not hardcoded, and not two separate parameter files like Day 23, since each `azd environment` (`dev`, `prod`) now owns its own `.env` under `.azure/<name>/` (gitignored — holds the SQL admin password).

## Deploy output — dev

```
$ azd env select dev
$ azd provision --no-prompt

Provisioning Azure resources (azd provision)

  WARNING: Feature 'deployment.stacks' is in alpha stage.

Initialize bicep provider
Reading subscription and location from environment...
Subscription: Azure for Students (5448e1e9-8c94-4d57-ac78-37733af38ee8)
Location: Central India

Creating a deployment plan
Validating deployment
Creating/Updating resources

  (✓) Done: Resource group: rg-quotes-dev (3.181s)
  (✓) Done: Service Bus Namespace: sb-quotes-dev (1.467s)
  (✓) Done: Azure SQL Server: sql-quotes-dev-tspowcmk2r5s6 (8.418s)
  (✓) Done: Container App: quotes-api-dev (18.271s)

SUCCESS: Your application was provisioned in Azure in 2 minutes 39 seconds.
```

SQL server names are globally unique across all of Azure (they resolve as `<name>.database.windows.net`), not just within a subscription — `sql-quotes-dev` alone collides with someone else's server, so `modules/sql.bicep`'s `serverName` is suffixed with `uniqueString(resourceGroup().id)` in `resources.bicep`.

## Deploy output — prod (promoted)

```
$ azd env select prod
$ azd provision --no-prompt

Provisioning Azure resources (azd provision)

  WARNING: Feature 'deployment.stacks' is in alpha stage.

Initialize bicep provider
Reading subscription and location from environment...
Subscription: Azure for Students (5448e1e9-8c94-4d57-ac78-37733af38ee8)
Location: Central India

Creating a deployment plan
Validating deployment
Creating/Updating resources

  (✓) Done: Resource group: rg-quotes-prod (2.202s)
  (✓) Done: Service Bus Namespace: sb-quotes-prod (536ms)
  (✓) Done: Azure SQL Server: sql-quotes-prod-bbh4g33kv7xqw (7.016s)
  (✓) Done: Container App: quotes-api-prod (16.578s)

SUCCESS: Your application was provisioned in Azure in 2 minutes 32 seconds.
```

Same template, same modules, two completely separate resource groups and SKU tiers — driven entirely by which `azd` environment is active:

| | dev | prod |
|---|---|---|
| SQL DB | Basic, 2 GB | Standard S1, 10 GB |
| Service Bus | Basic | Standard |
| Container App replicas | 0-1 | 2-10 |

## Verifying the stack, not just the deployment

`azd provision` reporting success only proves resources exist — the actual deliverable is that they're managed by a Deployment Stack, not a plain deployment:

```
$ az stack sub list -o table
Name             State      Last Modified
azd-stack-dev    succeeded  ...
azd-stack-prod   succeeded  ...

$ az stack sub show -n azd-stack-dev --query "{denySettings:denySettings.mode, actionOnUnmanage:actionOnUnmanage, resourceCount:length(resources)}"
{
  "actionOnUnmanage": {
    "managementGroups": "detach",
    "resourceGroups": "delete",
    "resources": "delete",
    "resourcesWithoutDeleteSupport": "fail"
  },
  "denySettings": "denyDelete",
  "resourceCount": 7
}
```

`azd-stack-prod` reports the identical `denySettings`/`actionOnUnmanage` shape. Both stacks are real `Microsoft.Resources/deploymentStacks` objects at subscription scope — not the plain `az deployment sub` resources you'd get if the alpha flag above were left off.

## Deployment Stacks vs plain deployments — one line

A plain `az deployment group create` leaves orphaned resources behind when you remove them from the template and redeploy; a Deployment Stack tracks exactly what it manages and deletes anything dropped from the template (or the whole resource group) on teardown, and can outright deny deletion of its resources outside the stack — giving you clean teardown and drift protection that a regular deployment can't.
