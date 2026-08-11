import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn ? true : router.createUrlTree(['/login']);
};

export const allGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn && auth.isAdmin || auth.isSender || auth.isRegistrar ? true : router.createUrlTree(['/']);
};

/**
 * Админ-панель работает только через AdminController, а он на бэкенде разрешён
 * ролям Admin и Registrar ([Authorize(Roles = "Admin,Registrar")]) — у Sender там
 * везде 403. Поэтому сюда его не пускаем, в отличие от allGuard.
 */
export const adminOrRegistrarGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn && (auth.isAdmin || auth.isRegistrar) ? true : router.createUrlTree(['/']);
};

export const senderGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn && auth.isSender || auth.isAdmin ? true : router.createUrlTree(['/']);
};

/** Строго администратор: коррекции остатка и переоценка меняют склад и цены. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn && auth.isAdmin ? true : router.createUrlTree(['/']);
};