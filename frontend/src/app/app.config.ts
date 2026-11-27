import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { errorInterceptor } from './core/errors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route params bind to page inputs (e.g. the workbench's `id`).
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: auth adds the header first, so the error interceptor can see it on a 401.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
  ],
};
