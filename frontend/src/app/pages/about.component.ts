import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-about',
  template: `
    <div class="about-card card">
      <h2>Об организации</h2>
      <dl class="about-info">
        <dt>Адрес</dt>
        <dd>РФ, г. Оренбург, ул. Полигонная, д. 28</dd>

        <dt>Номер телефона</dt>
        <dd><a href="tel:+79033672141">+7 (903) 367-21-41</a></dd>

        <dt>Наименование ИП</dt>
        <dd>ИП Химич Сергей Александрович</dd>

        <dt>ИНН</dt>
        <dd>561000007761</dd>
      </dl>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .about-card { max-width: 560px; margin: 40px auto; }
    .about-info { margin: 20px 0 0; }
    .about-info dt {
      font-size: 13px;
      color: var(--muted);
      margin-top: 16px;
    }
    .about-info dt:first-child { margin-top: 0; }
    .about-info dd {
      margin: 4px 0 0;
      font-size: 16px;
      color: var(--text);
    }
    .about-info dd a { color: var(--text); text-decoration: none; }
    .about-info dd a:hover { color: var(--accent); }
  `]
})
export class AboutComponent {}
