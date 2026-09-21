import { Component, inject } from '@angular/core';
import { QuoteFeedComponent } from './quote-feed/quote-feed.component';
import { CreateQuoteFormComponent } from './create-quote/create-quote-form.component';
import { LoginFormComponent } from './auth/login-form.component';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [QuoteFeedComponent, CreateQuoteFormComponent, LoginFormComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly authService = inject(AuthService);
}