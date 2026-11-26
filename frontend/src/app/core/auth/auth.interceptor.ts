import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../api/api';
import { AuthStore } from './auth.store';

/** Adds the bearer token, only to requests for our own API. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthStore).token();
  const ownApi = req.url.startsWith(`${inject(API_BASE_URL)}/`);

  return token && ownApi
    ? next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    : next(req);
};
