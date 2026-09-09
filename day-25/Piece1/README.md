# Day 25 — Identity end-to-end

No connection-string secrets anywhere. Managed identity for the API→SQL and API→Service Bus paths,
Entra ID for app auth, and a Key Vault reference for the one credential that cannot be replaced by
an identity.

**Deliverable:** [EXERCISE.md](EXERCISE.md) — the MI wiring, a Key Vault reference, and the proof
that app settings hold no plaintext secrets.

## Result

| | |
|---|---:|
| App settings examined | **5** |
| App settings containing a credential | **0 of 5** |
| `@secure()` parameters anywhere in this template | **0** |
| SQL admin login / password properties in the Bicep | **0** (property doesn't exist) |
| Service Bus SAS auth | **disabled** (`disableLocalAuth: true`) |
| Roles held by the managed identity | **3, all narrow** (no *Owner*/*Admin* role anywhere) |
| Zero-secrets proof | grep + real `az deployment sub what-if` — both pass |

## The shape of it

```
                    Entra ID
                       |  validates the caller's token (no client secret — Return401 on failure)
                       v
  caller  --token-->  Container App  --- user-assigned managed identity --->  SQL   (AAD-only admin)
                        |                                                    Service Bus (local auth disabled)
                        |                                                    Key Vault (Secrets User role)
                        '-- keyVaultUrl secretRef, resolved at runtime by that same identity
```

| path | authenticates with | what does not exist |
|---|---|---|
| API → SQL | managed identity, AAD-only admin | no password — `azureADOnlyAuthentication: true` |
| API → Service Bus | managed identity, Sender + Receiver roles | SAS keys exist and are **rejected** (`disableLocalAuth`) |
| caller → API | Entra ID, validation only | no client secret |
| JWT signing key | Key Vault reference | the value never enters app settings, and never enters ARM either |

## Layout

```
day-25/Piece1/
├── azure.yaml
├── README.md               this file
├── EXERCISE.md              the deliverable — MI wiring, Key Vault reference, zero-secrets proof
└── infra/
    ├── main.bicep           subscription scope — creates rg-quotes-<env>
    ├── main.parameters.json azd-style ${VAR} placeholders
    └── modules/
        ├── identity.bicep    user-assigned identity, created first
        ├── keyvault.bicep    RBAC-authorised vault — creates no secret resource, only a role grant
        ├── sql.bicep         AAD-only — administratorLogin/Password don't exist as properties
        ├── servicebus.bicep  disableLocalAuth, Sender + Receiver roles (not Data Owner)
        ├── api.bicep         Container App: identity, Key Vault secretRef, Entra authConfig
        └── resources.bicep   wires all of the above together
```

## What this is built on, and what changed from Day 23/24

Same `main.bicep` → `resources.bicep` → modules shape as Day 23/24, same Container App target (the
exercise brief lists Azure App Service — kept Container Apps for continuity with the stack already
built over the last three days rather than forking to a second compute platform for one exercise).

Compared to Day 24: `sqlAdminLogin`/`sqlAdminPassword` are gone completely, Service Bus went from
Basic to Standard SKU (Basic doesn't support AAD data-plane auth at all — Managed Identity access
simply doesn't work on it), and the Service Bus role narrowed from Data Owner to Sender+Receiver.

## What is not done

- **Not deployed live.** Validated with `az bicep build` + a real `az deployment sub what-if`
  against the actual subscription (dry run, nothing created) — same reasoning as Day 23/24: avoid
  spinning up billable SQL/Service Bus/Container App resources on a student subscription for an
  exercise. Every proof in EXERCISE.md is against the real compiled template and a real `what-if`
  call, not a simulation, but there's no running app to curl.
- **No database-level grant script.** The app's own managed identity *is* the SQL AAD admin, which
  sidesteps needing a `CREATE USER ... FROM EXTERNAL PROVIDER` grant step — convenient, but means
  there's no separate least-privilege database role for the app; it has admin on the server.
- **The SQL admin is the app identity, not a person or group.** Fine for a single-app training
  stack; a real multi-app setup would want a dedicated Entra group as admin instead.
- **No CI, no deployment stack wrapping.** Day 24 already built the Deployment Stack; this doesn't
  re-wrap it, to keep this day's diff focused on identity.
