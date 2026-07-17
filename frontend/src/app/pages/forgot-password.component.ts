import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
    selector: 'app-forgot-password',
    imports: [FormsModule, RouterLink],
    template: `
    <div class="auth-card card">
      <h2>Сброс пароля</h2>
      <p class="muted">Укажите email — мы отправим ссылку для сброса пароля.</p>
      <form (ngSubmit)="submit()">
        <div class="form-field">
          <label>Email</label>
          <input type="email" name="email" [(ngModel)]="email" required />
        </div>
        @if (message()) { <p class="success">{{ message() }}</p> }
        @if (error()) { <p class="error">{{ error() }}</p> }
        <button class="btn full" type="submit" [disabled]="busy()">Отправить ссылку</button>
      </form>
      <p class="links"><a routerLink="/login">Вернуться ко входу</a></p>
    </div>
  `,
    styles: [`
    .auth-card { max-width: 400px; margin: 40px auto; }
    .full { width: 100%; }
    .links { text-align: center; margin-top: 18px; font-size: 14px; }
  `]
})
export class ForgotPasswordComponent {
  private auth = inject(AuthService);

  email = '';
  message = signal('');
  error = signal('');
  busy = signal(false);

  submit(): void {
    this.message.set('');
    this.error.set('');
    this.busy.set(true);
    this.auth.forgotPassword(this.email).subscribe({
      next: r => {
        this.busy.set(false);
        this.message.set(r.message);
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Не удалось отправить письмо, попробуйте позже');
      },
    });
  }
}
