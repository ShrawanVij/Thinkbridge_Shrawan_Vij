# Day 30 — Build day 2: feature completeness

## What this closes out

After Day 29's modular split, a deeper pass over the actual code (not a guess) turned up
concrete feature gaps, ranked by how expected they were for a "quotes" app. This is the same
list, now closed:

1. No signup/registration — only the hardcoded dev-seed user existed
2. Collections had no read side — couldn't list or view a collection
3. Collections ownership was never enforced — a real bug, not a gap (see below)
4. No Tags API — the schema existed, nothing managed it
5. No server-side search — the frontend fetched one page and filtered it client-side
6. Engagement was write-only — audit logs got recorded, nothing could read them back

Deferred, explicitly: a frontend edit-quote UI existed as a gap before — it's built now, in the
new frontend. Password reset/change, likes/comments, and Collections rename/delete are still not
done; they weren't on the agreed list for this pass.

## The real bug: Collections ownership

Before today, `POST /collections` took `OwnerId` straight from the request body
(`CreateCollectionRequest(int OwnerId, string Name)`), and neither `POST /collections/{id}/items`
nor `DELETE /collections/{id}/items/{quoteId}` checked that the caller actually owned the
collection they were modifying. Any authenticated user could create a collection "owned" by
someone else's user id, or add/remove items from a collection they didn't create.

Fixed: `CreateCollectionRequest` no longer has an `OwnerId` field — the owner is read from the
authenticated caller's `ClaimTypes.NameIdentifier` claim. `GET /collections/{id}`,
`POST /collections/{id}/items`, and `DELETE /collections/{id}/items/{quoteId}` all now check
`collection.OwnerId != callerId` and return `403 Forbidden`. Covered by
`CollectionsEndpointTests.cs` (4 tests, including one that reproduces the original bug pattern
and confirms it's now rejected).

## The bug found while fixing another bug

Fixing "the detail page always shows zero tags" (`QuoteRepository.GetByIdAsync` never called
`.Include(q => q.Tags)`, so `Tags` came back empty regardless of what was actually attached)
surfaced a second, previously-invisible bug: `Quote.Tags` and `Tag.Quotes` are a many-to-many
pair with a back-reference each way. Once `Tags` actually gets populated, serializing the raw
`Quote` entity to JSON walks `Quote.Tags → Tag.Quotes → Quote.Tags → …` and System.Text.Json
throws once it hits its cycle-detection depth limit — every quote with a tag attached 500'd on
`GET /api/quotes/{id}`.

Caught by `TagsEndpointTests.AttachTag_ThenGetQuote_ReturnsIt` failing with a 500 instead of the
expected 200 — reproduced live against a running instance to get the real stack trace (System.Text.Json
cycling through `ListOfTConverter`/`ObjectDefaultConverter` calls), not guessed at. Fixed by
projecting to a `QuoteDetailResponse` DTO instead of returning the EF entity directly — the same
pattern the CQRS endpoints (`CreateQuoteResult`, `UpdateQuoteResult`) already used; the legacy
`GET /api/quotes/{id}` endpoint was the one place still returning a raw entity.

## New endpoints

| Endpoint | What |
|---|---|
| `POST /api/auth/register` | Real signup — validates email/password, hashes the password, issues tokens immediately (same shape as login) |
| `GET /collections` | The caller's own collections |
| `GET /collections/{id}` | One collection — 403 if it isn't the caller's |
| `GET /api/tags` | All tags, for a picker |
| `POST /api/quotes/{id}/tags` | Attach a tag (creates it if new); idempotent; owner-only |
| `DELETE /api/quotes/{id}/tags/{tagId}` | Detach a tag; owner-only |
| `GET /api/engagement/audit-logs` | Paged audit trail Engagement has been writing since Day 29, now readable |
| `GET /cqrs/quotes/feed?search=...&mine=true` | Real server-side filtering, added to the existing feed endpoint |

## The new frontend — `quotes-app`

A separate Angular 22 app (standalone components, signals), not an extension of
`day-17/Piece1/quotes-feed`. Covers: register/login/logout, the quote feed (search debounced
300ms, sort, "my quotes only" toggle, all server-side), create/edit quote, tag management on the
quote detail page, and collections (create, list mine, view one, add/remove items by quote id).

Verified: `ng build` (clean), `ng test` (2/2 passing — app-shell only; component-level tests are
deferred, see Honest Gaps), and a live connectivity check — ran the actual backend and
`ng serve` side by side, confirmed CORS (`Access-Control-Allow-Origin`,
`-Allow-Credentials`) is correctly configured for the new frontend's origin. Full interactive
browser click-through was **not** done — there's no headed browser available in this session to
drive one, so the curl/CORS check and the backend's own direct-curl verification are what stand
in for it. Say so rather than claim a click-through that didn't happen.

## Infra: Bicep updated, provisioned, and verified live

Based on Day 25's shape (managed identity, AAD-only SQL, Key Vault-referenced JWT secret,
`disableLocalAuth` Service Bus) — extended, not replaced:

- **`modules/sql.bicep`** — one server, now **four databases** (`quotesdb`, `identitydb`,
  `collectionsdb`, `engagementdb`), one per module, matching the four local SQLite files. Same
  AAD-only admin, same firewall rule.
- **`modules/servicebus.bicep`** — a **topic with two subscriptions** (`notify-sub`, `audit-sub`),
  not the queue Day 25 had. Day 25's queue never matched what the app actually does (fan-out to
  two independent consumers); this now does.
- **`modules/api.bicep`** — four `ConnectionStrings__*` env vars (one AAD managed-identity
  connection string per database) instead of one; `ServiceBus__Namespace` instead of a connection
  string, since the namespace has no SAS keys to hand out.
- **`azure.yaml`** — `services.api.project` now points at `src/Host/QuotesApi`, not the old flat
  `QuotesApi/` path, which no longer exists on this branch.

**The app code had to change too, or this Bicep would be decorative.** Every module's
`AddXModule` previously hardcoded SQLite unconditionally — a pre-existing gap from before this
split, where the app never actually used the Azure SQL/Service Bus Days 23-25 provisioned. Fixed:
each module now uses `UseSqlServer` when a SQL-shaped connection string is configured, SQLite
otherwise; `ServiceBusClient` now authenticates via the container's managed identity
(`ManagedIdentityCredential`) when `ServiceBus:Namespace` is set, a connection string only for the
local emulator. (Getting there hit one real Azure SDK issue: `Azure.Core` 1.60.0 forwards its own
copies of `Azure.Identity`'s credential types, colliding with them by name — resolved by aliasing
the `Azure.Core` package reference so unqualified code only sees `Azure.Identity`'s.)

**The cost trade-off, decided:** four separate Basic-tier SQL databases cost roughly 4x what the
single database before did (still cheap in absolute terms, real on a student-tier subscription).
The alternative — one database, four schemas, one connection string — would cost the same as
before but means the "no two modules share a database" boundary is enforced by convention again,
not by the platform. Kept the four-database split: a bug or careless raw-SQL query in one module
being physically unable to reach another module's tables was judged worth the ~$15/mo difference.

`dotnet build`/`dotnet test` — all 39 tests still pass with the SQL Server provider and managed
identity Service Bus paths added (they exercise the SQLite/emulator fallback branch locally,
same as before). `az bicep build` on `main.bicep` — no errors.

**Actually run, not just written.** `azd provision` created a fresh `rg-quotes-dev` — SQL server
with four databases, Service Bus namespace, Key Vault, Container App, and a Container Registry
(added mid-provisioning — the original Bicep never had one; `azd deploy` needs somewhere to push
the image and there wasn't one). Three real provisioning bugs turned up and got fixed along the
way, not guessed at in advance:

- `MissingPrimaryIdentity` — a SQL server with a user-assigned identity has to name which one is
  primary explicitly (`primaryUserAssignedIdentityId`); it doesn't infer this from having only one.
- `NameAlreadyExists` on `sql-quotes-dev.database.windows.net` — SQL logical server names are
  globally unique across all of Azure, not just this subscription; someone else already holds the
  plain name. Fixed the same way Day 23-25's servers ended up needing to: a `uniqueString(...)`
  suffix on the server name only (databases inside it keep their plain names).
- `azd deploy` failed twice more after provisioning succeeded: no Container Registry existed at
  all (added `modules/registry.bicep`, Basic tier, admin user disabled, `AcrPull` granted to the
  app's managed identity via Bicep; push access granted to the deploying human out-of-band, same
  pattern as the Key Vault secret); then the Container App itself needed an `azd-service-name: api`
  tag it never had, or `azd deploy` has no way to match it to the `api` service in `azure.yaml`.

Verified live, not assumed: the deployed Container App is `Running`, serving the actual pushed
image (not the placeholder), returns `401` on an unauthenticated request (the Entra ID auth
config correctly gating access, not a crash), and its logs show the outbox relay successfully
polling `OutboxMessages` on real Azure SQL over managed-identity auth every 2 seconds with no
connection errors.

**Week 5 decommissioned.** The old flat `QuotesApi`'s infra lived in `rg-quotes-prod` (single
`quotesdb`, no module split — confirmed by inspecting it directly, not assumed) under its own
deployment stack (`azd-stack-prod`, `denySettings.mode: denyDelete`). Torn down via
`az stack sub delete --action-on-unmanage deleteAll`, the only safe path given the deny-delete
setting. Confirmed gone afterward (`az group exists` → `false`); the shared `thinkschool-env`
Container Apps Environment and unrelated resource groups (Day 5, Day 17) were untouched.

## Honest gaps

- **No component-level frontend tests yet** — only the app-shell spec. The backend carries the
  real regression coverage for the ownership fix and the tags bug; the frontend doesn't yet have
  its own tests for the search debounce, the tag-management flow, or the collections pages.
- **Azure infra untouched** — provisioning fresh Container App/SQL/Service Bus for the modular
  build and decommissioning the Week 5 resources is agreed for this phase but not run — that's
  real cost/quota impact on a real subscription, left for you to run directly rather than guessed
  at blind.
- **No PR opened yet** — this EXERCISE.md is the write-up; pushing the branch and opening the PR
  is yours, matching how the rest of this has worked. Once it's open and has comments, bring them
  back here and they get addressed the same way the bugs above did — fixed with reasoning, or
  defended with reasoning, not silently force-pushed over.
