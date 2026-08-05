import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeRu from '@angular/common/locales/ru';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { provideEnvironmentNgxMask } from 'ngx-mask';

// Шаблоны вызывают пайпы с локалью 'ru' явно (например, цена в карточке товара).
// Без регистрации данных локали Angular бросает NG0701, и значение просто не выводится.
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideEnvironmentNgxMask(),
    provideHttpClient(withXhr(), withInterceptors([authInterceptor])),
  ],
};
