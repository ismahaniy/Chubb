import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, Schemas } from '../../core/api/api';
import { SILENT_ERRORS } from '../../core/errors/error.interceptor';

/** Polled reports: failures show as a banner on the dashboard, not as a toast every poll. */
const silent = () => ({ context: new HttpContext().set(SILENT_ERRORS, true) });

@Injectable({ providedIn: 'root' })
export class ManagerApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  exposure(): Observable<Schemas['ExposureReport']> {
    return this.http.get<Schemas['ExposureReport']>(`${this.baseUrl}/reports/exposure`, silent());
  }

  workload(): Observable<Schemas['WorkloadReport']> {
    return this.http.get<Schemas['WorkloadReport']>(`${this.baseUrl}/reports/workload`, silent());
  }
}
