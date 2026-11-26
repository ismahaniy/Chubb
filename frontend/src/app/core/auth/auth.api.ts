import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, Schemas } from '../api/api';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  login(body: Schemas['LoginRequest']): Observable<Schemas['LoginResponse']> {
    return this.http.post<Schemas['LoginResponse']>(`${this.baseUrl}/auth/login`, body);
  }
}
