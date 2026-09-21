import { Component, inject } from '@angular/core';
import { QuoteFeedComponent } from './quote-feed/quote-feed.component';
import { CreateQuoteFormComponent } from './create-quote/create-quote-form.component';
import { CreateQuoteFormReactiveComponent } from './create-quote/create-quote-form-reactive.component';
import { LoginFormComponent } from './auth/login-form.component';
import { AuthService } from './auth/auth.service';
import { ApiQuotesDemoComponent } from './api-quotes-demo/api-quotes-demo.component';

@Component({
  selector: 'app-root',
  imports: [
    QuoteFeedComponent,
    CreateQuoteFormComponent,
    CreateQuoteFormReactiveComponent,
    LoginFormComponent,
    ApiQuotesDemoComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly authService = inject(AuthService);
}