import { Component } from '@angular/core';
import { QuoteFeedComponent } from './quote-feed/quote-feed.component';

@Component({
  selector: 'app-root',
  imports: [QuoteFeedComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
