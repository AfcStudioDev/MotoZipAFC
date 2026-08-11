import { ApplicationConfig, LOCALE_ID, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeRu from '@angular/common/locales/ru';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { provideEnvironmentNgxMask } from 'ngx-mask';

// Без регистрации данных локали Angular бросает NG0701, и значение просто не выводится.
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    // Локаль по умолчанию — русская: разделитель тысяч становится пробелом (1 000),
    // а не запятой, как в en-US. Иначе пайпы number/currency без явной локали
    // форматируют суммы по-английски.
    { provide: LOCALE_ID, useValue: 'ru' },
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideEnvironmentNgxMask(),
    provideHttpClient(withXhr(), withInterceptors([authInterceptor])),
  ],
};
