import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { authInterceptor } from './auth.interceptor';
import { session, storeSession } from './testing';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    storeSession(session('Officer'));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  it('adds the bearer token to API requests', () => {
    http.get('/api/claims/queue').subscribe();
    expect(backend.expectOne('/api/claims/queue').request.headers.get('Authorization')).toBe(
      'Bearer token-Officer',
    );
  });

  it('does not send the token to other origins', () => {
    http.get('https://example.com/api/x').subscribe();
    expect(
      backend.expectOne('https://example.com/api/x').request.headers.has('Authorization'),
    ).toBe(false);
  });
});
