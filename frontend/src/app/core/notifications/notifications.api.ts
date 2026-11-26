import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL, Schemas } from '../api/api';
import { SILENT_ERRORS } from '../errors/error.interceptor';

/** Background traffic: failures are shown in the bell, not toasted. */
const silent = () => ({ context: new HttpContext().set(SILENT_ERRORS, true) });

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  list(): Observable<Schemas['NotificationView'][]> {
    return this.http.get<Schemas['NotificationView'][]>(`${this.baseUrl}/notifications`, silent());
  }

  markRead(body: Schemas['MarkNotificationsReadRequest']): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/notifications/read`, body, silent());
  }
}
