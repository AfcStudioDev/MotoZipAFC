import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, RouterLink, RouterLinkActive],
    template: `
    <header class="header">
      <div class="container header-inner">
        <a routerLink="/" class="logo">Moto<span>Parts</span></a>
        <nav class="nav">
          <!-- exact: true — иначе "/" подсвечивался бы на любой странице -->
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Каталог</a>
          @if (auth.user(); as user) {
            <a routerLink="/cabinet" routerLinkActive="active">Личный кабинет</a>
            @if (user.isAdmin || user.isRegistrar) {
              <a routerLink="/admin" routerLinkActive="active">Админ-панель</a>
            }
            @if (user.isAdmin || user.isSender || user.isRegistrar) {
              <a routerLink="/reports" routerLinkActive="active">Отчёты</a>
            }
            @if (user.isAdmin) {
              <a routerLink="/corrections" routerLinkActive="active">Коррекция</a>
            }
            @if (user.isAdmin || user.isSender) {
              <a routerLink="/sender" routerLinkActive="active">Отправления</a>
            }
            <span class="user-name">{{ user.fio }}</span>
            <button class="btn btn-secondary" (click)="logout()">Выйти</button>
          } @else {
            <a routerLink="/login" routerLinkActive="active">Войти</a>
            <a routerLink="/register" class="btn">Регистрация</a>
          }
        </nav>
      </div>
    </header>
    <main class="container main">
      <router-outlet />
    </main>
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    .header {
      background: #fff;
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      z-index: 10;
    }
    .header-inner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      height: 64px;
    }
    .logo {
      font-size: 24px;
      font-weight: 800;
      color: var(--text);
      text-decoration: none;
    }
    .logo span { color: var(--accent); }
    .nav { display: flex; align-items: center; gap: 18px; }
    .nav a {
      color: var(--text);
      font-weight: 500;
      position: relative;
      padding: 4px 0;
      transition: color 0.15s;
    }
    /* Подсветка вкладки текущей страницы. Кнопка «Регистрация» исключена —
       у неё собственный стиль .btn, подчёркивание его ломает. */
    .nav a.active:not(.btn) {
      color: var(--accent, #007bff);
      font-weight: 600;
    }
    .nav a.active:not(.btn)::after {
      content: '';
      position: absolute;
      left: 0;
      right: 0;
      bottom: -2px;
      height: 2px;
      background: var(--accent, #007bff);
      border-radius: 2px;
    }
    .user-name { color: var(--muted); font-size: 14px; }
    .main { padding: 24px 16px 48px; }

    /* На мобильном пункты меню не помещаются в одну строку рядом с логотипом:
       переносим их и позволяем шапке расти по высоте вместо горизонтальной прокрутки. */
    @media (max-width: 768px) {
      .header-inner {
        flex-wrap: wrap;
        height: auto;
        gap: 8px;
        padding-top: 10px;
        padding-bottom: 10px;
      }
      .logo { font-size: 20px; }
      .nav {
        flex-wrap: wrap;
        gap: 10px 14px;
        width: 100%;
      }
      .nav a { font-size: 14px; }
      .user-name { font-size: 13px; }
      .main { padding: 16px 12px 40px; }
    }
  `]
})
export class AppComponent {
  auth = inject(AuthService);
  private router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
