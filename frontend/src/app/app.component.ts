import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, RouterLink, RouterLinkActive],
    template: `
    <header class="header">
      <div class="container header-inner">
        <a routerLink="/" class="logo">
          <img src="/logo.svg" alt="" class="logo-img" />
          Donor<span>Garage</span>
        </a>
        <nav class="nav">
          <!-- exact: true — иначе "/" подсвечивался бы на любой странице -->
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Каталог</a>
          <a routerLink="/about" routerLinkActive="active">Об организации</a>
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
    <footer class="footer">
      <div class="container footer-inner">
        <a routerLink="/about">Об организации</a>
      </div>
    </footer>
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
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-size: 24px;
      font-weight: 800;
      color: var(--text);
      text-decoration: none;
    }
    .logo span { color: var(--accent); }
    .logo-img {
      display: block;
      height: 40px;
      width: auto;
    }
    .nav { display: flex; align-items: center; gap: 18px; }
    .nav a {
      color: var(--text);
      font-weight: 500;
      position: relative;
      padding: 4px 0;
      transition: color 0.15s;
    }
    /* .nav a (класс + тег) специфичнее одного класса .btn, поэтому без этого
       переопределения ссылка «Регистрация» получала бы цвет текста и отступы
       навигации вместо собственного вида кнопки. */
    .nav a.btn {
      color: #fff;
      padding: 10px 20px;
      font-weight: 600;
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
    .footer {
      border-top: 1px solid var(--border);
      padding: 20px 16px;
    }
    .footer-inner { text-align: center; }
    .footer a {
      color: var(--muted);
      font-size: 14px;
      text-decoration: none;
    }
    .footer a:hover { color: var(--accent, #007bff); }

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
      .logo { font-size: 20px; gap: 6px; }
      .logo-img { height: 30px; }
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
