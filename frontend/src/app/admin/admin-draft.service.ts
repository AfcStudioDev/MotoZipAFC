import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';

import { AdminService } from '../core/admin.service';

/** Что именно попадает в черновик: значения полей формы плюс состояние, живущее вне неё. */
export interface DraftPayload {
  form: Record<string, any>;
  zipPartNumId: number | null;
}

/**
 * Сторона, которая владеет формой. Сервис знает про черновики и не знает про поля формы,
 * компонент — наоборот; так сервис не тянет за собой половину состояния админ-панели.
 */
export interface DraftHost {
  /** Ключ формы, для которой сейчас ведём черновик, либо null (правка существующей записи, чужая вкладка). */
  currentFormKey(): string | null;
  /** Снимок текущего ввода. */
  collect(): DraftPayload;
  /** Подставить восстановленный черновик в форму. */
  apply(payload: DraftPayload): void;
}

/**
 * Черновики форм «Заказы» и «Запчасти (Приход)». Хранятся на сервере (таблица UserDrafts),
 * а не в localStorage: незавершённый ввод должен переживать очистку браузера и открываться
 * с другого устройства.
 *
 * Ограничение: выбранные фотографии — File-объекты браузера, их нельзя сериализовать в JSON,
 * поэтому в черновик попадают только значения полей.
 *
 * Регистрируется в providers компонента: состояние черновика привязано к экземпляру формы.
 */
@Injectable()
export class AdminDraftService implements OnDestroy {
  /** Формы, для которых черновики вообще ведутся. Совпадает с белым списком на бэкенде. */
  private static readonly ENDPOINTS = ['orders', 'zip'];

  private readonly admin = inject(AdminService);

  /** Показывается под формой, когда в ней есть восстановленный/сохранённый черновик. */
  readonly savedAt = signal<Date | null>(null);

  private readonly save$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();
  private host?: DraftHost;

  constructor() {
    this.save$
      .pipe(debounceTime(800), takeUntil(this.destroy$))
      .subscribe((formKey) => {
        // Пока шёл debounce, пользователь мог переключить вкладку. Ввод той формы уже
        // досохранён синхронно в flush(), а применять отложенное сохранение к новой
        // (ещё пустой) форме нельзя — иначе оно затрёт её черновик как пустой.
        if (formKey !== this.host?.currentFormKey()) return;
        this.flush();
      });
  }

  attach(host: DraftHost) {
    this.host = host;
  }

  static tracks(endpoint: string): boolean {
    return AdminDraftService.ENDPOINTS.includes(endpoint);
  }

  /** Ставит черновик в очередь: реальный запрос уходит после паузы в вводе. */
  schedule() {
    const formKey = this.host?.currentFormKey();
    if (!formKey) return;
    this.save$.next(formKey);
  }

  /** Сохраняет немедленно — при уходе со страницы и при переключении вкладки. */
  flush() {
    const formKey = this.host?.currentFormKey();
    if (!formKey || !this.host) return;

    const payload = this.host.collect();

    // Пустую форму не храним — иначе после сохранения записи всплывал бы пустой черновик.
    if (Object.keys(payload.form).length === 0) {
      this.discard(formKey);
      return;
    }

    this.admin.saveDraft(formKey, JSON.stringify(payload)).subscribe({
      next: () => this.savedAt.set(new Date()),
      error: () => { /* потеря черновика не должна мешать работе с формой */ }
    });
  }

  /** Подтягивает черновик формы и отдаёт его компоненту, если тот всё ещё готов его принять. */
  restore(endpoint: string) {
    if (!AdminDraftService.tracks(endpoint)) {
      this.savedAt.set(null);
      return;
    }

    this.admin.getDraft(endpoint).subscribe({
      next: (draft) => {
        // Пока шёл запрос, пользователь мог уйти на другую вкладку или начать править строку.
        if (!draft?.content || this.host?.currentFormKey() !== endpoint) return;

        try {
          const payload = JSON.parse(draft.content) as Partial<DraftPayload>;
          if (!payload?.form) return;

          this.host.apply({ form: payload.form, zipPartNumId: payload.zipPartNumId ?? null });
          this.savedAt.set(draft.updatedAt ? new Date(draft.updatedAt) : new Date());
        } catch {
          // Битый черновик просто игнорируем — форма останется пустой.
        }
      },
      error: () => { /* нет черновика или сервер недоступен — работаем с пустой формой */ }
    });
  }

  discard(formKey: string) {
    this.savedAt.set(null);
    this.admin.deleteDraft(formKey).subscribe({ error: () => { } });
  }

  ngOnDestroy() {
    // Уходя со страницы, дописываем последний ввод: он мог не дожить до конца debounce.
    this.flush();
    this.destroy$.next();
    this.destroy$.complete();
  }
}
