import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { JsonPipe } from '@angular/common';
import { AdminService } from '../core/admin.service';

interface FieldDef {
  key: string;
  label: string;
  // Добавлен тип 'select' для выпадающих списков
  type: 'text' | 'number' | 'checkbox' | 'select';
  required?: boolean;
  // Настройки для связи внешних ключей
  refTable?: string;     // Эндпоинт таблицы-справочника (например, 'marks')
  refLabelKey?: string;  // Поле, которое нужно показывать (например, 'mark' или 'name')
}

interface TableDef {
  endpoint: string;
  title: string;
  fields: FieldDef[];
}

@Component({
  selector: 'app-admin',
  imports: [FormsModule, JsonPipe],
  template: `
    <div class="admin-container">
      <h2>Админ-панель</h2>
      
      <div class="tabs">
        @for (t of tables; track t.endpoint) {
          <button [class.active]="t === current()" (click)="select(t)">{{ t.title }}</button>
        }
      </div>

      @if (current(); as table) {
        <div class="add-form card">
          <h3>{{ selectedId() ? 'Редактировать запись (ID: ' + selectedId() + ')' : 'Добавить новую запись' }}</h3>
          
          <div class="form-grid">
            @for (f of table.fields; track f.key) {
              <div class="form-group" style="position: relative;">
                <label>{{ f.label }}</label>
                
                @if (f.type === 'checkbox') {
                  <input type="checkbox" [(ngModel)]="form[f.key]">
                } 
                @else if (f.type === 'number') {
                  <input type="number" [(ngModel)]="form[f.key]" class="form-control">
                } 
                @else if (f.type === 'select') {
                  <select [(ngModel)]="form[f.key]" class="form-control">
                    <option [ngValue]="undefined" [selected]="!form[f.key]">— Выберите —</option>
                    @for (opt of references()[f.refTable!] || []; track opt.id) {
                      <option [ngValue]="opt.id">{{ opt[f.refLabelKey!] }}</option>
                    }
                  </select>
                }
                @else {
                  <input type="text" 
                         [ngModel]="form[f.key]" 
                         (ngModelChange)="onFieldInput(f.key, $event)"
                         (blur)="hideSuggestions()"
                         autocomplete="off"
                         class="form-control">
                         
                  @if (activeField() === f.key && fieldSuggestions().length > 0) {
                    <ul class="suggestions-dropdown">
                      @for (s of fieldSuggestions(); track s) {
                        <li (mousedown)="$event.preventDefault()" (click)="selectSuggestion(f.key, s)">
                          {{ s }}
                        </li>
                      }
                    </ul>
                  }
                }
              </div>
            }
          </div>
          
          <div class="actions">
            <button class="btn btn-primary" (click)="save(table)" [disabled]="busy()">
              {{ selectedId() ? 'Обновить' : 'Добавить' }}
            </button>
            @if (selectedId() && isAdmin()) {
              <button class="btn btn-danger" (click)="deleteRow(table, selectedId())" [disabled]="busy()" style="background: #dc3545; color: #fff; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer;">
                Удалить
              </button>
            }
            @if (selectedId()) {
              <button class="btn btn-secondary" (click)="cancelEdit()">Отмена</button>
            }
          </div>
          
          @if (error()) { <p class="error">{{ error() }}</p> }
          @if (message()) { <p class="success">{{ message() }}</p> }
        </div>

        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                @for (col of columns(); track col) {
                  <th>{{ getColLabel(col) }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row['id']) {
                <tr (click)="editRow(row)" [class.active-row]="row['id'] === selectedId()">
                  @for (col of columns(); track col) {
                    <td>{{ getDisplayValue(col, row[col]) }}</td>
                  }
                </tr>
              }
              @if (rows().length === 0) {
                <tr><td [colSpan]="columns().length">Нет данных</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .admin-container { padding-bottom: 40px; }
    .tabs { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 20px; } 
    .tabs button { padding: 8px 16px; border: 1px solid var(--border); border-radius: 20px; background: #fff; cursor: pointer; transition: 0.2s; } 
    .tabs button.active { background: var(--accent); border-color: var(--accent); color: #fff; } 
    .card { background: #fff; padding: 20px; border-radius: 8px; border: 1px solid var(--border); margin-bottom: 20px; }
    
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; align-items: end; margin-bottom: 15px; } 
    .form-group label { display: block; margin-bottom: 6px; font-weight: 500; font-size: 13px; color: #555; }
    .form-control { width: 100%; padding: 8px 12px; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; }
    
    .error { color: #dc3545; margin-top: 10px; font-weight: 500; }
    .success { color: #28a745; margin-top: 10px; font-weight: 500; }
    .actions { display: flex; gap: 10px; margin-top: 10px; }
    
    .table-container { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; background: #fff; }
    .data-table th, .data-table td { padding: 12px; border: 1px solid #eee; text-align: left; }
    .data-table th { background: #f8f9fa; font-weight: 600; }
    .data-table tr { cursor: pointer; transition: background 0.15s; }
    .data-table tr:hover { background: #f1f1f1; }
    .data-table tr.active-row { background: #e3f2fd; border-left: 3px solid var(--accent); }
    
    .suggestions-dropdown {
      position: absolute; top: 100%; left: 0; right: 0; background: white;
      border: 1px solid #ccc; border-radius: 4px; list-style: none; padding: 0;
      margin: 4px 0 0 0; z-index: 1000; box-shadow: 0 4px 12px rgba(0,0,0,0.15);
      max-height: 200px; overflow-y: auto;
    }
    .suggestions-dropdown li { padding: 8px 12px; cursor: pointer; border-bottom: 1px solid #f0f0f0; font-size: 14px; }
    .suggestions-dropdown li:hover { background: #f8f9fa; }
  `]
})
export class AdminComponent implements OnInit {
  private admin = inject(AdminService);
  isAdmin = signal<boolean>(true);
  // Таблицы с настройками связей (type: 'select', refTable, refLabelKey)
  tables: TableDef[] = [
    {
      endpoint: 'marks', title: 'Марки (MotoMarks)', fields: [
        { key: 'mark', label: 'Марка (Название)', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'models', title: 'Модели (MotoModels)', fields: [
        { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark', required: true },
        { key: 'model', label: 'Модель (Название)', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'groups', title: 'Группы (ZipGroups)', fields: [
        { key: 'groupName', label: 'Название группы', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'partnumbers', title: 'Парт-номера (PartNumbers)', fields: [
        { key: 'partNumber', label: 'Парт-номер', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'zip', title: 'Запчасти (Zip)', fields: [
        { key: 'name', label: 'Название запчасти', type: 'text', required: true },
        { key: 'cost', label: 'Стоимость', type: 'number', required: true },
        { key: 'countStored', label: 'Кол-во на складе', type: 'number', required: true },
        { key: 'partNumberId', label: 'Парт-номер', type: 'select', refTable: 'partnumbers', refLabelKey: 'partNumber' },
        { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark' },
        { key: 'modelId', label: 'Модель', type: 'select', refTable: 'models', refLabelKey: 'model' },
        { key: 'groupId', label: 'Группа', type: 'select', refTable: 'groups', refLabelKey: 'groupName' },
        { key: 'year', label: 'Год (числом)', type: 'number' },
        { key: 'incomeMotoId', label: 'Мотоцикл-донор', type: 'select', refTable: 'incomemotos', refLabelKey: 'description', required: true }
      ]
    },
    {
      endpoint: 'users', title: 'Пользователи (Users)', fields: [
        { key: 'email', label: 'Email', type: 'text', required: true },
        { key: 'fio', label: 'ФИО', type: 'text', required: true },
        { key: 'phoneNumber', label: 'Номер телефона', type: 'text' },
        { key: 'isAdmin', label: 'Администратор', type: 'checkbox' },
        { key: 'isRegistrar', label: 'Регистратор', type: 'checkbox' },
        { key: 'isSender', label: 'Отправитель', type: 'checkbox' }
      ]
    },
    {
      endpoint: 'addresses', title: 'Адреса (DeliveryAdressess)', fields: [
        { key: 'address', label: 'Адрес', type: 'text', required: true },
        { key: 'postCode', label: 'Индекс', type: 'text' },
        { key: 'userId', label: 'Пользователь (Email)', type: 'select', refTable: 'users', refLabelKey: 'email' }
      ]
    },
    {
      endpoint: 'orders', title: 'Заказы (Orders)', fields: [
        { key: 'orderNumber', label: 'Номер заказа', type: 'text', required: true },
        { key: 'countOrdered', label: 'Количество', type: 'number', required: true },
        { key: 'nomenclatureId', label: 'Запчасть', type: 'select', refTable: 'zip', refLabelKey: 'name' },
        { key: 'addressId', label: 'Адрес доставки', type: 'select', refTable: 'addresses', refLabelKey: 'address', required: true }
      ]
    },
    {
      endpoint: 'incomemotos', title: 'Мото-доноры (IncomeMoto)', fields: [
        { key: 'description', label: 'Описание (Description)', type: 'text', required: true }
      ]
    }
  ];

  current = signal<TableDef | null>(null);
  rows = signal<Record<string, any>[]>([]);
  columns = signal<string[]>([]);
  form: Record<string, any> = {};
  error = signal('');
  message = signal('');
  busy = signal(false);

  // Для редактирования
  selectedId = signal<number | string | null>(null);
  activeField = signal<string | null>(null);
  fieldSuggestions = signal<string[]>([]);

  // Хранилище справочников для подстановки имен вместо ID
  references = signal<Record<string, any[]>>({});

  isUserRoleMode = signal(false);
  userSearchQuery = signal('');
  foundUsers = signal<any[]>([]);

  deleteRow(table: TableDef, id: any, $event?: Event) {
    if ($event) {
      $event.stopPropagation(); // Предотвращаем клик по всей строке таблицы
    }

    if (!this.isAdmin()) {
      this.error.set('У вас нет прав для удаления записей');
      return;
    }

    if (!confirm('Вы уверены, что хотите удалить эту запись?')) {
      return;
    }

    this.busy.set(true);
    this.error.set('');
    this.message.set('');

    this.admin.delete(table.endpoint, id).subscribe({
      next: () => {
        this.message.set('Запись успешно удалена');
        this.busy.set(false);
        if (this.selectedId() === id) {
          this.cancelEdit();
        }
        this.reload(table);
      },
      error: (err) => {
        this.error.set('Ошибка при удалении: ' + (err.error?.message || err.message));
        this.busy.set(false);
      }
    });
  }

  // Метод включения режима управления ролями
  enableUserRoleMode() {
    this.current.set(null); // Скрываем стандартные таблицы из текущего функционала
    this.isUserRoleMode.set(true);
    this.foundUsers.set([]);
    this.userSearchQuery.set('');
  }

  // Обработка ввода в поиск
  onSearchUsers(query: string) {
    this.userSearchQuery.set(query);
    if (query.length < 2) {
      this.foundUsers.set([]);
      return;
    }
    this.admin.searchUsers(query).subscribe(users => {
      this.foundUsers.set(users);
    });
  }

  // Сохранение ролей
  saveUserRoles(user: any) {
    this.busy.set(true);
    this.admin.updateUserRoles(user.id, user.isSender, user.isRegistrar).subscribe({
      next: () => {
        this.busy.set(false);
        this.message.set(`Права для ${user.email} успешно обновлены!`);
        setTimeout(() => this.message.set(''), 3000);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(err.error?.message || 'Ошибка при обновлении прав');
      }
    });
  }

  ngOnInit(): void {
    // При запуске загружаем все основные таблицы, чтобы резолвить ID-шники
    const endpointsToLoad = ['marks', 'models', 'groups', 'partnumbers', 'zip', 'users', 'addresses', 'incomemotos'];
    endpointsToLoad.forEach(ep => {
      this.admin.list(ep).subscribe(data => {
        this.references.update(r => ({ ...r, [ep]: data }));
      });
    });

    this.select(this.tables[0]);
  }

  select(table: TableDef): void {
    // this.current.set(table);
    // this.cancelEdit();
    // this.reload(table);
    this.isUserRoleMode.set(false); // <--- Добавьте эту строку
    this.current.set(table);
    this.form = {};
    this.error.set('');
    this.message.set('');
    this.reload(table);
  }

  reload(table: TableDef): void {
    this.admin.list(table.endpoint).subscribe({
      next: (rows: any[]) => {
        this.rows.set(rows);
        this.columns.set(rows.length ? Object.keys(rows[0]) : []);
      },
      error: (err) => this.error.set('Ошибка загрузки данных: ' + err.message)
    });
  }

  // === ЛОГИКА ОТОБРАЖЕНИЯ ЧЕЛОВЕКОЧИТАЕМЫХ ДАННЫХ ===
  getColLabel(col: string): string {
    if (col === 'id') return 'ID';
    const field = this.current()?.fields.find(f => f.key === col);
    return field ? field.label : col;
  }

  getDisplayValue(col: string, val: any): any {
    if (val == null) return '';
    const field = this.current()?.fields.find(f => f.key === col);
    // Если это поле со связью (select), заменяем ID на имя из загруженного справочника
    if (field?.type === 'select' && field.refTable) {
      const refData = this.references()[field.refTable];
      if (refData) {
        // Используем нестрогое равенство == на случай если ID пришел строкой (UUID), а в базе Guid
        const item = refData.find((x: any) => x.id == val);
        if (item) return item[field.refLabelKey!];
      }
    }
    // Красивый вывод для булевых значений (isAdmin)
    if (typeof val === 'boolean') return val ? 'Да' : 'Нет';
    return val;
  }

  // === ЛОГИКА РЕДАКТИРОВАНИЯ ===
  editRow(row: Record<string, any>) {
    this.selectedId.set(row['id']);
    this.form = { ...row };
    this.error.set('');
    this.message.set('');
  }

  cancelEdit() {
    this.selectedId.set(null);
    this.form = {};
    this.error.set('');
    this.message.set('');
  }

  save(table: TableDef) {
    this.busy.set(true);
    this.error.set('');
    this.message.set('');

    const id = this.selectedId();

    if (id) {
      // Обновление (вызывает созданный вами PUT метод)
      this.admin.update(table.endpoint, id, this.form).subscribe({
        next: () => {
          this.message.set('Запись успешно обновлена!');
          this.busy.set(false);
          this.cancelEdit();
          this.reload(table);
        },
        error: (err) => {
          this.error.set('Ошибка при обновлении: ' + (err.error?.message || err.message));
          this.busy.set(false);
        }
      });
    } else {
      // Добавление
      this.admin.add(table.endpoint, this.form).subscribe({
        next: () => {
          this.message.set('Запись успешно добавлена!');
          this.busy.set(false);
          this.cancelEdit();
          this.reload(table);
        },
        error: (err) => {
          this.error.set('Ошибка при добавлении: ' + (err.error?.message || err.message));
          this.busy.set(false);
        }
      });
    }
  }

  // === ЛОГИКА АВТОПОДСКАЗОК ПО ЯЧЕЙКАМ (Для текста) ===
  onFieldInput(key: string, value: string) {
    this.form[key] = value;

    if (value && value.trim().length > 0) {
      this.activeField.set(key);
      const allValues = this.rows()
        .map(row => row[key])
        .filter(val => typeof val === 'string' && val.toLowerCase().includes(value.toLowerCase()));

      const uniqueValues = [...new Set(allValues)].slice(0, 8);
      this.fieldSuggestions.set(uniqueValues);
    } else {
      this.activeField.set(null);
      this.fieldSuggestions.set([]);
    }
  }

  selectSuggestion(key: string, suggestion: string) {
    this.form[key] = suggestion;
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
  }

  hideSuggestions() {
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
  }
}