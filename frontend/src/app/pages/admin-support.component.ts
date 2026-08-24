import { Component, OnDestroy, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { SupportService } from '../core/support.service';
import { SupportTicketDto, SupportTicketSummaryDto } from '../core/models';

/** Опрос списка и открытого обращения — SignalR/WebSocket в проекте нет, поэтому просто polling. */
const POLL_INTERVAL_MS = 5000;

@Component({
  selector: 'app-admin-support',
  standalone: true,
  imports: [FormsModule, CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="support-container">
      <h2>Обращения в поддержку</h2>

      @if (error()) {
        <div class="error">
          {{ error() }}
          <button class="close-inline" (click)="error.set('')">✖</button>
        </div>
      }

      <div class="layout">
        <div class="tickets-pane">
          @if (tickets().length === 0) {
            <p class="muted">Обращений пока нет.</p>
          }
          @for (t of tickets(); track t.id) {
            <div class="ticket-row" [class.active]="t.id === selectedId()" (click)="selectTicket(t.id)">
              <div class="ticket-row-top">
                <span class="order-number">{{ t.orderNumber ? 'Заказ ' + t.orderNumber : t.subject }}</span>
                <span class="date">{{ t.lastMessageAt | date:'dd.MM HH:mm' }}</span>
              </div>
              <div class="user-line">{{ t.userFio }} · {{ t.userEmail }}</div>
              <div class="preview">{{ t.lastMessagePreview }}</div>
              @if (t.isClosed) {
                <span class="closed-badge">закрыто</span>
              }
              <button class="delete-btn" title="Удалить обращение" (click)="deleteTicket(t.id, $event)">✖</button>
            </div>
          }
        </div>

        <div class="chat-pane">
          @if (selectedTicket(); as ticket) {
            <div class="card chat-card">
              <div class="chat-header">
                <span>{{ ticket.orderNumber ? 'Заказ ' + ticket.orderNumber : ticket.subject }}</span>
                @if (!ticket.isClosed) {
                  <button class="btn btn-secondary close-btn" (click)="closeTicket(ticket.id)" [disabled]="busy()">
                    Завершить обращение
                  </button>
                }
              </div>
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
                            placeholder="Ответ покупателю…"
                            (keydown.enter)="onEnter($event)"></textarea>
                  <button class="btn" (click)="submitReply()" [disabled]="busy() || !replyText.trim()">
                    Отправить
                  </button>
                </div>
              }
            </div>
          } @else {
            <p class="muted">Выберите обращение слева.</p>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .support-container { padding: 20px 0 40px; }
    h2 { margin-bottom: 16px; }

    .layout { display: grid; grid-template-columns: 340px 1fr; gap: 20px; align-items: start; }

    .tickets-pane { display: flex; flex-direction: column; gap: 8px; }

    .ticket-row {
      position: relative;
      border: 1px solid var(--border, #eee);
      border-radius: 8px;
      padding: 10px 34px 10px 12px;
      cursor: pointer;
      background: #fff;
      transition: 0.15s;
    }
    .delete-btn {
      position: absolute;
      top: 8px;
      right: 8px;
      border: none;
      background: none;
      color: var(--muted, #999);
      cursor: pointer;
      font-size: 13px;
      line-height: 1;
      padding: 2px 4px;
    }
    .delete-btn:hover { color: #dc3545; }
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
    .ticket-row:hover { border-color: var(--accent, #007bff); }
    .ticket-row.active { border-color: var(--accent, #007bff); background: #f0f7ff; }
    .ticket-row-top { display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 2px; }
    .order-number { font-weight: 600; }
    .date { color: var(--muted, #888); }
    .user-line { font-size: 12px; color: var(--muted, #777); margin-bottom: 4px; }
    .preview { font-size: 13px; color: var(--muted, #666); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    .card {
      background: #fff;
      padding: 20px;
      border-radius: 8px;
      border: 1px solid var(--border, #eee);
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }

    .form-control {
      width: 100%;
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 14px;
      box-sizing: border-box;
      font-family: inherit;
    }

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

    .chat-card { display: flex; flex-direction: column; height: 620px; }
    .chat-header {
      font-weight: 600; margin-bottom: 12px; padding-bottom: 10px; border-bottom: 1px solid var(--border, #eee);
      display: flex; align-items: center; justify-content: space-between; gap: 10px;
    }
    .close-btn { padding: 5px 12px; font-size: 12px; font-weight: 500; }
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
      .chat-card { height: 480px; }
    }
  `]
})
export class AdminSupportComponent implements OnDestroy {
  private support = inject(SupportService);

  tickets = signal<SupportTicketSummaryDto[]>([]);
  selectedId = signal<string | null>(null);
  selectedTicket = signal<SupportTicketDto | null>(null);

  busy = signal(false);
  error = signal('');
  replyText = '';

  private listPollHandle: ReturnType<typeof setInterval> | null = null;
  private ticketPollHandle: ReturnType<typeof setInterval> | null = null;

  constructor() {
    this.loadList();
    this.listPollHandle = setInterval(() => this.loadList(), POLL_INTERVAL_MS);
  }

  ngOnDestroy() {
    if (this.listPollHandle !== null) clearInterval(this.listPollHandle);
    this.stopTicketPolling();
  }

  private loadList() {
    this.support.adminList().subscribe({
      next: list => this.tickets.set(list),
      error: () => this.error.set('Не удалось загрузить обращения'),
    });
  }

  selectTicket(id: string) {
    this.selectedId.set(id);
    this.loadSelectedTicket();
    this.stopTicketPolling();
    this.ticketPollHandle = setInterval(() => this.loadSelectedTicket(), POLL_INTERVAL_MS);
  }

  private stopTicketPolling() {
    if (this.ticketPollHandle !== null) {
      clearInterval(this.ticketPollHandle);
      this.ticketPollHandle = null;
    }
  }

  private loadSelectedTicket() {
    const id = this.selectedId();
    if (!id) return;
    this.support.adminGetTicket(id).subscribe({
      next: t => this.selectedTicket.set(t),
      error: () => this.error.set('Не удалось загрузить переписку'),
    });
  }

  deleteTicket(id: string, event: Event) {
    event.stopPropagation();
    if (!confirm('Удалить обращение вместе со всей перепиской?')) return;

    this.support.adminDelete(id).subscribe({
      next: () => {
        this.tickets.set(this.tickets().filter(t => t.id !== id));
        if (this.selectedId() === id) {
          this.selectedId.set(null);
          this.selectedTicket.set(null);
          this.stopTicketPolling();
        }
      },
      error: err => this.error.set(err.error?.message || 'Не удалось удалить обращение'),
    });
  }

  submitReply() {
    const id = this.selectedId();
    if (!id || !this.replyText.trim()) return;

    this.busy.set(true);
    this.support.adminReply(id, this.replyText.trim()).subscribe({
      next: () => {
        this.busy.set(false);
        this.replyText = '';
        this.loadSelectedTicket();
        this.loadList();
      },
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message || 'Не удалось отправить сообщение');
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

  closeTicket(id: string) {
    if (!confirm('Завершить обращение? После этого отвечать в нём будет нельзя.')) return;

    this.busy.set(true);
    this.support.adminClose(id).subscribe({
      next: () => {
        this.busy.set(false);
        this.loadSelectedTicket();
        this.loadList();
      },
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message || 'Не удалось завершить обращение');
      },
    });
  }
}
