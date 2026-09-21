import { Component, inject, signal } from '@angular/core';
import { QuoteService } from '../quote-feed/quote.service';
import { QuoteDetail } from '../quote-feed/quote.model';
import { AppError } from '../core/http/app-error.model';

const PAGE_SIZE = 5;

@Component({
  selector: 'app-api-quotes-demo',
  templateUrl: './api-quotes-demo.component.html',
  styleUrl: './api-quotes-demo.component.css',
})
export class ApiQuotesDemoComponent {
  private readonly quoteService = inject(QuoteService);

  readonly page = signal(1);
  readonly quotes = signal<QuoteDetail[] | null>(null);
  readonly loading = signal(false);
  readonly error = signal<AppError | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.quoteService.getQuotesPage(this.page(), PAGE_SIZE).subscribe({
      next: (quotes) => {
        this.quotes.set(quotes);
        this.loading.set(false);
      },
      error: (err: AppError) => {
        this.error.set(err);
        this.quotes.set(null);
        this.loading.set(false);
      },
    });
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }
}