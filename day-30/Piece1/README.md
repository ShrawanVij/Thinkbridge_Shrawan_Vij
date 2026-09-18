# Day 30 — Build day 2: feature completeness

Closed the real feature gaps found after Day 29's modular split: registration, a real
Collections read side (plus a genuine ownership bug fix), a working Tags API, an Engagement
read endpoint, real server-side search, and a brand-new Angular frontend (`quotes-app`) —
separate from `day-17`'s `quotes-feed` — built against the modular backend.

**Deliverable:** [EXERCISE.md](EXERCISE.md) — what changed, the bug found and fixed along the
way, and what's still open for the PR review.

## Result

| | |
|---|---:|
| New backend endpoints | register, `GET /collections`, `GET /collections/{id}`, tags (attach/detach/list), `GET /api/engagement/audit-logs` |
| Real bug fixed | Collections ownership — `OwnerId` came from the request body; any authenticated user could create/modify another user's collection |
| Real bug found *while fixing another bug* | Fixing the "tags never show" bug (missing `.Include`) surfaced a JSON serialization cycle (`Quote.Tags` ↔ `Tag.Quotes`) that 500'd every tagged quote — fixed by projecting to a DTO |
| Server-side search | `GET /cqrs/quotes/feed?search=...&mine=true` — was client-side filtering of one fetched page before |
| Backend tests | 22 → **39**, all passing |
| New frontend | `quotes-app/` — Angular 22, standalone components, signals — login/register/logout, quote feed with real search + "mine" filter, create/edit quote, tag management, collections |
| Frontend tests | 2/2 passing (scaffold-level; component tests deferred, see EXERCISE.md) |
| Infra (Bicep/Azure) | Bicep updated (4 SQL databases, topic+subscriptions, managed-identity Service Bus) + app code wired to actually use it — **not run**; provisioning and decommissioning Week 5's resources is yours |
| PR | **not opened this pass** — yours to push/open, per how we've worked throughout |

## Layout

```
day-30/Piece1/
├── README.md            this file
├── EXERCISE.md            the deliverable
├── QuotesApi.slnx
├── azure.yaml
├── infra/                  updated Bicep — 4 SQL databases, topic+subscriptions, not yet run
├── servicebus-emulator/
├── src/                    same 4-module + Contracts + Host shape as Day 29, extended
├── tests/QuotesApi.Tests/  39 tests
└── quotes-app/             new Angular frontend (separate from day-17/quotes-feed)
```
