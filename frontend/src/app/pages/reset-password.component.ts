import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
    selector: 'app-reset-password',
    imports: [FormsModule, RouterLink],
    template: `
    <div class="auth-card card">
      <h2>Новый пароль</h2>
      <form (ngSubmit)="submit()">
        <div class="form-field">
          <label>Новый пароль (минимум 6 символов)</label>
          <input type="password" name="password" [(ngModel)]="password" required minlength="6" />
        </div>
        <div class="form-field">
          <label>Повторите пароль</label>
          <input type="password" name="confirm" [(ngModel)]="confirm" required />
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <button class="btn full" type="submit" [disabled]="busy()">Сменить пароль</button>
      </form>
      <p class="links"><a routerLink="/login">Вернуться ко входу</a></p>
    </div>
  `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [`
    .auth-card { max-width: 400px; margin: 40px auto; }
    .full { width: 100%; }
    .links { text-align: center; margin-top: 18px; font-size: 14px; }
  `]
})
export class ResetPasswordComponent implements OnInit {
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  private email = '';
  private token = '';
  password = '';
  confirm = '';
  error = signal('');
  busy = signal(false);

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.email || !this.token) {
      this.error.set('Некорректная ссылка для сброса пароля');
    }
  }

  submit(): void {
    if (this.password !== this.confirm) {
      this.error.set('Пароли не совпадают');
      return;
    }
    this.error.set('');
    this.busy.set(true);
    this.auth.resetPassword(this.email, this.token, this.password).subscribe({
      next: () => this.router.navigate(['/login']),
      error: err => {
        this.busy.set(false);
        this.error.set(err.error?.message ?? 'Не удалось сменить пароль');
      },
    });
  }
}
