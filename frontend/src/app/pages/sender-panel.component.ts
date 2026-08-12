import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { AdminService } from '../core/admin.service'; // или ваш сервис
import { AuthService } from '../core/auth.service';

@Component({
    selector: 'app-sender-panel',
    standalone: true,
    imports: [CommonModule, DatePipe],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="sender-panel">
        <h2>Панель отправителя</h2>

        <div class="tabs">
            <button [class.active]="activeTab() === 'created'" (click)="switchTab('created')">
                Не отправлены ({{ createdOrders().length }})
            </button>
            <button [class.active]="activeTab() === 'sent'" (click)="switchTab('sent')">
                Отправлены ({{ sentOrders().length }})
            </button>
            <button [class.active]="activeTab() === 'completed'" (click)="switchTab('completed')">
                Завершены ({{ completedOrders().length }})
            </button>
        </div>

        <div class="table-wrap">
            <table class="orders-table">
                <thead>
                    <tr>
                        <th>Номер</th>
                        <th>Дата</th>
                        <th>Парт-номер</th>
                        <th>Товар</th>
                        <th class="num">Кол-во</th>
                        <th>Адрес доставки</th>
                        <th>Действия / Статус</th>
                    </tr>
                </thead>
                <tbody>
                    @for (order of visibleOrders(); track order.id) {
                        <tr>
                            <td [attr.data-label]="'Номер'">{{ order.orderNumber }}</td>
                            <td [attr.data-label]="'Дата'">{{ order.orderDateTime | date:'dd.MM.yyyy HH:mm' }}</td>
                            <td [attr.data-label]="'Парт-номер'"><span class="partnum">{{ order.partNum }}</span></td>
                            <td [attr.data-label]="'Товар'">{{ order.zipName }}</td>
                            <td [attr.data-label]="'Кол-во'" class="num">{{ order.countOrdered }}</td>
                            <td [attr.data-label]="'Адрес доставки'">{{ order.address }}</td>
                            <td [attr.data-label]="'Действия'" class="actions-cell">
                                @if (activeTab() === 'created') {
                                    <button class="btn-act btn-primary" (click)="setStatus(order.id, 'sent')">Отправлено</button>
                                    <button class="btn-act btn-danger" (click)="setStatus(order.id, 'canceled')">Отменить</button>
                                }
                                @if (activeTab() === 'sent') {
                                    <button class="btn-act btn-success" (click)="setStatus(order.id, 'completed')">Завершено</button>
                                    <button class="btn-act btn-warning" (click)="setStatus(order.id, 'created')">Не отправлено</button>
                                }
                                @if (activeTab() === 'completed') {
                                    <span class="badge-done">Успешно доставлен</span>
                                }
                            </td>
                        </tr>
                    } @empty {
                        <tr class="empty-row">
                            <td colspan="7">В этой категории нет заказов</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
  `,
    styles: [`
    .sender-panel { padding: 20px 0 40px; }

    .tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }
    .tabs button {
      padding: 8px 16px;
      border: 1px solid var(--border, #ccc);
      border-radius: 20px;
      background: #fff;
      cursor: pointer;
      font-weight: 500;
      transition: 0.2s;
    }
    .tabs button.active {
      background: var(--accent, #007bff);
      border-color: var(--accent, #007bff);
      color: #fff;
    }

    .table-wrap {
      background: #fff;
      border: 1px solid var(--border, #eee);
      border-radius: 8px;
      overflow-x: auto;
    }

    .orders-table { width: 100%; border-collapse: collapse; font-size: 14px; }
    .orders-table th, .orders-table td { padding: 10px 12px; border-bottom: 1px solid #eee; text-align: left; }
    .orders-table th { background: #f8f9fa; font-weight: 600; white-space: nowrap; }
    .orders-table .num { text-align: right; }
    .orders-table tbody tr:hover { background: #fafafa; }

    .partnum {
      background: #eef2f7;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 13px;
      white-space: nowrap;
    }

    .actions-cell { white-space: nowrap; }
    .btn-act {
      padding: 5px 10px;
      margin-right: 6px;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-size: 13px;
      color: #fff;
    }
    .btn-act:last-child { margin-right: 0; }
    .btn-primary { background: #0d6efd; }
    .btn-danger  { background: #dc3545; }
    .btn-success { background: #198754; }
    .btn-warning { background: #fd7e14; }

    .badge-done {
      background: #d4edda;
      color: #155724;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 13px;
      white-space: nowrap;
    }

    .empty-row td { text-align: center; padding: 20px; color: #777; }

    /* На узких экранах таблица не помещается по ширине — разворачиваем каждую
       строку в карточку, где подпись колонки берётся из data-label. */
    @media (max-width: 768px) {
      .table-wrap { border: none; background: transparent; overflow-x: visible; }
      .orders-table thead { display: none; }
      .orders-table, .orders-table tbody, .orders-table tr, .orders-table td { display: block; width: 100%; }

      .orders-table tr {
        background: #fff;
        border: 1px solid var(--border, #eee);
        border-radius: 8px;
        margin-bottom: 12px;
        padding: 6px 0;
      }

      .orders-table td {
        border: none;
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 12px;
        padding: 7px 12px;
        text-align: right;
      }
      .orders-table td::before {
        content: attr(data-label);
        font-weight: 600;
        color: #6b7280;
        font-size: 12px;
        text-align: left;
        flex-shrink: 0;
      }
      .orders-table .num { text-align: right; }

      .actions-cell { flex-wrap: wrap; justify-content: flex-end; white-space: normal; }
      .btn-act { margin: 3px 0 3px 6px; }

      .empty-row td { justify-content: center; }
      .empty-row td::before { content: none; }
    }
  `]
})
export class SenderPanelComponent implements OnInit {
    private adminService = inject(AdminService);
    private authService = inject(AuthService);
    isAdmin = this.authService.isAdmin;

    orders = signal<any[]>([]);
    activeTab = signal<'created' | 'sent' | 'completed'>('created');
    // хранит ID выбранного заказа
    selectedOrderId = signal<string | null>(null);

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

    // Переключение вкладок со сбросом выделения
    switchTab(tab: 'created' | 'sent' | 'completed') {
        this.activeTab.set(tab);
        this.selectedOrderId.set(null);
    }

    ngOnInit() {
        this.loadOrders();
    }

    loadOrders() {
        this.adminService.getSenderOrders().subscribe({
            next: (data) => this.orders.set(data),
            error: (err) => console.error('Ошибка загрузки заказов', err)
        });
    }

    setStatus(orderId: string, status: string) {
        if (status === 'canceled' && !confirm('Вы уверены, что хотите отменить заказ? Товар вернется на склад.')) {
            return;
        }

        this.adminService.updateOrderStatus(orderId, status).subscribe({
            next: () => this.loadOrders(), // Перезагружаем список после изменения
            error: (err) => alert('Ошибка при обновлении статуса')
        });
    }
}
