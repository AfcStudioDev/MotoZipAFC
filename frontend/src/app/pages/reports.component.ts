import { Component, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { AdminService, ZipLookupItem } from '../core/admin.service';
import { AutocompleteInputComponent, AutocompleteOption } from '../shared/autocomplete-input.component';
import {
  IncomeReportRow,
  PriceHistoryRow,
  SalesReportRow,
  StockReportRow,
  ZipHistoryRow,
} from '../core/models';

type ReportKind = 'stock' | 'sales' | 'income' | 'price-history' | 'zip-history';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [FormsModule, CommonModule, CurrencyPipe, DatePipe, AutocompleteInputComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="reports-container">
      <h2>Отчёты</h2>

      @if (error()) {
        <div class="error">
          {{ error() }}
          <button class="close-inline" (click)="error.set('')">✖</button>
        </div>
      }

      <!-- Выбор отчёта -->
      <div class="report-tabs">
        <button [class.active]="kind() === 'stock'" (click)="select('stock')">Остатки</button>
        <button [class.active]="kind() === 'sales'" (click)="select('sales')">Проданные детали</button>
        <button [class.active]="kind() === 'income'" (click)="select('income')">Поступления</button>
        <button [class.active]="kind() === 'price-history'" (click)="select('price-history')">Наценка / уценка</button>
        <button [class.active]="kind() === 'zip-history'" (click)="select('zip-history')">История по детали</button>
      </div>

      <!-- Параметры -->
      <div class="card filters">
        @if (kind() === 'sales' || kind() === 'income') {
          <div class="filter-field">
            <label>Период с</label>
            <input type="date" [(ngModel)]="from" class="form-control">
          </div>
          <div class="filter-field">
            <label>по</label>
            <input type="date" [(ngModel)]="to" class="form-control">
          </div>
          <div class="filter-field grow">
            <label>Парт-номер</label>
            <app-autocomplete-input
              [(value)]="partNumFilter"
              [options]="partNumOptions()"
              placeholder="часть парт-номера" />
          </div>
          <div class="filter-field grow">
            <label>Наименование</label>
            <app-autocomplete-input
              [(value)]="nameFilter"
              [options]="nameOptions()"
              placeholder="часть наименования" />
          </div>
        }

        <!-- Донор есть и у среза остатков: он привязан к детали, а не к периоду. -->
        @if (kind() === 'stock' || kind() === 'sales' || kind() === 'income') {
          <div class="filter-field grow">
            <label>Донор</label>
            <app-autocomplete-input
              [(value)]="donorFilter"
              [options]="donorOptions()"
              placeholder="часть названия донора" />
          </div>
        }

        @if (kind() === 'price-history' || kind() === 'zip-history') {
          <div class="filter-field grow zip-picker">
            <label>
              Запчасть
              @if (kind() === 'zip-history') { <span class="req">*</span> }
            </label>
            <app-autocomplete-input
              [value]="zipQuery()"
              (valueChange)="onZipQuery($event)"
              [options]="zipOptions()"
              (picked)="onZipPicked($event)"
              [placeholder]="kind() === 'price-history'
                ? 'все детали — или введите наименование либо парт-номер'
                : 'наименование или парт-номер'" />

            @if (zipQuery() && !zipId()) {
              <small class="hint">Выберите деталь из подсказок.</small>
            }
          </div>
        }

        <button class="btn" (click)="run()" [disabled]="busy()">
          {{ busy() ? 'Загрузка…' : 'Сформировать' }}
        </button>
      </div>

      <!-- Результат -->
      <div class="card">
        @if (busy()) {
          <p class="muted">Загрузка…</p>
        } @else if (!loaded()) {
          <p class="muted">Выберите параметры и нажмите «Сформировать».</p>
        } @else {

          <!-- Остатки -->
          @if (kind() === 'stock') {
            <div class="summary">
              <span>Позиций: <b>{{ stock().length }}</b></span>
              <span>Всего: <b>{{ totalStockCount() }}</b> шт.</span>
              <span>На сумму: <b>{{ totalStockCost() | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Наименование</th>
                    <th>Парт-номер</th>
                    <th class="num">Количество на складе</th>
                    <th class="num">Стоимость</th>
                    <th>Донор</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of stock(); track r.zipId) {
                    <tr>
                      <td data-label="Наименование">{{ r.name }}</td>
                      <td data-label="Парт-номер" class="muted">{{ r.partNum }}</td>
                      <td data-label="Количество на складе" class="num">{{ r.count }}</td>
                      <td data-label="Стоимость" class="num">
                        {{ r.sellCost != null ? (r.sellCost | currency:'RUB':'symbol-narrow':'1.0-2') : '—' }}
                      </td>
                      <td data-label="Донор" class="muted">{{ r.incomeMoto }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="5" class="empty">На складе нет деталей с остатком больше нуля</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <!-- Продажи -->
          @if (kind() === 'sales') {
            <div class="summary">
              <span>Продано: <b>{{ totalSold() }}</b> шт.</span>
              <span>Выручка: <b>{{ totalRevenue() | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
              <span>Маржа: <b>{{ totalMargin() | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Наименование</th>
                    <th>Парт-номер</th>
                    <th class="num">Продано</th>
                    <th class="num">Выручка</th>
                    <th class="num">Себестоимость</th>
                    <th class="num">Маржа</th>
                    <th>Донор</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of sales(); track r.zipId) {
                    <tr>
                      <td data-label="Наименование">{{ r.name }}</td>
                      <td data-label="Парт-номер" class="muted">{{ r.partNum }}</td>
                      <td data-label="Продано" class="num">{{ r.sold }}</td>
                      <td data-label="Выручка" class="num">{{ r.revenue | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Себестоимость" class="num">{{ r.cost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Маржа" class="num" [class.negative]="r.margin < 0">
                        {{ r.margin | currency:'RUB':'symbol-narrow':'1.0-2' }}
                      </td>
                      <td data-label="Донор" class="muted">{{ r.incomeMoto }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="7" class="empty">За выбранный период продаж не было</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <!-- Поступления -->
          @if (kind() === 'income') {
            <div class="summary">
              <span>Поступило: <b>{{ totalQty() }}</b> шт.</span>
              <span>На сумму: <b>{{ totalIncome() | currency:'RUB':'symbol-narrow':'1.0-2' }}</b></span>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Дата</th>
                    <th>Наименование</th>
                    <th>Парт-номер</th>
                    <th class="num">Кол-во</th>
                    <th class="num">Цена закупки</th>
                    <th class="num">Сумма</th>
                    <th>Донор</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of income(); track $index) {
                    <tr>
                      <td data-label="Дата">{{ r.createdAt | date:'dd.MM.yyyy HH:mm' }}</td>
                      <td data-label="Наименование">{{ r.name }}</td>
                      <td data-label="Парт-номер" class="muted">{{ r.partNum }}</td>
                      <td data-label="Кол-во" class="num">{{ r.qty }}</td>
                      <td data-label="Цена закупки" class="num">{{ r.unitCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Сумма" class="num">{{ r.total | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Донор" class="muted">{{ r.incomeMoto }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="7" class="empty">За выбранный период поступлений не было</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <!-- Наценка / уценка -->
          @if (kind() === 'price-history') {
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Дата</th>
                    <th>Наименование</th>
                    <th>Операция</th>
                    <th class="num">Было</th>
                    <th class="num">Стало</th>
                    <th class="num">Изменение</th>
                    <th>Кто</th>
                    <th>Комментарий</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of priceHistory(); track r.id) {
                    <tr>
                      <td data-label="Дата">{{ r.createdAt | date:'dd.MM.yyyy HH:mm' }}</td>
                      <td data-label="Наименование">{{ r.name }}</td>
                      <td data-label="Операция">
                        <span class="badge" [class.markdown]="r.delta < 0">{{ r.operation }}</span>
                      </td>
                      <td data-label="Было" class="num">{{ r.oldCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Стало" class="num">{{ r.newCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Изменение" class="num" [class.negative]="r.delta < 0">
                        {{ r.delta > 0 ? '+' : '' }}{{ r.delta | currency:'RUB':'symbol-narrow':'1.0-2' }}
                      </td>
                      <td data-label="Кто" class="muted">{{ r.user }}</td>
                      <td data-label="Комментарий" class="muted">{{ r.comment }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="8" class="empty">Переоценок не зафиксировано</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <!-- История по детали -->
          @if (kind() === 'zip-history') {
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Дата</th>
                    <th>Операция</th>
                    <th class="num">Кол-во</th>
                    <th class="num">Себестоимость</th>
                    <th class="num">Цена продажи</th>
                    <th>Кто</th>
                    <th>Описание</th>
                  </tr>
                </thead>
                <tbody>
                  @for (r of zipHistory(); track r.id) {
                    <tr>
                      <td data-label="Дата">{{ r.createdAt | date:'dd.MM.yyyy HH:mm' }}</td>
                      <td data-label="Операция"><span class="badge">{{ r.operation }}</span></td>
                      <td data-label="Кол-во" class="num" [class.negative]="(r.qty ?? 0) < 0">
                        {{ r.qty !== null && r.qty !== undefined ? ((r.qty > 0 ? '+' : '') + r.qty) : '—' }}
                      </td>
                      <td data-label="Себестоимость" class="num">{{ r.unitCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Цена продажи" class="num">{{ r.sellCost | currency:'RUB':'symbol-narrow':'1.0-2' }}</td>
                      <td data-label="Кто" class="muted">{{ r.user }}</td>
                      <td data-label="Описание" class="muted">{{ r.description }}</td>
                    </tr>
                  } @empty {
                    <tr><td colspan="7" class="empty">Движений по этой детали нет</td></tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .reports-container { padding: 20px 0 40px; }

    .report-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }
    .report-tabs button {
      padding: 8px 16px;
      border: 1px solid var(--border, #ccc);
      border-radius: 20px;
      background: #fff;
      cursor: pointer;
      font-weight: 500;
      transition: 0.2s;
    }
    .report-tabs button.active {
      background: var(--accent, #007bff);
      border-color: var(--accent, #007bff);
      color: #fff;
    }

    .card {
      background: #fff;
      padding: 20px;
      border-radius: 8px;
      border: 1px solid var(--border, #eee);
      margin-bottom: 20px;
      box-shadow: 0 2px 4px rgba(0,0,0,0.05);
    }

    .filters { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; }
    .filter-field { display: flex; flex-direction: column; gap: 4px; }
    .filter-field.grow { flex: 1 1 260px; }
    .filter-field label { font-size: 13px; font-weight: 500; color: #555; }
    .req { color: red; }

    .form-control {
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 14px;
      box-sizing: border-box;
      width: 100%;
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

    .hint { display: block; margin-top: 4px; color: #777; font-size: 12px; }

    .summary {
      display: flex;
      flex-wrap: wrap;
      gap: 24px;
      padding-bottom: 14px;
      margin-bottom: 14px;
      border-bottom: 1px solid #eee;
      font-size: 15px;
    }

    .table-container { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 14px; }
    .data-table th, .data-table td { padding: 10px 12px; border: 1px solid #eee; text-align: left; }
    .data-table th { background: #f8f9fa; font-weight: 600; color: #333; white-space: nowrap; }
    .data-table .num { text-align: right; white-space: nowrap; }
    .data-table tbody tr:hover { background: #fafafa; }

    .badge {
      background: #e0f7fa;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 13px;
      white-space: nowrap;
    }
    .badge.markdown { background: #ffe0e0; }

    .negative { color: #dc3545; }
    .muted { color: #777; }
    .empty { text-align: center; padding: 20px; color: #777; }

    .error {
      color: #721c24;
      background: #f8d7da;
      border: 1px solid #f5c6cb;
      padding: 10px;
      margin-bottom: 15px;
      border-radius: 4px;
    }
    .close-inline { float: right; cursor: pointer; background: none; border: none; }

    /* На узких экранах отчётные таблицы шире экрана. Строку разворачиваем
       в карточку — подпись колонки берётся из data-label у ячейки. */
    @media (max-width: 768px) {
      .reports-container { padding: 12px 0 32px; }
      .card { padding: 14px; }

      .report-tabs { flex-wrap: nowrap; overflow-x: auto; padding-bottom: 4px; }
      .report-tabs button { flex: 0 0 auto; font-size: 14px; padding: 7px 13px; }

      .filters { flex-direction: column; align-items: stretch; }
      .filter-field.grow { flex: 1 1 auto; }
      .btn { width: 100%; }

      .summary { flex-direction: column; gap: 6px; }

      .table-container { overflow-x: visible; }
      .data-table, .data-table tbody, .data-table tr, .data-table td { display: block; width: 100%; }
      .data-table thead { display: none; }

      .data-table tr {
        border: 1px solid #eee;
        border-radius: 8px;
        margin-bottom: 12px;
        padding: 4px 0;
      }

      .data-table td {
        border: none;
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 12px;
        padding: 7px 12px;
        text-align: right;
        overflow-wrap: anywhere;
      }
      .data-table td::before {
        content: attr(data-label);
        font-weight: 600;
        color: #6b7280;
        font-size: 12px;
        text-align: left;
        flex-shrink: 0;
      }
      .data-table .num { text-align: right; }

      .data-table td.empty { justify-content: center; }
      .data-table td.empty::before { content: none; }
    }
  `]
})
export class ReportsComponent {
  private admin = inject(AdminService);

  kind = signal<ReportKind>('sales');
  busy = signal(false);
  loaded = signal(false);
  error = signal('');

  from = '';
  to = '';

  /**
   * Фильтры отчётов — сигналы, потому что связаны с полем подсказок через model():
   * двустороннее связывание сигнальной модели с обычным полем компонента невозможно.
   */
  partNumFilter = signal('');
  nameFilter = signal('');
  donorFilter = signal('');

  /** Список деталей — общий источник подсказок для всех трёх полей на странице. */
  zips = signal<ZipLookupItem[]>([]);

  /**
   * Выбранная деталь и текст в поле — раздельно: отчёт строится по id, а в поле лежит
   * произвольный ввод. Как только текст правят, id сбрасывается — иначе отчёт молча
   * строился бы по прежней детали, не совпадающей с тем, что видно в поле.
   */
  zipId = signal('');
  zipQuery = signal('');

  zipOptions = computed<AutocompleteOption[]>(() =>
    this.zips().map(z => ({ value: this.zipLabel(z), label: z.name, hint: z.partNum })));

  /**
   * Подсказки для фильтров берём из того же справочника деталей, но без дублей:
   * один парт-номер и одно наименование встречаются у многих Zip (деталь с разных доноров),
   * и в списке они иначе повторялись бы десятками одинаковых строк.
   */
  partNumOptions = computed<AutocompleteOption[]>(() =>
    [...new Set(this.zips().map(z => z.partNum).filter((p): p is string => !!p))]
      .sort((a, b) => a.localeCompare(b))
      .map(p => ({ value: p, label: p })));

  nameOptions = computed<AutocompleteOption[]>(() =>
    [...new Set(this.zips().map(z => z.name).filter(n => !!n))]
      .sort((a, b) => a.localeCompare(b))
      .map(n => ({ value: n, label: n })));

  donorOptions = computed<AutocompleteOption[]>(() =>
    [...new Set(this.zips().map(z => z.incomeMoto).filter((d): d is string => !!d))]
      .sort((a, b) => a.localeCompare(b))
      .map(d => ({ value: d, label: d })));

  stock = signal<StockReportRow[]>([]);
  sales = signal<SalesReportRow[]>([]);
  income = signal<IncomeReportRow[]>([]);
  priceHistory = signal<PriceHistoryRow[]>([]);
  zipHistory = signal<ZipHistoryRow[]>([]);

  totalSold = computed(() => this.sales().reduce((s, r) => s + r.sold, 0));
  totalRevenue = computed(() => this.sales().reduce((s, r) => s + r.revenue, 0));
  totalMargin = computed(() => this.sales().reduce((s, r) => s + r.margin, 0));
  totalQty = computed(() => this.income().reduce((s, r) => s + r.qty, 0));
  totalIncome = computed(() => this.income().reduce((s, r) => s + r.total, 0));
  totalStockCount = computed(() => this.stock().reduce((s, r) => s + r.count, 0));
  totalStockCost = computed(() => this.stock().reduce((s, r) => s + r.total, 0));

  constructor() {
    this.admin.zipLookup().subscribe({
      next: (data) => this.zips.set(Array.isArray(data) ? data : []),
      error: () => this.error.set('Не удалось загрузить список запчастей')
    });
  }

  select(kind: ReportKind) {
    this.kind.set(kind);
    this.loaded.set(false);
    this.error.set('');
  }

  // ---------- Выбор детали ----------

  onZipQuery(value: string) {
    this.zipQuery.set(value);
    this.zipId.set('');
  }

  /** Подпись в поле проставляет само поле подсказок — здесь запоминаем только id детали. */
  onZipPicked(option: AutocompleteOption) {
    this.zipId.set(this.zips().find(z => this.zipLabel(z) === option.value)?.id ?? '');
  }

  private zipLabel(z: ZipLookupItem): string {
    return z.partNum ? `${z.name} (${z.partNum})` : z.name;
  }

  run() {
    this.error.set('');

    if (this.kind() === 'zip-history' && !this.zipId()) {
      this.error.set('Выберите запчасть — этот отчёт строится по конкретной детали.');
      return;
    }

    // Набранный, но не выбранный текст — не фильтр: отчёт строится по id детали.
    // Пустое поле в «Наценке / уценке» законно и означает «по всем деталям».
    if (this.kind() === 'price-history' && this.zipQuery().trim() && !this.zipId()) {
      this.error.set('Выберите деталь из подсказок или очистите поле, чтобы получить историю по всем деталям.');
      return;
    }

    this.busy.set(true);

    // Даты уходят как есть: включение последнего дня в период делает бэкенд.
    const filters = {
      from: this.from || undefined,
      to: this.to || undefined,
      partNum: this.partNumFilter() || undefined,
      name: this.nameFilter() || undefined,
      donor: this.donorFilter() || undefined,
    };

    const done = () => { this.busy.set(false); this.loaded.set(true); };
    const fail = (err: any) => {
      this.busy.set(false);
      this.error.set('Не удалось построить отчёт: ' + (err.error?.message || err.message));
    };

    switch (this.kind()) {
      case 'stock':
        this.admin.stockReport(filters.donor).subscribe({
          next: r => { this.stock.set(r); done(); }, error: fail
        });
        break;
      case 'sales':
        this.admin.salesReport(filters).subscribe({
          next: r => { this.sales.set(r); done(); }, error: fail
        });
        break;
      case 'income':
        this.admin.incomeReport(filters).subscribe({
          next: r => { this.income.set(r); done(); }, error: fail
        });
        break;
      case 'price-history':
        this.admin.priceHistoryReport(this.zipId() || undefined).subscribe({
          next: r => { this.priceHistory.set(r); done(); }, error: fail
        });
        break;
      case 'zip-history':
        this.admin.zipHistory(this.zipId()).subscribe({
          next: r => { this.zipHistory.set(r); done(); }, error: fail
        });
        break;
    }
  }

}
