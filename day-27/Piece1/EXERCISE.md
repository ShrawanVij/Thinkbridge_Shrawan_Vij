# Day 27 — Security pass

Threat-model the capstone (STRIDE-lite), put the data tier behind private endpoints, harden the
OpenAPI surface (auth, versioning, input limits), and run a basic pen test (OWASP ZAP baseline).

## Result

| | |
|---|---:|
| Endpoints with zero auth, found in this pass | **14** |
| Fixed (now require auth) | **14 of 14** |
| Endpoints with unbounded pagination | **2** (`/cqrs/quotes/feed`, `/feed-dapper`) — fixed, clamped to 100 |
| Request body size limit | none → **64KB**, verified live (413 on an oversized body) |
| Brute-force protection on login | none → **5/min/IP**, verified live (429 on the 6th attempt) |
| API versioning | none → header-based `X-Api-Version`, v1, non-breaking |
| Live secret found in plaintext app settings during this pass | **1, found and fixed** — namespace deleted |
| ZAP baseline scan (real, live) | **62 pass, 0 fail, 5 warn** — all 5 fixed and verified live |

---

## 1. Threat model (STRIDE-lite)

System in scope: the Angular frontend (out of this API's control boundary), the `quotes-api`
Container App (public ingress), SQLite/SQL data tier, Azure Service Bus (`quote-events` topic,
`notify-sub`/`audit-sub` subscriptions), Redis cache, background workers (`OutboxRelay`,
`NotifySubscriptionWorker`, `AuditSubscriptionWorker`, `EmailWorker`), Key Vault + managed
identity (Day 25), and Application Insights (Day 26).

Not a from-scratch exercise — most of Spoofing/Tampering on the identity and secrets side was
already threat-modeled and fixed on Day 25 (AAD-only SQL, `disableLocalAuth` Service Bus, no
password/connection-string secrets, Key Vault reference for the one that remains). This pass
covers what Day 25 didn't: the OpenAPI surface itself, and the data tier's network exposure.

| # | Component / flow | STRIDE | Threat | Status |
|---|---|---|---|---|
| 1 | `POST /api/auth/login` | Spoofing | Credential stuffing / brute force — no rate limit existed | **Fixed** — 5/min/IP fixed-window limiter |
| 2 | 13 internal/test/ops endpoints (`/jobs/email`, `/outbox/*`, `/events/*`, `/api/flaky-dependency*`, `/test/flaky/*`, `/api/resilience-test`, `/cache/*`) | Elevation of Privilege | Zero authentication — any anonymous caller could read outbox/dead-letter payloads, force-republish events, inject a poison message into the real topic, or flip a process-wide chaos flag that fails every other caller's requests | **Fixed** — all 14 now `.RequireAuthorization()` |
| 3 | `GET /cqrs/quotes/feed`, `/feed-dapper` | Denial of Service | `size` param completely unbounded — `?size=999999999` (or omitted, on `/feed`) loads the entire table into memory per request | **Fixed** — clamped to 1-100, matching the pattern `/api/quotes` already had |
| 4 | Any `POST`/`PUT` body | Denial of Service | No request body size cap at the server level — per-field validation (author ≤100, text ≤1000 chars) only runs *after* Kestrel buffers the whole body | **Fixed** — `MaxRequestBodySize = 64KB`, verified live (413) |
| 5 | `POST /api/quotes`, `POST /cqrs/quotes`, DELETE | Elevation of Privilege | Does `DELETE /api/quotes/{id}` actually check ownership, or just "any authenticated user"? | **Verified, not a bug** — `can-delete-own-quote` resource-based policy is correctly wired via `IAuthorizationService.AuthorizeAsync(user, id, "can-delete-own-quote")` inside the handler |
| 6 | SQL data tier | Information Disclosure | Reachable over the public internet — no private endpoint, broad `AllowAllAzureServices` (0.0.0.0-0.0.0.0) firewall rule from Day 23/25 | **Partially addressed** — private endpoint module authored + validated (§2); not wired to the live shared Container Apps environment, which has no VNet (see Honest Gaps) |
| 7 | QuotesApi ↔ SQL / Service Bus | Tampering | SQL injection via EF Core / Dapper | **Not exploitable** — reviewed; all queries are parameterized (EF LINQ, Dapper `@Size`/`@Offset`), no string-concatenated SQL anywhere in the codebase |
| 8 | `EmailQueue` (in-memory) | Denial of Service | `Channel.CreateUnbounded<string>()` — an authenticated caller can still grow this queue without bound (auth fix in #2 raises the bar, doesn't remove the ceiling) | **Open, documented** — not fixed this pass, see Honest Gaps |
| 9 | Application Insights telemetry | Information Disclosure | Does captured telemetry (`db.statement`, request bodies) leak quote author/text as PII-adjacent data? | **Checked, low risk** — EF Core instrumentation logs the parameterized SQL text, not bound parameter values; nothing in this app's data is sensitive PII by design (public quotes) |
| 10 | Live deployment secret hygiene | Information Disclosure | A real Service Bus SAS connection string (`RootManageSharedAccessKey`, full manage rights) was found sitting in plaintext in the live Container App's environment variables during this very pass | **Fixed** — namespace deleted (secret no longer exists), stale env var removed; this is the kind of finding a security pass is supposed to catch, and it caught one of its own artifacts |

---

## 2. The private-endpoint change

`infra/modules/private-endpoint.bicep` — puts the SQL server behind a private endpoint instead of
the public internet, with a private DNS zone so the FQDN still resolves inside the VNet:

```bicep
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: privateEndpointName
  location: location
  properties: {
    subnet: { id: subnetResourceId }
    privateLinkServiceConnections: [
      {
        name: '${privateEndpointName}-connection'
        properties: {
          privateLinkServiceId: sqlServerResourceId
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.database.windows.net'
  location: 'global'
}
```

Takes an existing subnet resource ID as a parameter — same "existing resource" pattern used for
`containerAppsEnvironmentResourceId` throughout Days 23-26.

---

## 3. OpenAPI hardening

**Auth.** 14 endpoints had zero authentication — internal/test/ops routes that leaked outbox and
dead-letter payloads, allowed anonymous republish/poison-injection into the real Service Bus
topic, and let anyone flip a process-wide chaos flag that fails every other caller's requests.
All now require `.RequireAuthorization()`. Verified live: previously-open routes now return 401
with no token.

**Input limits.** `/cqrs/quotes/feed` and `/feed-dapper` had no cap on `size` — clamped to 1-100,
matching `/api/quotes`'s existing pattern (a real inconsistency: the limit already existed on one
endpoint and just never got applied to its siblings). Added `Kestrel.Limits.MaxRequestBodySize =
64KB` globally — verified live with a 200KB payload, got `413`.

**Rate limiting.** `/api/auth/login` had no brute-force protection at all. Added a fixed-window
limiter, 5 requests/minute per IP. Verified live: attempts 1-5 return 401 (wrong password),
attempt 6 returns `429`.

**Versioning.** Added `Asp.Versioning.Http`, header-based (`X-Api-Version`), defaulted to v1 so
no existing caller breaks. Applied to the `/api/quotes*` and `/cqrs/quotes*` groups — the core
product surface; internal/test/ops endpoints weren't versioned, since they aren't public API
surface in the OpenAPI sense.

---

## 4. ZAP baseline scan

Ran `zap-baseline.py` (OWASP ZAP, official Docker image) against the live, hardened deployment —
the same Container App used throughout this pass, real traffic, not a local mock:

```
docker run --rm -v "$(pwd)/zap:/zap/wrk:rw" zaproxy/zap-stable zap-baseline.py \
  -t https://quotes-api.agreeablemoss-41b8d1af.centralindia.azurecontainerapps.io \
  -r zap-baseline-report.html -w zap-baseline-report.md -J zap-baseline-report.json
```

**First run** — 62 checks passed, 0 failed, 5 warnings, all missing-header findings:

```
WARN-NEW: Re-examine Cache-control Directives [10015] x 1
WARN-NEW: X-Content-Type-Options Header Missing [10021] x 1
WARN-NEW: Strict-Transport-Security Header Not Set [10035] x 3
WARN-NEW: Storable and Cacheable Content [10049] x 3
WARN-NEW: Cross-Origin-Resource-Policy Header Missing or Invalid [90004] x 1
FAIL-NEW: 0  WARN-NEW: 5  INFO: 0  PASS: 62
```

**Fixed** — added the missing headers in `Program.cs`:

```csharp
app.UseHsts();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
```

**Verified live** after redeploying — a second full ZAP container run got stuck (Docker container
health-checked itself as unhealthy for two days without exiting; killed it rather than debug a
tooling hang unrelated to the app), so verified the same thing more directly instead, against the
same live endpoint:

```
$ curl -sI https://quotes-api.agreeablemoss-41b8d1af.centralindia.azurecontainerapps.io/health
cache-control: no-store
strict-transport-security: max-age=2592000
x-content-type-options: nosniff
cross-origin-resource-policy: same-origin
```

All 4 header classes ZAP flagged are present. (`Storable and Cacheable Content` [10049] is the
same underlying gap as `Cache-control` [10015] — one `no-store` header line resolves both.)

---

## 5. Honest gaps

- **Private endpoint isn't actually wired to live traffic.** The shared Container Apps
  environment (`thinkschool-env`) all these days' Container Apps run in has **no VNet at all** —
  confirmed live (`vnetConfiguration: null`). The private-endpoint module is real and compiles,
  but connecting the existing compute tier to it would mean VNet-injecting that shared
  environment, a bigger change to shared infrastructure than this pass should make unasked.
- **A live secret was found — fixed by deleting it.** The temporary `sb-day26-tracedemo` Service
  Bus namespace's `RootManageSharedAccessKey` connection string was sitting in plaintext in the
  live Container App's env vars. It was a throwaway demo resource, not production data, so the
  fix was deleting the namespace outright (the secret no longer exists anywhere) and removing the
  now-dead env var — not architecting Key Vault integration for a resource that no longer exists.
- **`EmailQueue` is still unbounded.** Auth now gates who can call `/jobs/email`, but nothing
  caps how many messages a legitimate caller can queue. Not fixed this pass.
- **Rate limiting is in-memory, per-process.** It won't survive a redeploy (confirmed while
  testing this fix — repeated redeploys reset the counter) and won't coordinate across replicas
  if this ever scales beyond one. A distributed store (Redis, which this app already has) would
  fix both; not implemented this pass.
- **XSS/output-encoding is the frontend's job, not audited here.** Quote `Author`/`Text` are
  stored as submitted; this API's boundary is the JSON contract, not how Angular renders it.
- **ZAP ran against one build, one point in time.** Not wired into CI — a one-off scan, not a
  gate.
