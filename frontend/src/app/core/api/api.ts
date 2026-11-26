import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';
import type { components } from './api-types';

/** Base URL every API call is built on. Comes from the environment; never hard-code a host. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});

/** Generated OpenAPI schemas. Regenerate with `npm run gen:api`; never hand-write DTOs. */
export type Schemas = components['schemas'];
export type Role = Schemas['Role'];
export type CurrentUser = Schemas['CurrentUser'];
