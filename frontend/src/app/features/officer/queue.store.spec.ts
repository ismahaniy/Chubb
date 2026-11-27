import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { Schemas } from '../../core/api/api';
import { QUEUE_POLL_MS, QueueStore } from './queue.store';

const QUEUE_URL = '/api/claims/queue';

function page(...ids: string[]): Schemas['QueuePage'] {
  return {
    items: ids.map((id) => ({
      id,
      claimNumber: `CLM-${id}`,
      policyNumber: 'POL-1',
      market: 'Singapore',
      productType: 'Motor',
      status: 'Submitted',
      claimedAmount: { amount: 1000, currency: 'SGD' },
      assignedOfficerId: null,
      submittedAt: '2026-11-01T00:00:00Z',
      slaDueAt: '2026-11-03T00:00:00Z',
      slaBreached: false,
    })),
    page: 1,
    pageSize: 20,
    totalCount: ids.length,
  };
}

describe('QueueStore', () => {
  let store: QueueStore;
  let http: HttpTestingController;

  beforeEach(() => {
    // Fake only setInterval (used by rxjs interval): the zoneless scheduler needs real timeouts.
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval'] });
    TestBed.configureTestingModule({
      providers: [QueueStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(QueueStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  /** Lets the resource's effect run, then returns the single pending queue request. */
  function nextQueueRequest() {
    TestBed.tick();
    return http.expectOne((r) => r.url === QUEUE_URL);
  }

  /** The resource publishes a flushed response asynchronously; wait until it has. */
  const settled = () => TestBed.inject(ApplicationRef).whenStable();

  it('loads the unassigned queue first', async () => {
    const req = nextQueueRequest();
    expect(req.request.params.toString()).toBe('scope=Unassigned&page=1&pageSize=20');
    req.flush(page('a', 'b'));
    await settled();

    expect(store.items().map((i) => i.id)).toEqual(['a', 'b']);
    expect(store.totalCount()).toBe(2);
  });

  it('switching scope cancels the in-flight load and clears the status filter', async () => {
    const first = nextQueueRequest();
    store.setFilters({ status: 'Approved' });
    store.setPage(3);
    store.setScope('Mine');

    const second = nextQueueRequest();
    expect(first.cancelled).toBe(true);
    expect(second.request.params.get('scope')).toBe('Mine');
    expect(second.request.params.get('page')).toBe('1');
    expect(second.request.params.has('status')).toBe(false);
    second.flush(page('m'));
    await settled();
    expect(store.items().map((i) => i.id)).toEqual(['m']);
  });

  it('assign posts and reloads the queue even when the claim was already taken', async () => {
    nextQueueRequest().flush(page('a'));
    await settled();

    const assigning = store.assign('a');
    expect(store.assigningId()).toBe('a');
    http
      .expectOne({ method: 'POST', url: '/api/claims/a/assign' })
      .flush({ title: 'Conflict' }, { status: 409, statusText: 'Conflict' });

    expect(await assigning).toBe(false);
    expect(store.assigningId()).toBeNull();
    nextQueueRequest().flush(page());
    await settled();
    expect(store.items()).toEqual([]);
  });

  it(`polls every ${QUEUE_POLL_MS / 1000} s, keeping the current rows while it refreshes`, async () => {
    nextQueueRequest().flush(page('a'));
    await settled();

    vi.advanceTimersByTime(QUEUE_POLL_MS);
    const poll = nextQueueRequest();
    expect(store.loading()).toBe(false);
    expect(store.items().map((i) => i.id)).toEqual(['a']);

    poll.flush(page('a', 'b'));
    await settled();
    expect(store.items().map((i) => i.id)).toEqual(['a', 'b']);
  });
});
