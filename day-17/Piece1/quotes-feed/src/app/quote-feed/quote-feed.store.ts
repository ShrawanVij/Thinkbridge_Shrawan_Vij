import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { QuoteFeedItem } from './quote.model';
import { QuoteService, SortOrder } from './quote.service';

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

    if (!term) {
      return this.quotes();
    }

    return this.quotes().filter((quote) =>
      quote.author.toLowerCase().includes(term),
    );
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

  setSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  selectAuthor(author: string): void {
    this.searchTerm.set(this.searchTerm() === author ? '' : author);
  }

  setPageSize(value: string): void {
    this.pageSize.set(value === 'all' ? null : Number(value));
  }

  setSortOrder(value: string): void {
    this.sortOrder.set(value as SortOrder);
  }

  deleteQuote(id: number): void {
    this.quoteService.deleteQuote(id).subscribe({
      next: () => {
        this.quotes.update((quotes) => quotes.filter((quote) => quote.id !== id));
      },
    });
  }
}
