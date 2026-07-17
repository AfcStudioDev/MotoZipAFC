import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { GroupDto, MarkDto, ModelDto, PagedResult, ZipDto } from './models';

export interface SearchFilters {
  query?: string;
  markId?: number;
  modelId?: number;
  groupId?: number;
  year?: number;
  partNumber?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private http = inject(HttpClient);
  private api = `${environment.apiUrl}/catalog`;

  search(filters: SearchFilters): Observable<PagedResult<ZipDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<ZipDto>>(`${this.api}/search`, { params });
  }

  marks(): Observable<MarkDto[]> {
    return this.http.get<MarkDto[]>(`${this.api}/marks`);
  }

  models(markId?: number): Observable<ModelDto[]> {
    const params = markId ? new HttpParams().set('markId', markId) : undefined;
    return this.http.get<ModelDto[]>(`${this.api}/models`, { params });
  }

  groups(): Observable<GroupDto[]> {
    return this.http.get<GroupDto[]>(`${this.api}/groups`);
  }

  years(): Observable<number[]> {
    return this.http.get<number[]>(`${this.api}/years`);
  }
}
