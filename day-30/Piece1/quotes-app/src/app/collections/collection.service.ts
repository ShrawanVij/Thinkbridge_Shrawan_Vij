import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Collection } from './collection.model';

@Injectable({ providedIn: 'root' })
export class CollectionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getMine(): Observable<Collection[]> {
    return this.http.get<Collection[]>(`${this.baseUrl}/collections`);
  }

  getById(id: number): Observable<Collection> {
    return this.http.get<Collection>(`${this.baseUrl}/collections/${id}`);
  }

  create(name: string): Observable<Collection> {
    return this.http.post<Collection>(`${this.baseUrl}/collections`, { name });
  }

  addItem(collectionId: number, quoteId: number): Observable<Collection> {
    return this.http.post<Collection>(`${this.baseUrl}/collections/${collectionId}/items`, { quoteId });
  }

  removeItem(collectionId: number, quoteId: number): Observable<Collection> {
    return this.http.delete<Collection>(`${this.baseUrl}/collections/${collectionId}/items/${quoteId}`);
  }
}
