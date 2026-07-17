import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-card card">
      <h2>Регистрация</h2>
      <form (ngSubmit)="submit()">
        <div class="form-field">
          <label>ФИО</label>
          <input type="text" name="fio" [(ngModel)]="fio" required />
        </div>
        <div class="form-field">
          <label>Email</label>
          <input type="email" name="email" [(ngModel)]="email" required />
        </div>
        <div class="form-field">
          <label>Телефон (необязательно)</label>
          <input type="tel" name="phone" [(ngModel)]="phone" />
        </div>
        <div class="form-field">
          <label>Пароль (минимум 6 символов)</label>
          <input type="password" name="password" [(ngModel)]="password" required minlength="6" />
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <button class="btn full" type="submit" [disabled]="busy()">Зарегистрироваться</button>
      </form>
      <p class="links">Уже есть аккаунт? <a routerLink="/login">Войти</a></p>
    </div>
  `,
  styles: [`
    .auth-card { max-width: 400px; margin: 40px auto; }
    .full { width: 100%; }
    .links { text-align: center; margin-top: 18px; font-size: 14px; }
  `],
})
export class RegisterComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  fio = '';
  email = '';
  phone = '';
  password = '';
  error = signal('');
  busy = signal(false);

  submit(): void {
    this.error.set('');
    this.busy.set(true);
    this.auth.register(this.email, this.password, this.fio, this.phone || undefined).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message ?? 'Не удалось зарегистрироваться');
      },
    });
  }
}
