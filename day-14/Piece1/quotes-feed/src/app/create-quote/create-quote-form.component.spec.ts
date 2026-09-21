import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CreateQuoteFormComponent } from './create-quote-form.component';

describe('CreateQuoteFormComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateQuoteFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  function create() {
    const fixture = TestBed.createComponent(CreateQuoteFormComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows both required errors and focuses the author field when submitted empty', () => {
    const fixture = create();

    fixture.nativeElement.querySelector('button[type=submit]').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Author is required.');
    expect(fixture.nativeElement.textContent).toContain('Text is required.');
    expect(document.activeElement?.id).toBe('quote-author');
  });

  it('renders the real API length limits as native maxlength attributes', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#quote-author').getAttribute('maxlength')).toBe('100');
    expect(fixture.nativeElement.querySelector('#quote-text').getAttribute('maxlength')).toBe('1000');
  });

  it('wires aria-invalid and aria-describedby onto an invalid, touched field', () => {
    const fixture = create();

    fixture.nativeElement.querySelector('button[type=submit]').click();
    fixture.detectChanges();

    const author = fixture.nativeElement.querySelector('#quote-author');
    expect(author.getAttribute('aria-invalid')).toBe('true');
    expect(author.getAttribute('aria-describedby')).toBe('quote-author-error');
    expect(fixture.nativeElement.querySelector('#quote-author-error').textContent).toContain('Author is required.');
  });

  it('submits the real fields, shows a success message, and resets the form', async () => {
    const fixture = create();
    const component = fixture.componentInstance;

    fixture.nativeElement.querySelector('#quote-author').value = 'Ada Lovelace';
    fixture.nativeElement.querySelector('#quote-author').dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('#quote-text').value = 'A real quote.';
    fixture.nativeElement.querySelector('#quote-text').dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button[type=submit]').click();

    const req = httpMock.expectOne((r) => r.url.endsWith('/cqrs/quotes'));
    expect(req.request.body).toEqual({ author: 'Ada Lovelace', text: 'A real quote.' });
    req.flush({ id: 1, author: 'Ada Lovelace', text: 'A real quote.', userId: 1, createdAt: '2026-01-01T00:00:00Z' });
    await fixture.whenStable();

    fixture.detectChanges();
    expect(component.successMessage()).toContain('Quote #1 by Ada Lovelace added.');
    expect(fixture.nativeElement.querySelector('#quote-author').value).toBe('');
  });

  it('maps a 400 validation-problem response onto the matching field, not a generic banner', async () => {
    const fixture = create();

    fixture.nativeElement.querySelector('#quote-author').value = 'Someone';
    fixture.nativeElement.querySelector('#quote-author').dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('#quote-text').value = 'Something';
    fixture.nativeElement.querySelector('#quote-text').dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button[type=submit]').click();

    const req = httpMock.expectOne((r) => r.url.endsWith('/cqrs/quotes'));
    req.flush(
      { title: 'One or more validation errors occurred.', status: 400, errors: { text: ['Text cannot exceed 1000 characters.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#quote-text-error').textContent).toContain('Text cannot exceed 1000 characters.');
    expect(fixture.nativeElement.querySelector('.form-error')).toBeNull();
  });

  it('shows a generic error banner on a non-validation failure (e.g. network/500)', async () => {
    const fixture = create();
    const component = fixture.componentInstance;

    fixture.nativeElement.querySelector('#quote-author').value = 'Someone';
    fixture.nativeElement.querySelector('#quote-author').dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('#quote-text').value = 'Something';
    fixture.nativeElement.querySelector('#quote-text').dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button[type=submit]').click();

    const req = httpMock.expectOne((r) => r.url.endsWith('/cqrs/quotes'));
    req.flush({ error: 'An unexpected error occurred.' }, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    fixture.detectChanges();
    expect(component.serverError()).toBe('Could not add the quote. Please try again.');
  });
});