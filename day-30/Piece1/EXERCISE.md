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

## Infra: Bicep updated, not run

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

**A cost trade-off worth deciding before you run this, not after:** four separate Basic-tier SQL
databases cost roughly 4x what the single database before did (still cheap in absolute terms, but
real on a student-tier subscription). The alternative — one database, four schemas, one
connection string — would cost the same as before but means the "no two modules share a
database" boundary is enforced by convention again, not by the platform. Left as your call; the
Bicep as written takes the four-database option.

`dotnet build`/`dotnet test` — all 39 tests still pass with the SQL Server provider and managed
identity Service Bus paths added (they exercise the SQLite/emulator fallback branch, same as
before — there's no Azure SQL to test against from here). `az bicep build` on `main.bicep` — no
errors. Actually provisioning (`azd provision`) and decommissioning the Week 5 resources: not run,
per how we agreed to split this — real cost/quota impact on a real subscription.

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
