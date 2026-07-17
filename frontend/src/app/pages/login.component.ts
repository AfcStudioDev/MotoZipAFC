import { AfterViewInit, Component, NgZone, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { environment } from '../../environments/environment';

declare const google: any;

@Component({
    selector: 'app-login',
    imports: [FormsModule, RouterLink],
    template: `
    <div class="auth-card card">
      <h2>Вход</h2>
      <form (ngSubmit)="submit()">
        <div class="form-field">
          <label>Email</label>
          <input type="email" name="email" [(ngModel)]="email" required />
        </div>
        <div class="form-field">
          <label>Пароль</label>
          <input type="password" name="password" [(ngModel)]="password" required />
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <button class="btn full" type="submit" [disabled]="busy()">Войти</button>
      </form>

      <div class="divider">или войдите через</div>
      <div id="google-btn"></div>
      <button class="btn vk-btn" type="button" (click)="loginVk()">Войти через VK</button>

      <p class="links">
        <a routerLink="/forgot-password">Забыли пароль?</a><br />
        Нет аккаунта? <a routerLink="/register">Зарегистрироваться</a>
      </p>
    </div>
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    .auth-card { max-width: 400px; margin: 40px auto; }
    .full { width: 100%; }
    .divider {
      text-align: center; color: var(--muted);
      font-size: 13px; margin: 18px 0 12px;
    }
    .vk-btn { width: 100%; background: #0077ff; margin-top: 10px; }
    .vk-btn:hover { background: #0066dd; }
    .links { text-align: center; margin-top: 18px; font-size: 14px; }
  `]
})
export class LoginComponent implements AfterViewInit {
  private auth = inject(AuthService);
  private router = inject(Router);
  private zone = inject(NgZone);

  email = '';
  password = '';
  error = signal('');
  busy = signal(false);

  ngAfterViewInit(): void {
    // Кнопка Google Identity Services (скрипт подключён в index.html)
    if (typeof google !== 'undefined' && environment.googleClientId) {
      google.accounts.id.initialize({
        client_id: environment.googleClientId,
        callback: (response: { credential: string }) =>
          this.zone.run(() => this.loginGoogle(response.credential)),
      });
      google.accounts.id.renderButton(document.getElementById('google-btn'), {
        theme: 'outline',
        size: 'large',
        width: 360,
        text: 'signin_with',
      });
    }
  }

  submit(): void {
    this.error.set('');
    this.busy.set(true);
    this.auth.login(this.email, this.password).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message ?? 'Не удалось войти');
      },
    });
  }

  loginGoogle(idToken: string): void {
    this.auth.loginWithGoogle(idToken).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => this.error.set(err.error?.message ?? 'Не удалось войти через Google'),
    });
  }

  loginVk(): void {
    const redirectUri = `${location.origin}/vk-callback`;
    const url = 'https://oauth.vk.com/authorize' +
      `?client_id=${environment.vkClientId}` +
      `&redirect_uri=${encodeURIComponent(redirectUri)}` +
      '&display=page&scope=email&response_type=code&v=5.199';
    location.href = url;
  }
}
