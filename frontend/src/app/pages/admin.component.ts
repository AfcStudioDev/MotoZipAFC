import { Component, OnInit, ViewChild, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { AdminService } from '../core/admin.service';
import { AuthService } from '../core/auth.service';
import { environment } from '../../environments/environment';
import { AdminDraftService, DraftPayload } from '../admin/admin-draft.service';
import { ADMIN_TABLES } from '../admin/admin.tables';
import { ApplicabilityEditorComponent } from '../admin/applicability-editor.component';
import { ApplicabilitySyncService } from '../admin/applicability-sync.service';
import { AdminDataTableComponent } from '../admin/admin-data-table.component';
import {
  DynamicRow, FieldDef, PriceConflict, QuickAddKind,
  StagedModelLink, StagedSeriesLink, TableDef, ZipPhotoRow
} from '../admin/admin.types';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [FormsModule, CommonModule, ApplicabilityEditorComponent, AdminDataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrls: ['../styles/admin.component.css'],
  // Состояние черновика привязано к экземпляру формы, поэтому сервис не глобальный.
  providers: [AdminDraftService],
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
        <button [class.active]="qrLabelTabActive()" (click)="openQrLabelTab()">
          Печать QR-кода
        </button>
      </div>

      @if (current(); as table) {
        
        <!-- КАРТОЧКА ФОРМЫ (Оригинальный дизайн) -->
        <div class="card">
          <h3 style="margin-top: 0;">{{ selectedId() ? 'Редактировать запись' : 'Добавить запись' }}</h3>
          
          <!-- input/change всплывают со всех полей формы, поэтому одного обработчика на <form>
               хватает, чтобы поймать любой ручной ввод и поставить черновик в очередь на сохранение. -->
          <form (ngSubmit)="save(table)" (input)="scheduleDraftSave()" (change)="scheduleDraftSave()">
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
                    @if (f.readonly) {
                      <!-- disabled + ngModel в Angular конфликтуют (варнинг в консоли и риск
                           рассинхронизации), поэтому для полей только для чтения — как и у
                           «Количество в приходе» — используем однонаправленный [value]. -->
                      <input type="text" class="form-control" [value]="form[f.key] ?? ''" disabled readonly>
                    } @else {
                      <input
                        type="number"
                        step="any"
                        class="form-control"
                        [ngModel]="form[f.key]"
                        (ngModelChange)="onNumberFieldChange(table, f.key, $event)"
                        [name]="f.key"
                        [required]="!!f.required"
                      >
                    }
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
                        [ngModel]="form[f.key]"
                        [name]="f.key"
                        (ngModelChange)="onZipCandidateChange($event)"
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
                    } @else if (activeCatalogField() === f.key && catalogSuggestions().length === 0 && !isEmpty(form[f.key])) {
                      <small class="picker-hint picker-hint-warn">Такого не найдено. Можно создать</small>
                    }
                    @if (f.partNumberOwned) {
                      <small class="owned-hint">Поле парт-номера — изменение применится ко всем запчастям с ним</small>
                    }
                  }
                  @else if (f.type === 'group-picker') {
                    <!-- Группа вводится текстом с подсказками, как парт-номер: точное совпадение
                         переиспользуется, иначе новая группа заводится при сохранении (см. resolveGroup). -->
                    <div class="field-with-add">
                      <input
                        type="text"
                        class="form-control"
                        [(ngModel)]="form['groupName']"
                        [name]="f.key"
                        (input)="onGroupInput(form['groupName'])"
                        (focus)="onGroupInput(form['groupName'])"
                        [required]="!!f.required"
                        autocomplete="off"
                      >
                      @if (f.quickAdd) {
                        <button type="button" class="btn-quick-add" (click)="openQuickAdd(f.quickAdd)" title="Добавить новую запись в справочник">+</button>
                      }
                    </div>
                    @if (activeGroupPicker() && groupSuggestions().length > 0) {
                      <ul class="suggestions-dropdown">
                        @for (g of groupSuggestions(); track g.id) {
                          <li (click)="applyGroupSuggestion(g)">{{ g.groupName }}</li>
                        }
                      </ul>
                    } @else if (activeGroupPicker() && groupSuggestions().length === 0 && !isEmpty(form['groupName'])) {
                      <small class="picker-hint picker-hint-warn">Такого не найдено. Можно создать</small>
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
                        @for (opt of refOptionsFor(f); track opt.id) {
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
                  @else if (f.type === 'file-link') {
                    <!-- Только просмотр: файл прикладывает клиент через каталог, не админ. -->
                    @if (form[f.key]) {
                      <a [href]="fileUrl(f, form[f.key])" target="_blank" rel="noopener">Открыть чек</a>
                    } @else {
                      <small class="owned-hint">Чек не прикреплён</small>
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
                              [src]="photoBaseUrl + photo.fileName + '?v=' + photo.id"
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
              <!-- Регистратор заводит новые записи, но не правит существующие:
                   в режиме редактирования кнопки сохранения у него нет. -->
              @if (!selectedId() || canModify) {
                <button type="submit" [disabled]="busy()" style="padding: 8px 16px; cursor: pointer;">
                  {{ selectedId() ? 'Обновить' : 'Добавить' }}
                </button>
              }
              @if (selectedId()) {
                <button type="button" (click)="cancelEdit()" style="padding: 8px 16px; cursor: pointer;">
                  Отмена
                </button>
              }
              @if (!selectedId() && draftSavedAt()) {
                <span class="draft-hint">
                  Черновик сохранён {{ draftSavedAt() | date:'dd.MM.yyyy HH:mm' }}
                  <button type="button" class="draft-clear-btn" (click)="clearDraft()">Очистить</button>
                </span>
              }
            </div>
          </form>
        </div>

        <!-- Таблица со строкой поиска: поиск живёт внутри компонента и сбрасывается
             сам при смене вкладки (см. AdminDataTableComponent.ngOnChanges). -->
        <app-admin-data-table
          [table]="table"
          [rows]="rows()"
          [references]="references()"
          [selectedId]="selectedId()"
          [busy]="busy()"
          [canDelete]="canModify"
          (reload)="reload(table)"
          (rowSelect)="editRow($event)"
          (rowDelete)="remove(table, $event)"
        />

      } @else if (qrLabelTabActive()) {

        <!-- Печать QR-кода: не CRUD-таблица, а утилита над уже существующей запчастью —
             генерирует PDF-этикетку и открывает её в новой вкладке. -->
        <div class="card">
          <h3 style="margin-top: 0;">Печать QR-кода</h3>

          <div class="form-grid">
            <div class="form-group" style="position: relative;">
              <label>Запчасть <span style="color: red;">*</span></label>
              <select
                class="form-control"
                [ngModel]="qrLabelPartNumId()"
                name="qrLabelPartNum"
                (ngModelChange)="onQrLabelPartNumChange($event)"
              >
                <option [ngValue]="null">— Выберите парт-номер —</option>
                @for (pn of qrLabelPartNumbers(); track pn.partNumId) {
                  <option [ngValue]="pn.partNumId">{{ pn.partNum }} — {{ pn.name }}</option>
                }
              </select>

              @if (qrLabelCandidates().length > 1) {
                <select
                  class="form-control zip-picker-second"
                  [ngModel]="qrLabelZipId()"
                  name="qrLabelZip"
                  (ngModelChange)="qrLabelZipId.set($event)"
                >
                  <option [ngValue]="null">— Выберите запчасть —</option>
                  @for (z of qrLabelCandidates(); track z.id) {
                    <option [ngValue]="z.id">{{ zipOptionLabel(z) }}</option>
                  }
                </select>
                <small class="picker-hint">
                  Под этим парт-номером заведено {{ qrLabelCandidates().length }} шт. — уточните, какая именно
                </small>
              } @else if (qrLabelCandidates().length === 1) {
                <small class="picker-hint picker-hint-ok">
                  Подставлено автоматически: {{ zipOptionLabel(qrLabelCandidates()[0]) }}
                </small>
              }
            </div>

            <div class="form-group">
              <label>Id</label>
              <input type="text" class="form-control" [value]="qrLabelZipId() ?? ''" disabled readonly>
            </div>

            <div class="form-group">
              <label>Парт-номер</label>
              <input type="text" class="form-control" [value]="qrLabelSelectedZip()?.partNum ?? ''" disabled readonly>
            </div>

            <div class="form-group">
              <label>Наименование</label>
              <input type="text" class="form-control" [value]="qrLabelSelectedZip()?.name ?? ''" disabled readonly>
            </div>
          </div>

          <div class="actions">
            <button
              type="button"
              [disabled]="!qrLabelZipId() || qrLabelBusy()"
              (click)="printQrLabel()"
              style="padding: 8px 16px; cursor: pointer;"
            >
              Печать QR-кода
            </button>
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
                <div class="field-with-add">
                  <input
                    type="text"
                    [(ngModel)]="quickAddForm['groupName']"
                    name="qaGroupName"
                    (input)="onQuickAddGroupInput(quickAddForm['groupName'])"
                    (focus)="onQuickAddGroupInput(quickAddForm['groupName'])"
                    autocomplete="off"
                  >
                  <button type="button" class="btn-quick-add" (click)="openGroupQuickAdd()" title="Добавить новую группу">+</button>
                </div>
                @if (activeQuickAddGroupPicker() && quickAddGroupSuggestions().length > 0) {
                  <ul class="suggestions-dropdown">
                    @for (g of quickAddGroupSuggestions(); track g.id) {
                      <li (click)="applyQuickAddGroupSuggestion(g)">{{ g.groupName }}</li>
                    }
                  </ul>
                } @else if (activeQuickAddGroupPicker() && quickAddGroupSuggestions().length === 0 && !isEmpty(quickAddForm['groupName'])) {
                  <small class="picker-hint picker-hint-warn">Такого не найдено. Можно создать</small>
                }
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

      <!-- Вложенное добавление группы поверх модалки «Новый парт-номер» — та же кнопка «+»,
           только рендерится позже в DOM, поэтому перекрывает первую модалку без доп. z-index. -->
      @if (groupQuickAddOpen()) {
        <div class="price-modal-backdrop" (click)="closeGroupQuickAdd()">
          <div class="price-modal" style="max-width: 380px;" (click)="$event.stopPropagation()">
            <h3>Новая группа запчастей</h3>

            @if (groupQuickAddError()) {
              <div class="error">{{ groupQuickAddError() }}</div>
            }

            <div class="form-field">
              <label>Название группы *</label>
              <input type="text" [(ngModel)]="groupQuickAddName" name="nestedGroupName" autocomplete="off">
            </div>

            <div class="quick-add-actions">
              <button type="button" (click)="closeGroupQuickAdd()" [disabled]="groupQuickAddBusy()">Отмена</button>
              <button type="button" (click)="submitGroupQuickAdd()" [disabled]="groupQuickAddBusy()">Добавить</button>
            </div>
          </div>
        </div>
      }

      <!-- Модели и серии парт-номера — общий фрагмент для формы вкладки «Парт-номера»
           и для модалки быстрого добавления (кнопка «+» у поля «Парт-номер»/«Наименование»
           на вкладке «Запчасти»), чтобы не дублировать разметку. -->
      <!-- Применимость вынесена в отдельный компонент: он редактирует список связей,
           а сохраняет их ApplicabilitySyncService после сохранения самого парт-номера. -->
      <ng-template #applicabilityFields>
        <app-applicability-editor
          [references]="references()"
          [models]="stagedModels()"
          (modelsChange)="stagedModels.set($event)"
          [series]="stagedSeries()"
          (seriesChange)="stagedSeries.set($event)"
          (error)="error.set($event)"
        />
      </ng-template>
    </div>
  `,
  // ТЕ САМЫЕ СТИЛИ, КОТОРЫЕ БЫЛИ У ВАС ИЗНАЧАЛЬНО
  styles: [`

  `]
})
export class AdminComponent implements OnInit {
  private admin = inject(AdminService);
  private readonly auth = inject(AuthService);

  /**
   * Правка и удаление существующих записей — только у администратора. Регистратор
   * заводит новые записи, но не меняет и не удаляет уже заведённые.
   *
   * Роль не меняется, пока пользователь не перезайдёт, поэтому это обычное поле, а не сигнал.
   * Здесь только внешний вид: то же ограничение проверяет бэкенд —
   * [Authorize(Roles = "Admin")] на PUT и DELETE в api/admin.
   */
  readonly canModify = this.auth.isAdmin;
  // Сигнал или обычный массив для хранения выбранных файлов
  selectedFiles = signal<File[]>([]);
  // Уже загруженные фотографии выбранной запчасти
  existingPhotos = signal<ZipPhotoRow[]>([]);
  photoBaseUrl = `${environment.apiUrl.replace('/api', '')}/ZipPhotos/`;

  /** Файлы отдаются статикой (wwwroot/&lt;fileFolder&gt;), а не через /api — например, чек оплаты. */
  fileUrl(field: FieldDef, fileName: string): string {
    const base = environment.apiUrl.replace(/\/api\/?$/, '');
    return `${base}/${field.fileFolder}/${fileName}`;
  }



  /** Описание таблиц вынесено в admin/admin.tables.ts — это статические данные, не состояние. */
  tables: TableDef[] = ADMIN_TABLES;

  current = signal<TableDef | null>(null);
  rows = signal<DynamicRow[]>([]); // Использование DynamicRow позволяет обращаться к r.id
  references = signal<Record<string, any[]>>({});

  selectedId = signal<any | null>(null);
  form: Record<string, any> = {};

  activeField = signal<string | null>(null);
  fieldSuggestions = signal<string[]>([]);

  //#region [Черновики форм]

  /**
   * Ведение черновиков (debounce, восстановление, гонка при переключении вкладок) вынесено
   * в AdminDraftService. Здесь остаётся только то, что знает про саму форму.
   */
  private readonly drafts = inject(AdminDraftService);

  /** Показывается под формой, когда в ней есть восстановленный/сохранённый черновик. */
  draftSavedAt = this.drafts.savedAt;

  /** Черновик ведём только для новой записи: при правке существующей строки источник истины — сама строка. */
  private currentFormKey(): string | null {
    const endpoint = this.current()?.endpoint;
    if (!endpoint || this.selectedId()) return null;
    return AdminDraftService.tracks(endpoint) ? endpoint : null;
  }

  /**
   * Снимок формы для черновика. Поля с пустыми значениями отбрасываются, чтобы дата «по
   * умолчанию сегодня» (её проставляет cancelEdit) сама по себе не считалась черновиком.
   */
  private collectDraftPayload(): DraftPayload {
    const form: Record<string, any> = {};
    Object.entries(this.form).forEach(([key, value]) => {
      if (!this.isEmpty(value)) form[key] = value;
    });

    // Первый шаг zip-picker'а живёт в отдельном сигнале, в form его нет — сохраняем отдельно,
    // иначе при восстановлении заказа не будет выбран парт-номер.
    return { form, zipPartNumId: this.zipPartNumId() };
  }

  private applyDraftPayload(payload: DraftPayload) {
    this.form = { ...this.form, ...payload.form };
    if (payload.zipPartNumId != null) this.zipPartNumId.set(payload.zipPartNumId);
    this.refreshPartNumStock();
  }

  /** Ставит черновик в очередь на сохранение (вызывается из шаблона на input/change). */
  scheduleDraftSave() {
    this.drafts.schedule();
  }

  /** Явно очистить черновик и форму по кнопке. */
  clearDraft() {
    const endpoint = this.current()?.endpoint;
    if (!endpoint) return;
    this.drafts.discard(endpoint);
    this.cancelEdit();
  }

  //#endregion

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
    this.closeQuickAddGroupSuggestions();
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
    this.closeQuickAddGroupSuggestions();
  }

  submitQuickAdd() {
    const kind = this.quickAddKind();
    if (!kind) return;

    // Группа вводится текстом — сперва связываем её с id, как и парт-номер на основной форме.
    if (kind === 'part-number') {
      this.resolveQuickAddGroup(() => this.continueSubmitQuickAdd(kind));
      return;
    }

    this.continueSubmitQuickAdd(kind);
  }

  private continueSubmitQuickAdd(kind: QuickAddKind) {
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
      if (res.groupId != null) {
        this.form['groupId'] = res.groupId;
        // Группа на форме Запчасти — текстовое поле (group-picker), поэтому кроме id
        // нужно подставить и отображаемое название.
        const group = (this.references()['groups'] || []).find((g: any) => String(g.id) === String(res.groupId));
        if (group) this.form['groupName'] = group.groupName;
      }
      this.refreshPartNumStock();
    } else if (kind === 'group') {
      this.form['groupId'] = res.id;
      this.form['groupName'] = res.groupName;
    } else if (kind === 'incomemoto') {
      this.form['incomeMotoId'] = res.id;
    }

    // Значения проставлены из модалки, минуя input/change основной формы.
    this.scheduleDraftSave();
  }

  //#endregion

  //#region [Вложенное добавление группы поверх модалки «Новый парт-номер»]

  /**
   * Отдельное состояние, а не переиспользование quickAddKind: пока эта модалка
   * открыта, «Новый парт-номер» должен остаться открытым под ней (со всем, что
   * пользователь уже успел ввести), а не подмениться.
   */
  groupQuickAddOpen = signal(false);
  groupQuickAddName = '';
  groupQuickAddBusy = signal(false);
  groupQuickAddError = signal('');

  openGroupQuickAdd() {
    this.groupQuickAddName = '';
    this.groupQuickAddError.set('');
    this.groupQuickAddOpen.set(true);
  }

  closeGroupQuickAdd() {
    this.groupQuickAddOpen.set(false);
    this.groupQuickAddName = '';
    this.groupQuickAddError.set('');
  }

  submitGroupQuickAdd() {
    const groupName = this.groupQuickAddName.trim();
    if (!groupName) {
      this.groupQuickAddError.set('Укажите название группы');
      return;
    }

    this.groupQuickAddBusy.set(true);
    this.groupQuickAddError.set('');
    this.admin.add('groups', { groupName }).subscribe({
      next: (res: any) => {
        this.groupQuickAddBusy.set(false);
        // Записываем в форму модалки «Новый парт-номер», которая осталась открытой.
        // Группа там — текстовое поле, поэтому кроме id подставляем и название.
        this.quickAddForm['groupId'] = res.id;
        this.quickAddForm['groupName'] = res.groupName;
        this.loadAllReferences();
        this.closeGroupQuickAdd();
      },
      error: (err) => {
        this.groupQuickAddBusy.set(false);
        this.groupQuickAddError.set(err.error?.message || err.message);
      }
    });
  }

  //#endregion

  //#region [Применимость парт-номера к моделям и сериям]

  /**
   * Желаемый состав связей. Редактирует его ApplicabilityEditorComponent, сохраняет —
   * ApplicabilitySyncService; здесь список живёт потому, что уезжает на сервер
   * вместе с самим парт-номером (см. saveRow).
   */
  stagedModels = signal<StagedModelLink[]>([]);
  stagedSeries = signal<StagedSeriesLink[]>([]);

  /** Связи, которые были у записи ДО открытия формы — нужны, чтобы понять, что удалено. */
  private originalModelLinkIds: number[] = [];
  private originalSeriesLinkIds: number[] = [];

  private readonly applicability = inject(ApplicabilitySyncService);

  /** Редактор живёт в двух местах шаблона (форма парт-номеров и модалка «+»), берём любой доступный. */
  @ViewChild(ApplicabilityEditorComponent) private applicabilityEditor?: ApplicabilityEditorComponent;

  /** Заполняет список уже существующими связями редактируемого парт-номера. */
  private loadStagedApplicabilityForEdit(partNumId: number) {
    const { models, series } = this.applicability.loadForEdit(partNumId, this.references());
    this.stagedModels.set(models);
    this.stagedSeries.set(series);
    this.originalModelLinkIds = models.map(m => m.applicabilityId!);
    this.originalSeriesLinkIds = series.map(s => s.applicabilityId!);
  }

  private resetStagedApplicability() {
    this.stagedModels.set([]);
    this.stagedSeries.set([]);
    this.originalModelLinkIds = [];
    this.originalSeriesLinkIds = [];
    // Недопечатанный ввод живёт внутри редактора — сбрасываем его там.
    this.applicabilityEditor?.reset();
  }

  /** Вызывается после успешного сохранения парт-номера: связям нужен его id. */
  private async syncApplicability(
    partNumId: number,
    models: StagedModelLink[],
    series: StagedSeriesLink[],
    originalModelLinkIds: number[],
    originalSeriesLinkIds: number[]
  ) {
    try {
      await this.applicability.sync(
        partNumId, models, series, originalModelLinkIds, originalSeriesLinkIds, this.references());
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
    this.applyZipSellCost(this.form['zipId']);
  }

  /** Шаг 2: пользователь уточнил конкретный экземпляр запчасти из нескольких. */
  onZipCandidateChange(zipId: any) {
    this.form['zipId'] = zipId;
    this.applyZipSellCost(zipId);
  }

  /**
   * Цена продажи и прайс-цена заказа подставляются из цены выбранной запчасти — обе сразу
   * равны, поэтому скидка на этот момент нулевая. Прайс-цена дальше не меняется (readonly)
   * и служит единственной опорной точкой для пересчёта Цены продажи / Скидки / Скидки в %
   * друг из друга (см. recalcOrderPricing).
   */
  private applyZipSellCost(zipId: any) {
    if (this.isEmpty(zipId)) return;
    const zip = (this.references()['zip'] ?? []).find(z => String(z.id) === String(zipId));
    if (zip && zip.sellCost != null) {
      this.form['sellCost'] = zip.sellCost;
      this.form['priceCost'] = zip.sellCost;
      this.recalcOrderPricing('sellCost');
    }
  }

  /** Правки числовых полей формы; у заказа Цена продажи/Скидка/Скидка в % пересчитывают друг друга. */
  onNumberFieldChange(table: TableDef, key: string, value: any) {
    this.form[key] = value;
    if (table.endpoint === 'orders' && (key === 'sellCost' || key === 'discount' || key === 'discountPercent')) {
      this.recalcOrderPricing(key);
    }
  }

  /**
   * Цена продажи, Скидка и Скидка в % однозначно выражаются друг через друга через прайс-цену
   * (Скидка = ПрайсЦена − ЦенаПродажи; Скидка% = Скидка / ПрайсЦена × 100). changedKey — какое
   * из трёх полей только что отредактировал пользователь; оставшиеся два пересчитываются от него.
   */
  private recalcOrderPricing(changedKey: 'sellCost' | 'discount' | 'discountPercent') {
    const priceCost = Number(this.form['priceCost']);
    if (!Number.isFinite(priceCost) || priceCost === 0) return;

    if (changedKey === 'sellCost') {
      const sellCost = Number(this.form['sellCost']);
      if (!Number.isFinite(sellCost)) {
        this.form['discount'] = null;
        this.form['discountPercent'] = null;
        return;
      }
      const discount = priceCost - sellCost;
      this.form['discount'] = this.round2(discount);
      this.form['discountPercent'] = this.round1((discount / priceCost) * 100);
    } else if (changedKey === 'discount') {
      const discount = Number(this.form['discount']);
      if (!Number.isFinite(discount)) {
        this.form['sellCost'] = this.round2(priceCost);
        this.form['discountPercent'] = 0;
        return;
      }
      this.form['sellCost'] = this.round2(priceCost - discount);
      this.form['discountPercent'] = this.round1((discount / priceCost) * 100);
    } else {
      const percent = Number(this.form['discountPercent']);
      if (!Number.isFinite(percent)) {
        this.form['sellCost'] = this.round2(priceCost);
        this.form['discount'] = 0;
        return;
      }
      const discount = (percent / 100) * priceCost;
      this.form['discount'] = this.round2(discount);
      this.form['sellCost'] = this.round2(priceCost - discount);
    }
  }

  private round2(v: number): number {
    return Math.round(v * 100) / 100;
  }

  private round1(v: number): number {
    return Math.round(v * 10) / 10;
  }

  /** Восстанавливает первый шаг по уже сохранённой в заказе запчасти. */
  private syncZipPickerFromForm() {
    const zipId = this.form['zipId'];
    if (!zipId) { this.zipPartNumId.set(null); return; }
    const zip = (this.references()['zip'] ?? []).find(z => String(z.id) === String(zipId));
    this.zipPartNumId.set(zip ? zip.partNumId : null);
  }

  //#endregion

  //#region [Печать QR-кода]

  /** Утилита, а не CRUD-таблица — активна вместо current(), не среди tables. */
  qrLabelTabActive = signal(false);
  qrLabelPartNumId = signal<number | null>(null);
  qrLabelZipId = signal<string | null>(null);
  qrLabelBusy = signal(false);

  // Прежний вариант: печать была доступна только для запчастей, по которым уже был
  // хотя бы один заказ. Сейчас не используется — печатаем по любой заведённой запчасти,
  // независимо от заказов. Оставлено на случай, если ограничение понадобится вернуть.
  // orderedZipIds = computed(() => {
  //   const ids = new Set<string>();
  //   for (const o of this.references()['orders'] ?? []) {
  //     if (o['zipId']) ids.add(String(o['zipId']));
  //   }
  //   return ids;
  // });

  /** Парт-номера, под которыми есть хотя бы одна заведённая запчасть. */
  qrLabelPartNumbers = computed(() => {
    const seen = new Map<number, { partNumId: number; partNum: string; name: string }>();
    for (const z of this.references()['zip'] ?? []) {
      if (z.partNumId != null && !seen.has(z.partNumId)) {
        seen.set(z.partNumId, { partNumId: z.partNumId, partNum: z.partNum, name: z.name });
      }
    }
    return Array.from(seen.values()).sort((a, b) => a.partNum.localeCompare(b.partNum));
  });

  qrLabelCandidates = computed(() => {
    const pn = this.qrLabelPartNumId();
    if (pn === null || pn === undefined) return [];
    return (this.references()['zip'] ?? []).filter(z => z.partNumId === pn);
  });

  qrLabelSelectedZip = computed(() => {
    const id = this.qrLabelZipId();
    if (!id) return null;
    return (this.references()['zip'] ?? []).find(z => String(z.id) === String(id)) ?? null;
  });

  openQrLabelTab() {
    this.current.set(null);
    this.qrLabelTabActive.set(true);
    this.error.set('');
    this.message.set('');
    this.qrLabelPartNumId.set(null);
    this.qrLabelZipId.set(null);
  }

  onQrLabelPartNumChange(partNumId: number | null) {
    this.qrLabelPartNumId.set(partNumId);
    const candidates = this.qrLabelCandidates();
    this.qrLabelZipId.set(candidates.length === 1 ? String(candidates[0].id) : null);
  }

  /**
   * Открываем пустую вкладку синхронно по клику — иначе браузер расценит её как
   * попап и заблокирует, если подставлять PDF в новую вкладку уже после ответа сервера.
   */
  printQrLabel() {
    const zipId = this.qrLabelZipId();
    if (!zipId) return;

    this.error.set('');
    this.qrLabelBusy.set(true);
    const tab = window.open('', '_blank');

    this.admin.printQrLabel(zipId).subscribe({
      next: (blob) => {
        this.qrLabelBusy.set(false);
        if (tab) tab.location.href = URL.createObjectURL(blob);
      },
      error: (err) => {
        this.qrLabelBusy.set(false);
        tab?.close();
        this.error.set('Не удалось получить PDF: ' + (err.error?.message || err.message));
      }
    });
  }

  //#endregion


  busy = signal<boolean>(false);
  message = signal<string>('');
  error = signal<string>('');

  ngOnInit() {
    // Сервис черновиков сам ничего не знает про поля формы — отдаём ему три коротких коллбэка.
    this.drafts.attach({
      currentFormKey: () => this.currentFormKey(),
      collect: () => this.collectDraftPayload(),
      apply: (payload) => this.applyDraftPayload(payload)
    });

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
      // Прежде здесь грузились 'orders' — «Печать QR-кода» показывала только уже заказанные
      // запчасти (см. закомментированный orderedZipIds). Теперь список строится по 'zip',
      // поэтому лишний запрос всего списка заказов при открытии админки не нужен.
      // 'orders'
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

  select(t: TableDef) {
    // Незавершённый ввод предыдущей вкладки досохраняем до сброса формы.
    this.drafts.flush();

    this.qrLabelTabActive.set(false);
    this.current.set(t);
    this.cancelEdit();
    // Условия поиска сбрасывает сама таблица, когда меняется её table (ngOnChanges).
    this.reload(t);
    this.drafts.restore(t.endpoint);
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

    // Заказ: при выборе покупателя подставляем его адрес, если он есть.
    // Если у покупателя несколько адресов — берём последний добавленный (обычно актуальный).
    if (table.endpoint === 'orders' && key === 'userId') {
      const addresses = (this.references()['addressess'] || [])
        .filter(a => !this.isEmpty(value) && String(a.userId) === String(value));
      this.form['addressId'] = addresses.length > 0 ? addresses[addresses.length - 1].id : null;
    }
  }

  /**
   * Список опций для select-поля. Для addressId в заказе — только адреса выбранного
   * покупателя (form['userId']), чтобы не путать одинаковым списком независимо от
   * того, кто выбран. Если у покупателя нет своих адресов — список пуст: чужие
   * адреса выбирать нельзя, сначала нужно завести адрес для этого покупателя
   * (вкладка «Адреса доставки»).
   */
  refOptionsFor(f: FieldDef): any[] {
    const list = this.references()[f.refTable!] || [];
    if (f.filterByUserId) {
      const uid = this.form['userId'];
      if (this.isEmpty(uid)) return [];
      return list.filter(opt => String(opt.userId) === String(uid));
    }
    return list;
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
    this.form['groupName'] = pn.group ?? '';

    // Цены берём с последней заведённой запчасти с этим же парт-номером — как подсказку.
    const sameZip = (this.references()['zip'] || [])
      .filter(z => String(z.partNumId) === String(partNumId))
      .pop();
    if (sameZip) {
      if (this.isEmpty(this.form['incomeCost'])) this.form['incomeCost'] = sameZip.incomeCost;
      if (this.isEmpty(this.form['sellCost'])) this.form['sellCost'] = sameZip.sellCost;
    }
  }

  //#region [Группа запчастей вручную]

  activeGroupPicker = signal(false);
  groupSuggestions = signal<{ id: number; groupName: string }[]>([]);

  /** Подсказки ищутся по справочнику groups; form['groupId'] пересчитывается только при сохранении (см. resolveGroup). */
  onGroupInput(value: string) {
    this.form['groupName'] = value;
    this.activeGroupPicker.set(true);

    const needle = (value ?? '').toString().trim().toLowerCase();
    const list = this.references()['groups'] || [];
    const filtered = needle.length === 0
      ? list
      : list.filter((g: any) => String(g.groupName ?? '').toLowerCase().includes(needle));

    this.groupSuggestions.set(filtered.slice(0, 8));
  }

  applyGroupSuggestion(g: { id: number; groupName: string }) {
    this.form['groupName'] = g.groupName;
    this.form['groupId'] = g.id;
    this.closeGroupSuggestions();
    // Клик по подсказке не порождает input/change на форме — ставим черновик в очередь вручную.
    this.scheduleDraftSave();
  }

  private closeGroupSuggestions() {
    this.activeGroupPicker.set(false);
    this.groupSuggestions.set([]);
  }

  /**
   * То же самое, но для поля «Группа запчастей» внутри модалки «Новый парт-номер»
   * (открывается кнопкой «+» у Парт-номера/Наименования) — своё состояние, так как
   * модалка оперирует quickAddForm, а не form.
   */
  activeQuickAddGroupPicker = signal(false);
  quickAddGroupSuggestions = signal<{ id: number; groupName: string }[]>([]);

  onQuickAddGroupInput(value: string) {
    this.quickAddForm['groupName'] = value;
    this.activeQuickAddGroupPicker.set(true);

    const needle = (value ?? '').toString().trim().toLowerCase();
    const list = this.references()['groups'] || [];
    const filtered = needle.length === 0
      ? list
      : list.filter((g: any) => String(g.groupName ?? '').toLowerCase().includes(needle));

    this.quickAddGroupSuggestions.set(filtered.slice(0, 8));
  }

  applyQuickAddGroupSuggestion(g: { id: number; groupName: string }) {
    this.quickAddForm['groupName'] = g.groupName;
    this.quickAddForm['groupId'] = g.id;
    this.closeQuickAddGroupSuggestions();
  }

  private closeQuickAddGroupSuggestions() {
    this.activeQuickAddGroupPicker.set(false);
    this.quickAddGroupSuggestions.set([]);
  }

  /**
   * Связывает введённый текст группы (quickAddForm) с записью в groups перед
   * созданием парт-номера — тот же принцип, что и resolveGroup для основной формы.
   */
  private resolveQuickAddGroup(next: () => void) {
    const groupText = (this.quickAddForm['groupName'] ?? '').toString().trim();
    if (!groupText) {
      this.quickAddForm['groupId'] = null;
      next();
      return;
    }

    const existing = (this.references()['groups'] || [])
      .find((g: any) => String(g.groupName).trim().toLowerCase() === groupText.toLowerCase());

    if (existing) {
      this.quickAddForm['groupName'] = existing.groupName;
      this.quickAddForm['groupId'] = existing.id;
      next();
      return;
    }

    this.quickAddBusy.set(true);
    this.admin.add('groups', { groupName: groupText }).subscribe({
      next: (g: any) => {
        this.quickAddForm['groupId'] = g.id;
        this.quickAddBusy.set(false);
        this.loadAllReferences();
        next();
      },
      error: (err) => {
        this.quickAddBusy.set(false);
        this.quickAddError.set('Не удалось создать группу: ' + (err.error?.message || err.message));
      }
    });
  }

  //#endregion

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
    // Клик по подсказке не порождает input/change на форме — ставим черновик в очередь вручную.
    this.scheduleDraftSave();
  }

  private closeCatalogSuggestions() {
    this.activeCatalogField.set(null);
    this.catalogSuggestions.set([]);
  }

  /**
   * Связывает введённый текст группы с записью в groups перед сохранением запчасти:
   * точное совпадение — переиспользуем её id, иначе заводим новую группу. Пустой текст
   * снимает привязку. Выполняется перед resolvePartNumber, так как groupId нужен уже
   * при заведении нового парт-номера.
   */
  private resolveGroup(next: () => void) {
    const groupText = (this.form['groupName'] ?? '').toString().trim();
    if (!groupText) {
      this.form['groupId'] = null;
      next();
      return;
    }

    const existing = (this.references()['groups'] || [])
      .find((g: any) => String(g.groupName).trim().toLowerCase() === groupText.toLowerCase());

    if (existing) {
      this.form['groupName'] = existing.groupName;
      this.form['groupId'] = existing.id;
      next();
      return;
    }

    this.busy.set(true);
    this.admin.add('groups', { groupName: groupText }).subscribe({
      next: (g: any) => {
        this.form['groupId'] = g.id;
        this.busy.set(false);
        this.loadAllReferences();
        next();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set('Не удалось создать группу: ' + (err.error?.message || err.message));
      }
    });
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

  protected isEmpty(v: any): boolean {
    return v === null || v === undefined || v === '';
  }

  //#endregion

  //#region [Line actions]
  editRow(row: any) {
    this.selectedId.set(row.id);
    this.form = { ...row };
    this.selectedFiles.set([]);
    this.existingPhotos.set(Array.isArray(row.photos) ? row.photos : []);

    // orderDateTime приходит с бэкенда как полный DateTimeOffset (с временем и
    // смещением) — input[type=date] понимает только YYYY-MM-DD, обрезаем.
    if (this.current()?.endpoint === 'orders' && typeof this.form['orderDateTime'] === 'string') {
      this.form['orderDateTime'] = this.form['orderDateTime'].slice(0, 10);
    }

    // Группа вводится текстом (group-picker) — подставляем название для отображения,
    // отдельно от groupId, который остаётся числовым идентификатором.
    if (this.current()?.endpoint === 'zip') {
      this.form['groupName'] = row.group ?? '';
    }

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
    this.closeGroupSuggestions();
    this.currentPartNumStock.set(0);
    this.selectedFiles.set([]);
    this.existingPhotos.set([]);
    this.zipPartNumId.set(null);
    this.resetStagedApplicability();

    // Дата поступления / дата заказа по умолчанию — сегодня, чтобы не проставлять
    // вручную на каждой новой записи. Только для добавления: при редактировании
    // существующей записи форму заполняет editRow из данных самой записи.
    if (this.current()?.endpoint === 'zip') {
      this.form['incomeDate'] = this.todayIso();
    }
    if (this.current()?.endpoint === 'orders') {
      this.form['orderDateTime'] = this.todayIso();
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

    // Форма отправляется ещё и по Enter в поле — одной спрятанной кнопки мало.
    if (this.selectedId() && !this.canModify) {
      this.error.set('Изменение существующих записей доступно только администратору');
      return;
    }

    // Группа и парт-номер вводятся вручную — сперва связываем введённый текст
    // с id существующей записи или заводим новую, и только потом продолжаем как раньше.
    if (table.endpoint === 'zip') {
      this.resolveGroup(() => this.resolvePartNumber(() => this.continueSave(table)));
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
        // Данные доехали до основной таблицы — черновик больше не нужен.
        this.drafts.discard(table.endpoint);
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
    if (!this.canModify) return;
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
    // Клик по подсказке не порождает input/change на форме — ставим черновик в очередь вручную.
    this.scheduleDraftSave();
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