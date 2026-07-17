import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/**
 * Универсальный сервис админ-панели: у каждой таблицы есть
 * GET /api/admin/<table> (список) и POST /api/admin/<table> (добавление).
 */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/admin`;

  list(table: string): Observable<Record<string, unknown>[]> {
    return this.http.get<Record<string, unknown>[]>(`${this.api}/${table}`);
  }

  add(table: string, payload: Record<string, unknown>): Observable<unknown> {
    return this.http.post(`${this.api}/${table}`, payload);
  }

  update(table: string, id: string | number, payload: Record<string, unknown>): Observable<unknown> {
    return this.http.put(`${this.api}/${table}/${id}`, payload);
  }
}
