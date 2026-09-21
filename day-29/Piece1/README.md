# Day 29 — Build day 1: foundation + happy path

`QuotesApi` split into a real modular monolith — four modules (Quotes, Identity, Collections,
Engagement), each its own project, boundaries enforced by compiler-checked project references,
not folder convention. Plus two new features on top: editing a quote, and a real logout.

**Deliverable:** [EXERCISE.md](EXERCISE.md) — what changed, what was verified live, and what's
deliberately deferred.

## Result

| | |
|---|---:|
| Modules split into their own `.csproj`, this session | **4 of 4** (Quotes, Identity, Collections, Engagement) — ahead of the Day 28 plan, which staged this over two days |
| Cross-module references (a module `ProjectReference`-ing another module) | **0** — only `Contracts` is shared, verified by the solution actually building |
| Automated tests | **22 of 22 passing** (backend) + **61 of 61 passing** (frontend, incl. 2 new for logout) |
| New: edit a quote | `PUT /cqrs/quotes/{id}`, ownership-checked, curl-verified live |
| New: logout | `POST /api/auth/logout`, revokes the refresh token + clears the cookie, curl-verified live |
| Happy path verified against a running instance | login → create → edit → feed shows the edit → logout → refresh correctly rejected |

## Layout

```
day-29/Piece1/
├── README.md               this file
├── EXERCISE.md              the deliverable
├── QuotesApi.slnx
├── servicebus-emulator/     local Service Bus emulator (same shape as Day 27's)
├── src/
│   ├── Contracts/QuotesApi.Contracts/         the only thing modules share
│   ├── Modules/
│   │   ├── Quotes/QuotesApi.Modules.Quotes/
│   │   ├── Identity/QuotesApi.Modules.Identity/
│   │   ├── Collections/QuotesApi.Modules.Collections/
│   │   └── Engagement/QuotesApi.Modules.Engagement/
│   └── Host/QuotesApi/                         composition root only
└── tests/QuotesApi.Tests/
```

The Angular frontend's logout button now calls the new endpoint too — see
[`day-17/Piece1/quotes-feed/src/app/auth/auth.service.ts`](../../day-17/Piece1/quotes-feed/src/app/auth/auth.service.ts).
No interceptor changes were needed: the modular split kept every existing route the frontend
already calls, and the auth/error/retry interceptors don't need to know logout exists.
