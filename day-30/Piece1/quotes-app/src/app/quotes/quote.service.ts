import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { QuoteDetail, QuoteFeedItem, SortOrder, Tag } from './quote.model';

export interface QuoteFeedOptions {
  page?: number;
  size?: number;
  sort?: SortOrder;
  search?: string;
  mine?: boolean;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

export interface UpdateQuoteRequest {
  author: string;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class QuoteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  // All filtering (search, "mine") happens server-side, in one request --
  // not fetch-a-page-then-filter-in-the-browser, which used to silently miss
  // any match outside whatever page happened to load.
  getFeed(options: QuoteFeedOptions): Observable<QuoteFeedItem[]> {
    let params = new HttpParams()
      .set('page', options.page ?? 1)
      .set('sort', options.sort ?? 'newest');

    if (options.size != null) {
      params = params.set('size', options.size);
    }
    if (options.search) {
      params = params.set('search', options.search);
    }
    if (options.mine) {
      params = params.set('mine', 'true');
    }

    return this.http.get<QuoteFeedItem[]>(`${this.baseUrl}/cqrs/quotes/feed`, { params });
  }

  getById(id: number): Observable<QuoteDetail> {
    return this.http.get<QuoteDetail>(`${this.baseUrl}/api/quotes/${id}`);
  }

  create(request: CreateQuoteRequest): Observable<QuoteDetail> {
    return this.http.post<QuoteDetail>(`${this.baseUrl}/cqrs/quotes`, request);
  }

  update(id: number, request: UpdateQuoteRequest): Observable<QuoteDetail> {
    return this.http.put<QuoteDetail>(`${this.baseUrl}/cqrs/quotes/${id}`, request);
  }

  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/quotes/${id}`);
  }

  addTag(quoteId: number, name: string): Observable<Tag[]> {
    return this.http.post<Tag[]>(`${this.baseUrl}/api/quotes/${quoteId}/tags`, { name });
  }

  removeTag(quoteId: number, tagId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/quotes/${quoteId}/tags/${tagId}`);
  }
}
