import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { StagedModelLink, StagedSeriesLink } from './admin.types';

/**
 * Редактор применимости парт-номера: к каким моделям и сериям он подходит.
 *
 * Компонент только редактирует список — ничего не сохраняет. Связи уезжают на сервер
 * вместе с самим парт-номером (см. ApplicabilitySyncService), потому что до сохранения
 * у новой каталожной позиции ещё нет id, к которому их можно привязать.
 *
 * Марку и модель можно вводить руками: если таких ещё нет в справочнике, они будут
 * созданы при сохранении — поэтому подсказки здесь не ограничивают ввод, а только ускоряют.
 */
@Component({
  selector: 'app-applicability-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Те же стили, что у AdminComponent (.chips, .chip-add-row, .suggestions-dropdown) —
  // эмуляция инкапсуляции Angular не пускает их из родителя в шаблон дочернего компонента.
  styleUrls: ['../styles/admin.component.css'],
  template: `
    <label>Модели</label>
    <div class="chips">
      @for (m of models; track $index) {
        <span class="chip">
          {{ m.markName ? m.markName + ' ' : '' }}{{ m.modelName }}
          <button type="button" (click)="removeModel($index)">✕</button>
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
          [(ngModel)]="newMarkName"
          name="newModelMark"
          (input)="onMarkInput(newMarkName)"
          (focus)="onMarkInput(newMarkName)"
          autocomplete="off"
        >
        @if (activeMarkSuggest() && markSuggestions().length > 0) {
          <ul class="suggestions-dropdown">
            @for (mk of markSuggestions(); track mk.id) {
              <li (click)="selectMark(mk)">{{ mk.mark }}</li>
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
          (input)="onModelInput(newModelName)"
          (focus)="onModelInput(newModelName)"
          autocomplete="off"
        >
        @if (activeModelSuggest() && modelSuggestions().length > 0) {
          <ul class="suggestions-dropdown">
            @for (md of modelSuggestions(); track md.id) {
              <li (click)="selectModel(md)">{{ md.mark ? md.mark + ' — ' : '' }}{{ md.model }}</li>
            }
          </ul>
        }
      </div>
      <button type="button" class="btn-chip-add" (click)="addModel()">Добавить</button>
    </div>
    <small class="owned-hint">Новые марка/модель будут созданы автоматически, если их ещё нет</small>

    <label style="margin-top: 14px;">Серии</label>
    <div class="chips">
      @for (s of series; track $index) {
        <span class="chip">
          {{ s.seriesName }}
          <button type="button" (click)="removeSeries($index)">✕</button>
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
          (input)="onSeriesInput(newSeriesName)"
          (focus)="onSeriesInput(newSeriesName)"
          autocomplete="off"
        >
        @if (activeSeriesSuggest() && seriesSuggestions().length > 0) {
          <ul class="suggestions-dropdown">
            @for (sr of seriesSuggestions(); track sr.id) {
              <li (click)="selectSeries(sr)">{{ sr.seriesName }}</li>
            }
          </ul>
        }
      </div>
      <button type="button" class="btn-chip-add" (click)="addSeries()">Добавить</button>
    </div>
    <small class="owned-hint">Новая серия будет создана автоматически, если её ещё нет</small>
  `
})
export class ApplicabilityEditorComponent {
  /** Справочники marks/models/series — из них строятся подсказки. */
  @Input({ required: true }) references: Record<string, any[]> = {};

  @Input() models: StagedModelLink[] = [];
  @Output() modelsChange = new EventEmitter<StagedModelLink[]>();

  @Input() series: StagedSeriesLink[] = [];
  @Output() seriesChange = new EventEmitter<StagedSeriesLink[]>();

  /** Сообщения вида «Введите название модели» показывает родитель — у него общая плашка ошибок. */
  @Output() error = new EventEmitter<string>();

  newMarkName = '';
  newModelName = '';
  newSeriesName = '';

  markSuggestions = signal<{ id: number; mark: string }[]>([]);
  activeMarkSuggest = signal(false);
  modelSuggestions = signal<{ id: number; model: string; markId: number | null; mark: string | null }[]>([]);
  activeModelSuggest = signal(false);
  seriesSuggestions = signal<{ id: string; seriesName: string }[]>([]);
  activeSeriesSuggest = signal(false);

  /** Сброс при закрытии формы: родитель обнуляет списки, а недопечатанный ввод живёт здесь. */
  reset() {
    this.newMarkName = '';
    this.newModelName = '';
    this.newSeriesName = '';
    this.closeModelSuggestions();
    this.closeSeriesSuggestions();
  }

  private suggest<T>(list: T[], needle: string, text: (item: T) => string): T[] {
    const q = (needle ?? '').trim().toLowerCase();
    const filtered = q.length === 0 ? list : list.filter(item => text(item).toLowerCase().includes(q));
    return filtered.slice(0, 8);
  }

  // ---------- Модели ----------

  onMarkInput(value: string) {
    this.newMarkName = value;
    this.activeMarkSuggest.set(true);
    this.markSuggestions.set(this.suggest(this.references['marks'] ?? [], value, m => String(m.mark)));
  }

  selectMark(mk: { id: number; mark: string }) {
    this.newMarkName = mk.mark;
    this.activeMarkSuggest.set(false);
    this.markSuggestions.set([]);
  }

  onModelInput(value: string) {
    this.newModelName = value;
    this.activeModelSuggest.set(true);
    this.modelSuggestions.set(this.suggest(this.references['models'] ?? [], value, m => String(m.model)));
  }

  /** Выбор подсказки модели заодно подставляет её марку — вводить её отдельно не нужно. */
  selectModel(md: { id: number; model: string; markId: number | null; mark: string | null }) {
    this.newModelName = md.model;
    if (md.mark) this.newMarkName = md.mark;
    this.activeModelSuggest.set(false);
    this.modelSuggestions.set([]);
  }

  /**
   * Добавляет модель в локальный список. Если название совпадает с уже существующей
   * маркой/моделью — запоминает их id (тогда при сохранении новых записей создавать
   * не придётся), иначе id остаются не заданы, и синхронизация заведёт их сама.
   */
  addModel() {
    const markName = this.newMarkName.trim();
    const modelName = this.newModelName.trim();
    if (!modelName) {
      this.error.emit('Введите название модели');
      return;
    }

    const alreadyAdded = this.models.some(m =>
      m.modelName.toLowerCase() === modelName.toLowerCase() &&
      (m.markName || '').toLowerCase() === markName.toLowerCase());
    if (alreadyAdded) {
      this.newMarkName = '';
      this.newModelName = '';
      this.closeModelSuggestions();
      return;
    }

    const existingMark = (this.references['marks'] ?? [])
      .find((m: any) => String(m.mark).toLowerCase() === markName.toLowerCase());
    const existingModel = (this.references['models'] ?? [])
      .find((m: any) => String(m.model).toLowerCase() === modelName.toLowerCase()
        && (!existingMark || m.markId === existingMark.id));

    this.modelsChange.emit([...this.models, {
      markId: existingMark?.id,
      markName: existingMark?.mark ?? markName,
      modelId: existingModel?.id,
      modelName: existingModel?.model ?? modelName
    }]);

    this.newMarkName = '';
    this.newModelName = '';
    this.closeModelSuggestions();
  }

  removeModel(index: number) {
    const list = [...this.models];
    list.splice(index, 1);
    this.modelsChange.emit(list);
  }

  private closeModelSuggestions() {
    this.activeMarkSuggest.set(false);
    this.markSuggestions.set([]);
    this.activeModelSuggest.set(false);
    this.modelSuggestions.set([]);
  }

  // ---------- Серии ----------

  onSeriesInput(value: string) {
    this.newSeriesName = value;
    this.activeSeriesSuggest.set(true);
    this.seriesSuggestions.set(this.suggest(this.references['series'] ?? [], value, s => String(s.seriesName)));
  }

  selectSeries(sr: { id: string; seriesName: string }) {
    this.newSeriesName = sr.seriesName;
    this.activeSeriesSuggest.set(false);
    this.seriesSuggestions.set([]);
  }

  addSeries() {
    const seriesName = this.newSeriesName.trim();
    if (!seriesName) {
      this.error.emit('Введите название серии');
      return;
    }

    const alreadyAdded = this.series.some(s => s.seriesName.toLowerCase() === seriesName.toLowerCase());
    if (alreadyAdded) {
      this.newSeriesName = '';
      this.closeSeriesSuggestions();
      return;
    }

    const existing = (this.references['series'] ?? [])
      .find((s: any) => String(s.seriesName).toLowerCase() === seriesName.toLowerCase());

    this.seriesChange.emit([...this.series, {
      seriesId: existing?.id,
      seriesName: existing?.seriesName ?? seriesName
    }]);

    this.newSeriesName = '';
    this.closeSeriesSuggestions();
  }

  removeSeries(index: number) {
    const list = [...this.series];
    list.splice(index, 1);
    this.seriesChange.emit(list);
  }

  private closeSeriesSuggestions() {
    this.activeSeriesSuggest.set(false);
    this.seriesSuggestions.set([]);
  }
}
