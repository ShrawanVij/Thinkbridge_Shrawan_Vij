# ADR 0001: Modular monolith for the capstone, not microservices, not the status quo

## Status

Accepted — 2026-09-16

**Update, Day 29 (build day):** the module the sections below call "Notifications" was built as
**Engagement** (Jobs/Messaging/Audit, matching the name a since-noticed Day 22 scaffold already
used for the same concept), and the outbox stayed inside **Quotes** rather than moving with it —
the outbox row is written in the same DB transaction as the `Quote` insert, which only works if
both live in one `DbContext`. The boundary reasoning below still held; this is the one detail the
real code corrected. Full account in [`../../day-29/Piece1/EXERCISE.md`](../../day-29/Piece1/EXERCISE.md).

**Update, Day 30 (infra):** the Decision and Consequences below assumed the modular split would
stay deploy-neutral — same Container App, same Bicep template, no infra change. That assumption is
now superseded: Day 30 provisions fresh Azure infra for the modular build and decommissions the
Week 5 resources built for the old flat `QuotesApi`, rather than reusing them. The architecture
choice itself (modular monolith over microservices) is unaffected — only the "infra stays as-is"
consequence no longer holds.

## Context

By Day 27, `QuotesApi` is a single ASP.NET Core project (`QuotesApi.csproj`), one `QuoteDbContext`,
deployed as one Azure Container App via the Bicep + `azd` Deployment Stack built on Days 23-24. It
already carries five distinct bounded contexts, all flattened into the same folders:

- **Quotes** — `Models/Quote.cs`, `Models/Tag.cs`, `Features/Quotes/*` (the newer CQRS slice)
- **Collections** — `Models/Collection.cs`, `Models/CollectionItem.cs`
- **Identity** — `Models/User.cs`, `Models/RefreshToken.cs`, `Models/JwtOptions.cs`,
  `Authorization/*`
- **Notifications/Audit** — `Jobs/EmailQueue.cs`+`EmailWorker.cs`, `Messaging/*` (Service Bus
  publisher, `NotifySubscriptionWorker`, `AuditSubscriptionWorker`), `Outbox/*`, `Models/AuditLog.cs`

All five sit in one flat `Models/` folder and one `QuoteDbContext`. Nothing stops, say,
`QuoteRepository` from reaching into `User` directly — there's no seam to cross.

The project already has the infrastructure a service split would want: Service Bus
(`quote-events` topic, `notify-sub`/`audit-sub` subscriptions), a transactional outbox
(`OutboxMessage`/`OutboxRelay`), managed identity for SQL and Service Bus (Day 25), and
end-to-end distributed tracing into App Insights (Day 26). It also runs on a real but finite
budget — the Day 23 `what-if` output confirms the subscription is "Azure for Students," not an
unlimited enterprise account.

The capstone build window is four days (29-32) plus this design day. Whatever architecture is
chosen has to be built, tested, and shipped in that window by one person.

## Decision

Reorganize `QuotesApi` into a **modular monolith**: four in-process modules — **Quotes**,
**Identity**, **Collections**, **Notifications** (folding Jobs/Messaging/Outbox/Audit together,
since they're already fully event-driven) — each owning its own tables and exposing only a
published **Contracts** surface (interfaces + DTOs) to the others. Cross-module calls go through
Contracts or the existing Service Bus/outbox path — never through another module's EF entities or
repositories directly. The whole thing still deploys as the single Container App / single Bicep
template Days 23-27 already built and hardened; the split is code-only, not an infra change.

## Alternatives considered

**A. Leave it as one flat project (status quo).** Rejected. The `Models/` folder already mixes
five bounded contexts with no seam between them, and every Week 5 exercise (identity, tracing,
security) had to reason about the whole project because there was nowhere smaller to look. Adding
Days 29-31's features on top of the same flat structure only makes that worse, and a capstone
that's supposed to demonstrate architectural judgment shouldn't just mean "more files in the same
folders."

**B. Split into microservices** — a separate Container App, Bicep module, and database per bounded
context (`quotes-svc`, `identity-svc`, `notify-svc`). Rejected for this timeline, not on
principle. The project already has the right prerequisites (Service Bus, outbox, distributed
tracing), but going further this week means also solving cross-service transactional integrity
correctly — e.g. deleting a `User` today is one DB transaction that cascades to their `Quotes`,
`RefreshTokens`, and `AuditLogs`; split across services, that becomes a saga, and there isn't room
in four days to build and verify that live on top of feature completeness and polish. It also
multiplies the Bicep modules, CI surface, and service-to-service auth by 3-4x against a
student-tier subscription. Revisit after the capstone, one module at a time — Notifications first,
since it's already the closest to isolated.

**C. Modular monolith (chosen).** Keeps the single deployable and single database, so none of the
Week 5 Bicep/Deployment Stack/Identity/App Insights work is at risk — but forces the same
bounded-context seams a microservices split would eventually need, so the boundary work done now
isn't thrown away if a module is extracted later.

## Consequences

- Week 5 infra (Bicep, Deployment Stacks, managed identity, App Insights wiring, ZAP baseline)
  stays valid unchanged, since the split is internal to the one project.
- Notifications (Jobs + Messaging + Outbox + audit workers) is already accidentally
  module-shaped from Days 18-22 — it's the lowest-risk module to extract first and the proof of
  the pattern (see Day 29 plan and the critique below).
- One shared SQL database means one module's migration or lock contention can still affect
  another's latency. Accepted at this scale; revisit if a module is ever pulled into its own
  service with its own database.
- Folder-based boundaries are a convention, not something the compiler enforces, until a module
  becomes its own class library. This gap surfaced in review — see
  [`EXERCISE.md`](EXERCISE.md#the-critique) for how it changed the plan.

