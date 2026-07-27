import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { CatalogService, SearchFilters } from '../core/catalog.service';
import { OrdersService } from '../core/orders.service';
import { AuthService } from '../core/auth.service';
import { AddressDto, GroupDto, MarkDto, ModelDto, PagedResult, ZipDto } from '../core/models';
import { NgxMaskDirective } from 'ngx-mask'; // Импорт маски
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';

@Component({
  selector: 'app-home',
  // ДОБАВЛЕНО: NgxMaskDirective в массив imports
  imports: [FormsModule, CurrencyPipe, NgxMaskDirective],
  template: `
    <section class="search-panel card">
      <h1>Запчасти для мотоциклов</h1>
      
      <div class="search-row" style="position: relative;">
        <input
          type="text"
          placeholder="Поиск по названию, марке, модели или парт-номеру…"
          [(ngModel)]="filters.query"
          (ngModelChange)="onSearchInput($event)"
          (keyup.enter)="search(1); suggestions.set([])" />
        <button class="btn" (click)="search(1); suggestions.set([])">Найти</button>

        @if (suggestions().length > 0) {
          <ul class="suggestions-dropdown">
            @for (item of suggestions(); track item.id) {
              <li (click)="selectSuggestion(item)">
                <span class="suggestion-name">{{ item.name }}</span>
                @if (item.partNumber) {
                  <span class="muted suggestion-pn">{{ item.partNumber }}</span>
                }
              </li>
            }
          </ul>
        }
      </div>
      
      <div class="filters">
        <select [(ngModel)]="filters.markId" (ngModelChange)="onMarkChange()">
          <option [ngValue]="undefined">Марка мотоцикла</option>
          @for (m of marks(); track m.id) {
            <option [ngValue]="m.id">{{ m.mark }}</option>
          }
        </select>
        <select [(ngModel)]="filters.modelId">
          <option [ngValue]="undefined">Модель мотоцикла</option>
          @for (m of models(); track m.id) {
            <option [ngValue]="m.id">{{ m.model }}</option>
          }
        </select>
        <select [(ngModel)]="filters.groupId">
          <option [ngValue]="undefined">Группа ZIP-запчастей</option>
          @for (g of groups(); track g.id) {
            <option [ngValue]="g.id">{{ g.groupName }}</option>
          }
        </select>
        <input 
          type="text" 
          [(ngModel)]="filters.year" 
          placeholder="ГГГГ" 
          mask="0000"
          class="form-control">
        <input type="text" placeholder="Part number" [(ngModel)]="filters.partNumber" />
        <button class="btn btn-secondary" (click)="reset()">Сбросить</button>
      </div>
    </section>

    <section class="results">
      @if (result(); as r) {
        <p class="muted">Найдено: {{ r.total }}</p>
        <div class="grid">
          @for (zip of r.items; track zip.id) {
            <div class="card zip-card">
              <h3>{{ zip.name }}</h3>
              <p class="muted">
                @if (zip.mark) { <span>{{ zip.mark }}</span> }
                @if (zip.model) { <span> · {{ zip.model }}</span> }
                @if (zip.year) { <span> · {{ zip.year }} г.</span> }
              </p>
              @if (zip.partNumber) { <p class="pn">Part number: {{ zip.partNumber }}</p> }
              @if (zip.group) { <p class="muted">Группа: {{ zip.group }}</p> }
              <div class="zip-footer">
                <span class="price">{{ zip.incomeCost | currency:'RUB':'symbol-narrow':'1.0-0' }}</span>
                @if (zip.countStored > 0) {
                  <button class="btn" (click)="openBuy(zip)">Купить</button>
                } @else {
                  <span class="muted">Нет в наличии</span>
                }
              </div>
            </div>
          } @empty {
            <p>По вашему запросу ничего не найдено.</p>
          }
        </div>
        @if (r.totalPages > 1) {
          <div class="pagination">
            <button [disabled]="r.page <= 1" (click)="search(r.page - 1)">‹</button>
            @for (p of pages(r); track p) {
              <button [class.active]="p === r.page" (click)="search(p)">{{ p }}</button>
            }
            <button [disabled]="r.page >= r.totalPages" (click)="search(r.page + 1)">›</button>
          </div>
        }
      }
    </section>

    @if (buying(); as zip) {
      <div class="modal-backdrop" (click)="closeBuy()">
        <div class="card modal" (click)="$event.stopPropagation()">
          <h3>Оформление заказа</h3>
          <p>{{ zip.name }} — <b>{{ zip.incomeCost | currency:'RUB':'symbol-narrow':'1.0-0' }}</b></p>
          <div class="form-field">
            <label>Количество (в наличии {{ zip.countStored }})</label>
            <input type="number" min="1" [max]="zip.countStored" [(ngModel)]="buyCount" />
          </div>
          <div class="form-field">
            <label>Адрес доставки</label>
            <select [(ngModel)]="buyAddressId">
              <option [ngValue]="undefined">— выберите адрес —</option>
              @for (a of adresses(); track a.id) {
                <option [ngValue]="a.id">{{ a.address }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label>…или добавьте новый адрес</label>
            <input type="text" placeholder="Город, улица, дом, квартира" [(ngModel)]="newAddress" />
          </div>
          @if (buyError()) { <p class="error">{{ buyError() }}</p> }
          <div class="modal-actions">
            <button class="btn btn-secondary" (click)="closeBuy()">Отмена</button>
            <button class="btn" [disabled]="busy()" (click)="confirmBuy(zip)">Заказать и оплатить</button>
          </div>
        </div>
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .search-panel { margin-bottom: 24px; }
    .search-row { display: flex; gap: 10px; margin-bottom: 14px; }
    .search-row input { flex: 1; }
    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 10px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }
    .zip-card h3 { font-size: 16px; margin-bottom: 8px; }
    .pn { font-family: monospace; font-size: 13px; }
    .zip-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 12px;
    }
    .price { font-size: 18px; font-weight: 700; }
    .modal-backdrop {
      position: fixed; inset: 0;
      background: rgba(0,0,0,.4);
      display: flex; align-items: center; justify-content: center;
      z-index: 100;
    }
    .modal { width: 420px; max-width: 92vw; }
    .modal-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 16px; }

    /* ДОБАВЛЕНО: Стили для выпадающего списка предложений */
    .suggestions-dropdown {
      position: absolute;
      top: 100%;
      left: 0;
      right: 90px; /* Оставляем место под кнопку Найти */
      background: white;
      border: 1px solid #ccc;
      border-radius: 4px;
      list-style: none;
      padding: 0;
      margin: 4px 0 0 0;
      z-index: 1000;
      box-shadow: 0 4px 12px rgba(0,0,0,0.15);
      max-height: 250px;
      overflow-y: auto;
    }
    .suggestions-dropdown li {
      padding: 10px 14px;
      cursor: pointer;
      border-bottom: 1px solid #f0f0f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .suggestions-dropdown li:hover {
      background: #f8f9fa;
    }
    .suggestion-name { font-weight: 500; }
    .suggestion-pn { font-size: 0.85em; }
  `]
})
export class HomeComponent implements OnInit {
  // ДОБАВЛЕНО: Сигнал и сабжект для автопредложений
  suggestions = signal<ZipDto[]>([]); 
  private searchSubject = new Subject<string>();

  private catalog = inject(CatalogService);
  private orders = inject(OrdersService);
  private auth = inject(AuthService);
  private router = inject(Router);

  filters: SearchFilters = {};
  marks = signal<MarkDto[]>([]);
  models = signal<ModelDto[]>([]);
  groups = signal<GroupDto[]>([]);
  years = signal<number[]>([]);
  result = signal<PagedResult<ZipDto> | null>(null);

  buying = signal<ZipDto | null>(null);
  buyCount = 1;
  buyAddressId?: number;
  newAddress = '';
  adresses = signal<AddressDto[]>([]);
  buyError = signal('');
  busy = signal(false);

  ngOnInit(): void {
    // ДОБАВЛЕНО: Логика обработки ввода для автопредложений
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(query => {
        if (query && query.trim().length > 0) {
          // Ищем подсказки, ограничивая выдачу до 5 элементов (по желанию pageSize можно убрать)
          return this.catalog.search({ ...this.filters, query: query, page: 1, pageSize: 5 }).pipe(
            catchError(() => of({ items: [] })) // Защита от падений, если бэкенд вернул ошибку
          );
        } else {
          return of({ items: [] });
        }
      })
    ).subscribe((res: any) => {
      this.suggestions.set(res.items || []);
    });

    this.search(1);

    this.catalog.models().subscribe(m => this.models.set(m));
    this.catalog.marks().subscribe(m => this.marks.set(m));
    this.catalog.groups().subscribe(g => this.groups.set(g));
  }

  // ДОБАВЛЕНО: Метод, вызываемый при каждом изменении поля ввода
  onSearchInput(query: string | undefined) {
    this.searchSubject.next(query || '');
  }

  // ДОБАВЛЕНО: Обработка клика по предложению из списка
  selectSuggestion(item: ZipDto) {
    this.filters.query = item.name; // Подставляем выбранное имя в строку поиска
    this.suggestions.set([]);       // Скрываем выпадающий список
    this.search(1);                 // Сразу запускаем полноценный поиск и обновляем сетку
  }

  onMarkChange(): void {
    this.filters.modelId = undefined;
    this.catalog.models(this.filters.markId).subscribe(m => this.models.set(m));
  }

  search(page: number): void {
    this.suggestions.set([]); // Скрываем подсказки при принудительном поиске
    this.catalog.search({ ...this.filters, page, pageSize: 12 }).subscribe(r => this.result.set(r));
  }

  reset(): void {
    this.filters = {};
    this.suggestions.set([]);
    this.catalog.models().subscribe(m => this.models.set(m));
    this.search(1);
  }

  pages(r: PagedResult<ZipDto>): number[] {
    const from = Math.max(1, r.page - 3);
    const to = Math.min(r.totalPages, r.page + 3);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  }

  openBuy(zip: ZipDto): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/login']);
      return;
    }
    this.buyError.set('');
    this.buyCount = 1;
    this.newAddress = '';
    this.buying.set(zip);
    this.orders.adresses().subscribe(a => {
      this.adresses.set(a);
      this.buyAddressId = a[0]?.id;
    });
  }

  closeBuy(): void {
    this.buying.set(null);
  }

  confirmBuy(zip: ZipDto): void {
    this.buyError.set('');
    this.busy.set(true);

    const placeOrder = (addressId: number) => {
      this.orders.createOrder(zip.id, this.buyCount, addressId).subscribe({
        next: order => {
          const returnUrl = `${location.origin}/payment-result/${order.id}`;
          this.orders.createPayment(order.id, returnUrl).subscribe({
            next: p => { location.href = p.confirmationUrl; },
            error: err => {
              this.busy.set(false);
              this.buyError.set(err.error?.message ?? 'Заказ создан, но оплату запустить не удалось. Оплатите из личного кабинета.');
            },
          });
        },
        error: err => {
          this.busy.set(false);
          this.buyError.set(err.error?.message ?? 'Не удалось создать заказ');
        },
      });
    };

    if (this.newAddress.trim()) {
      this.orders.addAddress(this.newAddress.trim()).subscribe({
        next: a => placeOrder(a.id),
        error: () => {
          this.busy.set(false);
          this.buyError.set('Не удалось сохранить адрес');
        },
      });
    } else if (this.buyAddressId) {
      placeOrder(this.buyAddressId);
    } else {
      this.busy.set(false);
      this.buyError.set('Укажите адрес доставки');
    }
  }
}