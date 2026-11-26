import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { CanMatchFn, Route, Router, UrlTree, provideRouter } from '@angular/router';
import type { Role } from '../api/api';
import { roleGuard } from './role.guard';
import { session, storeSession } from './testing';

type Snapshot = Parameters<CanMatchFn>[2];

function run(...allowed: Role[]) {
  return TestBed.runInInjectionContext(() =>
    roleGuard(...allowed)({} as Route, [], {} as Snapshot),
  );
}

describe('roleGuard', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  });

  const url = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('sends anonymous users to /login', () => {
    expect(url(run('Manager'))).toBe('/login');
  });

  it('sends a user with another role to their own home', () => {
    storeSession(session('Officer'));
    expect(url(run('Manager'))).toBe('/officer');
  });

  it('allows a matching role', () => {
    storeSession(session('Manager'));
    expect(run('Manager')).toBe(true);
  });
});
