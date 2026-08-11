import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
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
  type: 'text' | 'number' | 'checkbox' | 'select' | 'date' | 'zip-picker' | 'catalog-picker' | 'stock';
  required?: boolean;
  refTable?: string;
  refLabelKey?: string;
  /** Поле принадлежит каталожной позиции (PartNumbers), а не самой записи — сохраняется отдельным запросом. */
  partNumberOwned?: boolean;
  /**
   * Для catalog-picker: по какому свойству PartNumber ищутся подсказки (парт-номер
   * или наименование) — оба поля ищут по одному и тому же справочнику part-numbers,
   * и выбор в любом из них подставляет оба значения (см. applyCatalogSuggestion).
   */
  catalogRole?: 'partNum' | 'name';
  /** Кнопка «+» рядом с полем, открывающая модалку быстрого добавления записи в этот справочник. */
  quickAdd?: 'part-number' | 'group' | 'incomemoto';
}

/** Что именно создаём в модалке быстрого добавления и как это применить к форме после сохранения. */
type QuickAddKind = 'part-number' | 'group' | 'incomemoto';

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

/**
 * Одна строка применимости парт-номера к модели, выбранная/введённая в форме,
 * но ещё не обязательно сохранённая. applicabilityId задан только для уже
 * существующих связей (используется, чтобы отличить их от новых при сохранении).
 * modelId/markId заданы, только если модель/марка выбраны из существующего справочника —
 * иначе при сохранении их создаст syncApplicability по названию.
 */
interface StagedModelLink {
  applicabilityId?: number;
  modelId?: number;
  markId?: number;
  markName: string;
  modelName: string;
}

/** То же самое для серий — независимая от моделей связка (см. syncApplicability). */
interface StagedSeriesLink {
  applicabilityId?: number;
  seriesId?: string;
  seriesName: string;
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
                  @else if (f.type === 'catalog-picker') {
                    <!-- Парт-номер и наименование ищут по одному справочнику (part-numbers)
                         и подставляют друг друга: выбор в любом из полей заполняет оба. -->
                    <div class="field-with-add">
                      <input
                        type="text"
                        class="form-control"
                        [(ngModel)]="form[f.key]"
                        [name]="f.key"
                        (input)="onCatalogInput(f.key, f.catalogRole!, form[f.key])"
                        (focus)="onCatalogInput(f.key, f.catalogRole!, form[f.key])"
                        [required]="!!f.required"
                        autocomplete="off"
                      >
                      @if (f.quickAdd) {
                        <button type="button" class="btn-quick-add" (click)="openQuickAdd(f.quickAdd)" title="Добавить новую запись в справочник">+</button>
                      }
                    </div>
                    @if (activeCatalogField() === f.key && catalogSuggestions().length > 0) {
                      <ul class="suggestions-dropdown">
                        @for (pn of catalogSuggestions(); track pn.id) {
                          <li (click)="applyCatalogSuggestion(pn)">{{ pn.partNum }} — {{ pn.name }}</li>
                        }
                      </ul>
                    }
                    @if (f.partNumberOwned) {
                      <small class="owned-hint">Поле парт-номера — изменение применится ко всем запчастям с ним</small>
                    }
                  }
                  @else if (f.type === 'stock') {
                    @if (selectedId()) {
                      <!-- Прямая правка остатка при редактировании запрещена — она бы меняла
                           склад в обход журнала операций. Для этого есть «Коррекция». -->
                      <input type="text" class="form-control" [value]="currentPartNumStock() + ' шт.'" disabled readonly>
                      <small class="owned-hint">
                        Показана сумма остатков по всем партиям этого парт-номера. Изменить остаток можно только через «Коррекция».
                      </small>
                    } @else {
                      <input
                        type="number"
                        step="any"
                        class="form-control"
                        [(ngModel)]="form[f.key]"
                        [name]="f.key"
                        [required]="!!f.required"
                      >
                      <small class="picker-hint">Уже в наличии по этому парт-номеру: {{ currentPartNumStock() }} шт.</small>
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
                    <div class="field-with-add">
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
                      @if (f.quickAdd) {
                        <button type="button" class="btn-quick-add" (click)="openQuickAdd(f.quickAdd)" title="Добавить новую запись в справочник">+</button>
                      }
                    </div>
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
                @if (table.endpoint === 'part-numbers') {
                  <div class="applicability-section">
                    <ng-container [ngTemplateOutlet]="applicabilityFields"></ng-container>
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

      <!-- Быстрое добавление записи в справочник по кнопке «+» у поля -->
      @if (quickAddKind(); as kind) {
        <div class="price-modal-backdrop" (click)="closeQuickAdd()">
          <div class="price-modal" (click)="$event.stopPropagation()">
            <h3>{{ quickAddTitle(kind) }}</h3>

            @if (quickAddError()) {
              <div class="error">{{ quickAddError() }}</div>
            }

            @if (kind === 'part-number') {
              <div class="form-field">
                <label>Парт-номер *</label>
                <input type="text" [(ngModel)]="quickAddForm['partNum']" name="qaPartNum" autocomplete="off">
              </div>
              <div class="form-field">
                <label>Наименование *</label>
                <input type="text" [(ngModel)]="quickAddForm['name']" name="qaName" autocomplete="off">
              </div>
              <div class="form-field">
                <label>Группа запчастей</label>
                <select [(ngModel)]="quickAddForm['groupId']" name="qaGroupId">
                  <option [ngValue]="null">— Выберите —</option>
                  @for (g of references()['groups'] || []; track g.id) {
                    <option [ngValue]="g.id">{{ g.groupName }}</option>
                  }
                </select>
              </div>
              <div class="applicability-section">
                <ng-container [ngTemplateOutlet]="applicabilityFields"></ng-container>
              </div>
            }

            @if (kind === 'group') {
              <div class="form-field">
                <label>Название группы *</label>
                <input type="text" [(ngModel)]="quickAddForm['groupName']" name="qaGroupName" autocomplete="off">
              </div>
            }

            @if (kind === 'incomemoto') {
              <div class="form-field">
                <label>Описание *</label>
                <input type="text" [(ngModel)]="quickAddForm['description']" name="qaDescription" autocomplete="off">
              </div>
              <div class="form-field">
                <label>Поставщик</label>
                <select [(ngModel)]="quickAddForm['userId']" name="qaUserId">
                  <option [ngValue]="null">— Выберите —</option>
                  @for (u of references()['users'] || []; track u.id) {
                    <option [ngValue]="u.id">{{ u.fio }}</option>
                  }
                </select>
              </div>
            }

            <div class="quick-add-actions">
              <button type="button" (click)="closeQuickAdd()" [disabled]="quickAddBusy()">Отмена</button>
              <button type="button" (click)="submitQuickAdd()" [disabled]="quickAddBusy()">Добавить</button>
            </div>
          </div>
        </div>
      }

      <!-- Модели и серии парт-номера — общий фрагмент для формы вкладки «Парт-номера»
           и для модалки быстрого добавления (кнопка «+» у поля «Парт-номер»/«Наименование»
           на вкладке «Запчасти»), чтобы не дублировать разметку. -->
      <ng-template #applicabilityFields>
        <label>Модели</label>
        <div class="chips">
          @for (m of stagedModels(); track $index) {
            <span class="chip">
              {{ m.markName ? m.markName + ' ' : '' }}{{ m.modelName }}
              <button type="button" (click)="removeStagedModel($index)">✕</button>
            </span>
          } @empty {
            <span class="chips-empty">Модели не привязаны</span>
          }
        </div>
        <div class="chip-add-row">
          <div class="chip-add-field">
            <input
              type="text"
              class="form-control"
              placeholder="Марка"
              [(ngModel)]="newModelMark"
              name="newModelMark"
              (input)="onMarkSuggestInput(newModelMark)"
              (focus)="onMarkSuggestInput(newModelMark)"
              autocomplete="off"
            >
            @if (activeMarkSuggest() && markSuggestions().length > 0) {
              <ul class="suggestions-dropdown">
                @for (mk of markSuggestions(); track mk.id) {
                  <li (click)="selectMarkSuggestion(mk)">{{ mk.mark }}</li>
                }
              </ul>
            }
          </div>
          <div class="chip-add-field">
            <input
              type="text"
              class="form-control"
              placeholder="Модель"
              [(ngModel)]="newModelName"
              name="newModelName"
              (input)="onModelSuggestInput(newModelName)"
              (focus)="onModelSuggestInput(newModelName)"
              autocomplete="off"
            >
            @if (activeModelSuggest() && modelSuggestions().length > 0) {
              <ul class="suggestions-dropdown">
                @for (md of modelSuggestions(); track md.id) {
                  <li (click)="selectModelSuggestion(md)">{{ md.mark ? md.mark + ' — ' : '' }}{{ md.model }}</li>
                }
              </ul>
            }
          </div>
          <button type="button" class="btn-chip-add" (click)="addStagedModel()">Добавить</button>
        </div>
        <small class="owned-hint">Новые марка/модель будут созданы автоматически, если их ещё нет</small>

        <label style="margin-top: 14px;">Серии</label>
        <div class="chips">
          @for (s of stagedSeries(); track $index) {
            <span class="chip">
              {{ s.seriesName }}
              <button type="button" (click)="removeStagedSeries($index)">✕</button>
            </span>
          } @empty {
            <span class="chips-empty">Серии не привязаны</span>
          }
        </div>
        <div class="chip-add-row">
          <div class="chip-add-field">
            <input
              type="text"
              class="form-control"
              placeholder="Серия"
              [(ngModel)]="newSeriesName"
              name="newSeriesName"
              (input)="onSeriesSuggestInput(newSeriesName)"
              (focus)="onSeriesSuggestInput(newSeriesName)"
              autocomplete="off"
            >
            @if (activeSeriesSuggest() && seriesSuggestions().length > 0) {
              <ul class="suggestions-dropdown">
                @for (sr of seriesSuggestions(); track sr.id) {
                  <li (click)="selectSeriesSuggestion(sr)">{{ sr.seriesName }}</li>
                }
              </ul>
            }
          </div>
          <button type="button" class="btn-chip-add" (click)="addStagedSeries()">Добавить</button>
        </div>
        <small class="owned-hint">Новая серия будет создана автоматически, если её ещё нет</small>
      </ng-template>
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
      // Серия не привязана к конкретной модели — независимая классификация
      // (см. PartNumberSeriesApplicability ниже).
      endpoint: 'series',
      title: 'Серии',
      fields: [
        { key: 'seriesName', label: 'Название серии', type: 'text', required: true }
      ],
      searchFields: [{ key: 'seriesName', label: 'Название серии' }]
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
      // Независимая от моделей привязка: у одного парт-номера может быть
      // любое число моделей и любое число серий одновременно (см. server-side
      // PartNumberSeriesApplicability — отдельная таблица со своей уникальностью).
      endpoint: 'series-applicability',
      title: 'Применимость к сериям',
      fields: [
        { key: 'partNumId', label: 'Парт-номер', type: 'select', refTable: 'part-numbers', refLabelKey: 'partNum', required: true },
        { key: 'seriesId', label: 'Серия', type: 'select', refTable: 'series', refLabelKey: 'seriesName', required: true }
      ],
      searchFields: [
        { key: 'partNum', label: 'Парт-номер' },
        { key: 'series', label: 'Серия' }
      ]
    },
    {
      endpoint: 'incomemotos',
      title: 'Доноры',
      fields: [
        { key: 'description', label: 'Описание', type: 'text', required: true },
        { key: 'userId', label: 'Поставщик', type: 'select', refTable: 'users', refLabelKey: 'fio' }
      ],
      searchFields: [{ key: 'description', label: 'Описание' }]
    },
    {
      endpoint: 'zip',
      title: 'Запчасти (Приход)',
      fields: [
        // Парт-номер и наименование вводятся вручную с подсказками из справочника
        // part-numbers; выбор подсказки в любом из полей заполняет оба (см. applyCatalogSuggestion).
        // Группа принадлежит парт-номеру: подставляется при выборе и сохраняется
        // отдельным запросом в PartNumbers.
        { key: 'partNum', label: 'Парт-номер', type: 'catalog-picker', catalogRole: 'partNum', required: true, quickAdd: 'part-number' },
        { key: 'name', label: 'Наименование', type: 'catalog-picker', catalogRole: 'name', required: true, partNumberOwned: true, quickAdd: 'part-number' },
        { key: 'groupId', label: 'Группа запчастей', type: 'select', refTable: 'groups', refLabelKey: 'groupName', partNumberOwned: true, quickAdd: 'group' },
        { key: 'incomeCost', label: 'Закупочная цена', type: 'number', required: true },
        { key: 'sellCost', label: 'Цена продажи', type: 'number' },
        // Число редактируется только при добавлении новой партии; при редактировании
        // существующей запчасти поле показывает сумму по парт-номеру и заблокировано —
        // см. type: 'stock'.
        { key: 'countStored', label: 'Количество в приходе', type: 'stock', required: true },
        { key: 'year', label: 'Год выпуска (YYYY)', type: 'number' },
        { key: 'incomeDate', label: 'Дата поступления', type: 'date' },
        { key: 'incomeMotoId', label: 'Донор (IncomeMoto)', type: 'select', refTable: 'incomemotos', refLabelKey: 'description', required: true, quickAdd: 'incomemoto' },
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

  //#region [Быстрое добавление записи в справочник по кнопке «+»]

  quickAddKind = signal<QuickAddKind | null>(null);
  quickAddBusy = signal(false);
  quickAddError = signal('');
  quickAddForm: Record<string, any> = {};

  quickAddTitle(kind: QuickAddKind): string {
    switch (kind) {
      case 'part-number': return 'Новый парт-номер';
      case 'group': return 'Новая группа запчастей';
      case 'incomemoto': return 'Новый донор';
    }
  }

  openQuickAdd(kind: QuickAddKind) {
    this.quickAddForm = {};
    this.quickAddError.set('');
    this.quickAddKind.set(kind);
    // Модалка «Новый парт-номер» переиспользует те же Модели/Серии, что и вкладка
    // «Парт-номера» — начинаем с чистого списка, а не с того, что могло остаться
    // от редактирования в другом контексте.
    if (kind === 'part-number') {
      this.resetStagedApplicability();
    }
  }

  closeQuickAdd() {
    this.quickAddKind.set(null);
    this.quickAddForm = {};
    this.quickAddError.set('');
  }

  submitQuickAdd() {
    const kind = this.quickAddKind();
    if (!kind) return;

    const spec = this.quickAddSpec(kind);
    const validationError = spec.validate();
    if (validationError) {
      this.quickAddError.set(validationError);
      return;
    }

    // Модели/серии — те, что успели добавить в модалке до сохранения.
    const modelsToSync = this.stagedModels();
    const seriesToSync = this.stagedSeries();

    this.quickAddBusy.set(true);
    this.quickAddError.set('');
    this.admin.add(spec.endpoint, spec.payload).subscribe({
      next: (res: any) => {
        this.quickAddBusy.set(false);
        this.applyQuickAddResult(kind, res);
        this.loadAllReferences();
        this.closeQuickAdd();

        if (kind === 'part-number' && res?.id) {
          // Новый парт-номер — удалять нечего, поэтому исходные списки связей пустые.
          this.syncApplicability(res.id, modelsToSync, seriesToSync, [], []);
        }
        this.resetStagedApplicability();
      },
      error: (err) => {
        this.quickAddBusy.set(false);
        this.quickAddError.set(err.error?.message || err.message);
      }
    });
  }

  private quickAddSpec(kind: QuickAddKind): { endpoint: string; payload: any; validate: () => string | null } {
    switch (kind) {
      case 'part-number':
        return {
          endpoint: 'part-numbers',
          payload: {
            partNum: (this.quickAddForm['partNum'] ?? '').toString().trim(),
            name: (this.quickAddForm['name'] ?? '').toString().trim(),
            groupId: this.isEmpty(this.quickAddForm['groupId']) ? null : Number(this.quickAddForm['groupId'])
          },
          validate: () =>
            !(this.quickAddForm['partNum'] ?? '').toString().trim() ? 'Укажите парт-номер' :
            !(this.quickAddForm['name'] ?? '').toString().trim() ? 'Укажите наименование' : null
        };
      case 'group':
        return {
          endpoint: 'groups',
          payload: { groupName: (this.quickAddForm['groupName'] ?? '').toString().trim() },
          validate: () => !(this.quickAddForm['groupName'] ?? '').toString().trim() ? 'Укажите название группы' : null
        };
      case 'incomemoto':
        return {
          endpoint: 'incomemotos',
          payload: {
            description: (this.quickAddForm['description'] ?? '').toString().trim(),
            userId: this.isEmpty(this.quickAddForm['userId']) ? null : Number(this.quickAddForm['userId'])
          },
          validate: () => !(this.quickAddForm['description'] ?? '').toString().trim() ? 'Укажите описание' : null
        };
    }
  }

  /** Подставляет только что созданную запись в поле формы, из которого была открыта модалка. */
  private applyQuickAddResult(kind: QuickAddKind, res: any) {
    if (kind === 'part-number') {
      this.form['partNum'] = res.partNum;
      this.form['name'] = res.name;
      this.form['partNumId'] = res.id;
      if (res.groupId != null) this.form['groupId'] = res.groupId;
      this.refreshPartNumStock();
    } else if (kind === 'group') {
      this.form['groupId'] = res.id;
    } else if (kind === 'incomemoto') {
      this.form['incomeMotoId'] = res.id;
    }
  }

  //#endregion

  //#region [Применимость парт-номера к моделям и сериям прямо в форме]

  /**
   * Текущий (желаемый) состав связей. Заполняется существующими связями при
   * редактировании (см. editRow) и пополняется через addStagedModel/addStagedSeries.
   * Сохраняется вместе с парт-номером — см. syncApplicability.
   */
  stagedModels = signal<StagedModelLink[]>([]);
  stagedSeries = signal<StagedSeriesLink[]>([]);

  /** Связи, которые были у записи ДО открытия формы — нужны, чтобы понять, что удалено. */
  private originalModelLinkIds: number[] = [];
  private originalSeriesLinkIds: number[] = [];

  newModelMark = '';
  newModelName = '';
  newSeriesName = '';

  markSuggestions = signal<{ id: number; mark: string }[]>([]);
  activeMarkSuggest = signal(false);
  modelSuggestions = signal<{ id: number; model: string; markId: number | null; mark: string | null }[]>([]);
  activeModelSuggest = signal(false);
  seriesSuggestions = signal<{ id: string; seriesName: string }[]>([]);
  activeSeriesSuggest = signal(false);

  onMarkSuggestInput(value: string) {
    this.newModelMark = value;
    this.activeMarkSuggest.set(true);
    const needle = (value ?? '').trim().toLowerCase();
    const list = this.references()['marks'] || [];
    const filtered = needle.length === 0 ? list : list.filter((m: any) => String(m.mark).toLowerCase().includes(needle));
    this.markSuggestions.set(filtered.slice(0, 8));
  }

  selectMarkSuggestion(mk: { id: number; mark: string }) {
    this.newModelMark = mk.mark;
    this.activeMarkSuggest.set(false);
    this.markSuggestions.set([]);
  }

  onModelSuggestInput(value: string) {
    this.newModelName = value;
    this.activeModelSuggest.set(true);
    const needle = (value ?? '').trim().toLowerCase();
    const list = this.references()['models'] || [];
    const filtered = needle.length === 0 ? list : list.filter((m: any) => String(m.model).toLowerCase().includes(needle));
    this.modelSuggestions.set(filtered.slice(0, 8));
  }

  /** Выбор подсказки модели заодно подставляет её марку — вводить её отдельно не нужно. */
  selectModelSuggestion(md: { id: number; model: string; markId: number | null; mark: string | null }) {
    this.newModelName = md.model;
    if (md.mark) this.newModelMark = md.mark;
    this.activeModelSuggest.set(false);
    this.modelSuggestions.set([]);
  }

  /**
   * Добавляет модель в локальный список. Если название совпадает с уже существующей
   * маркой/моделью — запоминает их id (тогда при сохранении новых записей создавать
   * не придётся), иначе id остаются не заданы, и syncApplicability заведёт их сама.
   */
  addStagedModel() {
    const markName = this.newModelMark.trim();
    const modelName = this.newModelName.trim();
    if (!modelName) {
      this.error.set('Введите название модели');
      return;
    }

    const alreadyAdded = this.stagedModels().some(m =>
      m.modelName.toLowerCase() === modelName.toLowerCase() &&
      (m.markName || '').toLowerCase() === markName.toLowerCase());
    if (alreadyAdded) {
      this.newModelMark = '';
      this.newModelName = '';
      this.closeModelSuggestions();
      return;
    }

    const existingMark = (this.references()['marks'] || [])
      .find((m: any) => String(m.mark).toLowerCase() === markName.toLowerCase());
    const existingModel = (this.references()['models'] || [])
      .find((m: any) => String(m.model).toLowerCase() === modelName.toLowerCase()
        && (!existingMark || m.markId === existingMark.id));

    this.stagedModels.set([...this.stagedModels(), {
      markId: existingMark?.id,
      markName: existingMark?.mark ?? markName,
      modelId: existingModel?.id,
      modelName: existingModel?.model ?? modelName
    }]);

    this.newModelMark = '';
    this.newModelName = '';
    this.closeModelSuggestions();
  }

  removeStagedModel(index: number) {
    const list = [...this.stagedModels()];
    list.splice(index, 1);
    this.stagedModels.set(list);
  }

  private closeModelSuggestions() {
    this.activeMarkSuggest.set(false);
    this.markSuggestions.set([]);
    this.activeModelSuggest.set(false);
    this.modelSuggestions.set([]);
  }

  onSeriesSuggestInput(value: string) {
    this.newSeriesName = value;
    this.activeSeriesSuggest.set(true);
    const needle = (value ?? '').trim().toLowerCase();
    const list = this.references()['series'] || [];
    const filtered = needle.length === 0 ? list : list.filter((s: any) => String(s.seriesName).toLowerCase().includes(needle));
    this.seriesSuggestions.set(filtered.slice(0, 8));
  }

  selectSeriesSuggestion(sr: { id: string; seriesName: string }) {
    this.newSeriesName = sr.seriesName;
    this.activeSeriesSuggest.set(false);
    this.seriesSuggestions.set([]);
  }

  addStagedSeries() {
    const seriesName = this.newSeriesName.trim();
    if (!seriesName) {
      this.error.set('Введите название серии');
      return;
    }

    const alreadyAdded = this.stagedSeries().some(s => s.seriesName.toLowerCase() === seriesName.toLowerCase());
    if (alreadyAdded) {
      this.newSeriesName = '';
      this.closeSeriesSuggestions();
      return;
    }

    const existing = (this.references()['series'] || [])
      .find((s: any) => String(s.seriesName).toLowerCase() === seriesName.toLowerCase());

    this.stagedSeries.set([...this.stagedSeries(), {
      seriesId: existing?.id,
      seriesName: existing?.seriesName ?? seriesName
    }]);

    this.newSeriesName = '';
    this.closeSeriesSuggestions();
  }

  removeStagedSeries(index: number) {
    const list = [...this.stagedSeries()];
    list.splice(index, 1);
    this.stagedSeries.set(list);
  }

  private closeSeriesSuggestions() {
    this.activeSeriesSuggest.set(false);
    this.seriesSuggestions.set([]);
  }

  /** Заполняет список уже существующими связями редактируемого парт-номера. */
  private loadStagedApplicabilityForEdit(partNumId: number) {
    const models = (this.references()['applicability'] || [])
      .filter((a: any) => a.partNumId === partNumId)
      .map((a: any): StagedModelLink => ({
        applicabilityId: a.id,
        modelId: a.modelId,
        markName: a.mark ?? '',
        modelName: a.model
      }));
    this.stagedModels.set(models);
    this.originalModelLinkIds = models.map(m => m.applicabilityId!);

    const series = (this.references()['series-applicability'] || [])
      .filter((a: any) => a.partNumId === partNumId)
      .map((a: any): StagedSeriesLink => ({
        applicabilityId: a.id,
        seriesId: a.seriesId,
        seriesName: a.series
      }));
    this.stagedSeries.set(series);
    this.originalSeriesLinkIds = series.map(s => s.applicabilityId!);
  }

  private resetStagedApplicability() {
    this.stagedModels.set([]);
    this.stagedSeries.set([]);
    this.originalModelLinkIds = [];
    this.originalSeriesLinkIds = [];
    this.newModelMark = '';
    this.newModelName = '';
    this.newSeriesName = '';
    this.closeModelSuggestions();
    this.closeSeriesSuggestions();
  }

  /**
   * Приводит связи парт-номера к желаемому составу: недостающие марка/модель/серия
   * создаются по названию, новые связи добавляются, убранные из списка — удаляются.
   * Вызывается после успешного сохранения самого парт-номера (нужен его id).
   */
  private async syncApplicability(
    partNumId: number,
    models: StagedModelLink[],
    series: StagedSeriesLink[],
    originalModelLinkIds: number[],
    originalSeriesLinkIds: number[]
  ) {
    try {
      const keptModelLinkIds = new Set(models.filter(m => m.applicabilityId).map(m => m.applicabilityId));
      for (const linkId of originalModelLinkIds.filter(id => !keptModelLinkIds.has(id))) {
        await firstValueFrom(this.admin.delete('applicability', linkId));
      }

      for (const m of models) {
        if (m.applicabilityId) continue; // связь уже существует

        let markId = m.markId;
        if (!markId && m.markName.trim()) {
          const existingMark = (this.references()['marks'] || [])
            .find((x: any) => String(x.mark).toLowerCase() === m.markName.trim().toLowerCase());
          markId = existingMark
            ? existingMark.id
            : (await firstValueFrom(this.admin.add('marks', { mark: m.markName.trim() })) as any).id;
        }

        let modelId = m.modelId;
        if (!modelId) {
          const existingModel = (this.references()['models'] || [])
            .find((x: any) => String(x.model).toLowerCase() === m.modelName.trim().toLowerCase()
              && (markId == null || x.markId === markId));
          modelId = existingModel
            ? existingModel.id
            : (await firstValueFrom(this.admin.add('models', { markId: markId ?? null, model: m.modelName.trim() })) as any).id;
        }

        await firstValueFrom(this.admin.add('applicability', { partNumId, modelId }));
      }

      const keptSeriesLinkIds = new Set(series.filter(s => s.applicabilityId).map(s => s.applicabilityId));
      for (const linkId of originalSeriesLinkIds.filter(id => !keptSeriesLinkIds.has(id))) {
        await firstValueFrom(this.admin.delete('series-applicability', linkId));
      }

      for (const s of series) {
        if (s.applicabilityId) continue;

        let seriesId = s.seriesId;
        if (!seriesId) {
          const existingSeries = (this.references()['series'] || [])
            .find((x: any) => String(x.seriesName).toLowerCase() === s.seriesName.trim().toLowerCase());
          seriesId = existingSeries
            ? existingSeries.id
            : (await firstValueFrom(this.admin.add('series', { seriesName: s.seriesName.trim() })) as any).id;
        }

        await firstValueFrom(this.admin.add('series-applicability', { partNumId, seriesId }));
      }

      this.loadAllReferences();
    } catch (err: any) {
      this.error.set('Парт-номер сохранён, но применимость обновить не удалось: ' + (err.error?.message || err.message));
    }
  }

  //#endregion

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
    const refEndpoints = [
      'marks', 'models', 'series', 'groups', 'part-numbers', 'users', 'addressess', 'zip', 'incomemotos',
      // Нужны, чтобы при редактировании парт-номера подставить его текущие связи (см. loadStagedApplicabilityForEdit).
      'applicability', 'series-applicability'
    ];
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

  //#region [Парт-номер и наименование вручную]

  /** Поле (partNum/name), в котором сейчас открыт список подсказок. */
  activeCatalogField = signal<string | null>(null);
  catalogSuggestions = signal<{ id: number; partNum: string; name: string; groupId: number | null }[]>([]);

  /** Сумма остатков по всем партиям текущего (введённого/выбранного) парт-номера. */
  currentPartNumStock = signal<number>(0);

  /** Пересчитывает currentPartNumStock по тексту в form['partNum'] — точное совпадение с part-numbers. */
  private refreshPartNumStock() {
    const partNumText = (this.form['partNum'] ?? '').toString().trim().toLowerCase();
    if (!partNumText) {
      this.currentPartNumStock.set(0);
      return;
    }

    const match = (this.references()['part-numbers'] || [])
      .find((p: any) => String(p.partNum).trim().toLowerCase() === partNumText);
    if (!match) {
      this.currentPartNumStock.set(0);
      return;
    }

    const sum = (this.references()['zip'] || [])
      .filter((z: any) => String(z.partNumId) === String(match.id))
      .reduce((acc: number, z: any) => acc + (z.countStored ?? 0), 0);
    this.currentPartNumStock.set(sum);
  }

  /** Подсказки ищутся по общему справочнику part-numbers — сразу за оба поля. */
  onCatalogInput(key: string, role: 'partNum' | 'name', value: string) {
    this.form[key] = value;
    this.activeCatalogField.set(key);

    const needle = (value ?? '').toString().trim().toLowerCase();
    const list = this.references()['part-numbers'] || [];
    const filtered = needle.length === 0
      ? list
      : list.filter((p: any) => String(p[role] ?? '').toLowerCase().includes(needle));

    this.catalogSuggestions.set(filtered.slice(0, 8));
    // Подсказка «уже в наличии» реагирует и на точное совпадение текста без клика по списку.
    this.refreshPartNumStock();
  }

  /** Выбор подсказки в любом из полей подставляет оба значения и группу/цены. */
  applyCatalogSuggestion(pn: { id: number; partNum: string; name: string; groupId: number | null }) {
    this.form['partNum'] = pn.partNum;
    this.form['name'] = pn.name;
    this.form['partNumId'] = pn.id;
    this.closeCatalogSuggestions();
    this.fillFromPartNumber(pn.id);
    this.refreshPartNumStock();
  }

  private closeCatalogSuggestions() {
    this.activeCatalogField.set(null);
    this.catalogSuggestions.set([]);
  }

  /**
   * Связывает введённый текст парт-номера с записью в part-numbers перед сохранением
   * запчасти: точное совпадение — переиспользуем её id, иначе заводим новую каталожную
   * позицию (парт-номер вводится вручную, поэтому нового может ещё не быть в справочнике).
   * partNumId всегда пересчитывается заново по тексту, а не по тому, что было
   * подставлено при клике по подсказке — это позволяет спокойно донабрать/поправить
   * текст после выбора, не оставляя рассинхронизированный id.
   */
  private resolvePartNumber(next: () => void) {
    const partNumText = (this.form['partNum'] ?? '').toString().trim();
    if (!partNumText) {
      this.error.set('Парт-номер обязателен');
      return;
    }

    const existing = (this.references()['part-numbers'] || [])
      .find((p: any) => String(p.partNum).trim().toLowerCase() === partNumText.toLowerCase());

    if (existing) {
      this.form['partNum'] = existing.partNum;
      this.form['partNumId'] = existing.id;
      next();
      return;
    }

    const nameText = (this.form['name'] ?? '').toString().trim();
    if (!nameText) {
      this.error.set('Для нового парт-номера нужно указать наименование');
      return;
    }

    this.busy.set(true);
    this.admin.add('part-numbers', {
      partNum: partNumText,
      name: nameText,
      groupId: this.isEmpty(this.form['groupId']) ? null : Number(this.form['groupId'])
    }).subscribe({
      next: (pn: any) => {
        this.form['partNumId'] = pn.id;
        this.busy.set(false);
        this.loadAllReferences();
        next();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set('Не удалось создать парт-номер: ' + (err.error?.message || err.message));
      }
    });
  }

  //#endregion

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
    this.refreshPartNumStock();

    if (this.current()?.endpoint === 'part-numbers') {
      this.loadStagedApplicabilityForEdit(row.id);
    } else {
      this.resetStagedApplicability();
    }
  }

  cancelEdit() {
    this.selectedId.set(null);
    this.form = {};
    this.activeField.set(null);
    this.fieldSuggestions.set([]);
    this.closeCatalogSuggestions();
    this.currentPartNumStock.set(0);
    this.selectedFiles.set([]);
    this.existingPhotos.set([]);
    this.zipPartNumId.set(null);
    this.resetStagedApplicability();

    // Дата поступления по умолчанию — сегодня, чтобы не проставлять вручную
    // на каждой новой запчасти. Только для добавления: при редактировании
    // существующей записи форму заполняет editRow из данных самой записи.
    if (this.current()?.endpoint === 'zip') {
      this.form['incomeDate'] = this.todayIso();
    }
  }

  /** Сегодняшняя дата в формате YYYY-MM-DD — том же, что нативно использует input[type=date]. */
  private todayIso(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  save(table: TableDef) {
    this.error.set('');
    this.message.set('');

    // Парт-номер вводится вручную — сперва связываем текст с id существующей
    // или новой каталожной позиции, и только потом продолжаем как раньше.
    if (table.endpoint === 'zip') {
      this.resolvePartNumber(() => this.continueSave(table));
      return;
    }

    this.continueSave(table);
  }

  private continueSave(table: TableDef) {
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

    // Применимость сохраняется отдельными запросами после того, как у парт-номера
    // точно есть id — снимок делаем сейчас, до cancelEdit() в успешном колбэке.
    const isPartNumbers = table.endpoint === 'part-numbers';
    const modelsToSync = this.stagedModels();
    const seriesToSync = this.stagedSeries();
    const originalModelLinkIds = this.originalModelLinkIds;
    const originalSeriesLinkIds = this.originalSeriesLinkIds;

    const request = id
      ? this.admin.update(table.endpoint, id, this.form, this.selectedFiles())
      : this.admin.add(table.endpoint, this.form, this.selectedFiles());

    request.subscribe({
      next: (res: any) => {
        this.message.set(id ? 'Запись успешно обновлена!' : 'Запись успешно добавлена!');
        const partNumId = id ?? res?.id;
        this.busy.set(false);
        this.cancelEdit();
        this.reload(table);
        this.loadAllReferences();
        // Выбранное в модальном окне действие «обновить цены для всех».
        this.applyBulkRepriceIfNeeded(res?.id);

        if (isPartNumbers && partNumId) {
          this.syncApplicability(partNumId, modelsToSync, seriesToSync, originalModelLinkIds, originalSeriesLinkIds);
        }
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