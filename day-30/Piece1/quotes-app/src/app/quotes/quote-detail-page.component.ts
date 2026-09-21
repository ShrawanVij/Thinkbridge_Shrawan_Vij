import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { QuoteService } from './quote.service';
import { QuoteDetail } from './quote.model';

@Component({
  selector: 'app-quote-detail-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './quote-detail-page.component.html',
})
export class QuoteDetailPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly quoteService = inject(QuoteService);
  private readonly authService = inject(AuthService);

  readonly isAuthenticated = this.authService.isAuthenticated;
  readonly quote = signal<QuoteDetail | null>(null);
  readonly loading = signal(true);
  readonly newTagName = signal('');
  readonly tagError = signal<string | null>(null);

  private readonly quoteId = Number(this.route.snapshot.paramMap.get('id'));

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.quoteService.getById(this.quoteId).subscribe({
      next: (quote) => {
        this.quote.set(quote);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  addTag(): void {
    const name = this.newTagName().trim();
    if (!name) return;

    this.tagError.set(null);
    this.quoteService.addTag(this.quoteId, name).subscribe({
      next: () => {
        this.newTagName.set('');
        this.load();
      },
      // Most likely 403 -- tags can only be managed by the quote's owner.
      error: () => this.tagError.set('Only the owner of this quote can manage its tags.'),
    });
  }

  removeTag(tagId: number): void {
    this.quoteService.removeTag(this.quoteId, tagId).subscribe({
      next: () => this.load(),
      error: () => this.tagError.set('Only the owner of this quote can manage its tags.'),
    });
  }
}
