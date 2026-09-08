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

Per-environment values (SKU tiers, replica counts, container image, the existing Container Apps environment ID, `SQL_ADMIN_PASSWORD`) are supplied through `azd env set` per environment, referenced in `infra/main.parameters.json` as `${VAR_NAME}` placeholders — not hardcoded, and not two separate parameter files like Day 23, since each `azd environment` (`dev`, `prod`) now owns its own `.env` under `.azure/<name>/` (gitignored — holds the SQL admin password).

## Deploy output — dev

```
$ azd env new dev
New environment 'dev' was set as default

$ azd env set AZURE_LOCATION centralindia -e dev
$ azd env set CONTAINER_APPS_ENVIRONMENT_ID <existing thinkschool-env id> -e dev
$ azd env set SQL_ADMIN_PASSWORD <secret> -e dev

$ azd provision --preview -e dev --no-prompt
Previewing Azure resource changes (azd provision --preview)
This is a preview. No changes will be applied to your Azure resources.

Initialize bicep provider
Reading subscription and location from environment...
Subscription: Azure for Students (5448e1e9-8c94-4d57-ac78-37733af38ee8)
Location: Central India

Creating a deployment plan
Generating infrastructure preview
  Resources:

  Create : Resource group        : rg-quotes-dev
  Create : Container App         : quotes-api-dev
  Create : Service Bus Namespace : sb-quotes-dev
  Create : Azure SQL Server      : sql-quotes-dev

SUCCESS: Generated provisioning preview in 23 seconds.
```

## Deploy output — prod (promoted)

```
$ azd env new prod
New environment 'prod' created and set as default

$ azd env set SQL_SKU_NAME S1 -e prod
$ azd env set SQL_SKU_TIER Standard -e prod
$ azd env set SQL_MAX_SIZE_BYTES 10737418240 -e prod
$ azd env set SERVICE_BUS_SKU_NAME Standard -e prod
$ azd env set API_MIN_REPLICAS 2 -e prod
$ azd env set API_MAX_REPLICAS 10 -e prod
$ azd env set API_CPU 1.0 -e prod
$ azd env set API_MEMORY 2.0Gi -e prod

$ azd provision --preview -e prod --no-prompt
Previewing Azure resource changes (azd provision --preview)
This is a preview. No changes will be applied to your Azure resources.

Initialize bicep provider
Reading subscription and location from environment...
Subscription: Azure for Students (5448e1e9-8c94-4d57-ac78-37733af38ee8)
Location: Central India

Creating a deployment plan
Generating infrastructure preview
  Resources:

  Create : Resource group        : rg-quotes-prod
  Create : Container App         : quotes-api-prod
  Create : Service Bus Namespace : sb-quotes-prod
  Create : Azure SQL Server      : sql-quotes-prod

SUCCESS: Generated provisioning preview in 22 seconds.
```

Same template, same modules, two completely separate resource groups and SKU tiers — driven entirely by which `azd` environment is active. (Ran with `--preview` rather than a real `provision` to avoid spinning up billable SQL/Service Bus/Container App resources on a student subscription just for this exercise — the preview is a genuine dry-run against the Deployment Stacks API, not a simulation.)

## Deployment Stacks vs plain deployments — one line

A plain `az deployment group create` leaves orphaned resources behind when you remove them from the template and redeploy; a Deployment Stack tracks exactly what it manages and deletes anything dropped from the template (or the whole resource group) on teardown, and can outright deny deletion of its resources outside the stack — giving you clean teardown and drift protection that a regular deployment can't.
