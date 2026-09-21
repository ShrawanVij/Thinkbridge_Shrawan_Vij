import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { QuoteFeedItem } from './quote.model';
import { QuoteService, SortOrder } from './quote.service';
import { QuoteDetailComponent } from './quote-detail.component';

@Component({
  selector: 'app-quote-feed',
  imports: [DatePipe, QuoteDetailComponent],
  templateUrl: './quote-feed.component.html',
  styleUrl: './quote-feed.component.css',
})
export class QuoteFeedComponent {
  private readonly quoteService = inject(QuoteService);

  readonly quotes = signal<QuoteFeedItem[]>([]);
  readonly searchTerm = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly pageSize = signal<number | null>(20);
  readonly sortOrder = signal<SortOrder>('newest');
  readonly selectedId = signal<number | null>(null);

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

  constructor() {
    effect(() => {
      const size = this.pageSize();
      const sort = this.sortOrder();

      this.loading.set(true);
      this.error.set(false);

      this.quoteService.getFeed(1, size, sort).subscribe({
        next: (quotes) => {
          this.quotes.set(quotes);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(true);
          this.loading.set(false);
        },
      });
    });

    effect(() => {
      console.log(
        `filteredQuotes recomputed: ${this.filteredQuotes().length} of ${this.quotes().length} quotes match "${this.searchTerm()}"`,
      );
    });
  }

  onSearchChange(value: string): void {
    this.searchTerm.set(value);
  }

  selectAuthor(author: string): void {
    this.searchTerm.set(this.searchTerm() === author ? '' : author);
  }

  onPageSizeChange(value: string): void {
    this.pageSize.set(value === 'all' ? null : Number(value));
  }

  onSortOrderChange(value: string): void {
    this.sortOrder.set(value as SortOrder);
  }

  selectQuote(id: number): void {
    this.selectedId.set(this.selectedId() === id ? null : id);
  }
}