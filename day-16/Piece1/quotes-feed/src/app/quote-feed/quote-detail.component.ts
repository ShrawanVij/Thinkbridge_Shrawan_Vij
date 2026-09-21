import { Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { QuoteDetail } from './quote.model';
import { QuoteService } from './quote.service';

@Component({
  selector: 'app-quote-detail',
  imports: [DatePipe],
  templateUrl: './quote-detail.component.html',
  styleUrl: './quote-detail.component.css',
})
export class QuoteDetailComponent {
  private readonly quoteService = inject(QuoteService);

  readonly quoteId = input<number | null>(null);

  readonly quote = signal<QuoteDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);

  constructor() {
    effect(() => {
      const id = this.quoteId();

      if (id === null) {
        this.quote.set(null);
        this.loading.set(false);
        this.error.set(false);
        return;
      }

      this.loading.set(true);
      this.error.set(false);

      this.quoteService.getById(id).subscribe({
        next: (quote) => {
          if (this.quoteId() !== id) {
            return;
          }
          this.quote.set(quote);
          this.loading.set(false);
        },
        error: () => {
          if (this.quoteId() !== id) {
            return;
          }
          this.error.set(true);
          this.loading.set(false);
        },
      });
    });
  }
}