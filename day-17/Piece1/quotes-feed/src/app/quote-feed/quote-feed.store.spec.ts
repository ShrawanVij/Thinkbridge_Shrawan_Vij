import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { QuoteFeedStore } from './quote-feed.store';
import { QuoteFeedItem } from './quote.model';

const OLDEST_QUOTES: QuoteFeedItem[] = [
  { id: 1, author: 'Ada Lovelace', text: 'oldest-sorted result', createdAt: '2025-01-08T07:38:00Z', tags: '' },
];

const AUTHOR_QUOTES: QuoteFeedItem[] = [
  { id: 2, author: 'Albert Einstein', text: 'author-sorted result', createdAt: '2026-08-22T05:35:31Z', tags: '' },
];

describe('QuoteFeedStore', () => {
  let store: QuoteFeedStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    store = TestBed.inject(QuoteFeedStore);
    httpMock = TestBed.inject(HttpTestingController);

    TestBed.flushEffects();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush([]);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows loading, then real quotes, then an empty result for a fresh backend response', () => {
    expect(store.loading()).toBe(false);
    expect(store.quotes()).toEqual([]);

    store.setSortOrder('oldest');
    TestBed.flushEffects();
    expect(store.loading()).toBe(true);

    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(OLDEST_QUOTES);
    expect(store.loading()).toBe(false);
    expect(store.quotes()).toEqual(OLDEST_QUOTES);
  });

  it('sets error and clears loading when the feed request fails', () => {
    store.setSortOrder('author');
    TestBed.flushEffects();

    httpMock
      .expectOne((req) => req.url.includes('/cqrs/quotes/feed'))
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(store.loading()).toBe(false);
    expect(store.error()).toBe(true);
  });

  it('applies only the response matching the current sort order when two requests race out of order', () => {
    // User changes sort order twice in quick succession, before either response has arrived.
    store.setSortOrder('oldest');
    TestBed.flushEffects();
    const oldestReq = httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed'));
    expect(oldestReq.request.url).toContain('sort=oldest');

    store.setSortOrder('author');
    TestBed.flushEffects();
    const authorReq = httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed'));
    expect(authorReq.request.url).toContain('sort=author');

    // The network resolves them out of order: the newer ("author") request wins the race
    // and answers first; the stale ("oldest") request answers late.
    authorReq.flush(AUTHOR_QUOTES);
    oldestReq.flush(OLDEST_QUOTES);

    // The store's current sortOrder is 'author' -- the displayed quotes must match that,
    // not the stale 'oldest' response that happened to land last.
    expect(store.sortOrder()).toBe('author');
    expect(store.quotes()).toEqual(AUTHOR_QUOTES);
  });
});
