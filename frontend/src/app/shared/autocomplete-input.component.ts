import { ChangeDetectionStrategy, Component, computed, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface AutocompleteOption {
  /** Что попадёт в поле при выборе. */
  value: string;
  /** Основная строка подсказки. */
  label: string;
  /** Второстепенная строка справа — например, парт-номер рядом с наименованием. */
  hint?: string;
}

/** Список деталей длинный, а выбирают всегда из первых попавшихся. */
const MAX_SUGGESTIONS = 20;

/**
 * Поле ввода с подсказками из готового списка.
 *
 * Текст и выбор разведены намеренно: `value` — это то, что набрано в поле (для фильтров
 * по подстроке этого достаточно), а `picked` срабатывает только когда пользователь выбрал
 * вариант из списка. Отчётам, которые строятся по id детали, нужно именно второе — они
 * запоминают выбор отдельно и сбрасывают его на любой правке текста.
 */
@Component({
  selector: 'app-autocomplete-input',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ac-wrap">
      <input type="text" class="form-control"
             [ngModel]="value()"
             (ngModelChange)="onInput($event)"
             (focus)="open()"
             (blur)="isOpen.set(false)"
             (keydown)="onKeydown($event)"
             [placeholder]="placeholder()"
             autocomplete="off">

      @if (isOpen() && suggestions().length) {
        <ul class="ac-list">
          @for (o of suggestions(); track o.value; let i = $index) {
            <!-- mousedown, а не click: click пришёл бы уже после blur, когда список
                 свёрнут, и выбор бы не сработал. -->
            <li [class.active]="i === activeIndex()"
                (mousedown)="pick(o)"
                (mouseenter)="activeIndex.set(i)">
              <span>{{ o.label }}</span>
              @if (o.hint) { <span class="ac-hint">{{ o.hint }}</span> }
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .ac-wrap { position: relative; }

    .form-control {
      width: 100%;
      padding: 8px 12px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 14px;
      box-sizing: border-box;
    }

    .ac-list {
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      z-index: 20;
      margin: 2px 0 0;
      padding: 0;
      list-style: none;
      max-height: 260px;
      overflow-y: auto;
      background: #fff;
      border: 1px solid #ccc;
      border-radius: 4px;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
    }
    .ac-list li {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      padding: 7px 12px;
      font-size: 14px;
      cursor: pointer;
    }
    .ac-list li.active { background: #eef5ff; }
    .ac-hint { color: #777; white-space: nowrap; }
  `]
})
export class AutocompleteInputComponent {
  /** Текст в поле. Двусторонний: родитель кладёт сюда фильтр или подписанный выбор. */
  value = model('');

  options = input<AutocompleteOption[]>([]);
  placeholder = input('');

  /** Только выбор из списка — набранный вручную текст сюда не попадает. */
  picked = output<AutocompleteOption>();

  isOpen = signal(false);
  activeIndex = signal(0);

  suggestions = computed<AutocompleteOption[]>(() => {
    const q = this.value().trim().toLowerCase();
    const all = this.options();
    const matched = q
      ? all.filter(o => o.label.toLowerCase().includes(q) || (o.hint ?? '').toLowerCase().includes(q))
      : all;
    return matched.slice(0, MAX_SUGGESTIONS);
  });

  onInput(text: string) {
    this.value.set(text);
    this.activeIndex.set(0);
    this.isOpen.set(true);
  }

  open() {
    this.activeIndex.set(0);
    this.isOpen.set(true);
  }

  pick(option: AutocompleteOption) {
    this.value.set(option.value);
    this.isOpen.set(false);
    this.picked.emit(option);
  }

  onKeydown(event: KeyboardEvent) {
    const items = this.suggestions();

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.isOpen.set(true);
        this.activeIndex.set(Math.min(this.activeIndex() + 1, items.length - 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeIndex.set(Math.max(this.activeIndex() - 1, 0));
        break;
      case 'Enter':
        if (this.isOpen() && items[this.activeIndex()]) {
          event.preventDefault();
          this.pick(items[this.activeIndex()]);
        }
        break;
      case 'Escape':
        this.isOpen.set(false);
        break;
    }
  }
}
