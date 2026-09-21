import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QuoteDetail, QuoteFeedItem } from './quote.model';
import { AuthService } from '../auth/auth.service';
import { CreateQuoteRequest, CreateQuoteResult } from '../create-quote/create-quote.model';

export type SortOrder = 'newest' | 'oldest' | 'author';

@Injectable({ providedIn: 'root' })
export class QuoteService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly baseUrl = 'http://127.0.0.1:5220';

  getFeed(page: number, size: number | null, sort: SortOrder): Observable<QuoteFeedItem[]> {
    const sizeParam = size ? `&size=${size}` : '';
    return this.http.get<QuoteFeedItem[]>(`${this.baseUrl}/cqrs/quotes/feed?page=${page}&sort=${sort}${sizeParam}`);
  }

  getById(id: number): Observable<QuoteDetail> {
    return this.http.get<QuoteDetail>(`${this.baseUrl}/api/quotes/${id}`);
  }

  createQuote(request: CreateQuoteRequest): Observable<CreateQuoteResult> {
    return this.http.post<CreateQuoteResult>(`${this.baseUrl}/cqrs/quotes`, request, {
      headers: this.authService.authHeader(),
    });
  }
}