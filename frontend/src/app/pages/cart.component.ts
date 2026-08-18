import { Component, ViewChild, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CartService } from '../core/cart.service';
import { OrdersService } from '../core/orders.service';
import { AuthService } from '../core/auth.service';
import { AddressDto } from '../core/models';
import { environment } from '../../environments/environment';
import { PaymentModalComponent } from '../shared/payment-modal.component';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, RouterLink, PaymentModalComponent],
  template: `
    <h1>Корзина</h1>

    @if (cart.list().length === 0) {
      <p class="muted">Корзина пуста.</p>
    } @else {
      <div class="card">
        <table class="data">
          <thead>
            <tr>
              <th></th>
              <th>Наименование</th>
              <th class="num">Цена</th>
              <th class="num">Кол-во</th>
              <th class="num">Сумма</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of cart.list(); track item.zip.id) {
              <tr>
                <td>
                  @if (item.zip.photos?.length) {
                    <img [src]="photoBaseUrl + item.zip.photos[0]" alt="" class="cart-thumb">
                  }
                </td>
                <td>{{ item.zip.name }}</td>
                <td class="num">{{ item.zip.sellCost | currency:'RUB':'symbol-narrow':'1.0-0' }}</td>
                <td class="num">
                  <input
                    type="number" min="1" [max]="item.zip.countStored"
                    [ngModel]="item.count"
                    (ngModelChange)="cart.setCount(item.zip.id, $event)"
                    class="qty-input"
                  >
                </td>
                <td class="num">{{ (item.zip.sellCost ?? 0) * item.count | currency:'RUB':'symbol-narrow':'1.0-0' }}</td>
                <td><button class="btn btn-secondary" (click)="cart.remove(item.zip.id)">Убрать</button></td>
              </tr>
            }
          </tbody>
        </table>
        <p class="cart-total">Итого: <b>{{ cart.totalSum() | currency:'RUB':'symbol-narrow':'1.0-0' }}</b></p>
      </div>

      @if (auth.isLoggedIn) {
        <div class="card mt-4">
          <h2>Оформление</h2>

          <div class="form-field">
            <label>Адрес доставки</label>
            <select [(ngModel)]="addressId">
              <option [ngValue]="undefined">— выберите адрес —</option>
              @for (a of addressess(); track a.id) {
                <option [ngValue]="a.id">{{ a.address }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label>…или добавьте новый адрес</label>
            <input type="text" placeholder="Город, улица, дом, квартира" [(ngModel)]="newAddress" />
          </div>
          <div class="form-field">
            <label>Компания доставки (необязательно)</label>
            <select [(ngModel)]="deliveryCompany">
              <option value="">— не выбрано —</option>
              @for (c of deliveryCompanies; track c) {
                <option [value]="c">{{ c }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label>Комментарий к доставке</label>
            <small class="muted" style="display: block; margin-bottom: 6px;">
              Возможно оформление курьерской доставки выбранной клиентом компанией, за счёт клиента
            </small>
            <textarea rows="2" [(ngModel)]="deliveryComment"></textarea>
          </div>

          @if (error()) { <p class="error">{{ error() }}</p> }

          <button class="btn" [disabled]="busy()" (click)="checkout()">
            @if (busy()) { Оформляем… } @else { Оформить заказ }
          </button>
        </div>
      } @else {
        <div class="card mt-4">
          <p>Чтобы оформить заказ, войдите в личный кабинет.</p>
          <a class="btn" routerLink="/login">Войти</a>
        </div>
      }
    }

    <app-payment-modal #paymentModal />
  `,
  styles: [`
    h1 { margin-bottom: 16px; }
    .mt-4 { margin-top: 20px; }
    table.data { width: 100%; border-collapse: collapse; }
    table.data th, table.data td { padding: 8px; text-align: left; border-bottom: 1px solid var(--border); }
    table.data .num { text-align: right; }
    .cart-thumb { width: 48px; height: 48px; object-fit: cover; border-radius: 6px; }
    .qty-input { width: 64px; text-align: right; }
    .cart-total { text-align: right; margin-top: 12px; font-size: 18px; }
    .form-field { margin-bottom: 14px; }
    .form-field label { display: block; margin-bottom: 4px; font-size: 14px; color: var(--muted); }
  `]
})
export class CartComponent {
  cart = inject(CartService);
  auth = inject(AuthService);
  private orders = inject(OrdersService);

  photoBaseUrl = `${environment.apiUrl.replace('/api', '')}/ZipPhotos/`;

  readonly deliveryCompanies = ['СДЕК', 'Озон', 'Вайлдберриз'];

  addressess = signal<AddressDto[]>([]);
  addressId?: number;
  newAddress = '';
  deliveryCompany = '';
  deliveryComment = '';

  busy = signal(false);
  error = signal('');

  @ViewChild('paymentModal') private paymentModal!: PaymentModalComponent;

  constructor() {
    // Гость видит корзину без авторизации — адреса запрашиваем только когда есть кому их показывать.
    if (this.auth.isLoggedIn) {
      this.orders.addressess().subscribe(a => {
        this.addressess.set(a);
        this.addressId = a[0]?.id;
      });
    }
  }

  checkout(): void {
    this.error.set('');

    const items = this.cart.list().map(i => ({
      zipId: i.zip.id,
      count: i.count,
      sellCost: i.zip.sellCost ?? 0
    }));

    if (items.length === 0) {
      this.error.set('Корзина пуста');
      return;
    }

    this.busy.set(true);

    const place = (addressId: number) => {
      this.orders.checkout(
        items, addressId,
        this.deliveryCompany || undefined, this.deliveryComment || undefined
      ).subscribe({
        next: purchase => {
          this.busy.set(false);
          // Заказ уже создан и склад списан — держать эти позиции в корзине незачем,
          // повторное оформление тем же составом было бы ошибкой пользователя.
          this.cart.clear();
          this.paymentModal.open({ id: purchase.id, orderNumber: purchase.purchaseNumber });
        },
        error: err => {
          this.busy.set(false);
          this.error.set(err.error?.message ?? 'Не удалось оформить заказ');
        }
      });
    };

    if (this.newAddress.trim()) {
      this.orders.addAddress(this.newAddress.trim()).subscribe({
        next: a => place(a.id),
        error: () => {
          this.busy.set(false);
          this.error.set('Не удалось сохранить адрес');
        }
      });
    } else if (this.addressId) {
      place(this.addressId);
    } else {
      this.busy.set(false);
      this.error.set('Укажите адрес доставки');
    }
  }
}
