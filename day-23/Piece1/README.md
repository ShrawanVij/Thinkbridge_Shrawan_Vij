# Day 23 — Bicep IaC

## Objective
Describe the QuotesApi infrastructure as code: parameterized Bicep modules for the API (Container App), SQL (server + database), and Service Bus (namespace + queue), with separate dev/prod parameter files. No portal click-ops.

## Layout
```
infra/
  main.bicep                    — orchestrator, wires the 3 modules together
  modules/
    api.bicep                   — Container App
    sql.bicep                   — SQL logical server + database + firewall rule
    servicebus.bicep            — Service Bus namespace + queue
  main.parameters.dev.json
  main.parameters.prod.json
```

Each module takes a `sku`/size-shaped parameter rather than branching internally on environment — the dev/prod difference lives entirely in the two parameter files, not in the module logic.

## Dev vs prod
| | dev | prod |
|---|---|---|
| API replicas | 0–1 | 2–10 |
| API resources | 0.25 CPU / 0.5Gi | 1.0 CPU / 2.0Gi |
| SQL SKU | Basic | S1 / Standard |
| SQL max size | 2 GB | 10 GB |
| Service Bus SKU | Basic | Standard |

`sqlAdminPassword` is `@secure()` in `main.bicep` and deliberately absent from both parameter files — it's supplied at deploy time (`--parameters sqlAdminPassword=$env:SQL_ADMIN_PW`), never checked in.

## Proof: successful what-if

Ran against the existing `thinkschool-rg` resource group (subscription's region policy only allows `centralindia`, not `eastus`, hence that location in the params):

```
az deployment group what-if \
  --resource-group thinkschool-rg \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.dev.json \
  --parameters containerAppsEnvironmentResourceId=<existing thinkschool-env id> \
  --parameters sqlAdminPassword=<supplied at deploy time>
```

```
Resource and property changes are indicated with these symbols:
  + Create
  * Ignore

Scope: /subscriptions/<sub>/resourceGroups/thinkschool-rg

  + Microsoft.App/containerApps/quotes-api-dev [2024-03-01]
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev [2022-10-01-preview]
  + Microsoft.ServiceBus/namespaces/sb-quotes-dev/queues/quotes-events [2022-10-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev [2023-08-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev/databases/quotesdb [2023-08-01-preview]
  + Microsoft.Sql/servers/sql-quotes-dev/firewallRules/AllowAllAzureServices [2023-08-01-preview]
  * Microsoft.App/managedEnvironments/thinkschool-env

Resource changes: 6 to create, 1 to ignore.
```

Re-running with `main.parameters.prod.json` produces the same 6 resources named `*-prod` instead of `*-dev`, with `sql.sku.name: "S1"` and `properties.maxSizeBytes: 10737418240` — confirming the parameter files, not the modules, drive the environment difference.
