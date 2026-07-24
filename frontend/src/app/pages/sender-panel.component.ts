import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../core/admin.service'; // или ваш сервис

@Component({
  selector: 'app-sender-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container mt-4">
      <h2>Панель отправителя</h2>

      <ul class="nav nav-tabs mb-3">
        <li class="nav-item">
          <a class="nav-link" [class.active]="activeTab() === 'created'" (click)="activeTab.set('created')" href="javascript:void(0)">
            Не отправлены ({{ createdOrders().length }})
          </a>
        </li>
        <li class="nav-item">
          <a class="nav-link" [class.active]="activeTab() === 'sent'" (click)="activeTab.set('sent')" href="javascript:void(0)">
            Отправлены ({{ sentOrders().length }})
          </a>
        </li>
        <li class="nav-item">
          <a class="nav-link" [class.active]="activeTab() === 'completed'" (click)="activeTab.set('completed')" href="javascript:void(0)">
            Завершены ({{ completedOrders().length }})
          </a>
        </li>
      </ul>

      <table class="table table-bordered">
        <thead>
          <tr>
            <th>Номер</th>
            <th>Дата</th>
            <th>Товар</th>
            <th>Кол-во</th>
            <th>Адрес доставки</th>
            <th>Действия</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let order of visibleOrders()">
            <td>{{ order.orderNumber }}</td>
            <td>{{ order.orderDateTime | date:'short' }}</td>
            <td>{{ order.zipName }}</td>
            <td>{{ order.countOrdered }}</td>
            <td>{{ order.address }}</td>
            <td>
              <ng-container *ngIf="activeTab() === 'created'">
                <button class="btn btn-sm btn-primary me-2" (click)="setStatus(order.id, 'sent')">Отправлено</button>
                <button class="btn btn-sm btn-danger" (click)="setStatus(order.id, 'canceled')">Отменить</button>
              </ng-container>

              <ng-container *ngIf="activeTab() === 'sent'">
                <button class="btn btn-sm btn-success me-2" (click)="setStatus(order.id, 'completed')">Завершено</button>
                <button class="btn btn-sm btn-warning" (click)="setStatus(order.id, 'created')">Не отправлено</button>
              </ng-container>

              <span *ngIf="activeTab() === 'completed'" class="badge bg-success">Успешно доставлен</span>
            </td>
          </tr>
          <tr *ngIf="visibleOrders().length === 0">
            <td colspan="6" class="text-center">В этой категории нет заказов</td>
          </tr>
        </tbody>
      </table>
    </div>
  `
})
export class SenderPanelComponent implements OnInit {
  private senderService = inject(AdminService); // Замените на нужный сервис
  
  orders = signal<any[]>([]);
  activeTab = signal<'created' | 'sent' | 'completed'>('created');

  // Вычисляемые сигналы для фильтрации по вкладкам
  createdOrders = computed(() => this.orders().filter(o => o.deliveryStatus === 'created'));
  sentOrders = computed(() => this.orders().filter(o => o.deliveryStatus === 'sent'));
  completedOrders = computed(() => this.orders().filter(o => o.deliveryStatus === 'completed'));
  
  // Сигнал, который отдает массив для текущей активной вкладки
  visibleOrders = computed(() => {
    const tab = this.activeTab();
    if (tab === 'created') return this.createdOrders();
    if (tab === 'sent') return this.sentOrders();
    return this.completedOrders();
  });

  ngOnInit() {
    this.loadOrders();
  }

  loadOrders() {
    this.senderService.getSenderOrders().subscribe({
      next: (data) => this.orders.set(data),
      error: (err) => console.error('Ошибка загрузки заказов', err)
    });
  }

  setStatus(orderId: string, status: string) {
    if (status === 'canceled' && !confirm('Вы уверены, что хотите отменить заказ? Товар вернется на склад.')) {
      return;
    }

    this.senderService.updateOrderStatus(orderId, status).subscribe({
      next: () => this.loadOrders(), // Перезагружаем список после изменения
      error: (err) => alert('Ошибка при обновлении статуса')
    });
  }
}