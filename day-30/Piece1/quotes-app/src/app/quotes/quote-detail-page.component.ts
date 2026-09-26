import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { QuoteService } from './quote.service';
import { QuoteDetail } from './quote.model';
import { CollectionService } from '../collections/collection.service';
import { Collection } from '../collections/collection.model';

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
  private readonly collectionService = inject(CollectionService);
  private readonly location = inject(Location);

  readonly isAuthenticated = this.authService.isAuthenticated;
  readonly quote = signal<QuoteDetail | null>(null);
  readonly loading = signal(true);
  readonly newTagName = signal('');
  readonly tagError = signal<string | null>(null);

  readonly collections = signal<Collection[]>([]);
  readonly addToCollectionError = signal<string | null>(null);

  readonly memberCollections = computed(() => this.collections().filter((c) => this.isInCollection(c)));
  readonly availableCollections = computed(() => this.collections().filter((c) => !this.isInCollection(c)));

  readonly quoteId = Number(this.route.snapshot.paramMap.get('id'));

  constructor() {
    this.load();

    if (this.isAuthenticated()) {
      this.loadCollections();
    }
  }

  private loadCollections(): void {
    this.collectionService.getMine().subscribe((collections) => this.collections.set(collections));
  }

  goBack(): void {
    this.location.back();
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

  isInCollection(collection: Collection): boolean {
    return collection.items.some((item) => item.quoteId === this.quoteId);
  }

  toggleCollection(collection: Collection): void {
    this.addToCollectionError.set(null);

    const call = this.isInCollection(collection)
      ? this.collectionService.removeItem(collection.id, this.quoteId)
      : this.collectionService.addItem(collection.id, this.quoteId);

    call.subscribe({
      next: () => this.loadCollections(),
      error: () => this.addToCollectionError.set('Could not update that collection.'),
    });
  }

  addToCollectionById(collectionId: string): void {
    const collection = this.collections().find((c) => c.id === Number(collectionId));
    if (collection) {
      this.toggleCollection(collection);
    }
  }
}
