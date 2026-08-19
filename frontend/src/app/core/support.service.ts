import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SupportMessageDto, SupportTicketDto, SupportTicketSummaryDto } from './models';

@Injectable({ providedIn: 'root' })
export class SupportService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  myTickets(): Observable<SupportTicketSummaryDto[]> {
    return this.http.get<SupportTicketSummaryDto[]>(`${this.api}/support`);
  }

  getTicket(id: string): Observable<SupportTicketDto> {
    return this.http.get<SupportTicketDto>(`${this.api}/support/${id}`);
  }

  createTicket(orderId: string, message: string): Observable<SupportTicketDto> {
    return this.http.post<SupportTicketDto>(`${this.api}/support`, { orderId, message });
  }

  sendMessage(ticketId: string, text: string): Observable<SupportMessageDto> {
    return this.http.post<SupportMessageDto>(`${this.api}/support/${ticketId}/messages`, { text });
  }

  // ---------- Админ ----------

  adminList(): Observable<SupportTicketSummaryDto[]> {
    return this.http.get<SupportTicketSummaryDto[]>(`${this.api}/admin/support`);
  }

  adminGetTicket(id: string): Observable<SupportTicketDto> {
    return this.http.get<SupportTicketDto>(`${this.api}/admin/support/${id}`);
  }

  adminReply(ticketId: string, text: string): Observable<SupportMessageDto> {
    return this.http.post<SupportMessageDto>(`${this.api}/admin/support/${ticketId}/messages`, { text });
  }

  adminDelete(ticketId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.api}/admin/support/${ticketId}`);
  }

  adminClose(ticketId: string): Observable<SupportTicketSummaryDto> {
    return this.http.post<SupportTicketSummaryDto>(`${this.api}/admin/support/${ticketId}/close`, {});
  }
}
