import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QuoteDetail, QuoteFeedItem } from './quote.model';
import { CreateQuoteRequest, CreateQuoteResult } from '../create-quote/create-quote.model';

export type SortOrder = 'newest' | 'oldest' | 'author';

@Injectable({ providedIn: 'root' })
export class QuoteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://127.0.0.1:5220';

  getFeed(page: number, size: number | null, sort: SortOrder): Observable<QuoteFeedItem[]> {
    const sizeParam = size ? `&size=${size}` : '';
    return this.http.get<QuoteFeedItem[]>(`${this.baseUrl}/cqrs/quotes/feed?page=${page}&sort=${sort}${sizeParam}`);
  }

  getById(id: number): Observable<QuoteDetail> {
    return this.http.get<QuoteDetail>(`${this.baseUrl}/api/quotes/${id}`);
  }

  /** Real Week-1 contract: GET /api/quotes?page=N&size=N -> Quote[] ({id, author, text, userId, createdAt, tags}). */
  getQuotesPage(page: number, size: number): Observable<QuoteDetail[]> {
    return this.http.get<QuoteDetail[]>(`${this.baseUrl}/api/quotes?page=${page}&size=${size}`);
  }

  createQuote(request: CreateQuoteRequest): Observable<CreateQuoteResult> {
    return this.http.post<CreateQuoteResult>(`${this.baseUrl}/cqrs/quotes`, request);
  }
}