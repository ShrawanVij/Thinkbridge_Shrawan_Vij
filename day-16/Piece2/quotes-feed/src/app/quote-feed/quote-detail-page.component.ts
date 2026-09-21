import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { QuoteDetailComponent } from './quote-detail.component';

@Component({
  selector: 'app-quote-detail-page',
  imports: [QuoteDetailComponent, RouterLink],
  templateUrl: './quote-detail-page.component.html',
  styleUrl: './quote-detail-page.component.css',
})
export class QuoteDetailPageComponent {
  /** Bound from the real Week-1 route param -- GET /api/quotes/{id}'s id field. */
  readonly id = input<string>();

  readonly quoteId = computed(() => {
    const raw = this.id();
    if (raw === undefined) return null;
    const parsed = Number(raw);
    return Number.isNaN(parsed) ? null : parsed;
  });
}