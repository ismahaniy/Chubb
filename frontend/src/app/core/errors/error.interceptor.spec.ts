import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from '../auth/auth.interceptor';
import { AuthStore } from '../auth/auth.store';
import { session, storeSession } from '../auth/testing';
import { SILENT_ERRORS, errorInterceptor } from './error.interceptor';
import { ToastStore } from './toast.store';

describe('errorInterceptor', () => {
  function setUp(signedIn: boolean) {
    localStorage.clear();
    if (signedIn) storeSession(session('Officer'));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    return {
      http: TestBed.inject(HttpClient),
      backend: TestBed.inject(HttpTestingController),
      toasts: TestBed.inject(ToastStore),
      auth: TestBed.inject(AuthStore),
      navigate,
    };
  }

  function call(http: HttpClient, url: string): { error?: unknown } {
    const result: { error?: unknown } = {};
    http.post(url, {}).subscribe({ error: (e: unknown) => (result.error = e) });
    return result;
  }

  it('shows ProblemDetails title and detail, and rethrows', () => {
    const { http, backend, toasts } = setUp(true);
    const result = call(http, '/api/claims/1/settle');
    backend
      .expectOne('/api/claims/1/settle')
      .flush(
        { title: 'Invalid transition', detail: 'Cannot settle a rejected claim.', status: 409 },
        { status: 409, statusText: 'Conflict' },
      );

    expect(toasts.toasts()).toEqual([
      expect.objectContaining({
        title: 'Invalid transition',
        detail: 'Cannot settle a rejected claim.',
      }),
    ]);
    expect(result.error).toBeDefined();
  });

  it('flattens validation errors into the detail', () => {
    const { http, backend, toasts } = setUp(true);
    call(http, '/api/claims');
    backend.expectOne('/api/claims').flush(
      {
        title: 'One or more validation errors occurred.',
        errors: { Description: ['Description is required.'], Amount: ['Amount must be positive.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(toasts.toasts()[0]?.detail).toBe('Description is required. Amount must be positive.');
  });

  it('reports an unreachable server', () => {
    const { http, backend, toasts } = setUp(false);
    call(http, '/api/auth/login');
    backend.expectOne('/api/auth/login').error(new ProgressEvent('error'), { status: 0 });

    expect(toasts.toasts()[0]?.title).toBe('Cannot reach the server');
  });

  it('keeps a failed anonymous login as a toast without logging out', () => {
    const { http, backend, toasts, navigate } = setUp(false);
    call(http, '/api/auth/login');
    backend
      .expectOne('/api/auth/login')
      .flush({ title: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(toasts.toasts()[0]?.title).toBe('Invalid email or password.');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('ends the session on a 401 for an authenticated request', () => {
    const { http, backend, auth, navigate } = setUp(true);
    call(http, '/api/claims/queue');
    backend.expectOne('/api/claims/queue').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  describe('with SILENT_ERRORS', () => {
    const silent = () => ({ context: new HttpContext().set(SILENT_ERRORS, true) });

    it('rethrows without a toast', () => {
      const { http, backend, toasts } = setUp(true);
      let failed = false;
      http.get('/api/notifications', silent()).subscribe({ error: () => (failed = true) });
      backend.expectOne('/api/notifications').error(new ProgressEvent('error'), { status: 0 });

      expect(failed).toBe(true);
      expect(toasts.toasts()).toEqual([]);
    });

    it('still ends the session on a 401', () => {
      const { http, backend, auth, navigate } = setUp(true);
      http.get('/api/notifications', silent()).subscribe({ error: () => undefined });
      backend
        .expectOne('/api/notifications')
        .flush(null, { status: 401, statusText: 'Unauthorized' });

      expect(auth.isAuthenticated()).toBe(false);
      expect(navigate).toHaveBeenCalledWith(['/login']);
    });
  });
});
