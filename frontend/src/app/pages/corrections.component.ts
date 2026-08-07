import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { AdminService } from '../core/admin.service';

type Mode = 'correction' | 'reprice';

interface ZipRow {
  id: string;
  name: string;
  partNum?: string;
  incomeCost: number;
  sellCost?: number;
  countStored: number;
}

@Component({
  selector: 'app-corrections',
  standalone: true,
  imports: [FormsModule, CommonModule, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="corrections-container">
      <h2>Коррекция</h2>

      @if (message()) {
        <div class="success">
          {{ message() }}
          <button class="close-inline" (click)="message.set('')">✖</button>
        </div>
      }
      @if (error()) {
        <div class="error">
          {{ error() }}
          <button class="close-inline" (click)="error.set('')">✖</button>
        </div>
      }

      <!-- Выбор функции -->
      <div class="mode-tabs">
        <button [class.active]="mode() === 'correction'" (click)="select('correction')">
          Коррекция остатка
        </button>
        <button [class.active]="mode() === 'reprice'" (click)="select('reprice')">
          Переоценка
        </button>
      </div>

      <div class="card">
        <!-- Общий выбор детали -->
        <div class="form-group">
          <label>Запчасть <span class="req">*</span></label>
          <select [(ngModel)]="zipId" (ngModelChange)="onZipChange()" class="form-control">
            <option [ngValue]="''">— выберите деталь —</option>
            @for (z of zips(); track z.id) {
              <option [ngValue]="z.id">{{ z.name }} ({{ z.partNum }})</option>
            }
          </select>
        </div>

        @if (selected(); as z) {
          <div class="current-state">
            <span>На складе: <b>{{ z.countStored }}</b> шт.</span>
            <span>Закупочная: <b>{{ z.incomeCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
            <span>Цена продажи: <b>{{ z.sellCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
          </div>
        }

        <!-- Коррекция остатка -->
        @if (mode() === 'correction') {
          <div class="form-group">
            <label>Изменение количества <span class="req">*</span></label>
            <input type="number" [(ngModel)]="delta" class="form-control" placeholder="например −2 или 5">
            <small class="hint">
              Со знаком: положительное — оприходование, отрицательное — списание.
              @if (selected(); as z) {
                @if (delta) {
                  Станет: <b [class.negative]="z.countStored + delta < 0">{{ z.countStored + delta }}</b> шт.
                }
              }
            </small>
          </div>

          <div class="form-group">
            <label>Причина <span class="req">*</span></label>
            <input type="text" [(ngModel)]="comment" class="form-control"
                   placeholder="Пересорт, повреждение при хранении, инвентаризация…">
            <small class="hint">Попадёт в журнал операций — без причины запись бессмысленна.</small>
          </div>

          <button class="btn" (click)="submitCorrection()" [disabled]="busy()">
            {{ busy() ? 'Проведение…' : 'Провести коррекцию' }}
          </button>
        }

        <!-- Переоценка -->
        @if (mode() === 'reprice') {
          <div class="form-group">
            <label>Новая цена продажи <span class="req">*</span></label>
            <input type="number" [(ngModel)]="newCost" class="form-control" placeholder="например 2290">
            <small class="hint">
              Наценка или уценка определяется автоматически по знаку разницы.
              @if (selected(); as z) {
                @if (newCost !== null && newCost !== undefined && z.sellCost != null) {
                  Изменение:
                  <b [class.negative]="newCost - z.sellCost < 0">
                    {{ newCost - z.sellCost > 0 ? '+' : '' }}{{ newCost - z.sellCost | currency:'RUB':'symbol-narrow':'1.0-2' }}
                  </b>
                }
              }
            </small>
          </div>

          <div class="form-group">
            <label>Комментарий</label>
            <input type="text" [(ngModel)]="comment" class="form-control"
                   placeholder="Сезонная наценка, распродажа остатков…">
          </div>

          <button class="btn" (click)="submitReprice()" [disabled]="busy()">
            {{ busy() ? 'Сохранение…' : 'Изменить цену' }}
          </button>
        }
      </div>

      <p class="muted footnote">
        Каждая операция попадает в журнал и видна в отчётах:
        коррекции — в истории по детали, переоценка — в отчёте «Наценка / уценка».
      </p>
    </div>
  `,
  styles: [`
    .corrections-container { padding: 20px 0 40px; max-width: 720px; }

    .mode-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }
    .mode-tabs button {
      padding: 8px 16px;
      border: 1px solid var(--border, #ccc);
      border-radius: 20px;
      background: #fff;
      cursor: pointer;
      font-weight: 500;
      transition: 0.2s;
    }
    .mode-tabs button.active {
      background: var(--accent, #007bff);
      border-color: var(--accent, #007bff);
      color: #fff;
    }

    .card {
      background: #fff;
      padding: 20px;
      border-radius: 8px;
      border: 1px solid var(--border, #eee);
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }

    .form-group { margin-bottom: 16px; }
    .form-group label {
      display: block;
      margin-bottom: 6px;
      font-weight: 500;
      font-size: 13px;
      color: #555;
    }
    .req { color: red; }

    .form-control {
      width: 100%;
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 14px;
      box-sizing: border-box;
    }

    .hint { display: block; margin-top: 5px; color: #777; font-size: 12px; }

    .current-state {
      display: flex;
      flex-wrap: wrap;
      gap: 20px;
      padding: 12px 14px;
      margin-bottom: 16px;
      background: #f6f9fc;
      border: 1px solid #e3ecf3;
      border-radius: 6px;
      font-size: 14px;
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

    .negative { color: #dc3545; }
    .muted { color: #777; }
    .footnote { font-size: 13px; margin-top: 16px; }

    .error {
      color: #721c24;
      background: #f8d7da;
      border: 1px solid #f5c6cb;
      padding: 10px;
      margin-bottom: 15px;
      border-radius: 4px;
    }
    .success {
      color: #155724;
      background: #d4edda;
      border: 1px solid #c3e6cb;
      padding: 10px;
      margin-bottom: 15px;
      border-radius: 4px;
    }
    .close-inline { float: right; cursor: pointer; background: none; border: none; }

    @media (max-width: 768px) {
      .corrections-container { padding: 12px 0 32px; }
      .card { padding: 14px; }
      .mode-tabs { flex-wrap: nowrap; overflow-x: auto; padding-bottom: 4px; }
      .mode-tabs button { flex: 0 0 auto; font-size: 14px; padding: 7px 13px; }
      .current-state { flex-direction: column; gap: 6px; }
      .btn { width: 100%; }
    }
  `]
})
export class CorrectionsComponent {
  private admin = inject(AdminService);

  mode = signal<Mode>('correction');
  busy = signal(false);
  message = signal('');
  error = signal('');

  zips = signal<ZipRow[]>([]);
  zipId = '';
  delta: number | null = null;
  newCost: number | null = null;
  comment = '';

  /**
   * Именно сигнал, а не computed: zipId — обычное поле ngModel, и computed поверх него
   * никогда бы не пересчитался, потому что не читает ни одного сигнала.
   */
  selected = signal<ZipRow | null>(null);

  constructor() {
    this.loadZips();
  }

  private loadZips() {
    this.admin.list('zip').subscribe({
      next: (data: any) => {
        this.zips.set(Array.isArray(data) ? data : (data.items ?? []));
        // После проведения операции список перечитывается — обновляем и выбранную строку,
        // иначе на экране останутся старые остаток и цена.
        this.syncSelected();
      },
      error: () => this.error.set('Не удалось загрузить список запчастей')
    });
  }

  private syncSelected() {
    this.selected.set(this.zips().find(z => z.id === this.zipId) ?? null);
  }

  select(mode: Mode) {
    this.mode.set(mode);
    this.message.set('');
    this.error.set('');
    this.comment = '';
  }

  onZipChange() {
    this.syncSelected();
    // Подставляем текущую цену, чтобы её было видно и достаточно было поправить.
    this.newCost = this.selected()?.sellCost ?? null;
  }

  submitCorrection() {
    if (!this.validateZip()) return;
    if (!this.delta) {
      this.error.set('Укажите изменение количества — нулевая коррекция не имеет смысла.');
      return;
    }
    if (!this.comment.trim()) {
      this.error.set('Укажите причину коррекции — она попадёт в журнал.');
      return;
    }

    this.run(this.admin.addCorrection(this.zipId, this.delta, this.comment.trim()), res => {
      this.message.set(`Коррекция проведена. Остаток на складе: ${(res as any).count} шт.`);
      this.delta = null;
      this.comment = '';
    });
  }

  submitReprice() {
    if (!this.validateZip()) return;
    if (this.newCost === null || this.newCost === undefined) {
      this.error.set('Укажите новую цену продажи.');
      return;
    }
    if (this.newCost < 0) {
      this.error.set('Цена не может быть отрицательной.');
      return;
    }

    this.run(this.admin.reprice(this.zipId, this.newCost, this.comment.trim() || undefined), res => {
      const r = res as any;
      const kind = r.newCost > r.oldCost ? 'Наценка' : 'Уценка';
      this.message.set(`${kind}: цена изменена с ${r.oldCost} на ${r.newCost}.`);
      this.comment = '';
    });
  }

  private validateZip(): boolean {
    this.message.set('');
    this.error.set('');
    if (!this.zipId) {
      this.error.set('Выберите запчасть.');
      return false;
    }
    return true;
  }

  private run(request: any, onOk: (res: unknown) => void) {
    this.busy.set(true);
    request.subscribe({
      next: (res: unknown) => {
        onOk(res);
        this.busy.set(false);
        // Перечитываем список, чтобы остаток и цена на экране были актуальными.
        this.loadZips();
      },
      error: (err: any) => {
        this.busy.set(false);
        this.error.set(err.error?.message || err.message || 'Операция не выполнена');
      }
    });
  }
}
