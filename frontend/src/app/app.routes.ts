import { Routes } from '@angular/router';
import { authGuard, allGuard, senderGuard, adminGuard, adminOrRegistrarGuard } from './core/guards';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home.component').then(m => m.HomeComponent) },
  { path: 'login', loadComponent: () => import('./pages/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./pages/register.component').then(m => m.RegisterComponent) },
  { path: 'forgot-password', loadComponent: () => import('./pages/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'reset-password', loadComponent: () => import('./pages/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: 'vk-callback', loadComponent: () => import('./pages/vk-callback.component').then(m => m.VkCallbackComponent) },
  { path: 'cabinet', canActivate: [authGuard], loadComponent: () => import('./pages/cabinet.component').then(m => m.CabinetComponent) },
  { path: 'payment-result/:orderId', canActivate: [authGuard], loadComponent: () => import('./pages/payment-result.component').then(m => m.PaymentResultComponent) },
  { path: 'admin', canActivate: [adminOrRegistrarGuard], loadComponent: () => import('./pages/admin.component').then(m => m.AdminComponent) },
  { path: 'reports', canActivate: [allGuard], loadComponent: () => import('./pages/reports.component').then(m => m.ReportsComponent), title: 'Отчёты — DonorGarage' },
  { path: 'corrections', canActivate: [adminGuard], loadComponent: () => import('./pages/corrections.component').then(m => m.CorrectionsComponent), title: 'Коррекция — DonorGarage' },
  { path: 'sender',canActivate: [senderGuard], loadComponent: () => import('./pages/sender-panel.component').then(m => m.SenderPanelComponent), title: 'Панель отправителя — DonorGarage' },
  { path: '**', redirectTo: '' },
];
