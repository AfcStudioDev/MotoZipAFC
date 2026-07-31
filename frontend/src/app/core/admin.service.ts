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

  // add(table: string, payload: Record<string, unknown>): Observable<unknown> {
  //   return this.http.post(`${this.api}/${table}`, payload);
  // }

  add(endpoint: string, data: any, files?: File[]) {
    // ВСЕГДА используем FormData для таблиц, которые могут принимать файлы (например, zips)
    // Если вам нужно, чтобы этот метод работал и для других таблиц без файлов,
    // можно оставить условие if (files), как мы делали ранее.
    const formData = new FormData();

    Object.keys(data).forEach(key => {
      // Исключаем пустые значения, чтобы не сломать парсер C# (ошибка 400)
      if (data[key] !== null && data[key] !== undefined && data[key] !== '') {
        formData.append(key, data[key]);
      }
    });

    if (files && files.length > 0) {
      files.forEach(file => {
        formData.append('photos', file, file.name); // ключ 'photos' не обязателен для C# Request.Form.Files, но хорошая практика
      });
    }

    return this.http.post(`${this.api}/${endpoint}`, formData);
  }

  // update(table: string, id: string | number, payload: Record<string, unknown>): Observable<unknown> {
  //   return this.http.put(`${this.api}/${table}/${id}`, payload);
  // }



  update(endpoint: string, id: any, data: any, files?: File[]) {
    // Если параметр files был передан (даже если массив пустой []),
    // значит эндпоинт ожидает multipart/form-data
    if (files) {
      const formData = new FormData();

      // Добавляем все текстовые и числовые поля
      Object.keys(data).forEach(key => {
        const val = data[key];
        // Пропускаем null, undefined и пустые строки, чтобы не вызывать ошибку 400 на бэкенде
        if (val !== null && val !== undefined && val !== '') {
          formData.append(key, val);
        }
      });

      // Если есть прикреплённые файлы — добавляем их в FormData
      if (files.length > 0) {
        files.forEach(file => {
          formData.append('photos', file, file.name);
        });
      }

      return this.http.put(`${this.api}/${endpoint}/${id}`, formData);
    }

    // Обычный JSON-запрос для таблиц без файлов
    return this.http.put(`${this.api}/${endpoint}/${id}`, data);
  }

  searchUsers(query: string) {
    return this.http.get<any[]>(`${this.api}/admin/users/search?q=${encodeURIComponent(query)}`);
  }

  // Admin
  updateUserRoles(userId: number, isSender: boolean, isRegistrar: boolean) {
    return this.http.put(`${this.api}/admin/users/${userId}/roles`, { isSender, isRegistrar });
  }
  delete(endpoint: string, id: any) {
    return this.http.delete(`${this.api}/${endpoint}/${id}`);
  }

  // Sender
  getSenderOrders(): Observable<any[]> {
    return this.http.get<any[]>(`${environment.apiUrl}/sender/orders`);
  }

  updateOrderStatus(id: string, status: string): Observable<any> {
    return this.http.put(`${environment.apiUrl}/sender/orders/${id}/status`, { status });
  }
}
