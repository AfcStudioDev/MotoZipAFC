import {
  ChangeDetectionStrategy, Component, EventEmitter, Input, Output,
  OnChanges, SimpleChanges, computed, signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { DynamicRow, FieldDef, TableDef } from './admin.types';

/**
 * Таблица записей выбранного справочника вместе со строкой поиска.
 *
 * Поиск живёт здесь, а не в родителе: он существует только чтобы фильтровать эту таблицу,
 * и при переходе на другую вкладку теряет смысл. Условия сбрасываются автоматически при
 * смене table — родителю больше не нужно помнить об этом (раньше он звал resetSearch вручную).
 *
 * Компонент ничего не сохраняет и не удаляет — только сообщает наружу о выборе строки,
 * запросе на удаление и обновление.
 */
@Component({
  selector: 'app-admin-data-table',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Стили общие с AdminComponent (.table-container, .data-table, .search-bar и т.д.):
  // из-за эмуляции инкапсуляции Angular правила родителя не достают до шаблона дочернего
  // компонента — без этого .table-container { overflow-x: auto } не применялся, и широкие
  // таблицы («Запчасти», «Заказы») распирали разметку вместо горизонтальной прокрутки.
  styleUrls: ['../styles/admin.component.css'],
  template: `
    <div class="card">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;">
        <h3 style="margin: 0;">
          {{ table.title }} (Всего: {{ visibleRows().length }}@if (isFiltered()) { <span> из {{ rows.length }}</span> })
        </h3>
        <button (click)="reload.emit()" [disabled]="busy" style="padding: 6px 12px; cursor: pointer;">
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
                (keyup.enter)="applySearch()"
                autocomplete="off"
                placeholder="Введите значение…">

              @if (activeSearchField() === sf.key && searchSuggestions().length > 0) {
                <ul class="suggestions-dropdown">
                  @for (sug of searchSuggestions(); track sug) {
                    <li (click)="applySearchSuggestion(sf.key, sug)">{{ sug }}</li>
                  }
                </ul>
              }
            </div>
          }

          <div class="search-actions">
            <button type="button" class="btn-search" (click)="applySearch()">Найти</button>
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
              <tr [class.active-row]="selectedId === r.id" (click)="rowSelect.emit(r)">
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
                        {{ refDisplay(f, r[f.key]) }}
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
                  <button class="btn-delete" (click)="rowDelete.emit(r.id)">
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
  `
})
export class AdminDataTableComponent implements OnChanges {
  @Input({ required: true }) table!: TableDef;

  /** Строки уже загружены родителем: он владеет загрузкой и обновлением. */
  @Input() set rows(value: DynamicRow[]) {
    this._rows.set(value ?? []);
  }
  get rows(): DynamicRow[] {
    return this._rows();
  }
  private readonly _rows = signal<DynamicRow[]>([]);

  /** Справочники — для подстановки читаемых значений вместо внешних ключей. */
  @Input({ required: true }) references: Record<string, any[]> = {};

  @Input() selectedId: any = null;
  @Input() busy = false;

  @Output() reload = new EventEmitter<void>();
  @Output() rowSelect = new EventEmitter<DynamicRow>();
  @Output() rowDelete = new EventEmitter<any>();

  /** Введённые значения по каждому поисковому полю. */
  searchValues: Record<string, string> = {};
  /** Применённые условия — обновляются только по кнопке «Найти». */
  private readonly appliedSearch = signal<Record<string, string>>({});
  activeSearchField = signal<string | null>(null);
  searchSuggestions = signal<string[]>([]);

  ngOnChanges(changes: SimpleChanges) {
    // Условия поиска относятся к конкретной таблице — при смене вкладки они не имеют смысла.
    if (changes['table'] && !changes['table'].firstChange) this.resetSearch();
  }

  /** Строки с учётом применённых условий поиска. */
  readonly visibleRows = computed<DynamicRow[]>(() => {
    const conditions = Object.entries(this.appliedSearch())
      .filter(([, v]) => v.trim().length > 0)
      .map(([k, v]) => [k, v.trim().toLowerCase()] as const);

    if (conditions.length === 0) return this._rows();

    // Условия по разным полям объединяются по И.
    return this._rows().filter(row =>
      conditions.every(([key, needle]) => {
        const val = row[key];
        return val !== null && val !== undefined &&
          String(val).toLowerCase().includes(needle);
      })
    );
  });

  readonly isFiltered = computed(() =>
    Object.values(this.appliedSearch()).some(v => v.trim().length > 0));

  /** Подсказки берём из уже загруженных строк — по тому полю, в которое вводят. */
  onSearchInput(key: string, value: string) {
    this.searchValues[key] = value;
    this.activeSearchField.set(key);

    const needle = (value ?? '').trim().toLowerCase();
    const values = this._rows()
      .map(row => row[key])
      .filter(v => v !== null && v !== undefined && String(v).trim().length > 0)
      .map(v => String(v))
      .filter(v => needle.length === 0 || v.toLowerCase().includes(needle));

    this.searchSuggestions.set(Array.from(new Set(values)).slice(0, 8));
  }

  applySearchSuggestion(key: string, value: string) {
    this.searchValues[key] = value;
    this.closeSearchSuggestions();
    this.applySearch();
  }

  applySearch() {
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

  /** Внешний ключ → читаемое значение из справочника. */
  refDisplay(field: FieldDef, val: any): string {
    if (val === null || val === undefined || !field.refTable) return '—';
    const list = this.references[field.refTable];
    if (!list || list.length === 0) return String(val);

    const item = list.find(x => String(x.id) === String(val));
    if (!item) return String(val);

    return item[field.refLabelKey!] || item.name || item.mark || item.model || item.address || String(val);
  }
}
