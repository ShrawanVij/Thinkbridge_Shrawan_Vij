import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { QuoteDetailPageComponent } from './quote-detail-page.component';

describe('QuoteDetailPageComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuoteDetailPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('parses a real numeric route param and fetches the matching quote by its real id field', () => {
    const fixture = TestBed.createComponent(QuoteDetailPageComponent);
    fixture.componentRef.setInput('id', '42');
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/quotes/42'));
    req.flush({ id: 42, author: 'Ada Lovelace', text: 'A real quote.', userId: 1, createdAt: '2026-01-01T00:00:00Z', tags: [] });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('A real quote.');
  });

  it('shows an invalid-id message for a non-numeric route param, without calling the API', () => {
    const fixture = TestBed.createComponent(QuoteDetailPageComponent);
    fixture.componentRef.setInput('id', 'not-a-number');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid quote id in the URL.');
    httpMock.expectNone(() => true);
  });

  it('shows an invalid-id message when no id input is bound at all', () => {
    const fixture = TestBed.createComponent(QuoteDetailPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Invalid quote id in the URL.');
  });
});