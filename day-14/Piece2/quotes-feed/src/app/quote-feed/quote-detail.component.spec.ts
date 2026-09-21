import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { QuoteDetailComponent } from './quote-detail.component';
import { QuoteDetail } from './quote.model';

const QUOTE_A: QuoteDetail = {
  id: 1,
  author: 'Ada Lovelace',
  text: 'That brain of mine is something more than merely mortal.',
  userId: 1,
  createdAt: '2026-08-22T05:35:31Z',
  tags: [{ id: 1, name: 'wisdom' }],
};

const QUOTE_B: QuoteDetail = {
  id: 2,
  author: 'Albert Einstein',
  text: 'Imagination is more important than knowledge.',
  userId: 1,
  createdAt: '2025-01-08T07:39:00Z',
  tags: [],
};

@Component({
  selector: 'app-host',
  imports: [QuoteDetailComponent],
  template: `<app-quote-detail [quoteId]="id()" />`,
})
class HostComponent {
  readonly id = signal<number | null>(null);
}

describe('QuoteDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows the empty state when no quote is selected', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Select a quote');
  });

  it('shows loading, then the fetched quote', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.id.set(1);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Loading detail');

    httpMock.expectOne('http://127.0.0.1:5220/api/quotes/1').flush(QUOTE_A);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
    expect(fixture.nativeElement.textContent).toContain('wisdom');
  });

  it('shows an error message when the detail request fails', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.id.set(1);
    fixture.detectChanges();

    httpMock.expectOne('http://127.0.0.1:5220/api/quotes/1').flush('not found', { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Could not load this quote');
  });

  it('does not let a stale response for quote A overwrite quote B once B is selected', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.id.set(1);
    fixture.detectChanges();
    const reqA = httpMock.expectOne('http://127.0.0.1:5220/api/quotes/1');

    fixture.componentInstance.id.set(2);
    fixture.detectChanges();
    const reqB = httpMock.expectOne('http://127.0.0.1:5220/api/quotes/2');

    reqB.flush(QUOTE_B);
    fixture.detectChanges();
    reqA.flush(QUOTE_A);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Albert Einstein');
    expect(fixture.nativeElement.textContent).not.toContain('Ada Lovelace');
  });
});