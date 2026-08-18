import { Component, EventEmitter, Output, inject, signal } from '@angular/core';

import { OrdersService } from '../core/orders.service';

/**
 * Оплата переводом на карту: онлайн-эквайринг (ЮKassa) отключён, поэтому вместо редиректа
 * на оплату показываем реквизиты для ручного перевода и принимаем чек. Используется и в
 * каталоге сразу после оформления заказа, и в личном кабинете — для уже созданных заказов,
 * к которым чек ещё не приложен.
 *
 * Управляется снаружи через open()/close(), а не через @Input — родителю не нужно держать
 * отдельный сигнал видимости, достаточно передать заказ.
 */
@Component({
  selector: 'app-payment-modal',
  standalone: true,
  template: `
    @if (order(); as o) {
      <div class="modal-backdrop" (click)="close()">
        <div class="card modal" (click)="$event.stopPropagation()">
          <h3>Оплата заказа {{ o.orderNumber }}</h3>
          <p>Осуществите перевод суммы по номеру данной карты, после прикрепите чек об успешном переводе</p>

          @if (cardNumber()) {
            <p class="card-number">{{ cardNumber() }}</p>
          } @else {
            <p class="muted">Не удалось получить номер карты. Свяжитесь с администратором.</p>
          }

          <input
            #receiptInput
            type="file"
            accept="image/jpeg,image/png,image/webp,application/pdf"
            style="display: none;"
            (change)="onFileSelected($event)"
          >

          @if (receiptUploaded()) {
            <p class="success">Чек загружен — спасибо! Мы проверим оплату и обновим статус заказа.</p>
          } @else {
            <button class="btn" [disabled]="uploadingReceipt()" (click)="receiptInput.click()">
              @if (uploadingReceipt()) { Загрузка… } @else { Прикрепить чек }
            </button>
          }
          @if (receiptError()) { <p class="error">{{ receiptError() }}</p> }

          <div class="modal-actions">
            <button class="btn btn-secondary" (click)="close()">Готово</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, .4);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    .modal {
      width: 420px;
      max-width: 92vw;
    }
    .modal-actions {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      margin-top: 16px;
    }
    .card-number {
      font-family: 'Courier New', monospace;
      font-size: 22px;
      font-weight: 700;
      letter-spacing: 2px;
      margin: 10px 0 16px;
    }
  `]
})
export class PaymentModalComponent {
  private orders = inject(OrdersService);

  order = signal<{ id: string; orderNumber: string } | null>(null);
  cardNumber = signal('');
  uploadingReceipt = signal(false);
  receiptUploaded = signal(false);
  receiptError = signal('');

  /** Чек успешно загружен — родитель может, например, перезапросить список заказов. */
  @Output() uploaded = new EventEmitter<void>();

  open(order: { id: string; orderNumber: string }): void {
    this.order.set(order);
    this.receiptUploaded.set(false);
    this.receiptError.set('');

    if (!this.cardNumber()) {
      this.orders.paymentInfo().subscribe({
        next: info => this.cardNumber.set(info.cardNumber),
        error: () => this.cardNumber.set('')
      });
    }
  }

  close(): void {
    this.order.set(null);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const order = this.order();
    if (!file || !order) return;

    this.receiptError.set('');
    this.uploadingReceipt.set(true);

    this.orders.uploadReceipt(order.id, file).subscribe({
      next: () => {
        this.uploadingReceipt.set(false);
        this.receiptUploaded.set(true);
        this.uploaded.emit();
      },
      error: err => {
        this.uploadingReceipt.set(false);
        this.receiptError.set(err.error?.message ?? 'Не удалось загрузить чек');
      }
    });

    // Сбрасываем значение, иначе повторный выбор того же файла не вызовет change.
    input.value = '';
  }
}
