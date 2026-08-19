import { Component, OnInit, ViewChild, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../core/auth.service';
import { OrdersService } from '../core/orders.service';
import { AddressDto, OrderDto, PagedResult } from '../core/models';
import { PaymentModalComponent } from '../shared/payment-modal.component';

@Component({
    selector: 'app-cabinet',
    imports: [CurrencyPipe, DatePipe, FormsModule, PaymentModalComponent],
    template: `
    <h1>Личный кабинет</h1>

    @if (auth.user(); as user) {
      <div class="card profile">
        <p><b>{{ user.fio }}</b></p>
        <p class="muted">{{ user.email }}@if (user.phoneNumber) { · {{ user.phoneNumber }} }</p>
      </div>
    }

    <section class="card">
      <h2>Мои заказы</h2>
      @if (orders(); as r) {
        @if (r.total === 0) {
          <p class="muted">Заказов пока нет.</p>
        } @else {
          <div style="overflow-x:auto">
            <table class="data">
              <thead>
                <tr>
                  <th>Номер</th>
                  <th>Дата</th>
                  <th>Запчасть</th>
                  <th>Кол-во</th>
                  <th>Сумма</th>
                  <th>Адрес</th>
                  <th>Оплата</th>
                </tr>
              </thead>
              <tbody>
                @for (order of r.items; track order.id) {
                  <tr>
                    <td>{{ order.orderNumber }}</td>
                    <td>{{ order.orderDateTime | date:'dd.MM.yyyy HH:mm' }}</td>
                    <td>{{ order.zipName ?? '—' }}</td>
                    <td>{{ order.countOrdered }}</td>
                    <td>
                      @if (order.zipCost != null) {
                        {{ order.zipCost * order.countOrdered | currency:'RUB':'symbol-narrow':'1.0-0' }}
                      } @else { — }
                    </td>
                    <td>{{ order.address }}</td>
                    <td>
                      <!-- Онлайн-оплата отключена — статус подтверждает администратор вручную
                           (см. Order.IsPaid), проверив чек, который покупатель прикладывает сам. -->
                      @if (order.isPaid) {
                        <span class="success">Оплачен</span>
                      } @else if (order.receiptFileName) {
                        <span class="muted">Чек на проверке</span>
                      } @else {
                        <button class="btn pay-btn" (click)="confirmPayment(order)">Подтвердить оплату</button>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          <!-- Пагинация по 10 заказов на страницу -->
          @if (r.totalPages > 1) {
            <div class="pagination">
              <button [disabled]="r.page <= 1" (click)="load(r.page - 1)">‹</button>
              @for (p of pages(r); track p) {
                <button [class.active]="p === r.page" (click)="load(p)">{{ p }}</button>
              }
              <button [disabled]="r.page >= r.totalPages" (click)="load(r.page + 1)">›</button>
            </div>
          }
        }
      }
      @if (error()) { <p class="error">{{ error() }}</p> }
    </section>

    <section class="card" style="margin-top:20px">
      <h2>Адреса доставки</h2>
      @for (a of addressess(); track a.id) {
        <p>{{ a.address }}@if (a.postCode) { , {{ a.postCode }} }</p>
      } @empty {
        <p class="muted">Адресов пока нет.</p>
      }
      <div class="add-address">
        <input type="text" placeholder="Новый адрес" [(ngModel)]="newAddress" />
        <input type="text" placeholder="Индекс" [(ngModel)]="newPostCode" style="max-width:120px" />
        <button class="btn" (click)="addAddress()">Добавить</button>
      </div>
    </section>

    <app-payment-modal #paymentModal (uploaded)="load(orders()?.page ?? 1)" />
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    .profile { margin-bottom: 20px; }
    .profile p { margin: 4px 0; }
    .pay-btn { padding: 6px 12px; font-size: 13px; }
    .add-address { display: flex; gap: 10px; margin-top: 12px; }
    .add-address input:first-child { flex: 1; }
  `]
})
export class CabinetComponent implements OnInit {
  auth = inject(AuthService);
  private ordersService = inject(OrdersService);

  @ViewChild('paymentModal') private paymentModal!: PaymentModalComponent;

  orders = signal<PagedResult<OrderDto> | null>(null);
  addressess = signal<AddressDto[]>([]);
  error = signal('');
  newAddress = '';
  newPostCode = '';

  ngOnInit(): void {
    this.load(1);
    this.ordersService.addressess().subscribe(a => this.addressess.set(a));
  }

  load(page: number): void {
    this.ordersService.myOrders(page).subscribe(r => this.orders.set(r));
  }

  pages(r: PagedResult<OrderDto>): number[] {
    const from = Math.max(1, r.page - 3);
    const to = Math.min(r.totalPages, r.page + 3);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  }

  confirmPayment(order: OrderDto): void {
    // Оплата и чек — на всю покупку, а не на одну позицию: если в покупке несколько
    // товаров, кнопка на любой из них открывает одну и ту же модалку.
    this.paymentModal.open({ id: order.purchaseId, orderNumber: order.purchaseNumber });
  }

  addAddress(): void {
    if (!this.newAddress.trim()) return;
    this.ordersService.addAddress(this.newAddress.trim(), this.newPostCode || undefined).subscribe(a => {
      this.addressess.update(list => [...list, a]);
      this.newAddress = '';
      this.newPostCode = '';
    });
  }
}
