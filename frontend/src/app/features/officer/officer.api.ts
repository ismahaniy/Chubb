import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, Schemas } from '../../core/api/api';
import type { paths } from '../../core/api/api-types';

export type QueueQuery = NonNullable<paths['/api/claims/queue']['get']['parameters']['query']>;
type ClaimDetail = Schemas['ClaimDetail'];

@Injectable({ providedIn: 'root' })
export class OfficerApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  queue(query: QueueQuery): Observable<Schemas['QueuePage']> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) params = params.set(key, String(value));
    }
    return this.http.get<Schemas['QueuePage']>(`${this.baseUrl}/claims/queue`, { params });
  }

  get(id: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`${this.baseUrl}/claims/${id}`);
  }

  assign(id: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims/${id}/assign`, null);
  }

  requestInformation(
    id: string,
    body: Schemas['InformationRequestRequest'],
  ): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims/${id}/information-requests`, body);
  }

  recordAssessment(id: string, body: Schemas['AssessmentRequest']): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims/${id}/assessment`, body);
  }

  decide(id: string, body: Schemas['DecisionRequest']): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims/${id}/decision`, body);
  }

  settle(id: string): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims/${id}/settle`, null);
  }
}
