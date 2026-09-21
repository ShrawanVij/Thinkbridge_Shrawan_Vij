import { Component, inject } from '@angular/core';
import { CreateQuoteFormComponent } from './create-quote-form.component';
import { CreateQuoteFormReactiveComponent } from './create-quote-form-reactive.component';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-create-quote-page',
  imports: [CreateQuoteFormComponent, CreateQuoteFormReactiveComponent],
  templateUrl: './create-quote-page.component.html',
  styleUrl: './create-quote-page.component.css',
})
export class CreateQuotePageComponent {
  protected readonly authService = inject(AuthService);
}