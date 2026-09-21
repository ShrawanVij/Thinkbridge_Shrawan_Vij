## Objective

Direct an agent to extract the quote feed's state out of the component and into a signal-based store service, against the real Week-1 API. Read the diff like a junior's PR, catch and fix a genuine bug, verify live, and defend where the line is between "plain signal service" and "reach for NgRx."

## 1. Brief given to the agent

> **Goal**: `quotes-feed`'s feed state (`quotes`, `loading`, `error`, `pageSize`, `sortOrder`, `searchTerm`, plus computed `filteredQuotes`/`availableAuthors`) currently lives directly inside `QuoteFeedComponent`, fetched via `QuoteService.getFeed(page, size, sort)`. Extract it into an injectable signal-based store service so the component becomes a thin consumer, without changing the template or existing behavior.
>
> **Real API contract** (Week-1 QuotesApi, base `http://127.0.0.1:5220`):
> - `GET /cqrs/quotes/feed?page=N&sort=newest|oldest|author&size=N` → `QuoteFeedItem[]` (`{id, author, text, createdAt, tags}`).
>
> **States to verify live, not just unit-tested**: loading before the first response, real data rendering, an empty filtered result, an error from a failed request, and what happens when the user changes the sort order faster than the network can respond (concurrent updates).

## 2. Agent's output

**Signal-based store** (`quote-feed/quote-feed.store.ts`, final version, after the fix in §3):
```typescript
@Injectable({ providedIn: 'root' })
export class QuoteFeedStore {
  private readonly quoteService = inject(QuoteService);

  readonly quotes = signal<QuoteFeedItem[]>([]);
  readonly searchTerm = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly pageSize = signal<number | null>(20);
  readonly sortOrder = signal<SortOrder>('newest');

  readonly filteredQuotes = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return this.quotes();
    return this.quotes().filter((quote) => quote.author.toLowerCase().includes(term));
  });

  readonly availableAuthors = computed(() => {
    const authors = new Set(this.quotes().map((quote) => quote.author));
    return [...authors].sort();
  });

  /** Bumped on every fetch so a late, stale response can be told apart from the current one. */
  private requestId = 0;

  constructor() {
    effect(() => {
      const size = this.pageSize();
      const sort = this.sortOrder();
      const thisRequestId = ++this.requestId;

      this.loading.set(true);
      this.error.set(false);

      this.quoteService.getFeed(1, size, sort).subscribe({
        next: (quotes) => {
          if (thisRequestId !== this.requestId) return;
          this.quotes.set(quotes);
          this.loading.set(false);
        },
        error: () => {
          if (thisRequestId !== this.requestId) return;
          this.error.set(true);
          this.loading.set(false);
        },
      });
    });
  }

  setSearchTerm(value: string): void { this.searchTerm.set(value); }
  selectAuthor(author: string): void { this.searchTerm.set(this.searchTerm() === author ? '' : author); }
  setPageSize(value: string): void { this.pageSize.set(value === 'all' ? null : Number(value)); }
  setSortOrder(value: string): void { this.sortOrder.set(value as SortOrder); }
}
```

`QuoteFeedComponent` now just injects the store and aliases its signals/methods; the template (`quote-feed.component.html`) was not touched:
```typescript
export class QuoteFeedComponent {
  private readonly store = inject(QuoteFeedStore);

  readonly quotes = this.store.quotes;
  readonly searchTerm = this.store.searchTerm;
  readonly loading = this.store.loading;
  readonly error = this.store.error;
  readonly pageSize = this.store.pageSize;
  readonly sortOrder = this.store.sortOrder;
  readonly filteredQuotes = this.store.filteredQuotes;
  readonly availableAuthors = this.store.availableAuthors;

  onSearchChange(value: string): void { this.store.setSearchTerm(value); }
  selectAuthor(author: string): void { this.store.selectAuthor(author); }
  onPageSizeChange(value: string): void { this.store.setPageSize(value); }
  onSortOrderChange(value: string): void { this.store.setSortOrder(value); }
}
```

**Rule for when to adopt NgRx:**

> I'll move off a plain signal service once the update logic itself needs rules, not just a `.set()` — e.g. today's fix required manually tracking a request generation number to reject stale responses. One more overlapping case like that (say, needing to cancel a previous save if a new edit starts, or coordinating two async operations that must not clobber each other) and I'd rather have NgRx's structured action/reducer flow than keep hand-rolling guards inside `effect()` blocks that get harder to reason about with each addition.

## 3. The bug caught (and fixed) reading the diff

The first pass had no protection against out-of-order network responses. Wrote `quote-feed.store.spec.ts`'s third test to change `sortOrder` twice rapidly (`oldest`, then `author`) and flush the two HTTP responses **out of order** — the newer `author` request answering first, the stale `oldest` request answering late:
```
AssertionError: expected [ { id: 1, ... } ] to deeply equal [ { id: 2, ... } ]
- "author": "Albert Einstein", "text": "Imagination is more important than knowledge."   (stale, oldest-sort)
+ "author": "Ada Lovelace",   "text": "That brain of mine is something more than merely mortal."  (expected, author-sort)
```
Confirmed: `store.sortOrder()` read `'author'`, but `store.quotes()` still held the stale `oldest`-sorted data — the store was trusting whichever response arrived *last*, not whichever request was made *last*. Fixed by adding a `requestId` generation counter (see §2): each fetch stamps its own id, and a response is only applied if that id still matches the current one. Re-ran the test: passes.

## 4. Verification log

- **Loading**: `store.loading()` asserted `true` immediately after a signal change, before the HTTP response is flushed (`quote-feed.store.spec.ts`, test 1).
- **Real data**: live against the running backend — `GET /cqrs/quotes/feed?page=1&sort=newest&size=20` returns real quotes (e.g. id `10017`, "Hello brother" — Prince Hamlet), and the home page renders them.
- **Error**: `store.error()` asserted `true` and `loading()` `false` after a flushed `500` response (`quote-feed.store.spec.ts`, test 2).
- **Empty**: live in the browser — filtering by a non-matching author shows "No quotes match...", `filteredQuotes().length === 0`.
- **Concurrent updates**: the race-condition test in §3, deterministic via `HttpTestingController`'s out-of-order `flush()` — this is the real, reproducible proof, since forcing a genuine network race live is not reliable. Live sanity check: rapid real `oldest → author → newest` clicks against the live backend settle correctly on `newest`'s real top quote ("Hello brother"), not a stale intermediate response.
- **Tests**: 60/60 passing (57 carried over + 3 new in `quote-feed.store.spec.ts`).

## 5. What breaks if the Week-1 API's feed endpoint or its fields change

- If `GET /cqrs/quotes/feed`'s response shape changed (e.g. `author` renamed), `filteredQuotes`'s author-filter and `availableAuthors`' chip list would both silently produce `undefined`/broken entries — no compile error, since `QuoteFeedItem` is a plain interface, not runtime-validated.
- If the endpoint stopped accepting `sort`/`size` query params, the store would still fire requests correctly, but the backend would just ignore the unrecognized params — there's no client-side way to detect a *silently ignored* param versus one that's actually being honored.
- If a new field were added that other parts of the app need (e.g. a `likeCount`), the store and `QuoteFeedItem` would both need updating together — the store has no schema validation layer, so a mismatch between what the API actually returns and what the interface claims would only surface as a runtime `undefined`, not a build error.