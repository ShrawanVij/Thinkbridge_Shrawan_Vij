# Day 22 — Capstone Kickoff: QuotesHub

## Objective
Pick a real product slice, design it as a modular monolith (clean architecture by default, not microservices), scaffold the solution structure, and write the one-page design.

Product slice: **QuotesHub**. Basically the same domain as the Week-1 `QuotesApi`, but restructured from feature folders into real module boundaries. The idea is that as this app grows and different areas start needing different teams or release schedules, it could split into separate services without much drama - but it stays one deployable until there's an actual reason not to. No point paying for distributed-systems problems before you have them.

---

## One-Page Design

### Bounded contexts

Four modules. Quotes is the core one - it's the actual reason this product exists. The rest exist to support it:

| Module | What it does |
|---|---|
| **Quotes** | Authoring and tagging quotes |
| **Collections** | Grouping quotes into named lists |
| **Identity** | Users, auth. Nothing special, could be swapped for any auth provider |
| **Engagement** | Notifications and an audit trail. Doesn't do anything unless Quotes tells it something happened |

Each module gets its own Domain, Application and Infrastructure project - three projects per module, not shared layers across the whole app. Rule I stuck to: nothing in one module's Domain or Application can reference another module's Domain or Application. So when Collections needs to point at a quote, it just holds a `QuoteRef` (a plain `Guid` wrapper), not the real `Quote` class from the Quotes module. That's basically the entire boundary, in one small type.

### Core aggregate: `Quote`

Lives in `Quotes.Domain`. You can only build one through `Quote.Create(...)`, and every setter is private, so nothing outside the class can leave it in a bad state.

What it actually enforces:
- Author required, capped at 100 characters. Text required, capped at 1000. Checked inside `Create`, not off in some separate validator class.
- Up to 10 tags. Tagging the same thing twice doesn't throw, it's just a no-op. Going past 10 does throw.
- Creating one raises `QuoteCreatedDomainEvent`, which is what feeds the async flow below.

Honestly this is the only aggregate I really fleshed out. `Collection` and `User` exist and have a couple of small invariants too, but `Quote` is where the actual design thinking went, since it's the core domain and the rest are secondary.

### Async flows

What happens when someone creates a quote:

1. `CreateQuoteCommandHandler` calls `Quote.Create(...)`. That raises a domain event.
2. `QuotesDbContext.SaveChangesAsync` writes the `Quote` row and an outbox row for that event, in the same transaction. One can't happen without the other.
3. A relay picks up the outbox row and publishes it to the `quote-events` topic on Service Bus. This is the Day 20 pattern - I designed around it rather than rebuilding it here.
4. Engagement has its own subscription to that topic. Competing consumers, idempotent on `MessageId`, same setup as Day 19-21. When a message arrives it turns into a `RecordQuoteNotificationCommand`, which writes a `NotificationRecord`.

```
[Quotes]  Quote.Create()
             |
             v  raises QuoteCreatedDomainEvent
          SaveChangesAsync
             |  writes Quote row + Outbox row, one transaction
             v
          outbox relay  ---publishes--->  quote-events topic (Service Bus)
                                              |
                                              v
                                   [Engagement]  QuoteCreatedConsumer
                                              |  MessageId-idempotent
                                              v
                                   RecordQuoteNotificationCommand
                                              |
                                              v
                                        NotificationRecord
```

Quotes never calls Engagement directly, and Engagement never touches the Quotes database - the message on the bus is the only thing connecting them. That's kind of the whole point: if Engagement ever needed to become its own service, this wouldn't need a rewrite, just a different deploy target. There's no reason to actually do that right now, so it stays in the same process.

One honest note: `QuoteCreatedConsumer` isn't wired to anything real, it's a stub (there's a comment in the file explaining why). Didn't see much point setting up a live Service Bus subscription just for a kickoff scaffold when that exact wiring already works, proven on Day 19-21.

---

## Scaffolded Solution Layout

Bare folder structure first, then what's actually in each project:

```
QuotesHub.slnx
src/
├─ SharedKernel/
│  └─ QuotesHub.SharedKernel/
├─ Modules/
│  ├─ Quotes/
│  │  ├─ QuotesHub.Modules.Quotes.Domain/
│  │  ├─ QuotesHub.Modules.Quotes.Application/
│  │  └─ QuotesHub.Modules.Quotes.Infrastructure/
│  ├─ Collections/
│  │  ├─ QuotesHub.Modules.Collections.Domain/
│  │  ├─ QuotesHub.Modules.Collections.Application/
│  │  └─ QuotesHub.Modules.Collections.Infrastructure/
│  ├─ Identity/
│  │  ├─ QuotesHub.Modules.Identity.Domain/
│  │  ├─ QuotesHub.Modules.Identity.Application/
│  │  └─ QuotesHub.Modules.Identity.Infrastructure/
│  └─ Engagement/
│     ├─ QuotesHub.Modules.Engagement.Domain/
│     ├─ QuotesHub.Modules.Engagement.Application/
│     └─ QuotesHub.Modules.Engagement.Infrastructure/
└─ Host/
   └─ QuotesHub.Api/
tests/
└─ QuotesHub.Modules.Quotes.Domain.Tests/
```

| Module | Domain | Application | Infrastructure |
|---|---|---|---|
| **SharedKernel** | `Entity<TId>`, `AggregateRoot<TId>`, `IDomainEvent`, `IIntegrationEvent` | — | — |
| **Quotes** | `Quote` (aggregate root), `QuoteId`, `AuthorId`, `Tag`, `QuoteCreatedDomainEvent` | `CreateQuoteCommand`+Handler, `IQuoteRepository`, `IUnitOfWork` | `QuotesDbContext` (EF Core/SQLite, writes the outbox row inside `SaveChangesAsync`), `QuoteRepository`, `QuotesModule` (DI registration) |
| **Collections** | `Collection` (aggregate root), `QuoteRef` | `ICollectionRepository` | `InMemoryCollectionRepository`, `CollectionsModule` |
| **Identity** | `User` (aggregate root) | `IUserRepository` | `InMemoryUserRepository`, `IdentityModule` |
| **Engagement** | `NotificationRecord` | `RecordQuoteNotificationCommand`+Handler, `INotificationRepository` | `InMemoryNotificationRepository`, `QuoteCreatedConsumer` (scaffolded, not wired), `EngagementModule` |
| **Host** (`QuotesHub.Api`) | — | — | `Program.cs` — calls `AddQuotesModule` / `AddCollectionsModule` / `AddIdentityModule` / `AddEngagementModule`. Nothing else touches a module's internals. |
| **tests** | — | — | `QuotesHub.Modules.Quotes.Domain.Tests` — 5 invariant tests on `Quote` (create validation, idempotent tagging, max-tags), all passing |

A couple of notes on this:

- The dependency direction isn't just written down somewhere, it's enforced by the actual project references. `Domain` only references `SharedKernel`. `Application` references its own `Domain`. `Infrastructure` references its own `Application` and `Domain`. `Host` references every module's `Infrastructure` and nothing else - it never touches a `Domain` or `Application` project directly. Get the reference direction wrong and the solution just won't compile, which is the point of doing it this way instead of trusting everyone to follow a rule.
- This isn't just folders sitting there for show. The solution builds clean across all 15 projects, the 5 domain tests pass, and `POST /api/quotes` on the running host actually returns `201`, with the `Quote` row and its `OutboxMessages` row both written in the same transaction - checked that against the SQL log, not just assumed.
