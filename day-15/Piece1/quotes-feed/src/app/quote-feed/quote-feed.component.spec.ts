import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { QuoteFeedComponent } from './quote-feed.component';
import { QuoteFeedItem } from './quote.model';

const SAMPLE_QUOTES: QuoteFeedItem[] = [
  { id: 1, author: 'Ada Lovelace', text: 'That brain of mine is something more than merely mortal.', createdAt: '2026-08-22T05:35:31Z', tags: '' },
  { id: 2, author: 'Albert Einstein', text: 'Imagination is more important than knowledge.', createdAt: '2025-01-08T07:39:00Z', tags: 'wisdom' },
  { id: 3, author: 'Ada Lovelace', text: 'The Analytical Engine has no pretensions whatever to originate anything.', createdAt: '2025-01-08T07:38:00Z', tags: '' },
];

describe('QuoteFeedComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuoteFeedComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the loading state before the API responds', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Loading quotes');

    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
  });

  it('renders every quote returned by the API once loaded', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.componentInstance.filteredQuotes().length).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('article.quote-card').length).toBe(3);
  });

  it('shows the @empty block when the API returns zero quotes', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush([]);
    fixture.detectChanges();

    expect(fixture.componentInstance.filteredQuotes().length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('No quotes match');
  });

  it('recomputes filteredQuotes when the search signal changes, without touching the quotes signal', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.filteredQuotes().length).toBe(3);

    component.onSearchChange('einstein');
    expect(component.filteredQuotes().length).toBe(1);
    expect(component.filteredQuotes()[0].author).toBe('Albert Einstein');
    expect(component.quotes().length).toBe(3);

    component.onSearchChange('nobody-matches-this');
    expect(component.filteredQuotes().length).toBe(0);

    component.onSearchChange('');
    expect(component.filteredQuotes().length).toBe(3);
  });

  it('derives the distinct, sorted author chip list from the quotes signal', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    expect(fixture.componentInstance.availableAuthors()).toEqual(['Ada Lovelace', 'Albert Einstein']);
  });

  it('selectAuthor toggles the search term on and off for the same author', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.selectAuthor('Ada Lovelace');
    expect(component.searchTerm()).toBe('Ada Lovelace');
    expect(component.filteredQuotes().length).toBe(2);

    component.selectAuthor('Ada Lovelace');
    expect(component.searchTerm()).toBe('');
    expect(component.filteredQuotes().length).toBe(3);
  });

  it('changing the page-size control refetches with the new size in the URL', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    fixture.componentInstance.onPageSizeChange('50');
    fixture.detectChanges();
    const req = httpMock.expectOne((r) => r.url.includes('/cqrs/quotes/feed'));
    expect(req.request.url).toContain('size=50');
    req.flush(SAMPLE_QUOTES);

    fixture.componentInstance.onPageSizeChange('all');
    fixture.detectChanges();
    const allReq = httpMock.expectOne((r) => r.url.includes('/cqrs/quotes/feed'));
    expect(allReq.request.url).not.toContain('size=');
    allReq.flush(SAMPLE_QUOTES);
  });

  it('changing the sort-order control refetches with the new sort in the URL', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock.expectOne((req) => req.url.includes('/cqrs/quotes/feed')).flush(SAMPLE_QUOTES);
    fixture.detectChanges();

    fixture.componentInstance.onSortOrderChange('oldest');
    fixture.detectChanges();
    const req = httpMock.expectOne((r) => r.url.includes('/cqrs/quotes/feed'));
    expect(req.request.url).toContain('sort=oldest');
    req.flush(SAMPLE_QUOTES);
  });

  it('shows an error message if the API call fails', () => {
    const fixture = TestBed.createComponent(QuoteFeedComponent);
    fixture.detectChanges();
    httpMock
      .expectOne((req) => req.url.includes('/cqrs/quotes/feed'))
      .flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Could not load quotes');
  });
});
