import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../auth/auth.store';
import { session, storeSession } from '../auth/testing';
import { SILENT_ERRORS } from '../errors/error.interceptor';
import { NOTIFICATION_POLL_MS, NotificationStore } from './notification.store';
import { notification } from './testing';

const LIST_URL = '/api/notifications';

describe('NotificationStore', () => {
  let http: HttpTestingController;

  function setUp(signedIn: boolean) {
    localStorage.clear();
    if (signedIn) storeSession(session('Claimant'));
    // Fake only setInterval (used by rxjs interval): the zoneless scheduler needs real timeouts.
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(NotificationStore);
  }

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  const settled = () => TestBed.inject(ApplicationRef).whenStable();

  function nextList() {
    TestBed.tick();
    return http.expectOne((r) => r.method === 'GET' && r.url === LIST_URL);
  }

  it('fetches nothing while signed out', () => {
    const store = setUp(false);
    TestBed.tick();
    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    TestBed.tick();

    http.expectNone(LIST_URL);
    expect(store.items()).toEqual([]);
  });

  it('loads silently, counts unread from readAt and polls', async () => {
    const store = setUp(true);
    const first = nextList();
    expect(first.request.context.get(SILENT_ERRORS)).toBe(true);
    first.flush([
      notification('b', '2026-11-02T00:00:00Z'),
      notification('a', '2026-11-01T00:00:00Z', '2026-11-01T01:00:00Z'),
    ]);
    await settled();
    expect(store.unreadCount()).toBe(1);

    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    nextList().flush([
      notification('c', '2026-11-03T00:00:00Z'),
      notification('b', '2026-11-02T00:00:00Z'),
    ]);
    await settled();
    expect(store.unreadCount()).toBe(2);
  });

  it('marks read up to the newest notification shown, then reloads', async () => {
    const store = setUp(true);
    nextList().flush([
      notification('b', '2026-11-02T00:00:00Z'),
      notification('a', '2026-11-01T00:00:00Z'),
    ]);
    await settled();

    const marking = store.markAllRead();
    const post = http.expectOne({ method: 'POST', url: '/api/notifications/read' });
    expect(post.request.body).toEqual({ upTo: '2026-11-02T00:00:00Z' });
    post.flush(null, { status: 204, statusText: 'No Content' });
    await marking;

    nextList().flush([
      notification('b', '2026-11-02T00:00:00Z', '2026-11-03T00:00:00Z'),
      notification('a', '2026-11-01T00:00:00Z', '2026-11-03T00:00:00Z'),
    ]);
    await settled();
    expect(store.unreadCount()).toBe(0);
  });

  it('does not post when nothing is unread', async () => {
    const store = setUp(true);
    nextList().flush([notification('a', '2026-11-01T00:00:00Z', '2026-11-01T01:00:00Z')]);
    await settled();

    await store.markAllRead();
    http.expectNone('/api/notifications/read');
  });

  it("clears on sign-out, so the next user never sees the previous user's list", async () => {
    const store = setUp(true);
    nextList().flush([notification('a', '2026-11-01T00:00:00Z')]);
    await settled();

    TestBed.inject(AuthStore).logout();
    TestBed.tick();
    expect(store.items()).toEqual([]);
  });
});
