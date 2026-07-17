import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { JsonPipe } from '@angular/common';
import { AdminService } from '../core/admin.service';

interface FieldDef {
  key: string;
  label: string;
  type: 'text' | 'number' | 'checkbox';
  required?: boolean;
}

interface TableDef {
  endpoint: string;
  title: string;
  fields: FieldDef[];
}

/** Админ-панель: ручное добавление записей в каждую таблицу базы данных. */
@Component({
    selector: 'app-admin',
    imports: [FormsModule],
    template: `
    <h1>Админ-панель</h1>

    <div class="tabs">
      @for (t of tables; track t.endpoint) {
        <button [class.active]="t === current()" (click)="select(t)">{{ t.title }}</button>
      }
    </div>

    @if (current(); as table) {
      <section class="card">
        <h2>Добавить запись — {{ table.title }}</h2>
        <form class="add-form" (ngSubmit)="add(table)">
          @for (f of table.fields; track f.key) {
            <div class="form-field">
              <label>{{ f.label }}@if (f.required) { <span class="error">*</span> }</label>
              @if (f.type === 'checkbox') {
                <input type="checkbox" [(ngModel)]="form[f.key]" [name]="f.key" style="width:auto" />
              } @else {
                <input [type]="f.type" [(ngModel)]="form[f.key]" [name]="f.key" [required]="f.required ?? false" />
              }
            </div>
          }
          @if (error()) { <p class="error">{{ error() }}</p> }
          @if (message()) { <p class="success">{{ message() }}</p> }
          <button class="btn" type="submit" [disabled]="busy()">Добавить</button>
        </form>
      </section>

      <section class="card" style="margin-top:20px">
        <h2>Содержимое таблицы</h2>
        @if (rows().length === 0) {
          <p class="muted">Таблица пуста.</p>
        } @else {
          <div style="overflow-x:auto">
            <table class="data">
              <thead>
                <tr>
                  @for (col of columns(); track col) { <th>{{ col }}</th> }
                </tr>
              </thead>
              <tbody>
                @for (row of rows(); track $index) {
                  <tr>
                    @for (col of columns(); track col) { <td>{{ format(row[col]) }}</td> }
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
    }
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    .tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; }
    .tabs button {
      padding: 8px 16px;
      border: 1px solid var(--border);
      border-radius: 20px;
      background: #fff;
      cursor: pointer;
    }
    .tabs button.active { background: var(--accent); border-color: var(--accent); color: #fff; }
    .add-form {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 12px;
      align-items: end;
    }
    .add-form .error, .add-form .success, .add-form .btn { grid-column: 1 / -1; }
    .add-form .btn { justify-self: start; }
  `]
})
export class AdminComponent implements OnInit {
  private admin = inject(AdminService);

  // Описание всех таблиц базы и полей формы добавления
  tables: TableDef[] = [
    { endpoint: 'marks', title: 'Марки (MotoMarks)', fields: [
      { key: 'mark', label: 'Марка', type: 'text', required: true },
    ]},
    { endpoint: 'models', title: 'Модели (MotoModels)', fields: [
      { key: 'markId', label: 'ID марки', type: 'number', required: true },
      { key: 'model', label: 'Модель', type: 'text', required: true },
    ]},
    { endpoint: 'groups', title: 'Группы ZIP (ZipGroups)', fields: [
      { key: 'groupName', label: 'Название группы', type: 'text', required: true },
    ]},
    { endpoint: 'partnumbers', title: 'Парт-номера (PartNumbers)', fields: [
      { key: 'partNumber', label: 'Парт-номер', type: 'text', required: true },
    ]},
    { endpoint: 'zip', title: 'Запчасти (Zip)', fields: [
      { key: 'name', label: 'Название', type: 'text', required: true },
      { key: 'cost', label: 'Стоимость, ₽', type: 'number', required: true },
      { key: 'countStored', label: 'Кол-во на складе', type: 'number', required: true },
      { key: 'partNumberId', label: 'ID парт-номера', type: 'number' },
      { key: 'markId', label: 'ID марки', type: 'number' },
      { key: 'modelId', label: 'ID модели', type: 'number' },
      { key: 'groupId', label: 'ID группы', type: 'number' },
      { key: 'year', label: 'Год выпуска', type: 'number' },
    ]},
    { endpoint: 'users', title: 'Пользователи (Users)', fields: [
      { key: 'email', label: 'Email', type: 'text', required: true },
      { key: 'fio', label: 'ФИО', type: 'text', required: true },
      { key: 'phoneNumber', label: 'Телефон', type: 'text' },
      { key: 'password', label: 'Пароль', type: 'text' },
      { key: 'isAdmin', label: 'Администратор', type: 'checkbox' },
    ]},
    { endpoint: 'addresses', title: 'Адреса (DeliveryAdressess)', fields: [
      { key: 'address', label: 'Адрес', type: 'text', required: true },
      { key: 'postCode', label: 'Индекс', type: 'text' },
      { key: 'userId', label: 'ID пользователя', type: 'number' },
    ]},
    { endpoint: 'orders', title: 'Заказы (Orders)', fields: [
      { key: 'orderNumber', label: 'Номер заказа', type: 'text', required: true },
      { key: 'countOrdered', label: 'Количество', type: 'number', required: true },
      { key: 'nomenclatureId', label: 'ID запчасти (uuid)', type: 'text' },
      { key: 'addressId', label: 'ID адреса', type: 'number', required: true },
    ]},
  ];

  current = signal<TableDef | null>(null);
  rows = signal<Record<string, unknown>[]>([]);
  columns = signal<string[]>([]);
  form: Record<string, unknown> = {};
  error = signal('');
  message = signal('');
  busy = signal(false);

  ngOnInit(): void {
    this.select(this.tables[0]);
  }

  select(table: TableDef): void {
    this.current.set(table);
    this.form = {};
    this.error.set('');
    this.message.set('');
    this.reload(table);
  }

  reload(table: TableDef): void {
    this.admin.list(table.endpoint).subscribe(rows => {
      this.rows.set(rows);
      this.columns.set(rows.length ? Object.keys(rows[0]) : []);
    });
  }

  add(table: TableDef): void {
    this.error.set('');
    this.message.set('');
    this.busy.set(true);

    // Пустые строки не отправляем, числовые поля приводим к числу
    const payload: Record<string, unknown> = {};
    for (const f of table.fields) {
      const value = this.form[f.key];
      if (value === undefined || value === null || value === '') continue;
      payload[f.key] = f.type === 'number' ? Number(value) : value;
    }

    this.admin.add(table.endpoint, payload).subscribe({
      next: () => {
        this.busy.set(false);
        this.message.set('Запись добавлена');
        this.form = {};
        this.reload(table);
      },
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message ?? 'Не удалось добавить запись');
      },
    });
  }

  format(value: unknown): string {
    if (value === null || value === undefined) return '—';
    if (typeof value === 'boolean') return value ? 'да' : 'нет';
    return String(value);
  }
}
