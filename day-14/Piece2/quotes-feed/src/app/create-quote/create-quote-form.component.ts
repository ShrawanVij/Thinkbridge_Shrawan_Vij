import { Component, ElementRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { form, required, maxLength, submit, FormField } from '@angular/forms/signals';
import { QuoteService } from '../quote-feed/quote.service';
import { CreateQuoteRequest, ValidationProblemDetails } from './create-quote.model';

@Component({
  selector: 'app-create-quote-form',
  imports: [FormField],
  templateUrl: './create-quote-form.component.html',
  styleUrl: './create-quote-form.component.css',
})
export class CreateQuoteFormComponent {
  private readonly quoteService = inject(QuoteService);
  private readonly heading = viewChild.required<ElementRef<HTMLHeadingElement>>('heading');

  readonly serverError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  constructor() {
    // This component only exists in the DOM once the user is authenticated, so this runs
    // exactly when the login form is replaced by this one - move focus so keyboard/screen
    // reader users land here instead of wherever the browser resets focus to (document body).
    afterNextRender(() => this.heading().nativeElement.focus());
  }

  private readonly model = signal<CreateQuoteRequest>({ author: '', text: '' });

  readonly quoteForm = form(this.model, (f) => {
    required(f.author, { message: 'Author is required.' });
    maxLength(f.author, 100, { message: 'Author cannot exceed 100 characters.' });
    required(f.text, { message: 'Text is required.' });
    maxLength(f.text, 1000, { message: 'Text cannot exceed 1000 characters.' });
  });

  async onSubmit(): Promise<void> {
    this.serverError.set(null);
    this.successMessage.set(null);

    await submit(this.quoteForm, {
      action: async () => {
        try {
          const result = await firstValueFrom(this.quoteService.createQuote(this.model()));
          this.successMessage.set(`Quote #${result.id} by ${result.author} added.`);
          this.model.set({ author: '', text: '' });
          return undefined;
        } catch (err) {
          if (err instanceof HttpErrorResponse && err.status === 400 && err.error?.errors) {
            const problem = err.error as ValidationProblemDetails;
            return Object.entries(problem.errors).flatMap(([key, messages]) => {
              const target = key === 'author' ? this.quoteForm.author : key === 'text' ? this.quoteForm.text : this.quoteForm;
              return messages.map((message) => ({ fieldTree: target, kind: 'server', message }));
            });
          }
          this.serverError.set(
            err instanceof HttpErrorResponse && (err.status === 401 || err.status === 403)
              ? 'You must be logged in to add a quote.'
              : 'Could not add the quote. Please try again.',
          );
          return undefined;
        }
      },
      onInvalid: () => {
        if (this.quoteForm.author().invalid()) {
          this.quoteForm.author().focusBoundControl();
        } else if (this.quoteForm.text().invalid()) {
          this.quoteForm.text().focusBoundControl();
        }
      },
    });
  }
}