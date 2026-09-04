import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { IncomeReportRow, PriceHistoryRow, SalesReportRow, StockReportRow, ZipHistoryRow } from './models';

/** Параметры отчётов: период и/или отбор по детали. Все поля необязательные. */
export interface ReportFilters {
  from?: string;
  to?: string;
  partNum?: string;
  name?: string;
  donor?: string;
}

/** Строка справочника деталей: источник подсказок во всех фильтрах страницы отчётов. */
export interface ZipLookupItem {
  id: string;
  name: string;
  partNum?: string;
  incomeMoto?: string;
}

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

  /**
   * Эндпоинты, принимающие multipart/form-data (у них на бэкенде стоит [FromForm]).
   * Все остальные ждут JSON и на multipart отвечают 415 Unsupported Media Type,
   * поэтому формат запроса выбирается по этому списку, а не по наличию файлов:
   * zip нужно слать формой даже без единой фотографии.
   */
  private static readonly MULTIPART_ENDPOINTS = ['zip'];

  private toFormData(data: any, files?: File[]): FormData {
    const formData = new FormData();

    Object.keys(data).forEach(key => {
      // Исключаем пустые значения, чтобы не сломать парсер C# (ошибка 400)
      if (data[key] !== null && data[key] !== undefined && data[key] !== '') {
        formData.append(key, data[key]);
      }
    });

    if (files && files.length > 0) {
      files.forEach(file => {
        formData.append('photos', file, file.name);
      });
    }

    return formData;
  }

  /** Убирает пустые строки, чтобы необязательные поля уходили как null, а не "" . */
  private cleanPayload(data: any): Record<string, unknown> {
    const payload: Record<string, unknown> = {};
    Object.keys(data).forEach(key => {
      payload[key] = data[key] === '' ? null : data[key];
    });
    return payload;
  }

  add(endpoint: string, data: any, files?: File[]) {
    if (AdminService.MULTIPART_ENDPOINTS.includes(endpoint)) {
      return this.http.post(`${this.api}/${endpoint}`, this.toFormData(data, files));
    }
    return this.http.post(`${this.api}/${endpoint}`, this.cleanPayload(data));
  }

  update(endpoint: string, id: any, data: any, files?: File[]) {
    if (AdminService.MULTIPART_ENDPOINTS.includes(endpoint)) {
      return this.http.put(`${this.api}/${endpoint}/${id}`, this.toFormData(data, files));
    }
    return this.http.put(`${this.api}/${endpoint}/${id}`, this.cleanPayload(data));
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

  deleteZipPhoto(photoId: number) {
    return this.http.delete(`${this.api}/zip-photos/${photoId}`);
  }

  /** PDF с QR-кодом запчасти. Blob, а не прямой window.open — так к запросу подцепляется Bearer-токен. */
  printQrLabel(zipId: string) {
    return this.http.get(`${this.api}/zip/${zipId}/qr-label`, { responseType: 'blob' });
  }

  /** Правка каталожной позиции: наименование и группа общие для всех запчастей с этим парт-номером. */
  updatePartNumber(id: number, data: { partNum: string; name: string; groupId: number | null }) {
    return this.http.put(`${this.api}/part-numbers/${id}`, data);
  }

  // ---------- Склад и цены ----------

  /** Коррекция остатка. delta со знаком: отрицательная трактуется бэкендом как списание. */
  addCorrection(zipId: string, delta: number, comment: string) {
    return this.http.post(`${this.api}/corrections`, { zipId, delta, comment });
  }

  /** Изменение цены продажи. Наценка/уценка определяется бэкендом по знаку разницы. */
  reprice(zipId: string, newCost: number, comment?: string) {
    return this.http.post(`${this.api}/reprice`, { zipId, newCost, comment });
  }

  /** Ставит одну цену всем запчастям парт-номера; exceptZipId исключает только что заведённую. */
  repricePartNum(partNumId: number, newCost: number, exceptZipId?: string, comment?: string) {
    return this.http.post<{ message: string; updated: number }>(
      `${this.api}/reprice-part-num`, { partNumId, newCost, exceptZipId, comment });
  }

  // ---------- Отчёты ----------

  /**
   * Минимальный список деталей для выпадающих списков на странице отчётов.
   * Не list('zip') — тот эндпоинт закрыт для Sender и несёт лишние для этой формы данные
   * (цены, остатки, фото).
   */
  zipLookup(): Observable<ZipLookupItem[]> {
    return this.http.get<ZipLookupItem[]>(`${this.api}/reports/zip-lookup`);
  }

  /** Остатки склада на сейчас — всё, чего больше нуля. Периода у среза нет, только донор. */
  stockReport(donor?: string): Observable<StockReportRow[]> {
    return this.http.get<StockReportRow[]>(`${this.api}/reports/stock`, { params: this.reportParams({ donor }) });
  }

  salesReport(filters: ReportFilters = {}): Observable<SalesReportRow[]> {
    return this.http.get<SalesReportRow[]>(`${this.api}/reports/sales`, { params: this.reportParams(filters) });
  }

  incomeReport(filters: ReportFilters = {}): Observable<IncomeReportRow[]> {
    return this.http.get<IncomeReportRow[]>(`${this.api}/reports/income`, { params: this.reportParams(filters) });
  }

  priceHistoryReport(zipId?: string): Observable<PriceHistoryRow[]> {
    let params = new HttpParams();
    if (zipId) params = params.set('zipId', zipId);
    return this.http.get<PriceHistoryRow[]>(`${this.api}/reports/price-history`, { params });
  }

  zipHistory(zipId: string): Observable<ZipHistoryRow[]> {
    return this.http.get<ZipHistoryRow[]>(`${this.api}/reports/zip-history/${zipId}`);
  }

  /**
   * Даты в формате YYYY-MM-DD, обе границы включительно — доводит их бэкенд.
   * Пустые поля не отправляем вовсе: пустая строка на бэкенде отсеялась бы как
   * IsNullOrWhiteSpace, но зря висела бы в URL.
   */
  private reportParams(f: ReportFilters): HttpParams {
    let params = new HttpParams();
    if (f.from) params = params.set('from', f.from);
    if (f.to) params = params.set('to', f.to);
    if (f.partNum?.trim()) params = params.set('partNum', f.partNum.trim());
    if (f.name?.trim()) params = params.set('name', f.name.trim());
    if (f.donor?.trim()) params = params.set('donor', f.donor.trim());
    return params;
  }

  // Sender
  getSenderOrders(): Observable<any[]> {
    return this.http.get<any[]>(`${environment.apiUrl}/sender/orders`);
  }

  updateOrderStatus(id: string, status: string): Observable<any> {
    return this.http.put(`${environment.apiUrl}/sender/orders/${id}/status`, { status });
  }

  getSenderZipInfo(id: string): Observable<any> {
    return this.http.get(`${environment.apiUrl}/sender/zip/${id}`);
  }

  // ---------- Черновики форм ----------
  // Незавершённый ввод хранится на сервере, а не в localStorage, чтобы переживать
  // очистку браузера и открываться с другого устройства.

  /** Черновик формы или null, если его нет. */
  getDraft(formKey: string): Observable<{ formKey: string; content: string; updatedAt: string } | null> {
    return this.http.get<{ formKey: string; content: string; updatedAt: string } | null>(
      `${environment.apiUrl}/drafts/${formKey}`);
  }

  saveDraft(formKey: string, content: string): Observable<any> {
    return this.http.put(`${environment.apiUrl}/drafts/${formKey}`, { content });
  }

  deleteDraft(formKey: string): Observable<any> {
    return this.http.delete(`${environment.apiUrl}/drafts/${formKey}`);
  }
}
