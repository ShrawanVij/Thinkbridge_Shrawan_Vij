# Day 29 — Build day 1: foundation + happy path

## What this is built on

Day 28's ADR ([`../../day-28/Piece1/ADR.md`](../../day-28/Piece1/ADR.md)) called for a modular
monolith: Quotes / Identity / Collections / Engagement, one deployable, module boundaries that
fail the build rather than a code review. That ADR was written without knowing Day 22 Piece 2
(`QuotesHub`) already scaffolded exactly this shape as a from-scratch kickoff project — Clean
Architecture, real Domain/Application/Infrastructure separation, its own SQLite/in-memory stack.

Given the choice between adopting that scaffold (and re-building every feature Weeks 2-5 already
shipped — real SQL persistence, JWT auth, refresh-token rotation, the Service Bus outbox, managed
identity, security hardening) or applying the same module-boundary discipline directly to the
real, already-hardened `QuotesApi`, today's actual direction (confirmed this session) was the
second: convert `QuotesApi` itself into a modular monolith, in place, keeping every feature that
was already real. `QuotesHub` stays as a separate reference scaffold; nothing here depends on it.

## The module split

Four modules, each its own `.csproj`, each with its own SQLite database (no shared tables, no
shared `DbContext`), referencing only `QuotesApi.Contracts`:

| Module | Owns | Talks to other modules via |
|---|---|---|
| **Quotes** | `Quote`, `Tag`, the outbox, Service Bus publisher, quote CRUD + edit + feed | `Contracts.QuoteCreatedEvent` (out), `Contracts.IQuoteOwnershipCheck` (implements it) |
| **Identity** | `User`, `RefreshToken`, JWT issuing, login/refresh/logout, the ownership-authorization handler | `Contracts.IQuoteOwnershipCheck` (consumes it — never touches Quotes' `DbContext`) |
| **Collections** | `Collection`, `CollectionItem` | `Contracts.IClock` |
| **Engagement** | `AuditLog`, the email queue, both Service Bus subscription workers | `Contracts.QuoteCreatedEvent` (in) |

`QuotesApi.Contracts` has zero dependencies on any module — it's the one thing every module is
allowed to reference, matching the boundary rule from the ADR.

**The one place the split didn't go the way the ADR first assumed:** the ADR's Day 29 plan called
the group "Notifications" and lumped the outbox in with it. In the real code, the outbox row is
written in the *same database transaction* as the `Quote` insert (`CreateQuoteCommandHandler`
begins a transaction, adds the `Quote`, adds the `OutboxMessage`, commits once) — that's the whole
point of the outbox pattern, and it only works if both rows live in one `DbContext`. So the outbox
stays inside **Quotes**, not a separate module; what actually turned out fully separable and
event-driven-only was the audit/notification consumers, which became **Engagement**. The
boundary was correct in spirit, wrong in exactly one detail, and the real code is what corrected
it — the kind of thing a plan written before touching the code can't know in advance.

**Cross-module authorization**, done properly instead of the obvious shortcut: the "can this user
edit/delete this quote" check used to live in Identity's authorization handler as a direct EF
query against the shared `QuoteDbContext`. Once Identity and Quotes have separate databases, that
query is a boundary violation. It's now `IQuoteOwnershipCheck` (in `Contracts`), implemented in
Quotes, injected into Identity's `CanModifyOwnQuoteHandler` — Identity asks a question, it doesn't
reach into another module's table to answer it itself.

## New features

**Edit a quote** — `PUT /cqrs/quotes/{id}`, same validation as create (author ≤100 chars, text
≤1000), same ownership model as the existing delete (`can-edit-own-quote` policy, backed by the
same `CanModifyOwnQuoteRequirement` the delete policy already used — one handler, two policies,
since the check is identical and only the action differs).

**Logout** — `POST /api/auth/logout`. Revokes the refresh token behind the cookie (so it can't be
replayed even though it hasn't expired yet) and clears the cookie. Doesn't require a valid access
token, since a caller with an already-expired one still needs to be able to log out. This also
closed a real gap in the existing refresh logic: a token revoked *without* a replacement (which
logout is the first thing to ever produce — token rotation always revoked-with-a-replacement) fell
through the original reuse-detection check and would have still been accepted by `/api/auth/refresh`.
Fixed as part of adding logout, verified by `Logout_RevokesRefreshToken_SoItCanNoLongerBeUsedToRefresh`.

The Angular frontend's `AuthService.logout()` now also calls this endpoint (best-effort — local
logout still always clears local state even if the network call fails). No interceptor changes
were needed: every route the frontend already calls kept its path, so `auth.interceptor.ts`,
`error.interceptor.ts`, and `retry.interceptor.ts` are all untouched.

## What was cut to keep "foundation" honest

Removed the second, older `POST /api/quotes` (raw `Quote`-body) create endpoint — the frontend has
never called it (confirmed by grepping `quotes-feed/src`; it only ever posts to `/cqrs/quotes`),
and keeping two parallel ways to create a quote across a module boundary wasn't worth the
duplication. `GET`/`DELETE /api/quotes/{id}` and `GET /api/quotes` stay, since the frontend uses
them.

Deferred, not lost — these existed in the pre-split `QuotesApi` and aren't gone, just not carried
into today's build:

- OpenTelemetry/App Insights tracing (Day 26), Redis/HybridCache (Day 21), Polly resilience demo
  endpoints (Day 22), and the outbox-crash-simulator/poison-message/dead-letter chaos-testing
  endpoints (Days 19-22) — none of these are part of "foundation + happy path," and re-wiring
  each into four separate modules properly is real work that belongs in a later polish pass, not
  bolted back on today just to preserve a demo endpoint nobody's using yet.
- Each module uses `Database.EnsureCreated()` against its own local SQLite file, not EF Core
  migrations against Azure SQL. The Week 5 Bicep/Deployment Stack still targets one Container App
  running one image, which this split doesn't change — but wiring the four module databases to
  the real provisioned Azure SQL server (schema-per-module or database-per-module) and writing
  real migrations for each is not done this session.
- The frontend has no edit-quote UI yet — the backend endpoint is real and tested, there's just
  nothing calling it from the app.

## Verified, not just claimed

`dotnet build QuotesApi.slnx` — all 7 projects build clean.

`dotnet test QuotesApi.slnx` — 22 of 22 pass, including the new edit/logout tests and the ported
auth/collection tests (each test run now gets its own isolated SQLite files per module — the
original shared-file-by-default setup would have let one test's auto-increment ids leak into
another's assertions, which the new edit-ownership tests would have hit immediately).

Ran the real Host (`dotnet run`) and walked the happy path with curl against the live process:

```
$ curl -X POST /api/auth/login -d '{"email":"test@example.com","password":"Test123!"}'
{"access_token":"eyJ...","expires_in":900}

$ curl -X POST /cqrs/quotes -H "Authorization: Bearer <token>" \
    -d '{"author":"Ada Lovelace","text":"That brain of mine is something more than merely mortal."}'
{"id":1,"author":"Ada Lovelace","text":"...","userId":1,"createdAt":"2026-09-16T07:35:42Z"}
HTTP:201

$ curl -X PUT /cqrs/quotes/1 -H "Authorization: Bearer <token>" \
    -d '{"author":"Ada Lovelace","text":"...(edited)"}'
HTTP:200

$ curl /cqrs/quotes/feed
[{"id":1,"author":"Ada Lovelace","text":"...(edited)", ...}]   <- edit landed

$ curl -X POST /api/auth/logout
HTTP:204

$ curl -X POST /api/auth/refresh   # same cookie as before logout
HTTP:401   <- revoked, can't be replayed

$ curl -X PUT /cqrs/quotes/1        # no token
HTTP:401
$ curl -X DELETE /api/quotes/1      # no token
HTTP:401
```

Frontend: `npx ng test --watch=false` — 61 of 61 pass, including the two new logout tests
(revoke-call succeeds, and local state still clears when the revoke call fails).

## Local Service Bus

`servicebus-emulator/` — same `quote-events` topic and `notify-sub`/`audit-sub` subscriptions as
Day 27's, now consumed by Engagement's two workers instead of QuotesApi's. Start it with
`docker compose up` before running the Host if you want the outbox relay to actually deliver
instead of logging connection errors on each poll (which it does harmlessly — the outbox row still
gets written in the same transaction as the quote either way, so the synchronous happy path above
doesn't depend on the emulator being up).
