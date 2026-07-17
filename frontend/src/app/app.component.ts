import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
    selector: 'app-root',
    imports: [RouterOutlet, RouterLink],
    template: `
    <header class="header">
      <div class="container header-inner">
        <a routerLink="/" class="logo">Moto<span>Parts</span></a>
        <nav class="nav">
          <a routerLink="/">Каталог</a>
          @if (auth.user(); as user) {
            <a routerLink="/cabinet">Личный кабинет</a>
            @if (user.isAdmin) {
              <a routerLink="/admin">Админ-панель</a>
            }
            <span class="user-name">{{ user.fio }}</span>
            <button class="btn btn-secondary" (click)="logout()">Выйти</button>
          } @else {
            <a routerLink="/login">Войти</a>
            <a routerLink="/register" class="btn">Регистрация</a>
          }
        </nav>
      </div>
    </header>
    <main class="container main">
      <router-outlet />
    </main>
  `,
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
    .nav a { color: var(--text); font-weight: 500; }
    .user-name { color: var(--muted); font-size: 14px; }
    .main { padding: 24px 16px 48px; }
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
