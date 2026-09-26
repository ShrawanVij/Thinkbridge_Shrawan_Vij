import { Component, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { QuoteService } from './quote.service';
import { QuoteDetail } from './quote.model';
import { CollectionService } from '../collections/collection.service';
import { Collection } from '../collections/collection.model';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-quote-form-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './quote-form-page.component.html',
})
export class QuoteFormPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly quoteService = inject(QuoteService);
  private readonly collectionService = inject(CollectionService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editingId = this.route.snapshot.paramMap.get('id');
  readonly isEditMode = this.editingId !== null;

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly collections = signal<Collection[]>([]);
  readonly selectedCollectionIds = signal<ReadonlySet<number>>(new Set());
  readonly selectedCollections = computed(() =>
    this.collections().filter((c) => this.selectedCollectionIds().has(c.id)),
  );
  readonly availableCollections = computed(() =>
    this.collections().filter((c) => !this.selectedCollectionIds().has(c.id)),
  );

  readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
    tags: [''],
  });

  constructor() {
    if (this.editingId) {
      this.quoteService.getById(Number(this.editingId)).subscribe((quote) => {
        this.form.patchValue({ author: quote.author, text: quote.text });
      });
    }

    if (this.authService.isAuthenticated()) {
      this.collectionService.getMine().subscribe((collections) => this.collections.set(collections));
    }
  }

  toggleCollection(id: number): void {
    this.selectedCollectionIds.update((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  addCollectionById(collectionId: string): void {
    const id = Number(collectionId);
    if (!Number.isNaN(id)) {
      this.toggleCollection(id);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const { author, text, tags } = this.form.getRawValue();
    const request = { author, text };
    const result$ = this.isEditMode
      ? this.quoteService.update(Number(this.editingId), request)
      : this.quoteService.create(request);

    result$.subscribe({
      next: (quote) => this.applyTagsAndCollections(quote, tags),
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Could not save this quote.');
      },
    });
  }

  // The quote itself is already saved by this point — tags/collections are a
  // best-effort follow-up, so a failure here still lands the user on their
  // saved quote rather than losing it behind a form error.
  private applyTagsAndCollections(quote: QuoteDetail, tagsInput: string): void {
    const tagNames = tagsInput
      .split(',')
      .map((name) => name.trim())
      .filter((name) => name.length > 0);

    const tagCalls = tagNames.map((name) =>
      this.quoteService.addTag(quote.id, name).pipe(catchError(() => of(null))),
    );
    const collectionCalls = [...this.selectedCollectionIds()].map((collectionId) =>
      this.collectionService.addItem(collectionId, quote.id).pipe(catchError(() => of(null))),
    );

    forkJoin([of(null), ...tagCalls, ...collectionCalls]).subscribe(() => {
      this.submitting.set(false);
      this.router.navigate(['/quotes', quote.id]);
    });
  }
}
