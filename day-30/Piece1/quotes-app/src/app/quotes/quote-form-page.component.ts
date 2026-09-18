import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { QuoteService } from './quote.service';

@Component({
  selector: 'app-quote-form-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './quote-form-page.component.html',
})
export class QuoteFormPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly quoteService = inject(QuoteService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly editingId = this.route.snapshot.paramMap.get('id');
  readonly isEditMode = this.editingId !== null;

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  constructor() {
    if (this.editingId) {
      this.quoteService.getById(Number(this.editingId)).subscribe((quote) => {
        this.form.setValue({ author: quote.author, text: quote.text });
      });
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const request = this.form.getRawValue();
    const result$ = this.isEditMode
      ? this.quoteService.update(Number(this.editingId), request)
      : this.quoteService.create(request);

    result$.subscribe({
      next: (quote) => {
        this.submitting.set(false);
        this.router.navigate(['/quotes', quote.id]);
      },
      error: () => {
        this.submitting.set(false);
        this.errorMessage.set('Could not save this quote.');
      },
    });
  }
}
