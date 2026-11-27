import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Schemas } from '../../core/api/api';
import { session, storeSession } from '../../core/auth/testing';
import { NOTIFICATION_POLL_MS } from '../../core/notifications/notification.store';
import { notification } from '../../core/notifications/testing';
import { ClaimStore } from './claim.store';

const CLAIM_URL = '/api/claims/c1';
const NOTIFICATIONS_URL = '/api/notifications';

function claim(
  status: Schemas['ClaimStatus'],
  openInformationRequests: Schemas['InformationRequestDto'][] = [],
): Schemas['ClaimDetail'] {
  const sgd = (amount: number) => ({ amount, currency: 'SGD' });
  return {
    id: 'c1',
    claimNumber: 'CLM-1',
    policyNumber: 'POL-SG-0001',
    claimantId: 'u1',
    productType: 'Motor',
    market: 'Singapore',
    status,
    incidentDate: '2026-11-01',
    description: 'Rear-ended',
    claimedAmount: sgd(1000),
    maxPayable: sgd(49500),
    submittedAt: '2026-11-01T00:00:00Z',
    decision: null,
    timeline: [],
    openInformationRequests,
  };
}

const QUESTION = { id: 'r1', question: 'Photos?', requestedAt: '2026-11-02T00:00:00Z' };

describe('ClaimStore', () => {
  let store: ClaimStore;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    storeSession(session('Claimant'));
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    TestBed.configureTestingModule({
      providers: [ClaimStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ClaimStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  const settled = () => TestBed.inject(ApplicationRef).whenStable();

  /**
   * The reload is started by an effect once the resource has published the flushed notifications, which happens
   * asynchronously; whenStable() would wait on that very request, so poll for it instead.
   */
  async function nextClaimRequest() {
    for (let i = 0; i < 20; i++) {
      TestBed.tick();
      const [req] = http.match(CLAIM_URL);
      if (req) return req;
      await new Promise((resolve) => setTimeout(resolve));
    }
    throw new Error(`No request to ${CLAIM_URL}`);
  }

  /** Selects the claim and answers both first loads (claim and the session's notifications). */
  async function open(
    detail: Schemas['ClaimDetail'],
    notifications: Schemas['NotificationView'][],
  ) {
    store.select('c1');
    TestBed.tick();
    http.expectOne(NOTIFICATIONS_URL).flush(notifications);
    http.expectOne(CLAIM_URL).flush(detail);
    await settled();
  }

  it('answers the open request and shows the returned claim', async () => {
    await open(claim('AwaitingInformation', [QUESTION]), []);

    const answering = store.respond('r1', 'Uploaded.');
    expect(store.responding()).toBe('r1');
    const post = http.expectOne({
      method: 'POST',
      url: '/api/claims/c1/information-requests/r1/response',
    });
    expect(post.request.body).toEqual({ answer: 'Uploaded.' });
    post.flush(claim('UnderReview'));

    expect(await answering).toBe(true);
    expect(store.responding()).toBeNull();
    expect(store.detail()?.status).toBe('UnderReview');
  });

  it('reloads the claim after a refused answer, so the screen is never stale', async () => {
    await open(claim('AwaitingInformation', [QUESTION]), []);

    const answering = store.respond('r1', 'Uploaded.');
    http
      .expectOne({ method: 'POST', url: '/api/claims/c1/information-requests/r1/response' })
      .flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });

    expect(await answering).toBe(false);
    (await nextClaimRequest()).flush(claim('UnderReview'));
    await settled();
    expect(store.detail()?.status).toBe('UnderReview');
  });

  it('reloads when a newer notification about this claim arrives, and only then', async () => {
    const received = notification('n1', '2026-11-01T00:00:00Z', null, 'c1');
    await open(claim('Submitted'), [received]);

    // A poll with nothing new, or news about another claim: no reload.
    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    TestBed.tick();
    http
      .expectOne(NOTIFICATIONS_URL)
      .flush([notification('n2', '2026-11-02T00:00:00Z', null, 'other'), received]);
    await settled();
    http.expectNone(CLAIM_URL);

    // The officer picked it up: the claimant is notified, so the claim reloads.
    vi.advanceTimersByTime(NOTIFICATION_POLL_MS);
    TestBed.tick();
    http
      .expectOne(NOTIFICATIONS_URL)
      .flush([notification('n3', '2026-11-03T00:00:00Z', null, 'c1'), received]);
    (await nextClaimRequest()).flush(claim('UnderReview'));
    await settled();
    expect(store.detail()?.status).toBe('UnderReview');
  });
});
