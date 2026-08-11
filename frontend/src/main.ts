import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

/**
 * В Docker config.json генерируется entrypoint-скриптом nginx-контейнера из
 * переменных окружения (.env) при каждом старте контейнера — так апи-адрес и
 * OAuth client id можно поменять без пересборки образа. При локальном
 * `ng serve` файла нет: fetch падает, ловим и остаёмся на environment.ts.
 */
fetch('/config.json')
  .then(res => (res.ok ? res.json() : null))
  .then(config => {
    if (config) Object.assign(environment, config);
  })
  .catch(() => { /* config.json недоступен — используем значения из environment.ts */ })
  .finally(() => {
    bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
  });
