import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
    selector: 'app-vk-callback',
    imports: [RouterLink],
    changeDetection: ChangeDetectionStrategy.Eager,
    template: `
    <div class="card" style="max-width:400px;margin:40px auto;text-align:center">
      @if (error()) {
        <p class="error">{{ error() }}</p>
        <a routerLink="/login">Вернуться ко входу</a>
      } @else {
        <p>Входим через VK…</p>
      }
    </div>
  `
})
export class VkCallbackComponent implements OnInit {
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  error = signal('');

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    if (!code) {
      this.error.set('VK не вернул код авторизации');
      return;
    }
    this.auth.loginWithVk(code, `${location.origin}/vk-callback`).subscribe({
      next: () => this.router.navigate(['/']),
      error: err => this.error.set(err.error?.message ?? 'Не удалось войти через VK'),
    });
  }
}
