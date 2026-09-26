import { Component, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { QuoteService } from './quote.service';
import { QuoteFeedItem, SortOrder } from './quote.model';

@Component({
  selector: 'app-quote-feed-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './quote-feed-page.component.html',
})
export class QuoteFeedPageComponent {
  private readonly quoteService = inject(QuoteService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isAuthenticated = this.authService.isAuthenticated;

  readonly quotes = signal<QuoteFeedItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly searchTerm = signal('');
  readonly mineOnly = signal(false);
  readonly sortOrder = signal<SortOrder>('newest');

  private searchDebounce?: ReturnType<typeof setTimeout>;
  private requestId = 0;

  constructor() {
    effect((onCleanup) => {
      const term = this.searchTerm();
      const mine = this.mineOnly();
      const sort = this.sortOrder();

      clearTimeout(this.searchDebounce);
      this.searchDebounce = setTimeout(() => this.fetch(term, mine, sort), 300);

      onCleanup(() => clearTimeout(this.searchDebounce));
    });
  }

  private fetch(search: string, mine: boolean, sort: SortOrder): void {
    const thisRequestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(false);

    this.quoteService.getFeed({ page: 1, size: 20, sort, search: search || undefined, mine }).subscribe({
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
  }

  setSearchTerm(value: string): void {
    this.searchTerm.set(value);
  }

  toggleMineOnly(): void {
    this.mineOnly.update((v) => !v);
  }

  setSortOrder(value: string): void {
    this.sortOrder.set(value as SortOrder);
  }

  openQuote(id: number): void {
    this.router.navigate(['/quotes', id]);
  }

  deleteQuote(id: number): void {
    this.quoteService.deleteQuote(id).subscribe({
      next: () => this.quotes.update((quotes) => quotes.filter((q) => q.id !== id)),
      // Most likely a 403 (not the owner) -- the feed doesn't carry
      // per-quote ownership, so the button shows for every quote, and the
      // backend's ownership check is what actually decides.
      error: () => window.alert("You can only delete your own quotes."),
    });
  }
}
