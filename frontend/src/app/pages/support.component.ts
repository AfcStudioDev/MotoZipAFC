import { Component, OnDestroy, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { SupportService } from '../core/support.service';
import { OrdersService } from '../core/orders.service';
import { OrderDto, SupportTicketDto, SupportTicketSummaryDto } from '../core/models';

/** Опрос активного обращения на новые сообщения — SignalR/WebSocket в проекте нет, поэтому просто polling. */
const POLL_INTERVAL_MS = 5000;

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [FormsModule, CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="support-container">
      <h2>Поддержка</h2>

      @if (error()) {
        <div class="error">
          {{ error() }}
          <button class="close-inline" (click)="error.set('')">✖</button>
        </div>
      }

      <div class="layout">
        <div class="tickets-pane">
          <button class="btn new-btn" (click)="openNewForm()">+ Новое обращение</button>

          @if (tickets().length === 0) {
            <p class="muted">У вас пока нет обращений в поддержку.</p>
          }
          @for (t of tickets(); track t.id) {
            <div class="ticket-row" [class.active]="t.id === selectedId()" (click)="selectTicket(t.id)">
              <div class="ticket-row-top">
                <span class="order-number">Заказ {{ t.orderNumber }}</span>
                <span class="date">{{ t.lastMessageAt | date:'dd.MM HH:mm' }}</span>
              </div>
              <div class="preview">{{ t.lastMessagePreview }}</div>
              @if (t.isClosed) {
                <span class="closed-badge">закрыто</span>
              }
            </div>
          }
        </div>

        <div class="chat-pane">
          @if (showNewForm()) {
            <div class="card">
              <h3>Новое обращение</h3>
              <div class="form-group">
                <label>Заказ <span class="req">*</span></label>
                <select [(ngModel)]="newOrderId" class="form-control">
                  <option [ngValue]="''">— выберите заказ —</option>
                  @for (o of orders(); track o.id) {
                    <option [ngValue]="o.id">{{ o.orderNumber }} — {{ o.zipName }}</option>
                  }
                </select>
              </div>
              <div class="form-group">
                <label>Сообщение <span class="req">*</span></label>
                <textarea [(ngModel)]="newMessage" class="form-control" rows="4"
                          placeholder="Опишите вашу проблему или вопрос…"></textarea>
              </div>
              <div class="actions">
                <button class="btn" (click)="submitNewTicket()" [disabled]="busy()">
                  {{ busy() ? 'Отправка…' : 'Отправить обращение' }}
                </button>
                <button class="btn btn-secondary" (click)="showNewForm.set(false)">Отмена</button>
              </div>
            </div>
          } @else if (selectedTicket(); as ticket) {
            <div class="card chat-card">
              <div class="chat-header">Заказ {{ ticket.orderNumber }}</div>
              <div class="messages">
                @for (m of ticket.messages; track m.id) {
                  <div class="message" [class.from-admin]="m.isFromAdmin">
                    <div class="message-author">{{ m.isFromAdmin ? 'Поддержка' : m.authorName }}</div>
                    <div class="message-text">{{ m.text }}</div>
                    <div class="message-time">{{ m.createdAt | date:'dd.MM HH:mm' }}</div>
                  </div>
                }
              </div>
              @if (ticket.isClosed) {
                <div class="closed-notice">Обращение закрыто</div>
              } @else {
                <div class="reply-box">
                  <textarea [(ngModel)]="replyText" class="form-control" rows="2"
                            placeholder="Ваш ответ…"
                            (keydown.enter)="onEnter($event)"></textarea>
                  <button class="btn" (click)="submitReply()" [disabled]="busy() || !replyText.trim()">
                    Отправить
                  </button>
                </div>
              }
            </div>
          } @else {
            <p class="muted">Выберите обращение слева или создайте новое.</p>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .support-container { padding: 20px 0 40px; }
    h2 { margin-bottom: 16px; }

    .layout { display: grid; grid-template-columns: 320px 1fr; gap: 20px; align-items: start; }

    .tickets-pane { display: flex; flex-direction: column; gap: 8px; }
    .new-btn { margin-bottom: 8px; }

    .ticket-row {
      border: 1px solid var(--border, #eee);
      border-radius: 8px;
      padding: 10px 12px;
      cursor: pointer;
      background: #fff;
      transition: 0.15s;
    }
    .ticket-row:hover { border-color: var(--accent, #007bff); }
    .ticket-row.active { border-color: var(--accent, #007bff); background: #f0f7ff; }
    .ticket-row-top { display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 4px; }
    .order-number { font-weight: 600; }
    .date { color: var(--muted, #888); }
    .preview { font-size: 13px; color: var(--muted, #666); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .closed-badge {
      display: inline-block;
      margin-top: 4px;
      font-size: 10px;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      color: var(--muted, #888);
      background: #eee;
      border-radius: 4px;
      padding: 1px 6px;
    }

    .card {
      background: #fff;
      padding: 20px;
      border-radius: 8px;
      border: 1px solid var(--border, #eee);
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }

    .form-group { margin-bottom: 16px; }
    .form-group label { display: block; margin-bottom: 6px; font-weight: 500; font-size: 13px; color: #555; }
    .req { color: red; }
    .form-control {
      width: 100%;
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 14px;
      box-sizing: border-box;
      font-family: inherit;
    }

    .actions { display: flex; gap: 10px; }

    .btn {
      padding: 9px 18px;
      border: none;
      border-radius: 4px;
      background: var(--accent, #007bff);
      color: #fff;
      cursor: pointer;
      font-weight: 500;
    }
    .btn:disabled { opacity: 0.6; cursor: default; }
    .btn-secondary { background: #6c757d; }

    .chat-card { display: flex; flex-direction: column; height: 560px; }
    .chat-header { font-weight: 600; margin-bottom: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--border, #eee); }
    .closed-notice {
      margin-top: 12px; padding: 10px 12px; text-align: center;
      background: #f1f3f5; color: var(--muted, #777); border-radius: 6px; font-size: 13px;
    }
    .messages { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 10px; padding-right: 4px; }

    .message { max-width: 75%; padding: 8px 12px; border-radius: 10px; background: #f1f3f5; align-self: flex-start; }
    .message.from-admin { align-self: flex-end; background: #dbeafe; }
    .message-author { font-size: 11px; font-weight: 600; color: var(--muted, #777); margin-bottom: 2px; }
    .message-text { font-size: 14px; white-space: pre-wrap; word-break: break-word; }
    .message-time { font-size: 10px; color: var(--muted, #999); margin-top: 4px; text-align: right; }

    .reply-box { display: flex; gap: 10px; margin-top: 12px; align-items: flex-end; }
    .reply-box textarea { flex: 1; }

    .muted { color: #777; }
    .error {
      color: #721c24;
      background: #f8d7da;
      border: 1px solid #f5c6cb;
      padding: 10px;
      margin-bottom: 15px;
      border-radius: 4px;
    }
    .close-inline { float: right; cursor: pointer; background: none; border: none; }

    @media (max-width: 768px) {
      .layout { grid-template-columns: 1fr; }
      .chat-card { height: 460px; }
    }
  `]
})
export class SupportComponent implements OnDestroy {
  private support = inject(SupportService);
  private ordersService = inject(OrdersService);

  tickets = signal<SupportTicketSummaryDto[]>([]);
  orders = signal<OrderDto[]>([]);
  selectedId = signal<string | null>(null);
  selectedTicket = signal<SupportTicketDto | null>(null);
  showNewForm = signal(false);

  busy = signal(false);
  error = signal('');

  newOrderId = '';
  newMessage = '';
  replyText = '';

  private pollHandle: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.loadTickets();
    this.ordersService.myOrders(1).subscribe({
      next: r => this.orders.set(r.items),
    });
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  private loadTickets() {
    this.support.myTickets().subscribe({
      next: list => this.tickets.set(list),
      error: () => this.error.set('Не удалось загрузить обращения'),
    });
  }

  openNewForm() {
    this.showNewForm.set(true);
    this.selectedId.set(null);
    this.selectedTicket.set(null);
    this.stopPolling();
    this.newOrderId = '';
    this.newMessage = '';
  }

  selectTicket(id: string) {
    this.showNewForm.set(false);
    this.selectedId.set(id);
    this.loadSelectedTicket();
    this.startPolling();
  }

  private loadSelectedTicket() {
    const id = this.selectedId();
    if (!id) return;
    this.support.getTicket(id).subscribe({
      next: t => this.selectedTicket.set(t),
      error: () => this.error.set('Не удалось загрузить переписку'),
    });
  }

  private startPolling() {
    this.stopPolling();
    this.pollHandle = setInterval(() => this.loadSelectedTicket(), POLL_INTERVAL_MS);
  }

  private stopPolling() {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
  }

  submitNewTicket() {
    this.error.set('');
    if (!this.newOrderId) {
      this.error.set('Выберите заказ.');
      return;
    }
    if (!this.newMessage.trim()) {
      this.error.set('Опишите ваше обращение.');
      return;
    }

    this.busy.set(true);
    this.support.createTicket(this.newOrderId, this.newMessage.trim()).subscribe({
      next: ticket => {
        this.busy.set(false);
        this.showNewForm.set(false);
        this.loadTickets();
        this.selectedTicket.set(ticket);
        this.selectedId.set(ticket.id);
        this.startPolling();
      },
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message || 'Не удалось отправить обращение');
      },
    });
  }

  /** Enter отправляет сообщение, Shift+Enter — перенос строки. */
  onEnter(event: Event) {
    const e = event as KeyboardEvent;
    if (e.shiftKey) return;
    e.preventDefault();
    this.submitReply();
  }

  submitReply() {
    const id = this.selectedId();
    if (!id || !this.replyText.trim()) return;

    this.busy.set(true);
    this.support.sendMessage(id, this.replyText.trim()).subscribe({
      next: () => {
        this.busy.set(false);
        this.replyText = '';
        this.loadSelectedTicket();
        this.loadTickets();
      },
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message || 'Не удалось отправить сообщение');
      },
    });
  }
}
