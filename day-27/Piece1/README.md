# Day 27 — Security pass

STRIDE-lite threat model, a private-endpoint module for the data tier, a real OpenAPI hardening
pass (14 endpoints had zero auth), and a real OWASP ZAP baseline scan against the live deployment
— 5 findings, all fixed and verified live.

**Deliverable:** [EXERCISE.md](EXERCISE.md) — the threat model, the private-endpoint change, and
the ZAP baseline summary with what was fixed.

## Result

| | |
|---|---:|
| Endpoints with zero auth, found and fixed | **14 of 14** |
| Unbounded pagination endpoints, found and fixed | **2** |
| Request body size limit | none → 64KB (verified: 413 live) |
| Login brute-force protection | none → 5/min/IP (verified: 429 live) |
| ZAP baseline scan (live) | 62 pass, 0 fail, 5 warn — all 5 fixed and verified live |
| Live plaintext secret found during this pass | found and deleted |

## Layout

```
day-27/Piece1/
├── README.md          this file
├── EXERCISE.md          the deliverable
├── QuotesApi/            hardened application code
├── redis/, servicebus-emulator/    local dev dependencies
├── zap/                  ZAP scan reports (html/json/md)
└── infra/
    └── modules/
        └── private-endpoint.bicep
```

Everything was tested against the same live Container App used since Day 26 (per instruction, not
a fresh throwaway deployment) — real auth checks, real rate limiting, a real ZAP scan against real
traffic, not local mocks.
