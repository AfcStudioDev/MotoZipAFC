import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule, JsonPipe } from '@angular/common';
import { AdminService } from '../core/admin.service';

interface FieldDef {
  key: string;
  label: string;
  type: 'text' | 'number' | 'checkbox' | 'select' | 'date';
  required?: boolean;
  refTable?: string;
  refLabelKey?: string;
}

interface TableDef {
  endpoint: string;
  title: string;
  fields: FieldDef[];
}

// Решает проблему TS4111 (noPropertyAccessFromIndexSignature)
interface DynamicRow {
  id: any;
  [key: string]: any;
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [FormsModule, CommonModule, JsonPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
    styleUrls: ['../styles/admin.component.css'],
  template: `
    <div class="admin-container">
      <h2>Панель администратора</h2>

      @if (message()) {
        <div class="success">
          {{ message() }}
          <button style="float: right; cursor: pointer; background: none; border: none;" (click)="message.set('')">✖</button>
        </div>
      }
      @if (error()) {
        <div class="error">
          {{ error() }}
          <button style="float: right; cursor: pointer; background: none; border: none;" (click)="error.set('')">✖</button>
        </div>
      }

      <div class="tabs">
        @for (t of tables; track t.endpoint) {
          <button 
            [class.active]="current()?.endpoint === t.endpoint" 
            (click)="select(t)"
          >
            {{ t.title }}
          </button>
        }
      </div>

      @if (current(); as table) {
        
        <!-- КАРТОЧКА ФОРМЫ (Оригинальный дизайн) -->
        <div class="card">
          <h3 style="margin-top: 0;">{{ selectedId() ? 'Редактировать запись' : 'Добавить запись' }}</h3>
          
          <form (ngSubmit)="save(table)">
            <div class="form-grid">
              @for (f of table.fields; track f.key) {
                <div class="form-group" style="position: relative;">
                  <label>
                    {{ f.label }}
                    @if (f.required) { <span style="color: red;">*</span> }
                  </label>

                  @if (f.type === 'checkbox') {
                    <input 
                      type="checkbox" 
                      [(ngModel)]="form[f.key]" 
                      [name]="f.key"
                      style="width: 20px; height: 20px; margin-top: 5px;"
                    >
                  } 
                  @else if (f.type === 'number') {
                    <input 
                      type="number" 
                      step="any"
                      class="form-control" 
                      [(ngModel)]="form[f.key]" 
                      [name]="f.key"
                      [required]="!!f.required"
                    >
                  } 
                  @else if (f.type === 'select') {
                    <!-- Новый функционал: Выпадающий список для связей -->
                    <select 
                      class="form-control" 
                      [(ngModel)]="form[f.key]" 
                      [name]="f.key"
                      [required]="!!f.required"
                    >
                      <option [ngValue]="null">— Выберите —</option>
                      @for (opt of references()[f.refTable!] || []; track opt.id) {
                        <option [ngValue]="opt.id">
                          {{ opt[f.refLabelKey!] || opt.name || opt.id }}
                        </option>
                      }
                    </select>
                  }
                  @else {
                    <input
                      type="text"
                      class="form-control"
                      [(ngModel)]="form[f.key]"
                      [name]="f.key"
                      (input)="onFieldInput(f.key, form[f.key])"
                      (focus)="onFieldInput(f.key, form[f.key])"
                      [required]="!!f.required"
                      autocomplete="off"
                    >
                    <!-- Выпадающие подсказки -->
                    @if (activeField() === f.key && fieldSuggestions().length > 0) {
                      <ul class="suggestions-dropdown">
                        @for (sug of fieldSuggestions(); track sug) {
                          <li (click)="applySuggestion(f.key, sug)">{{ sug }}</li>
                        }
                      </ul>
                    }
                  }
                </div>
              }
                @if (table.endpoint === 'zip') {
                  <div style="margin-top: 15px;">
                    <label>Фотографии (Максимум 3):</label>
                    <input type="file" multiple accept="image/jpeg, image/png" (change)="onFileSelected($event)" [disabled]="selectedFiles().length >= 3">

                    @if (selectedFiles().length > 0) {
                      <ul style="list-style: none; padding-left: 0; margin-top: 10px;">
                        @for (file of selectedFiles(); track file.name; let i = $index) {
                          <li style="display: flex; align-items: center; margin-bottom: 5px;">
                            <span>{{ file.name }}</span>
                            <button type="button" (click)="removeFile(i)" style="margin-left: 10px; color: red;">Удалить</button>
                          </li>
                        }
                      </ul>
                    }

                    @if (selectedFiles().length >= 3) {
                      <small style="color: orange;">Достигнут лимит в 3 фотографии.</small>
                    }
                  </div>
                }
            </div>

            <div class="actions">
              <button type="submit" [disabled]="busy()" style="padding: 8px 16px; cursor: pointer;">
                {{ selectedId() ? 'Обновить' : 'Добавить' }}
              </button>
              @if (selectedId()) {
                <button type="button" (click)="cancelEdit()" style="padding: 8px 16px; cursor: pointer;">
                  Отмена
                </button>
              }
            </div>
          </form>
        </div>

        <!-- КАРТОЧКА ТАБЛИЦЫ (Оригинальный дизайн) -->
        <div class="card">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;">
            <h3 style="margin: 0;">{{ table.title }} (Всего: {{ rows().length }})</h3>
            <button (click)="reload(table)" [disabled]="busy()" style="padding: 6px 12px; cursor: pointer;">
              Обновить таблицу
            </button>
          </div>
          
          <div class="table-container">
            <table class="data-table">
              <thead>
                <tr>
                  <th style="width: 80px;">ID</th>
                  @for (f of table.fields; track f.key) {
                    <th>{{ f.label }}</th>
                  }
                  <th style="text-align: right; width: 100px;">Действия</th>
                </tr>
              </thead>
              <tbody>
                @for (r of rows(); track r.id) {
                  <!-- Использование r.id теперь работает благодаря интерфейсу DynamicRow -->
                  <tr [class.active-row]="selectedId() === r.id" (click)="editRow(r)">
                    <td style="color: #888; font-size: 12px;">{{ r.id }}</td>
                    @for (f of table.fields; track f.key) {
                      <td>
                        @if (f.type === 'select') {
                          <span style="background: #e0f7fa; padding: 2px 6px; border-radius: 4px; font-size: 13px;">
                            {{ getRefDisplay(f, r[f.key]) }}
                          </span>
                        } @else if (f.type === 'checkbox') {
                          <strong [style.color]="r[f.key] ? '#28a745' : '#aaa'">
                            {{ r[f.key] ? 'Да' : 'Нет' }}
                          </strong>
                        } @else {
                          {{ r[f.key] }}
                        }
                      </td>
                    }
                    <td style="text-align: right;" (click)="$event.stopPropagation()">
                      <button (click)="remove(table, r.id)" style="color: red; cursor: pointer; padding: 4px 8px;">
                        Удалить
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td [attr.colspan]="table.fields.length + 2" style="text-align: center; padding: 20px; color: #777;">
                      Нет данных в этой таблице
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

      }
    </div>
  `,
  // ТЕ САМЫЕ СТИЛИ, КОТОРЫЕ БЫЛИ У ВАС ИЗНАЧАЛЬНО
  styles: [`

  `]
})
export class AdminComponent implements OnInit {
  private admin = inject(AdminService);
  // Сигнал или обычный массив для хранения выбранных файлов
  selectedFiles = signal<File[]>([]);



  tables: TableDef[] = [
    {
      endpoint: 'marks',
      title: 'Марки мотоциклов',
      fields: [
        { key: 'mark', label: 'Марка', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'models',
      title: 'Модели',
      fields: [
        { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark', required: true },
        { key: 'model', label: 'Модель', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'groups',
      title: 'Группы запчастей',
      fields: [
        { key: 'groupName', label: 'Название группы', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'part-numbers',
      title: 'Парт-номера',
      fields: [
        { key: 'partNum', label: 'Парт-номер', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'zip',
      title: 'Запчасти (Номенклатура)',
      fields: [
        { key: 'name', label: 'Наименование', type: 'text', required: true },
        { key: 'incomeCost', label: 'Закупочная цена', type: 'number', required: true },
        { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum' },
        { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark' },
        { key: 'modelId', label: 'Модель', type: 'select', refTable: 'models', refLabelKey: 'model' },
        { key: 'groupId', label: 'Группа запчастей', type: 'select', refTable: 'groups', refLabelKey: 'groupName' },
        { key: 'countStored', label: 'Остаток на складе', type: 'number', required: true },
        { key: 'year', label: 'Год выпуска (YYYY)', type: 'text' },
        { key: 'incomeMotoId', label: 'ID Донора (IncomeMoto)', type: 'text', required: true }
      ]
    },
    {
      endpoint: 'users',
      title: 'Пользователи',
      fields: [
        { key: 'email', label: 'Email', type: 'text', required: true },
        { key: 'fio', label: 'ФИО', type: 'text', required: true },
        { key: 'phoneNumber', label: 'Телефон', type: 'text' },
        { key: 'isAdmin', label: 'Администратор', type: 'checkbox' },
        { key: 'isRegistrar', label: 'Регистратор', type: 'checkbox' },
        { key: 'isSender', label: 'Отправитель', type: 'checkbox' },
        { key: 'password', label: 'Новый пароль', type: 'text' }
      ]
    },
    {
      endpoint: 'addressess',
      title: 'Адреса доставки',
      fields: [
        { key: 'address', label: 'Адрес', type: 'text', required: true },
        { key: 'postCode', label: 'Почтовый индекс', type: 'text' },
        { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio' }
      ]
    },
    {
      endpoint: 'orders',
      title: 'Заказы',
      fields: [
        { key: 'orderNumber', label: 'Номер заказа', type: 'text', required: true },
        { key: 'countOrdered', label: 'Кол-во', type: 'number', required: true },
        { key: 'nomenclatureId', label: 'Запчасть', type: 'select', refTable: 'zip', refLabelKey: 'name', required: true },
        { key: 'addressId', label: 'Адрес доставки', type: 'select', refTable: 'addressess', refLabelKey: 'address', required: true },
        { key: 'sellCost', label: 'Цена продажи', type: 'number', required: true },
        { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio', required: true },
        { key: 'discount', label: 'Скидка', type: 'number' },
        { key: 'orderDateTime', label: 'Дата заказа', type: 'text' }
      ]
    }
  ];

  current = signal<TableDef | null>(null);
  rows = signal<DynamicRow[]>([]); // Использование DynamicRow позволяет обращаться к r.id
  references = signal<Record<string, any[]>>({});

  selectedId = signal<any | null>(null);
  form: Record<string, any> = {};

  activeField = signal<string | null>(null);
  fieldSuggestions = signal<string[]>([]);

  busy = signal<boolean>(false);
  message = signal<string>('');
  error = signal<string>('');

  ngOnInit() {
    this.loadAllReferences();
    if (this.tables.length > 0) {
      this.select(this.tables[0]);
    }
  }

  loadAllReferences() {
    const refEndpoints = ['marks', 'models', 'groups', 'part-numbers', 'users', 'addressess', 'zip'];
    const loadedRefs: Record<string, any[]> = {};

    refEndpoints.forEach(endpoint => {
      this.admin.list(endpoint).subscribe({
        next: (data: any) => {
          loadedRefs[endpoint] = Array.isArray(data) ? data : (data.items || []);
          this.references.set({ ...this.references(), ...loadedRefs });
        },
        error: () => console.warn(`Не удалось загрузить справочник ${endpoint}`)
      });
    });
  }

  getRefDisplay(field: FieldDef, val: any): string {
    if (val === null || val === undefined || !field.refTable) return '—';
    const list = this.references()[field.refTable];
    if (!list || list.length === 0) return String(val);

    const item = list.find(x => String(x.id) === String(val));
    if (!item) return String(val);

    return item[field.refLabelKey!] || item.name || item.mark || item.model || item.address || String(val);
  }

  select(t: TableDef) {
    this.current.set(t);
    this.cancelEdit();
    this.reload(t);
  }

  reload(t: TableDef) {
    this.busy.set(true);
    this.error.set('');
    this.admin.list(t.endpoint).subscribe({
      next: (data: any) => {
        const items = Array.isArray(data) ? data : (data.items || []);
        this.rows.set(items);
        this.busy.set(false);
      },
      error: (err) => {
        this.error.set('Ошибка загрузки данных: ' + (err.error?.message || err.message));
        this.busy.set(false);
      }
    });
  }

  //#region [Line actions]
  editRow(row: any) {
    this.selectedId.set(row.id);
    this.form = { ...row };
  }

  cancelEdit() {
    this.selectedId.set(null);
    this.form = {};
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
    this.selectedFiles.set([]);
  }

  save(table: TableDef) {
    this.busy.set(true);
    this.error.set('');
    this.message.set('');

    const id = this.selectedId();

    const request = id
      ? this.admin.update(table.endpoint, id, this.form, this.selectedFiles())
      : this.admin.add(table.endpoint, this.form, this.selectedFiles());

    request.subscribe({
      next: () => {
        this.message.set(id ? 'Запись успешно обновлена!' : 'Запись успешно добавлена!');
        this.busy.set(false);
        this.cancelEdit();
        this.reload(table);
        this.loadAllReferences();
      },
      error: (err) => {
        this.error.set('Ошибка сохранения: ' + (err.error?.message || err.message));
        this.busy.set(false);
      }
    });
  }

  remove(table: TableDef, id: any) {
    if (!confirm('Вы уверены, что хотите удалить эту запись?')) return;

    this.busy.set(true);
    this.admin.delete(table.endpoint, id).subscribe({
      next: () => {
        this.message.set('Запись удалена');
        this.reload(table);
        this.loadAllReferences();
      },
      error: (err) => {
        this.error.set('Ошибка при удалении: ' + (err.error?.message || err.message));
        this.busy.set(false);
      }
    });
  }
  //#endregion

  //#region [Sugestions]
  onFieldInput(key: string, value: string) {
    this.form[key] = value;
    if (value && typeof value === 'string' && value.trim().length > 0) {
      this.activeField.set(key);
      const allValues = this.rows()
        .map(row => row[key])
        .filter(val => typeof val === 'string' && val.toLowerCase().includes(value.toLowerCase()));

      const uniqueValues = Array.from(new Set(allValues)).slice(0, 5);
      this.fieldSuggestions.set(uniqueValues);
    } else {
      this.fieldSuggestions.set([]);
    }
  }

  applySuggestion(key: string, value: string) {
    this.form[key] = value;
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
  }
  //#endregion

  //#region [Photo]
  onFileSelected(event: any) {
    const files: FileList = event.target.files;
    if (!files) return;

    const currentFiles = this.selectedFiles();
    const newFiles = Array.from(files);
    const totalFilesCount = currentFiles.length + newFiles.length;

    if (totalFilesCount > 3) {
      // Выводим уведомление пользователю
      alert('Превышен лимит! Можно загрузить не более 3-х фотографий.');
      // Или если используете сигнал error: this.error.set('Можно загрузить не более 3-х фотографий.');
      return;
    }

    // Добавляем новые файлы к уже выбранным
    this.selectedFiles.set([...currentFiles, ...newFiles]);

    // Очищаем input, чтобы можно было выбрать тот же файл снова при необходимости
    event.target.value = '';
  }

  removeFile(index: number) {
    const currentFiles = this.selectedFiles();
    currentFiles.splice(index, 1);
    this.selectedFiles.set([...currentFiles]);
  }
  //#endregion
}