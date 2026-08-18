import { Component, OnInit, ViewChild, inject, signal, computed, HostListener, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';
import { CatalogService, SearchFilters } from '../core/catalog.service';
import { OrdersService } from '../core/orders.service';
import { AuthService } from '../core/auth.service';
import { AddressDto, GroupDto, MarkDto, ModelDto, PagedResult, ZipDto } from '../core/models';
import { PaymentModalComponent } from '../shared/payment-modal.component';
import { NgxMaskDirective } from 'ngx-mask';
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ChangeDetectorRef } from '@angular/core';

@Component({
  selector: 'app-home',
  styleUrls: ['../styles/home.component.css'],
  imports: [FormsModule, CurrencyPipe, NgxMaskDirective, PaymentModalComponent],
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
                @if (item.partNum) {
                  <span class="muted suggestion-pn">{{ item.partNum }}</span>
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
        <div class="pn-field">
          <input
            type="text"
            placeholder="Part number"
            [(ngModel)]="filters.partNumber"
            (ngModelChange)="onPartNumberInput($event)"
            (focus)="onPartNumberInput(filters.partNumber || '')"
            (keyup.enter)="search(1); partNumSuggestions.set([])"
            autocomplete="off" />

          @if (partNumSuggestions().length > 0) {
            <ul class="suggestions-dropdown pn-dropdown">
              @for (pn of partNumSuggestions(); track pn.partNum) {
                <li (click)="selectPartNumber(pn.partNum)">
                  <span class="suggestion-name">{{ pn.partNum }}</span>
                  <span class="muted suggestion-pn">{{ pn.name }}</span>
                </li>
              }
            </ul>
          }
        </div>
        <button class="btn btn-secondary" (click)="reset()">Сбросить</button>
      </div>
    </section>

    <section class="results">
      @if (result(); as r) {
        <p class="muted">Найдено: {{ r.total }}</p>
        <div class="grid">
          @for (zip of r.items; track zip.id) {
            <div class="card zip-card" (click)="openDetails(zip)">
              @if (zip.photos && zip.photos.length > 0) {
                <img
                  [src]="photoBaseUrl + zip.photos[0]"
                  alt="{{ zip.name }}"
                  class="product-image"
                  (error)="onImageError($event)"
                >
              } @else {
                <div class="product-image product-image-placeholder">Нет фото</div>
              }

              <h3>{{ zip.name }}</h3>
              <p class="muted">
                @if (zip.marks.length) { <span>{{ zip.marks.join(', ') }}</span> }
                @if (zip.models.length) { <span> · {{ zip.models.join(', ') }}</span> }
                @if (zip.year) { <span> · {{ zip.year }} г.</span> }
              </p>
              @if (zip.partNum) { <p class="pn">Part number: {{ zip.partNum }}</p> }
              @if (zip.group) { <p class="muted">Группа: {{ zip.group }}</p> }
              <div class="zip-footer">
                <span class="price">{{ zip.sellCost | currency:'RUB':'symbol-narrow':'1.0-0' }}</span>
                @if (zip.countStored > 0) {
                  <button class="btn" (click)="$event.stopPropagation(); openBuy(zip)">Купить</button>
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
          <p>{{ zip.name }} — <b>{{ zip.sellCost | currency:'RUB':'symbol-narrow':'1.0-0' }}</b></p>
          <div class="form-field">
            <label>Количество (в наличии {{ zip.countStored }})</label>
            <input type="number" min="1" [max]="zip.countStored" [(ngModel)]="buyCount" />
          </div>
          <div class="form-field">
            <label>Адрес доставки</label>
            <select [(ngModel)]="buyAddressId">
              <option [ngValue]="undefined">— выберите адрес —</option>
              @for (a of addressess(); track a.id) {
                <option [ngValue]="a.id">{{ a.address }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label>…или добавьте новый адрес</label>
            <input type="text" placeholder="Город, улица, дом, квартира" [(ngModel)]="newAddress" />
          </div>
          <div class="form-field">
            <label>Компания доставки (необязательно)</label>
            <select [(ngModel)]="buyDeliveryCompany">
              <option value="">— не выбрано —</option>
              @for (c of deliveryCompanies; track c) {
                <option [value]="c">{{ c }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label>Комментарий к доставке</label>
            <small class="muted" style="display: block; margin-bottom: 6px;">
              Возможно оформление курьерской доставки выбранной клиентом компанией, за счёт клиента
            </small>
            <textarea rows="2" [(ngModel)]="buyDeliveryComment"></textarea>
          </div>
          @if (buyError()) { <p class="error">{{ buyError() }}</p> }
          <div class="modal-actions">
            <button class="btn btn-secondary" (click)="closeBuy()">Отмена</button>
            <button class="btn" [disabled]="busy()" (click)="confirmBuy(zip)">Заказать и оплатить</button>
          </div>
        </div>
      </div>
    }

    @if (isGuestBuying(); as guestZip) {
      <div class="modal-backdrop">
        <div class="modal-content">
          <h3>Оформление заказа</h3>
          <p>Вы покупаете: <strong>{{ guestZip.name }}</strong></p>

          @if (buyError()) {
            <div class="alert alert-danger">{{ buyError() }}</div>
          }

          <div class="form-group mb-2">
            <label>ФИО</label>
            <input type="text" class="form-control" [(ngModel)]="guestForm.fio" placeholder="Иванов Иван Иванович">
          </div>

          <div class="form-group mb-2">
            <label>Email</label>
            <input type="email" class="form-control" [(ngModel)]="guestForm.email" placeholder="example@mail.ru">
          </div>

          <div class="form-group mb-2">
            <label>Телефон</label>
            <input type="text" class="form-control" [(ngModel)]="guestForm.phone" mask="+0 (000) 000-00-00" placeholder="+7 (999) 000-00-00">
          </div>

          <div class="form-group mb-2">
            <label>Пароль для личного кабинета <small class="text-muted">(если заказываете в первый раз)</small></label>
            <input type="password" class="form-control" [(ngModel)]="guestForm.password">
          </div>

          <div class="form-group mb-2">
            <label>Адрес доставки</label>
            <input type="text" class="form-control" [(ngModel)]="guestForm.address" placeholder="г. Москва, ул. Пушкина, д. 1">
          </div>

          <div class="form-group mb-2">
            <label>Почтовый индекс</label>
            <input type="text" class="form-control" [(ngModel)]="guestForm.postCode" placeholder="123456">
          </div>

          <div class="form-group mb-3">
            <label>Количество</label>
            <input type="number" class="form-control" [(ngModel)]="guestForm.count" min="1">
          </div>

          <div class="form-group mb-2">
            <label>Компания доставки <small class="text-muted">(необязательно)</small></label>
            <select class="form-control" [(ngModel)]="guestForm.deliveryCompany">
              <option value="">— не выбрано —</option>
              @for (c of deliveryCompanies; track c) {
                <option [value]="c">{{ c }}</option>
              }
            </select>
          </div>

          <div class="form-group mb-3">
            <label>Комментарий к доставке</label>
            <div class="text-muted mb-1" style="font-size: 13px;">
              Возможно оформление курьерской доставки выбранной клиентом компанией, за счёт клиента
            </div>
            <textarea class="form-control" rows="2" [(ngModel)]="guestForm.deliveryComment"></textarea>
          </div>

          <div class="d-flex gap-2 justify-content-end">
            <button class="btn btn-secondary" (click)="closeBuy()" [disabled]="busy()">Отмена</button>
            <button class="btn btn-primary" (click)="confirmGuestBuy(guestZip)" [disabled]="busy() || !guestForm.fio || !guestForm.phone || !guestForm.address">
              @if (busy()) { Загрузка... } @else { Подтвердить и перейти к оплате }
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Оплата переводом на карту: онлайн-эквайринг (ЮKassa) отключён, поэтому после
         создания заказа показываем реквизиты для ручного перевода и принимаем чек. -->
    <app-payment-modal #paymentModal />

    <!--Модальное окно товара-->
    @if (selectedZip(); as zip) {
      <div class="modal-backdrop" (click)="onBackdropClick($event)">
        <div class="modal-content item-details-modal">
          <button class="close-btn" (click)="closeDetails()">&times;</button>
          
          <h3 class="mb-3">{{ zip.name }}</h3>
          
          <div class="details-info">
            <p><strong>Марка:</strong> {{ zip.marks.length ? zip.marks.join(', ') : 'Не указана' }}</p>
            <p><strong>Модель:</strong> {{ zip.models.length ? zip.models.join(', ') : 'Не указана' }}</p>
            <p><strong>Группа:</strong> {{ zip.group || 'Не указана' }}</p>
            <p><strong>Год:</strong> {{ zip.year || 'Не указан' }}</p>
            <p><strong>Парт-номер:</strong> {{ zip.partNum || 'Не указан' }}</p>
            @if (zip.comment) {
              <p class="zip-comment"><strong>Доп. инфо:</strong> {{ zip.comment }}</p>
            }
            <h4 class="mt-3 text-primary">Цена: {{ zip.sellCost | currency:'RUB':'symbol':'1.0-0':'ru' }}</h4>
          </div>

          <div class="gallery mt-4">
            <p class="text-muted mb-2">Фотографии:</p>
            <div class="d-flex gap-2" style="overflow-x: auto;">
              @for (photo of getZipPhotos(zip); track photo) {
                <img 
                  [src]="photo" 
                  alt="Фото запчасти {{ zip.name }}" 
                  class="gallery-img" 
                  (error)="onImageError($event)"
                  (click)="openImage(photo)"
                  style="cursor: pointer;"
                  title="Нажмите для увеличения">
              }
            </div>
          </div>

          <div class="d-flex justify-content-end align-items-center mt-4">
            @if (zip.countStored <= 0) {
              <span class="muted me-2">Нет в наличии</span>
            }
            <button class="btn btn-secondary me-2" (click)="closeDetails()">Закрыть</button>
            <button class="btn btn-success" [disabled]="zip.countStored <= 0" (click)="openBuy(zip); closeDetails()">Купить</button>
          </div>
        </div>
      </div>

      <!--Открытие изображения крупным планом при клике-->
      @if (expandedImage(); as imgUrl) {
        <div class="image-lightbox-backdrop" (click)="closeImage()" (mousemove)="onMouseMove($event)">
          <button class="lightbox-close-btn" (click)="closeImage()">&times;</button>
          @if (currentGalleryPhotos().length > 1) {
            <button class="lightbox-nav-btn lightbox-prev-btn" (click)="prevImage($event)" title="Предыдущее фото">&#8249;</button>
            <button class="lightbox-nav-btn lightbox-next-btn" (click)="nextImage($event)" title="Следующее фото">&#8250;</button>
            <div class="lightbox-counter">{{ (expandedPhotoIndex() ?? 0) + 1 }} / {{ currentGalleryPhotos().length }}</div>
          }
          <img [src]="imgUrl" class="lightbox-img" [class.zoomed]="isZoomed()" [style.transform-origin]="zoomOrigin()" (click)="toggleZoom($event)" alt="Крупное фото">
        </div>
      }
}
  `,
  changeDetection: ChangeDetectionStrategy.Eager
})

export class HomeComponent implements OnInit {
  public photoBaseUrl = `${environment.apiUrl.replace('/api', '')}/ZipPhotos/`;
  // ДОБАВЛЕНО: Сигнал и сабжект для автопредложений
  suggestions = signal<ZipDto[]>([]);

  /** Индекс открытой в лайтбоксе фотографии среди фото текущего товара (selectedZip). */
  expandedPhotoIndex = signal<number | null>(null);

  /** Фото текущего открытого товара — базис для листания в лайтбоксе. */
  currentGalleryPhotos = computed(() => {
    const zip = this.selectedZip();
    return zip ? this.getZipPhotos(zip) : [];
  });

  expandedImage = computed(() => {
    const idx = this.expandedPhotoIndex();
    const photos = this.currentGalleryPhotos();
    return idx !== null && idx >= 0 && idx < photos.length ? photos[idx] : null;
  });

  /** Подсказки для поля «Part number» в фильтрах. */
  partNumSuggestions = signal<{ partNum: string; name: string }[]>([]);

  private searchSubject = new Subject<string>();
  private partNumSubject = new Subject<string>();

  private catalog = inject(CatalogService);
  private orders = inject(OrdersService);
  private auth = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  // Добавляем новые сигналы в класс компонента
  isZoomed = signal(false);
  zoomOrigin = signal('50% 50%'); // По умолчанию центр

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
  addressess = signal<AddressDto[]>([]);
  buyError = signal('');
  busy = signal(false);

  /** Компании, которыми клиент может заказать курьерскую доставку — за свой счёт. */
  readonly deliveryCompanies = ['СДЕК', 'Озон', 'Вайлдберриз'];
  buyDeliveryCompany = '';
  buyDeliveryComment = '';

  /** Модалка оплаты переводом на карту — своё состояние держит сама (см. PaymentModalComponent). */
  @ViewChild('paymentModal') private paymentModal!: PaymentModalComponent;

  // Для заказа без регистрации
  guestForm = {
    fio: '',
    email: '',
    phone: '',
    password: '',
    address: '',
    postCode: '',
    count: 1,
    deliveryCompany: '',
    deliveryComment: ''
  };
  isGuestBuying = signal<ZipDto | null>(null);

  selectedZip = signal<ZipDto | null>(null);

  // Открыть информацию о товаре
  openDetails(zip: ZipDto): void {
    this.selectedZip.set(zip);
    this.cdr.detectChanges();
  }

  // Закрыть информацию о товаре
  closeDetails(): void {
    this.selectedZip.set(null);
  }

  // Закрытие при клике на затемненный фон (вне окна)
  onBackdropClick(event: MouseEvent): void {
    // Проверяем, что клик был именно по фону, а не по самому окну внутри
    if ((event.target as HTMLElement).classList.contains('modal-backdrop')) {
      this.closeDetails();
    }
  }

  // Метод для получения путей к реально загруженным фотографиям запчасти
  getZipPhotos(zip: ZipDto): string[] {
    return (zip.photos || []).map(fileName => this.photoBaseUrl + fileName);
  }

  // Если фото не найдено (например, их только 1 или 2), скрываем сломанную картинку
  onImageError(event: Event): void {
    (event.target as HTMLImageElement).style.display = 'none';
  }


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

    // Подсказки по парт-номеру. Пустой ввод тоже допустим — тогда показываем
    // первые доступные номера, чтобы поле было подсказкой само по себе.
    this.partNumSubject.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(query => this.catalog.partNumbers(query).pipe(catchError(() => of([]))))
    ).subscribe(list => this.partNumSuggestions.set(list));

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

  onPartNumberInput(query: string | undefined) {
    this.partNumSubject.next(query || '');
  }

  selectPartNumber(partNum: string) {
    this.filters.partNumber = partNum;
    this.partNumSuggestions.set([]);
    this.search(1);
  }

  onMarkChange(): void {
    this.filters.modelId = undefined;
    this.catalog.models(this.filters.markId).subscribe(m => this.models.set(m));
  }

  search(page: number): void {
    this.suggestions.set([]); // Скрываем подсказки при принудительном поиске
    this.partNumSuggestions.set([]);
    this.catalog.search({ ...this.filters, page, pageSize: 12 }).subscribe(r => this.result.set(r));
  }

  reset(): void {
    this.filters = {};
    this.suggestions.set([]);
    this.partNumSuggestions.set([]);
    this.catalog.models().subscribe(m => this.models.set(m));
    this.search(1);
  }

  pages(r: PagedResult<ZipDto>): number[] {
    const from = Math.max(1, r.page - 3);
    const to = Math.min(r.totalPages, r.page + 3);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  }

  // openBuy(zip: ZipDto): void {
  //   if (!this.auth.isLoggedIn) {
  //     this.router.navigate(['/login']);
  //     return;
  //   }
  //   this.buyError.set('');
  //   this.buyCount = 1;
  //   this.newAddress = '';
  //   this.buying.set(zip);
  //   this.orders.addressess().subscribe(a => {
  //     this.addressess.set(a);
  //     this.buyAddressId = a[0]?.id;
  //   });
  // }

  openBuy(zip: ZipDto): void {
    if (!this.auth.isLoggedIn) {
      // Если не авторизован - открываем окно гостевой покупки
      this.buyError.set('');
      this.guestForm = {
        fio: '', email: '', phone: '', password: '', address: '', postCode: '', count: 1,
        deliveryCompany: '', deliveryComment: ''
      };
      this.isGuestBuying.set(zip);
      return;
    }

    // Существующая логика для авторизованного пользователя
    this.buyError.set('');
    this.buyCount = 1;
    this.newAddress = '';
    this.buyDeliveryCompany = '';
    this.buyDeliveryComment = '';
    this.buying.set(zip);

    this.orders.addressess().subscribe(a => {
      this.addressess.set(a);
      this.buyAddressId = a[0]?.id;
    });
  }

  closeBuy(): void {
    this.buying.set(null);
    this.isGuestBuying.set(null);
  }

  confirmGuestBuy(zip: ZipDto): void {
    this.buyError.set('');
    this.busy.set(true);

    const requestData = {
      zipId: zip.id,
      count: this.guestForm.count,
      fio: this.guestForm.fio,
      email: this.guestForm.email,
      phone: this.guestForm.phone,
      password: this.guestForm.password,
      address: this.guestForm.address,
      postCode: this.guestForm.postCode,
      deliveryCompany: this.guestForm.deliveryCompany || undefined,
      deliveryComment: this.guestForm.deliveryComment || undefined
    };

    this.orders.createGuestOrder(requestData).subscribe({
      next: order => this.openPaymentModal(order),
      error: err => {
        this.busy.set(false);
        this.buyError.set(err.error?.message ?? 'Не удалось создать заказ');
      }
    });
  }

  // closeBuy(): void {
  //   this.buying.set(null);
  // }

  confirmBuy(zip: ZipDto): void {
    this.buyError.set('');
    this.busy.set(true);

    const placeOrder = (addressId: number) => {
      this.orders.createOrder(
        zip.id, this.buyCount, addressId,
        this.buyDeliveryCompany || undefined, this.buyDeliveryComment || undefined
      ).subscribe({
        next: order => this.openPaymentModal(order),
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

  /** После создания заказа закрываем форму оформления и показываем реквизиты для перевода. */
  private openPaymentModal(order: { id: string; orderNumber: string }): void {
    this.busy.set(false);
    this.buying.set(null);
    this.isGuestBuying.set(null);
    this.paymentModal.open(order);
  }

  openImage(photoUrl: string) {
    this.expandedImage.set(photoUrl);
  }

  // closeImage() {
  //   this.expandedImage.set(null);
  // }

  // Дополнительное приближение открытой картинки на 50%
  // Метод для клика по самой картинке
  toggleZoom(event: MouseEvent) {
    event.stopPropagation(); // Чтобы клик не передался оверлею и не закрыл окно
    this.isZoomed.update(z => !z);

    if (this.isZoomed()) {
      this.calculateZoomOrigin(event);
    } else {
      this.zoomOrigin.set('50% 50%'); // Сбрасываем позицию при отдалении
    }
  }

  // Метод для отслеживания движения мыши
  onMouseMove(event: MouseEvent) {
    if (this.isZoomed()) {
      this.calculateZoomOrigin(event);
    }
  }

  // Вычисление координат в процентах относительно экрана
  private calculateZoomOrigin(event: MouseEvent) {
    const x = (event.clientX / window.innerWidth) * 100;
    const y = (event.clientY / window.innerHeight) * 100;
    this.zoomOrigin.set(`${x}% ${y}%`);
  }

  private resetZoom() {
    this.isZoomed.set(false);
    this.zoomOrigin.set('50% 50%');
  }

  // Обновите ваш метод закрытия картинки, чтобы сбрасывать зум
  closeImage() {
    this.expandedPhotoIndex.set(null);
    this.resetZoom();
  }
}