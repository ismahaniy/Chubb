import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthStore, SESSION_STORAGE_KEY } from './auth.store';
import { session, storeSession } from './testing';

describe('AuthStore', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('logs in, exposes the role and persists the session', async () => {
    const store = TestBed.inject(AuthStore);
    const login = store.login({ email: 'u@claimdesk.test', password: 'pw' });

    const req = TestBed.inject(HttpTestingController).expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(session('Officer'));

    expect((await login).role).toBe('Officer');
    expect(store.role()).toBe('Officer');
    expect(store.token()).toBe('token-Officer');
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).not.toBeNull();
  });

  it('restores a valid stored session', () => {
    storeSession(session('Manager'));
    expect(TestBed.inject(AuthStore).role()).toBe('Manager');
  });

  it.each([
    ['expired', session('Officer', -1000)],
    [
      'unknown role',
      { ...session('Officer'), user: { ...session('Officer').user, role: 'Admin' } },
    ],
    ['missing token', { ...session('Officer'), accessToken: undefined }],
    ['corrupt JSON', '{not json'],
  ])('drops a stored session that is %s', (_, value) => {
    storeSession(value);
    const store = TestBed.inject(AuthStore);
    expect(store.isAuthenticated()).toBe(false);
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('logout clears state and storage', () => {
    storeSession(session('Claimant'));
    const store = TestBed.inject(AuthStore);
    store.logout();
    expect(store.user()).toBeNull();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });
});
