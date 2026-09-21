# Day 28 — Design review + ADR

Design review for the Week 6 capstone build. One architecture decision, written up as an ADR;
a day-by-day build plan for Days 29-32; and the critique that changed the plan.

No mentor/peer review was available for this pass, so the critique below is a self-review — I
argued against my own first draft before locking the plan in. Flagged as such, not presented as
someone else's feedback.

## The ADR

[`ADR.md`](ADR.md) —
`QuotesApi` becomes a modular monolith (Quotes / Identity / Collections / Notifications modules,
one deployable, one database) instead of staying a flat single project or splitting into
microservices. Full context, alternatives, and consequences are in the ADR; the short version:
the project already has the Service Bus/outbox/tracing plumbing microservices would need, but not
the four spare days it would take to also get cross-service transactional integrity right — so
the seams get drawn now, the network hop doesn't.

## The critique

**First draft of the decision** said: reorganize the existing folders into
`Modules/Quotes`, `Modules/Identity`, `Modules/Collections`, `Modules/Notifications` inside the
one `QuotesApi.csproj`, and treat that as the module boundary.

**Self-review, before locking it in:** folder-only boundaries don't fix anything the project
doesn't already have wrong. `Models/` today already puts `Quote`, `Collection`, `User`, and
`AuditLog` side by side, and nothing currently stops `QuoteRepository` from reaching into `User`
directly — that isn't a rule being broken, it's a rule that was never written. Renaming
`Models/User.cs` to `Modules/Identity/User.cs` doesn't add that rule, it just gives the same
absence of a rule nicer folder names. If Day 28's job is to pick a decision that will actually
hold once Day 29-31's time pressure hits, it needs to fail the *build*, not fail a code review
someone might skip.

**How it changed the design:** the module boundary was escalated from "folder convention" to
"separate class library per module" for at least one module inside the build window, instead of
folders everywhere. Each module becomes its own `.csproj` (`QuotesApi.Modules.Notifications`,
etc.), referenced only by a thin `QuotesApi.Host` composition project — a module physically cannot
see another module's internals, because there is no project reference to see them through, only
its published Contracts. Given four days, this gets applied first and fully to **Notifications**
(Jobs/Messaging/Outbox/Audit), since Days 18-22 already left it the closest to isolated — it's the
proof that the pattern holds under a real build, not just on paper. Quotes/Identity/Collections
get the same treatment on Day 30 if there's room; if there isn't, they stay folder-only with that
gap written down honestly in the Day 31/32 notes rather than claimed as a boundary that isn't
real — the same "document the gap, don't hide it" call Day 25 made about the SQL admin identity
and Day 27 made about the unbounded `EmailQueue`.

## Day-by-day build plan (Days 29-32)

### Day 29 — Build day 1: foundation + happy path

- Add `QuotesApi.Host` (composition project: `Program.cs`, DI wiring, endpoint mapping) and
  `QuotesApi.Modules.Notifications` (own `.csproj`) as the first real module split — move
  `Jobs/`, `Messaging/`, `Outbox/`, `Models/AuditLog.cs` into it.
- Define `Notifications`' Contracts surface (`IQuoteEventPublisher`, the DTOs `NotifySubscriptionWorker`/`AuditSubscriptionWorker` consume) — nothing outside the module touches `OutboxMessage` or the Service Bus client types directly.
- Quotes and Identity move to `Modules/Quotes/` and `Modules/Identity/` folders (not yet separate
  projects) so the folder shape matches where Day 30 is headed.
- Re-run the existing `azd provision` against `dev` unchanged — confirms the Week 5
  Bicep/Deployment Stack still holds with zero infra edits, since this is a code-only change.
- Exit criteria: solution builds as `Host` + `Modules.Notifications` + the rest; existing
  integration tests (`AuthorizationTests`, `CollectionTests`, `CancellationTests`, etc.) green;
  one clean `dev` deploy.

### Day 30 — Build day 2: feature completeness

- Extract `Identity` (`User`, `RefreshToken`, `JwtOptions`, `Authorization/*`) into its own
  `.csproj` the same way Notifications was done on Day 29.
- Decide, based on remaining time, whether `Quotes`/`Collections` get the same full `.csproj`
  split or stay folder-only for this capstone — write down whichever is true, per the critique
  above.
- Wire the cross-module calls that used to be plain in-process calls (e.g. Quotes needing the
  current user's id/roles) through each module's Contracts interfaces, resolved via DI in
  `QuotesApi.Host` — not through a direct reference to another module's project.
- Confirm `QuoteCreatedEvent → Service Bus → NotifySubscriptionWorker/AuditSubscriptionWorker`
  still works unchanged — Notifications already only talks to the rest of the system over Service
  Bus, so this is the existing proof the pattern doesn't need a rewrite of already-working code.
- Exit criteria: no module project has a `ProjectReference` to another module's project, only to
  its Contracts; full feature set (quotes CRUD, collections, auth, notifications, audit) works
  end to end against the `dev` deployment.

### Day 31 — Polish: tests, perf, security

- Add an architecture test (reflection-based assembly-dependency check) asserting no module
  assembly references another module's internal namespace, only its Contracts — turns the
  critique's fix into something CI checks, not something a reviewer has to remember to look for.
- Re-run the Day 27 ZAP baseline against the reorganized app — confirm the refactor didn't
  reopen any of the 14 auth gaps or 2 unbounded-pagination findings that pass already fixed.
- Re-check the Day 8/11/23 projection and indexing work, and the Day 21 `HybridCache` usage,
  still apply per-module and that the new Contracts DTOs didn't reintroduce an over-fetch.
- Security: note whether per-module data access still runs under the single AAD-admin identity
  flagged as a gap in Day 25's README, or whether least-privilege per-module DB roles got added —
  document whichever is actually true.

### Day 32 — Ship + demo + postmortem

- Final `azd provision` to the prod-shaped environment using the unchanged Week 5 Bicep — proves
  the modular-monolith refactor was deploy-neutral, as the ADR claims.
- Demo: one request that crosses all four modules, shown as a single stitched distributed trace
  in App Insights (extending the Day 26 proof that tracing survives a Service Bus hop).
- Postmortem: which module boundaries actually became separate `.csproj`s versus stayed
  folder-only-with-a-documented-gap, and whether the Day 28 critique (fail the build, not the
  review) held up once real deadline pressure hit on Days 30-31.
