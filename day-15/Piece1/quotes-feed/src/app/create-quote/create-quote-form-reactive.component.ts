import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { QuoteService } from '../quote-feed/quote.service';
import { ValidationProblemDetails } from './create-quote.model';

@Component({
  selector: 'app-create-quote-form-reactive',
  imports: [ReactiveFormsModule],
  templateUrl: './create-quote-form-reactive.component.html',
  styleUrl: './create-quote-form-reactive.component.css',
})
export class CreateQuoteFormReactiveComponent {
  private readonly quoteService = inject(QuoteService);
  private readonly fb = inject(FormBuilder);
  private readonly authorInput = viewChild.required<ElementRef<HTMLInputElement>>('authorInput');
  private readonly textInput = viewChild.required<ElementRef<HTMLTextAreaElement>>('textInput');

  readonly serverError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly submitting = signal(false);

  readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  authorErrorMessage(): string {
    const control = this.form.controls.author;
    if (control.hasError('required')) return 'Author is required.';
    if (control.hasError('maxlength')) return 'Author cannot exceed 100 characters.';
    if (control.hasError('server')) return control.getError('server');
    return '';
  }

  textErrorMessage(): string {
    const control = this.form.controls.text;
    if (control.hasError('required')) return 'Text is required.';
    if (control.hasError('maxlength')) return 'Text cannot exceed 1000 characters.';
    if (control.hasError('server')) return control.getError('server');
    return '';
  }

  async onSubmit(): Promise<void> {
    this.serverError.set(null);
    this.successMessage.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      if (this.form.controls.author.invalid) this.authorInput().nativeElement.focus();
      else if (this.form.controls.text.invalid) this.textInput().nativeElement.focus();
      return;
    }

    this.submitting.set(true);
    try {
      const { author, text } = this.form.getRawValue();
      const result = await firstValueFrom(this.quoteService.createQuote({ author, text }));
      this.successMessage.set(`Quote #${result.id} by ${result.author} added.`);
      this.form.reset({ author: '', text: '' });
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 400 && err.error?.errors) {
        const problem = err.error as ValidationProblemDetails;
        Object.entries(problem.errors).forEach(([key, messages]) => {
          const control = this.form.get(key);
          if (control) {
            control.setErrors({ server: messages[0] });
            control.markAsTouched();
          } else {
            this.serverError.set(messages[0]);
          }
        });
      } else {
        this.serverError.set(
          err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)
            ? 'You must be logged in to add a quote.'
            : 'Could not add the quote. Please try again.',
        );
      }
    } finally {
      this.submitting.set(false);
    }
  }
}