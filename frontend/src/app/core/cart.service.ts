import { Injectable, computed, signal } from '@angular/core';
import { ZipDto } from './models';

/** Одна позиция в корзине — деталь и выбранное количество. */
export interface CartItem {
  zip: ZipDto;
  count: number;
}

const STORAGE_KEY = 'motoparts_cart';

/**
 * Корзина живёт в localStorage — переживает перезагрузку и работает даже для гостя,
 * который ещё не залогинен (логин/чекаут происходит уже на этапе оформления).
 * Оформление всех позиций разом — см. OrdersService.checkout.
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly items = signal<CartItem[]>(this.restore());

  readonly list = this.items.asReadonly();
  readonly totalCount = computed(() => this.items().reduce((sum, i) => sum + i.count, 0));
  readonly totalSum = computed(() =>
    this.items().reduce((sum, i) => sum + (i.zip.sellCost ?? 0) * i.count, 0));

  add(zip: ZipDto, count = 1): void {
    const existing = this.items().find(i => i.zip.id === zip.id);
    const maxCount = zip.countStored;

    if (existing) {
      this.items.update(list => list.map(i =>
        i.zip.id === zip.id ? { ...i, count: Math.min(i.count + count, maxCount) } : i));
    } else {
      this.items.update(list => [...list, { zip, count: Math.min(count, maxCount) }]);
    }
    this.persist();
  }

  setCount(zipId: string, count: number): void {
    if (count <= 0) {
      this.remove(zipId);
      return;
    }
    this.items.update(list => list.map(i =>
      i.zip.id === zipId ? { ...i, count: Math.min(count, i.zip.countStored) } : i));
    this.persist();
  }

  remove(zipId: string): void {
    this.items.update(list => list.filter(i => i.zip.id !== zipId));
    this.persist();
  }

  clear(): void {
    this.items.set([]);
    this.persist();
  }

  private persist(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.items()));
    } catch {
      // localStorage недоступен (приватный режим и т.п.) — корзина просто не переживёт перезагрузку.
    }
  }

  private restore(): CartItem[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as CartItem[]) : [];
    } catch {
      return [];
    }
  }
}
