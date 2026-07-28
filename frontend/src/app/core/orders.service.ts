import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AddressDto, OrderDto, PagedResult } from './models';

@Injectable({ providedIn: 'root' })
export class OrdersService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  myOrders(page: number): Observable<PagedResult<OrderDto>> {
    return this.http.get<PagedResult<OrderDto>>(`${this.api}/orders/my`, {
      params: new HttpParams().set('page', page),
    });
  }

  createOrder(zipId: string, count: number, addressId: number): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.api}/orders`, { zipId, count, addressId });
  }

  addressess(): Observable<AddressDto[]> {
    return this.http.get<AddressDto[]>(`${this.api}/addressess`);
  }

  addAddress(address: string, postCode?: string): Observable<AddressDto> {
    return this.http.post<AddressDto>(`${this.api}/addressess`, { address, postCode });
  }

  createPayment(orderId: string, returnUrl: string): Observable<{ paymentId: string; confirmationUrl: string }> {
    return this.http.post<{ paymentId: string; confirmationUrl: string }>(
      `${this.api}/payments/create`, { orderId, returnUrl });
  }

  paymentStatus(orderId: string): Observable<{ orderId: string; status: string }> {
    return this.http.get<{ orderId: string; status: string }>(`${this.api}/payments/status/${orderId}`);
  }

  createGuestOrder(data: any) {
    return this.http.post<OrderDto>(`${this.api}/orders/guest-order`, data);
  }

  // Отключено по просьбе заказчика
  // deleteSenderOrder(id: string): Observable<any> {
  // return this.http.delete(`${environment.apiUrl}/sender/orders/${id}`);
}