import { Component } from '@angular/core';
import { QuoteFeedComponent } from '../quote-feed/quote-feed.component';
import { ApiQuotesDemoComponent } from '../api-quotes-demo/api-quotes-demo.component';

@Component({
  selector: 'app-home',
  imports: [ApiQuotesDemoComponent, QuoteFeedComponent],
  templateUrl: './home.component.html',
  styleUrl: './home.component.css',
})
export class HomeComponent {}