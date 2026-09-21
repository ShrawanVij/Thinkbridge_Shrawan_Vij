import { Component, computed, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Quote } from './quote.model';

export type SortOrder = 'newest' | 'oldest' | 'author';

@Component({
  selector: 'app-quote-feed',
  imports: [DatePipe],
  templateUrl: './quote-feed.component.html',
  styleUrl: './quote-feed.component.css',
})
export class QuoteFeedComponent {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = 'http://127.0.0.1:5220/cqrs/quotes/feed';

  readonly quotes = signal<Quote[]>([]);
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

  constructor() {
    effect(() => {
      const size = this.pageSize();
      const sort = this.sortOrder();
      const sizeParam = size ? `&size=${size}` : '';
      const url = `${this.baseUrl}?page=1&sort=${sort}${sizeParam}`;

      this.loading.set(true);
      this.error.set(false);

      this.http.get<Quote[]>(url).subscribe({
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
}