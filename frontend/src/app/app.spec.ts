import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { session, storeSession } from './core/auth/testing';

describe('App', () => {
  beforeEach(() => localStorage.clear());

  async function render() {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    // Signed in, the bell loads notifications; stability waits for that request, so answer it.
    TestBed.tick();
    for (const req of TestBed.inject(HttpTestingController).match('/api/notifications'))
      req.flush([]);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the product name without nav when signed out', async () => {
    const el = await render();
    expect(el.querySelector('h1')?.textContent).toContain('ClaimDesk');
    expect(el.querySelector('app-nav')).toBeNull();
  });

  it('shows the role nav when signed in', async () => {
    storeSession(session('Officer'));
    const el = await render();
    expect(el.querySelector('app-nav')?.textContent).toContain('Work queue');
    expect(el.querySelector('app-notification-bell')).not.toBeNull();
  });
});
