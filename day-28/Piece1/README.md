# Day 28 — Design review + ADR

One real architecture decision for the capstone, written as an ADR, plus the Days 29-32 build
plan and the critique that changed it.

**Deliverable:** [EXERCISE.md](EXERCISE.md) — the critique and the build plan.
**ADR:** [ADR.md](ADR.md)

## Result

| | |
|---|---:|
| Decision | Modular monolith: Quotes / Identity / Collections / Notifications modules, one deployable, one database |
| Rejected alternatives | (A) status quo flat project, (B) microservices per bounded context |
| Modules fully split into their own `.csproj` by design (Day 29) | **1** — Notifications |
| Modules planned for Day 30 if time allows | Identity, then Quotes/Collections |
| Infra changes required by this decision | **0** — Week 5 Bicep/Deployment Stack untouched |
| Critique source | self-review (no mentor/peer pass available this round) |

## Layout

```
day-28/Piece1/
├── README.md          this file
├── EXERCISE.md          the deliverable — critique + Day 29-32 build plan
└── ADR.md               the ADR
```
