import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrdersService } from '../core/orders.service';

@Component({
  selector: 'app-payment-result',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="card" style="max-width:480px;margin:40px auto;text-align:center">
      @switch (status()) {
        @case ('loading') { <p>Проверяем статус оплаты…</p> }
        @case ('succeeded') {
          <h2 class="success">Оплата прошла успешно ✓</h2>
          <p>Спасибо за заказ! Статус можно отслеживать в личном кабинете.</p>
        }
        @case ('canceled') {
          <h2 class="error">Платёж отменён</h2>
          <p>Вы можете повторить оплату из личного кабинета.</p>
        }
        @default {
          <h2>Платёж обрабатывается</h2>
          <p class="muted">Статус: {{ status() }}. Обновите страницу через минуту или проверьте личный кабинет.</p>
        }
      }
      <a class="btn" routerLink="/cabinet" style="margin-top:16px">В личный кабинет</a>
    </div>
  `,
})
export class PaymentResultComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private orders = inject(OrdersService);

  status = signal('loading');

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId')!;
    this.orders.paymentStatus(orderId).subscribe({
      next: r => this.status.set(r.status),
      error: () => this.status.set('unknown'),
    });
  }
}
