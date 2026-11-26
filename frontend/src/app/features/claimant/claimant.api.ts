import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, Schemas } from '../../core/api/api';

type ClaimDetail = Schemas['ClaimDetail'];

@Injectable({ providedIn: 'root' })
export class ClaimantApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  policies(): Observable<Schemas['PolicySummary'][]> {
    return this.http.get<Schemas['PolicySummary'][]>(`${this.baseUrl}/policies/mine`);
  }

  mine(): Observable<Schemas['ClaimSummary'][]> {
    return this.http.get<Schemas['ClaimSummary'][]>(`${this.baseUrl}/claims/mine`);
  }

  get(id: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(`${this.baseUrl}/claims/${id}`);
  }

  submit(body: Schemas['SubmitClaimRequest']): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(`${this.baseUrl}/claims`, body);
  }

  respond(
    id: string,
    requestId: string,
    body: Schemas['InformationResponseRequest'],
  ): Observable<ClaimDetail> {
    return this.http.post<ClaimDetail>(
      `${this.baseUrl}/claims/${id}/information-requests/${requestId}/response`,
      body,
    );
  }
}
