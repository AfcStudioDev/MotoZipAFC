import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AdminService } from '../core/admin.service';
import { environment } from '../../environments/environment';

interface ZipPhotoRow {
  id: number;
  fileName: string;
  isMain: boolean;
}

interface FieldDef {
  key: string;
  label: string;
  /**
   * zip-picker — выбор запчасти в два шага: сначала парт-номер, затем конкретный
   * экземпляр. Нужен потому, что одно наименование встречается у разных парт-номеров
   * (например, «Масляный фильтр» и у Honda, и у Yamaha), и по одному названию
   * невозможно понять, какая именно деталь выбирается.
   */
  type: 'text' | 'number' | 'checkbox' | 'select' | 'date' | 'zip-picker';
  required?: boolean;
  refTable?: string;
  refLabelKey?: string;
  /** Поле принадлежит каталожной позиции (PartNumbers), а не самой записи — сохраняется отдельным запросом. */
  partNumberOwned?: boolean;
}

/** Поле строки поиска. Только содержательные колонки — без Id и внешних ключей. */
interface SearchFieldDef {
  key: string;
  label: string;
}

interface TableDef {
  endpoint: string;
  title: string;
  fields: FieldDef[];
  searchFields?: SearchFieldDef[];
}

// Решает проблему TS4111 (noPropertyAccessFromIndexSignature)
interface DynamicRow {
  id: any;
  [key: string]: any;
}

/** Расхождение цены с уже заведёнными запчастями того же парт-номера. */
interface PriceConflict {
  partNumId: number;
  partNum: string;
  name: string;
  /** Цена, стоящая сейчас у запчастей в базе. */
  existingCost: number;
  /** Цена, введённая пользователем. */
  newCost: number;
  /** Сколько запчастей с этим парт-номером уже заведено. */
  count: number;
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [FormsModule, CommonModule],
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
                  @else if (f.type === 'zip-picker') {
                    <!-- Шаг 1: парт-номер. Наименование в списке — подсказка, что это за деталь. -->
                    <select
                      class="form-control"
                      [ngModel]="zipPartNumId()"
                      [name]="f.key + '__partNum'"
                      (ngModelChange)="onZipPartNumChange($event)"
                    >
                      <option [ngValue]="null">— Выберите парт-номер —</option>
                      @for (pn of zipPartNumbers(); track pn.partNumId) {
                        <option [ngValue]="pn.partNumId">{{ pn.partNum }} — {{ pn.name }}</option>
                      }
                    </select>

                    <!-- Шаг 2: нужен только когда под одним парт-номером заведено несколько
                         экземпляров. Единственный подставляется сам (см. onZipPartNumChange). -->
                    @if (zipCandidates().length > 1) {
                      <select
                        class="form-control zip-picker-second"
                        [(ngModel)]="form[f.key]"
                        [name]="f.key"
                        [required]="!!f.required"
                      >
                        <option [ngValue]="null">— Выберите запчасть —</option>
                        @for (z of zipCandidates(); track z.id) {
                          <option [ngValue]="z.id">{{ zipOptionLabel(z) }}</option>
                        }
                      </select>
                      <small class="picker-hint">
                        Под этим парт-номером заведено {{ zipCandidates().length }} шт. — уточните, какая именно
                      </small>
                    } @else if (zipCandidates().length === 1) {
                      <small class="picker-hint picker-hint-ok">
                        Подставлено автоматически: {{ zipOptionLabel(zipCandidates()[0]) }}
                      </small>
                    } @else if (zipPartNumId() !== null) {
                      <small class="picker-hint picker-hint-warn">
                        По этому парт-номеру нет заведённых запчастей
                      </small>
                    }
                  }
                  @else if (f.type === 'date') {
                    <!-- Бэкенд принимает и отдаёт DateOnly в формате YYYY-MM-DD —
                         это же значение нативно использует input[type=date]. -->
                    <input
                      type="date"
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
                      (ngModelChange)="onSelectChange(table, f.key, $event)"
                      [required]="!!f.required"
                    >
                      <option [ngValue]="null">— Выберите —</option>
                      @for (opt of references()[f.refTable!] || []; track opt.id) {
                        <option [ngValue]="opt.id">
                          {{ opt[f.refLabelKey!] || opt.name || opt.id }}
                        </option>
                      }
                    </select>
                    @if (f.partNumberOwned) {
                      <small class="owned-hint">Поле парт-номера — изменение применится ко всем запчастям с ним</small>
                    }
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
                    @if (f.partNumberOwned) {
                      <small class="owned-hint">Поле парт-номера — изменение применится ко всем запчастям с ним</small>
                    }
                  }
                </div>
              }
                @if (table.endpoint === 'zip') {
                  <div class="photo-section">
                    @if (selectedId() && existingPhotos().length > 0) {
                      <label>Загруженные фотографии:</label>
                      <div class="photo-preview-list">
                        @for (photo of existingPhotos(); track photo.id) {
                          <div class="photo-preview-item">
                            <img
                              [src]="photoBaseUrl + photo.fileName"
                              [alt]="photo.fileName"
                            >
                            <button type="button" (click)="deleteExistingPhoto(photo)">
                              Удалить
                            </button>
                          </div>
                        }
                      </div>
                    }

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
            <h3 style="margin: 0;">
              {{ table.title }} (Всего: {{ visibleRows().length }}@if (isFiltered()) { <span> из {{ rows().length }}</span> })
            </h3>
            <button (click)="reload(table)" [disabled]="busy()" style="padding: 6px 12px; cursor: pointer;">
              Обновить таблицу
            </button>
          </div>

          <!-- СТРОКА ПОИСКА -->
          @if (table.searchFields?.length) {
            <div class="search-bar">
              @for (sf of table.searchFields!; track sf.key) {
                <div class="search-field">
                  <label>{{ sf.label }}</label>
                  <input
                    type="text"
                    class="form-control"
                    [ngModel]="searchValues[sf.key] || ''"
                    [name]="'search_' + sf.key"
                    (ngModelChange)="onSearchInput(sf.key, $event)"
                    (focus)="onSearchInput(sf.key, searchValues[sf.key] || '')"
                    (keyup.enter)="applySearch(table)"
                    autocomplete="off"
                    placeholder="Введите значение…">

                  @if (activeSearchField() === sf.key && searchSuggestions().length > 0) {
                    <ul class="suggestions-dropdown">
                      @for (sug of searchSuggestions(); track sug) {
                        <li (click)="applySearchSuggestion(table, sf.key, sug)">{{ sug }}</li>
                      }
                    </ul>
                  }
                </div>
              }

              <div class="search-actions">
                <button type="button" class="btn-search" (click)="applySearch(table)">Найти</button>
                @if (isFiltered()) {
                  <button type="button" class="btn-reset" (click)="resetSearch()">Сбросить</button>
                }
              </div>
            </div>
          }

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
                @for (r of visibleRows(); track r.id) {
                  <!-- Использование r.id теперь работает благодаря интерфейсу DynamicRow -->
                  <tr [class.active-row]="selectedId() === r.id" (click)="editRow(r)">
                    <td class="id-cell" data-label="ID">{{ r.id }}</td>
                    @for (f of table.fields; track f.key) {
                      <td [attr.data-label]="f.label">
                        @if (f.type === 'zip-picker') {
                          <!-- В строке заказа бэкенд отдаёт и партномер, и наименование -->
                          <span style="background: #e0f7fa; padding: 2px 6px; border-radius: 4px; font-size: 13px;">
                            {{ r['partNum'] }} — {{ r['zipName'] }}
                          </span>
                        } @else if (f.type === 'select') {
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
                    <td class="actions-cell" data-label="Действия" (click)="$event.stopPropagation()">
                      <button class="btn-delete" (click)="remove(table, r.id)">
                        Удалить
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr class="empty-row">
                    <td [attr.colspan]="table.fields.length + 2">
                      {{ isFiltered() ? 'Ничего не найдено по заданным условиям' : 'Нет данных в этой таблице' }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

      }

      <!-- Выбор действия с ценой, когда парт-номер уже заведён с другой ценой -->
      @if (pricePrompt(); as p) {
        <div class="price-modal-backdrop" (click)="cancelPriceChoice()">
          <div class="price-modal" (click)="$event.stopPropagation()">
            <h3>Какое действие применить к таким же парт-номерам?</h3>

            <p class="price-modal-summary">
              По парт-номеру <b>{{ p.partNum }}</b> ({{ p.name }}) уже заведено:
              <b>{{ p.count }}</b> шт. с ценой <b>{{ p.existingCost }} ₽</b>.
              Вы указали <b>{{ p.newCost }} ₽</b>.
            </p>

            <div class="price-modal-actions">
              <button type="button" class="price-option" (click)="applyPriceChoice('update-all')">
                <span class="price-option-title">Обновить цены для всех существующих запчастей</span>
                <span class="price-option-note">Всем {{ p.count }} шт. будет проставлено {{ p.newCost }} ₽</span>
              </button>

              <button type="button" class="price-option" (click)="applyPriceChoice('use-existing')">
                <span class="price-option-title">Установить текущую цену такой же, как у запчастей в базе</span>
                <span class="price-option-note">Введённое значение заменится на {{ p.existingCost }} ₽</span>
              </button>

              <button type="button" class="price-option" (click)="applyPriceChoice('keep-unique')">
                <span class="price-option-title">Оставить уникальную цену не обновляя старые запчасти</span>
                <span class="price-option-note">Новая запчасть получит {{ p.newCost }} ₽, остальные не изменятся</span>
              </button>
            </div>

            <button type="button" class="price-modal-cancel" (click)="cancelPriceChoice()">Отмена</button>
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
  // Уже загруженные фотографии выбранной запчасти
  existingPhotos = signal<ZipPhotoRow[]>([]);
  photoBaseUrl = `${environment.apiUrl.replace('/api', '')}/ZipPhotos/`;



  tables: TableDef[] = [
    {
      endpoint: 'marks',
      title: 'Марки мотоциклов',
      fields: [
        { key: 'mark', label: 'Марка', type: 'text', required: true }
      ],
      searchFields: [{ key: 'mark', label: 'Марка' }]
    },
    {
      endpoint: 'models',
      title: 'Модели',
      fields: [
        { key: 'markId', label: 'Марка', type: 'select', refTable: 'marks', refLabelKey: 'mark', required: true },
        { key: 'model', label: 'Модель', type: 'text', required: true }
      ],
      searchFields: [{ key: 'model', label: 'Модель' }]
    },
    {
      endpoint: 'groups',
      title: 'Группы запчастей',
      fields: [
        { key: 'groupName', label: 'Название группы', type: 'text', required: true }
      ],
      searchFields: [{ key: 'groupName', label: 'Название группы' }]
    },
    {
      endpoint: 'part-numbers',
      title: 'Парт-номера',
      fields: [
        { key: 'partNum', label: 'Парт-номер', type: 'text', required: true },
        { key: 'name', label: 'Наименование', type: 'text', required: true },
        { key: 'groupId', label: 'Группа запчастей', type: 'select', refTable: 'groups', refLabelKey: 'groupName' }
      ],
      searchFields: [
        { key: 'partNum', label: 'Парт-номер' },
        { key: 'name', label: 'Наименование' }
      ]
    },
    {
      // Применимость: одна каталожная позиция подходит к нескольким моделям.
      endpoint: 'applicability',
      title: 'Применимость к моделям',
      fields: [
        { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum', required: true },
        { key: 'modelId', label: 'Модель', type: 'select', refTable: 'models', refLabelKey: 'model', required: true }
      ],
      searchFields: [
        { key: 'partNum', label: 'Парт-номер' },
        { key: 'model', label: 'Модель' }
      ]
    },
    {
      endpoint: 'zip',
      title: 'Запчасти (Номенклатура)',
      fields: [
        // Наименование и группа принадлежат парт-номеру: подставляются при его выборе
        // и сохраняются отдельным запросом в PartNumbers.
        { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum', required: true },
        { key: 'name', label: 'Наименование', type: 'text', required: true, partNumberOwned: true },
        { key: 'groupId', label: 'Группа запчастей', type: 'select', refTable: 'groups', refLabelKey: 'groupName', partNumberOwned: true },
        { key: 'incomeCost', label: 'Закупочная цена', type: 'number', required: true },
        { key: 'sellCost', label: 'Цена продажи', type: 'number' },
        { key: 'countStored', label: 'Остаток на складе', type: 'number', required: true },
        { key: 'year', label: 'Год выпуска (YYYY)', type: 'number' },
        { key: 'incomeDate', label: 'Дата поступления', type: 'date' },
        { key: 'incomeMotoId', label: 'Донор (IncomeMoto)', type: 'select', refTable: 'incomemotos', refLabelKey: 'description', required: true },
        { key: 'comment', label: 'Комментарий', type: 'text' }
      ],
      searchFields: [
        { key: 'partNum', label: 'Парт-номер' },
        { key: 'name', label: 'Наименование' }
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
      ],
      searchFields: [
        { key: 'fio', label: 'ФИО' },
        { key: 'email', label: 'Email' },
        { key: 'phoneNumber', label: 'Телефон' }
      ]
    },
    {
      endpoint: 'addressess',
      title: 'Адреса доставки',
      fields: [
        { key: 'address', label: 'Адрес', type: 'text', required: true },
        { key: 'postCode', label: 'Почтовый индекс', type: 'text' },
        { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio' }
      ],
      searchFields: [
        { key: 'address', label: 'Адрес' },
        { key: 'postCode', label: 'Индекс' }
      ]
    },
    {
      endpoint: 'orders',
      title: 'Заказы',
      fields: [
        { key: 'orderNumber', label: 'Номер заказа', type: 'text', required: true },
        { key: 'countOrdered', label: 'Кол-во', type: 'number', required: true },
        { key: 'zipId', label: 'Запчасть', type: 'zip-picker', required: true },
        { key: 'addressId', label: 'Адрес доставки', type: 'select', refTable: 'addressess', refLabelKey: 'address', required: true },
        { key: 'sellCost', label: 'Цена продажи', type: 'number', required: true },
        { key: 'userId', label: 'Покупатель', type: 'select', refTable: 'users', refLabelKey: 'fio', required: true },
        { key: 'discount', label: 'Скидка', type: 'number' },
        { key: 'orderDateTime', label: 'Дата заказа', type: 'text' }
      ],
      searchFields: [
        { key: 'orderNumber', label: 'Номер заказа' },
        { key: 'partNum', label: 'Парт-номер' },
        // Бэкенд отдаёт наименование в поле zipName; прежний ключ nomenclatureName
        // не существовал в ответе, из-за чего поиск по запчасти ничего не находил.
        { key: 'zipName', label: 'Наименование' }
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

  //#region [Выбор запчасти по парт-номеру]

  /** Парт-номер, выбранный на первом шаге поля zip-picker. */
  zipPartNumId = signal<number | null>(null);

  /** Парт-номера, по которым реально заведены запчасти — заказать можно только их. */
  zipPartNumbers = computed(() => {
    const seen = new Map<number, { partNumId: number; partNum: string; name: string }>();
    for (const z of this.references()['zip'] ?? []) {
      if (z.partNumId != null && !seen.has(z.partNumId)) {
        seen.set(z.partNumId, { partNumId: z.partNumId, partNum: z.partNum, name: z.name });
      }
    }
    return Array.from(seen.values()).sort((a, b) => a.partNum.localeCompare(b.partNum));
  });

  /** Экземпляры запчастей выбранного парт-номера. */
  zipCandidates = computed(() => {
    const pn = this.zipPartNumId();
    if (pn === null || pn === undefined) return [];
    return (this.references()['zip'] ?? []).filter(z => z.partNumId === pn);
  });

  /**
   * Наименование у всех экземпляров одного парт-номера совпадает (оно хранится
   * в PartNumbers), поэтому к нему добавляем то, что реально их различает.
   */
  zipOptionLabel(z: any): string {
    const parts = [z.name];
    if (z.incomeMoto) parts.push(z.incomeMoto);
    if (z.sellCost != null) parts.push(`${z.sellCost} ₽`);
    parts.push(`остаток ${z.countStored ?? 0}`);
    return parts.join(' · ');
  }

  onZipPartNumChange(partNumId: number | null) {
    this.zipPartNumId.set(partNumId);
    const candidates = this.zipCandidates();
    // Единственный экземпляр подставляем сразу, иначе ждём выбора во втором списке.
    this.form['zipId'] = candidates.length === 1 ? candidates[0].id : null;
  }

  /** Восстанавливает первый шаг по уже сохранённой в заказе запчасти. */
  private syncZipPickerFromForm() {
    const zipId = this.form['zipId'];
    if (!zipId) { this.zipPartNumId.set(null); return; }
    const zip = (this.references()['zip'] ?? []).find(z => String(z.id) === String(zipId));
    this.zipPartNumId.set(zip ? zip.partNumId : null);
  }

  //#endregion

  // --- Поиск по таблице ---
  /** Введённые значения по каждому поисковому полю. */
  searchValues: Record<string, string> = {};
  /** Применённые условия — обновляются только по кнопке «Найти». */
  appliedSearch = signal<Record<string, string>>({});
  activeSearchField = signal<string | null>(null);
  searchSuggestions = signal<string[]>([]);

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
    const refEndpoints = ['marks', 'models', 'groups', 'part-numbers', 'users', 'addressess', 'zip', 'incomemotos'];
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

  //#region [Поиск по таблице]

  /** Строки с учётом применённых условий поиска. */
  visibleRows = computed<DynamicRow[]>(() => {
    const conditions = Object.entries(this.appliedSearch())
      .filter(([, v]) => v.trim().length > 0)
      .map(([k, v]) => [k, v.trim().toLowerCase()] as const);

    if (conditions.length === 0) return this.rows();

    // Условия по разным полям объединяются по И.
    return this.rows().filter(row =>
      conditions.every(([key, needle]) => {
        const val = row[key];
        return val !== null && val !== undefined &&
          String(val).toLowerCase().includes(needle);
      })
    );
  });

  isFiltered = computed(() =>
    Object.values(this.appliedSearch()).some(v => v.trim().length > 0));

  /** Подсказки берём из уже загруженных строк — по тому полю, в которое вводят. */
  onSearchInput(key: string, value: string) {
    this.searchValues[key] = value;
    this.activeSearchField.set(key);

    const needle = (value ?? '').trim().toLowerCase();
    const values = this.rows()
      .map(row => row[key])
      .filter(v => v !== null && v !== undefined && String(v).trim().length > 0)
      .map(v => String(v))
      .filter(v => needle.length === 0 || v.toLowerCase().includes(needle));

    this.searchSuggestions.set(Array.from(new Set(values)).slice(0, 8));
  }

  applySearchSuggestion(table: TableDef, key: string, value: string) {
    this.searchValues[key] = value;
    this.closeSearchSuggestions();
    this.applySearch(table);
  }

  applySearch(_table: TableDef) {
    this.closeSearchSuggestions();
    this.appliedSearch.set({ ...this.searchValues });
  }

  resetSearch() {
    this.searchValues = {};
    this.appliedSearch.set({});
    this.closeSearchSuggestions();
  }

  private closeSearchSuggestions() {
    this.activeSearchField.set(null);
    this.searchSuggestions.set([]);
  }

  //#endregion

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
    // Условия поиска относятся к конкретной таблице — при смене вкладки они не имеют смысла.
    this.resetSearch();
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

  //#region [Автозаполнение по парт-номеру]

  onSelectChange(table: TableDef, key: string, value: any) {
    this.form[key] = value;
    if (table.endpoint === 'zip' && key === 'partNumId') {
      this.fillFromPartNumber(value);
    }
  }

  /**
   * Подставляет данные выбранной каталожной позиции: наименование и группу — всегда,
   * цены — только в пустые поля, чтобы не затирать введённое вручную.
   */
  private fillFromPartNumber(partNumId: any) {
    if (partNumId === null || partNumId === undefined) return;

    const pn = (this.references()['part-numbers'] || [])
      .find(p => String(p.id) === String(partNumId));
    if (!pn) return;

    this.form['name'] = pn.name ?? '';
    this.form['groupId'] = pn.groupId ?? null;

    // Цены берём с последней заведённой запчасти с этим же парт-номером — как подсказку.
    const sameZip = (this.references()['zip'] || [])
      .filter(z => String(z.partNumId) === String(partNumId))
      .pop();
    if (sameZip) {
      if (this.isEmpty(this.form['incomeCost'])) this.form['incomeCost'] = sameZip.incomeCost;
      if (this.isEmpty(this.form['sellCost'])) this.form['sellCost'] = sameZip.sellCost;
    }
  }

  private isEmpty(v: any): boolean {
    return v === null || v === undefined || v === '';
  }

  //#endregion

  //#region [Line actions]
  editRow(row: any) {
    this.selectedId.set(row.id);
    this.form = { ...row };
    this.selectedFiles.set([]);
    this.existingPhotos.set(Array.isArray(row.photos) ? row.photos : []);
    // Чтобы в заказе первым шагом сразу стоял парт-номер сохранённой запчасти.
    this.syncZipPickerFromForm();
  }

  cancelEdit() {
    this.selectedId.set(null);
    this.form = {};
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
    this.selectedFiles.set([]);
    this.existingPhotos.set([]);
    this.zipPartNumId.set(null);
  }

  save(table: TableDef) {
    this.error.set('');
    this.message.set('');

    // Новая запчасть с уже существующим парт-номером и другой ценой продажи —
    // спрашиваем, что делать с ценами остальных, и продолжаем после ответа.
    const conflict = this.detectPriceConflict(table);
    if (conflict) {
      this.pendingPriceTable = table;
      this.pricePrompt.set(conflict);
      return;
    }

    this.runSave(table);
  }

  private runSave(table: TableDef) {
    this.busy.set(true);

    // Поля, принадлежащие парт-номеру, живут в другой таблице — сохраняем их отдельно.
    const ownedChanged = this.partNumberPatch(table);
    if (ownedChanged) {
      this.admin.updatePartNumber(this.form['partNumId'], ownedChanged).subscribe({
        next: () => this.saveRow(table),
        error: (err) => {
          this.error.set('Не удалось сохранить данные парт-номера: ' + (err.error?.message || err.message));
          this.busy.set(false);
        }
      });
      return;
    }

    this.saveRow(table);
  }

  //#region [Цена при совпадении парт-номера]

  /** Данные для модального окна выбора действия с ценой. */
  pricePrompt = signal<PriceConflict | null>(null);
  private pendingPriceTable: TableDef | null = null;
  /** Если задано — после сохранения проставить эту цену всем запчастям парт-номера. */
  private bulkRepriceAfterSave: { partNumId: number; newCost: number } | null = null;

  /**
   * Срабатывает только при добавлении новой запчасти: если по этому парт-номеру
   * уже есть позиции и введённая цена продажи от них отличается — надо спросить.
   */
  private detectPriceConflict(table: TableDef): PriceConflict | null {
    if (table.endpoint !== 'zip' || this.selectedId()) return null;

    const partNumId = Number(this.form['partNumId']);
    if (!partNumId) return null;

    const newCost = Number(this.form['sellCost']);
    if (this.isEmpty(this.form['sellCost']) || Number.isNaN(newCost)) return null;

    const siblings = (this.references()['zip'] ?? [])
      .filter(z => Number(z.partNumId) === partNumId && z.sellCost != null);
    if (siblings.length === 0) return null;

    const existingCost = Number(siblings[0].sellCost);
    if (existingCost === newCost) return null;

    return {
      partNumId,
      partNum: siblings[0].partNum,
      name: siblings[0].name,
      existingCost,
      newCost,
      count: siblings.length
    };
  }

  applyPriceChoice(choice: 'update-all' | 'use-existing' | 'keep-unique') {
    const prompt = this.pricePrompt();
    const table = this.pendingPriceTable;
    this.pricePrompt.set(null);
    this.pendingPriceTable = null;
    if (!prompt || !table) return;

    if (choice === 'use-existing') {
      // Цену пользователя заменяем на ту, что уже в базе.
      this.form['sellCost'] = prompt.existingCost;
      this.bulkRepriceAfterSave = null;
    } else if (choice === 'update-all') {
      // Сохраняем как ввёл пользователь, а остальным проставим её же после сохранения.
      this.bulkRepriceAfterSave = { partNumId: prompt.partNumId, newCost: prompt.newCost };
    } else {
      this.bulkRepriceAfterSave = null;
    }

    this.runSave(table);
  }

  cancelPriceChoice() {
    this.pricePrompt.set(null);
    this.pendingPriceTable = null;
  }

  /** Массовая переоценка после успешного создания запчасти. */
  private applyBulkRepriceIfNeeded(createdZipId?: string) {
    const bulk = this.bulkRepriceAfterSave;
    this.bulkRepriceAfterSave = null;
    if (!bulk) return;

    this.admin.repricePartNum(bulk.partNumId, bulk.newCost, createdZipId).subscribe({
      next: (res) => {
        this.message.set(`${this.message()} ${res.message}.`);
        this.loadAllReferences();
      },
      error: (err) => this.error.set('Запчасть добавлена, но обновить цены остальных не удалось: '
        + (err.error?.message || err.message))
    });
  }

  //#endregion

  /**
   * Возвращает данные для обновления PartNumbers, если поля парт-номера в форме
   * отличаются от сохранённых. null — менять нечего.
   */
  private partNumberPatch(table: TableDef): { partNum: string; name: string; groupId: number | null } | null {
    const owned = table.fields.filter(f => f.partNumberOwned);
    if (owned.length === 0) return null;

    const partNumId = this.form['partNumId'];
    if (this.isEmpty(partNumId)) return null;

    const pn = (this.references()['part-numbers'] || [])
      .find(p => String(p.id) === String(partNumId));
    if (!pn) return null;

    const name = (this.form['name'] ?? '').toString().trim();
    const groupId = this.isEmpty(this.form['groupId']) ? null : Number(this.form['groupId']);

    const unchanged = name === (pn.name ?? '') &&
      String(groupId ?? '') === String(pn.groupId ?? '');
    if (unchanged || name.length === 0) return null;

    return { partNum: pn.partNum, name, groupId };
  }

  /** Сохранение самой записи таблицы. */
  private saveRow(table: TableDef) {
    const id = this.selectedId();

    const request = id
      ? this.admin.update(table.endpoint, id, this.form, this.selectedFiles())
      : this.admin.add(table.endpoint, this.form, this.selectedFiles());

    request.subscribe({
      next: (res: any) => {
        this.message.set(id ? 'Запись успешно обновлена!' : 'Запись успешно добавлена!');
        this.busy.set(false);
        this.cancelEdit();
        this.reload(table);
        this.loadAllReferences();
        // Выбранное в модальном окне действие «обновить цены для всех».
        this.applyBulkRepriceIfNeeded(res?.id);
      },
      error: (err) => {
        this.error.set('Ошибка сохранения: ' + (err.error?.message || err.message));
        this.busy.set(false);
        this.bulkRepriceAfterSave = null;
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

  deleteExistingPhoto(photo: ZipPhotoRow) {
    if (!confirm('Удалить эту фотографию?')) return;

    this.admin.deleteZipPhoto(photo.id).subscribe({
      next: () => {
        this.existingPhotos.set(this.existingPhotos().filter(p => p.id !== photo.id));
        this.message.set('Фотография удалена');
      },
      error: (err) => {
        this.error.set('Ошибка при удалении фотографии: ' + (err.error?.message || err.message));
      }
    });
  }
  //#endregion
}