import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ApiQuotesDemoComponent } from './api-quotes-demo.component';
import { errorInterceptor } from '../core/http/error.interceptor';

describe('ApiQuotesDemoComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ApiQuotesDemoComponent],
      // errorInterceptor is what actually turns a raw HttpErrorResponse into
      // the AppError this component reads -- omitting it here would silently
      // test against a shape the component never sees in the real app.
      providers: [provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function create() {
    const fixture = TestBed.createComponent(ApiQuotesDemoComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows a loading state immediately on init', () => {
    const fixture = create();
    expect(fixture.nativeElement.querySelector('.state.loading')).not.toBeNull();
    httpMock.expectOne((r) => r.urlWithParams.includes('/api/quotes?page=1&size=5')).flush([]);
  });

  it('renders real quotes on success, matching the real {id, author, text} shape', () => {
    const fixture = create();

    httpMock.expectOne((r) => r.urlWithParams.includes('/api/quotes?page=1&size=5')).flush([
      { id: 1, author: 'Ada Lovelace', text: 'That brain of mine is something more than merely mortal.', userId: 1, createdAt: '2026-01-01T00:00:00Z', tags: [] },
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
    expect(fixture.nativeElement.querySelector('.state.loading')).toBeNull();
  });

  it('shows an empty state for a page with zero quotes', () => {
    const fixture = create();

    httpMock.expectOne((r) => r.urlWithParams.includes('/api/quotes?page=1&size=5')).flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.state.empty')).not.toBeNull();
  });

  it('shows a friendly error message on a 4xx, not the raw response', () => {
    const fixture = create();

    httpMock.expectOne((r) => r.urlWithParams.includes('/api/quotes?page=1&size=5')).flush('Page must be >= 1 and size must be between 1 and 100.', {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();

    const errorEl = fixture.nativeElement.querySelector('.state.error');
    expect(errorEl.textContent).toContain('Something went wrong. Please try again.');
    expect(errorEl.textContent).not.toContain('Page must be >=');
  });
});