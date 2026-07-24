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

export const senderGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn && auth.isSender || auth.isAdmin ? true : router.createUrlTree(['/']);
};