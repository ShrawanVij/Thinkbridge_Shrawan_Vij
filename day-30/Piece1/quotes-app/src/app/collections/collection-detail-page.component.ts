import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CollectionService } from './collection.service';
import { Collection } from './collection.model';

@Component({
  selector: 'app-collection-detail-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './collection-detail-page.component.html',
})
export class CollectionDetailPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly collectionService = inject(CollectionService);

  private readonly collectionId = Number(this.route.snapshot.paramMap.get('id'));

  readonly collection = signal<Collection | null>(null);
  readonly loading = signal(true);
  readonly forbidden = signal(false);
  readonly newQuoteId = signal<number | null>(null);
  readonly addQuoteError = signal<string | null>(null);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.collectionService.getById(this.collectionId).subscribe({
      next: (collection) => {
        this.collection.set(collection);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 403) this.forbidden.set(true);
      },
    });
  }

  addQuote(): void {
    const quoteId = this.newQuoteId();
    if (!quoteId) return;

    this.addQuoteError.set(null);
    this.collectionService.addItem(this.collectionId, quoteId).subscribe({
      next: () => {
        this.newQuoteId.set(null);
        this.load();
      },
      error: (err) => {
        this.addQuoteError.set(
          err.status === 404 ? `Quote #${quoteId} does not exist.` : 'Could not add this quote.',
        );
      },
    });
  }

  removeQuote(quoteId: number): void {
    this.collectionService.removeItem(this.collectionId, quoteId).subscribe(() => this.load());
  }

  delete(): void {
    if (!confirm('Delete this collection? This cannot be undone.')) return;

    this.collectionService.delete(this.collectionId).subscribe(() => this.router.navigate(['/collections']));
  }
}
