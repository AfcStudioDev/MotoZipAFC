import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AddressDto, CartItemRequest, OrderDto, PagedResult, PurchaseDto } from './models';

@Injectable({ providedIn: 'root' })
export class OrdersService {
  private http = inject(HttpClient);
  private api = environment.apiUrl;

  myOrders(page: number): Observable<PagedResult<OrderDto>> {
    return this.http.get<PagedResult<OrderDto>>(`${this.api}/orders/my`, {
      params: new HttpParams().set('page', page),
    });
  }

  /**
   * Оформление покупки — из одной позиции («Купить» на карточке) или из нескольких
   * (корзина). Разница только в длине items: сервер всегда создаёт одну Purchase.
   */
  checkout(
    items: CartItemRequest[], addressId: number,
    deliveryCompany?: string, deliveryComment?: string
  ): Observable<PurchaseDto> {
    return this.http.post<PurchaseDto>(`${this.api}/purchases`, { items, addressId, deliveryCompany, deliveryComment });
  }

  guestCheckout(data: any): Observable<PurchaseDto> {
    return this.http.post<PurchaseDto>(`${this.api}/purchases/guest`, data);
  }

  addressess(): Observable<AddressDto[]> {
    return this.http.get<AddressDto[]>(`${this.api}/addressess`);
  }

  addAddress(address: string, postCode?: string): Observable<AddressDto> {
    return this.http.post<AddressDto>(`${this.api}/addressess`, { address, postCode });
  }

  /** Номер карты для ручного перевода — онлайн-оплата (ЮKassa) отключена. */
  paymentInfo(): Observable<{ cardNumber: string }> {
    return this.http.get<{ cardNumber: string }>(`${this.api}/purchases/payment-info`);
  }

  /** Чек о переводе прикладывает сам покупатель — один на всю покупку, не на каждую позицию. */
  uploadReceipt(purchaseId: string, file: File): Observable<{ receiptFileName: string }> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ receiptFileName: string }>(`${this.api}/purchases/${purchaseId}/receipt`, form);
  }

  // Отключено по просьбе заказчика
  // deleteSenderOrder(id: string): Observable<any> {
  // return this.http.delete(`${environment.apiUrl}/sender/orders/${id}`);
}