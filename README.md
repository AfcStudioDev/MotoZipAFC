# MotoParts — интернет-магазин запчастей для мотоциклов

Полноценный сайт с каталогом ZIP-запчастей, личным кабинетом, админ-панелью и оплатой через ЮKassa.

## Стек

| Слой | Технологии |
|---|---|
| Фронтенд | Angular 18 (standalone-компоненты, signals) |
| Бэкенд | ASP.NET Core 8 (C#), Entity Framework Core (Code First) |
| База данных | PostgreSQL 16 |
| Аутентификация | JWT + email/пароль, OAuth Google, OAuth VK |
| Платежи | ЮKassa (банковские карты РФ) |

## Структура

```
backend/MotoParts.Api/   — Web API (контроллеры, EF Core Code First, сервисы)
frontend/                — Angular-приложение
docker-compose.yml       — PostgreSQL для локальной разработки
```

## Возможности

- **Главная страница в стиле auto.ru**: строка поиска + фильтры «Марка мотоцикла», «Модель мотоцикла», «Группа ZIP-запчастей», «Год выпуска», «Part number».
- **Регистрация и вход**: email/пароль, вход через Google (Google Identity Services) и VK (OAuth-код).
- **Сброс пароля**: форма «Забыли пароль?» → письмо со ссылкой (токен действует 1 час) → форма нового пароля.
- **Личный кабинет**: заказы текущего пользователя (связь через `DeliveryAdressess.UserId`), пагинация по 10 заказов на страницу, управление адресами доставки, кнопка «Оплатить».
- **Админ-панель** (`IsAdmin = true`): ручное добавление записей в каждую таблицу (MotoMarks, MotoModels, ZipGroups, PartNumbers, Zip, Users, DeliveryAdressess, Orders) и просмотр их содержимого.
- **Оплата ЮKassa**: создание платежа `bank_card`, редирект на страницу подтверждения ЮKassa, страница результата, webhook для обновления статуса.

## База данных (Code First)

Схема из задания воспроизведена в `Data/AppDbContext.cs` один в один (включая имя таблицы `DeliveryAdressess` и поведение внешних ключей: каскадное удаление моделей при удалении марки, `NO ACTION` для остальных связей).

Расширения сверх исходной схемы (необходимы для функционала):
- `Users`: `PasswordHash`, `OAuthProvider`, `OAuthSubject`, `PasswordResetTokenHash`, `PasswordResetTokenExpiresAt` — аутентификация и сброс пароля;
- таблица `Payments` — статусы платежей ЮKassa (исходные таблицы не изменялись).

## Запуск

### 1. PostgreSQL

```bash
docker compose up -d postgres
```

### 2. Бэкенд

```bash
cd backend/MotoParts.Api
dotnet ef migrations add Initial   # первая генерация миграций (требуется dotnet-ef)
dotnet run                          # http://localhost:5000, Swagger: /swagger
```

Если миграции ещё не созданы, при старте выполняется `EnsureCreated()` — схема будет создана автоматически. Для продакшена сгенерируйте миграции (`dotnet tool install -g dotnet-ef`).

При первом запуске создаётся администратор: `admin@motoparts.local` / `Admin123!` (настраивается в `appsettings.json`, секция `Seed`) и демо-каталог запчастей.

### 3. Фронтенд

```bash
cd frontend
npm install
npm start                           # http://localhost:4200
```

## Настройка внешних сервисов

Все ключи задаются в `backend/MotoParts.Api/appsettings.json` (для секретов используйте `dotnet user-secrets` или переменные окружения):

| Секция | Что указать |
|---|---|
| `OAuth:Google:ClientId` | Client ID из Google Cloud Console (тот же ID укажите в `frontend/src/environments/environment.ts`) |
| `OAuth:Vk:ClientId`, `OAuth:Vk:ClientSecret` | Приложение VK с включённым scope `email`; redirect URI: `http://localhost:4200/vk-callback` |
| `YooKassa:ShopId`, `YooKassa:SecretKey` | Из личного кабинета ЮKassa; webhook: `POST /api/payments/webhook` |
| `Smtp:*` | SMTP для писем сброса пароля; если не заполнено — ссылка пишется в лог бэкенда (удобно для разработки) |
| `Jwt:Key` | Замените на собственный секрет ≥ 32 символов |

## Основные API-эндпоинты

| Метод | Путь | Описание |
|---|---|---|
| POST | `/api/auth/register`, `/login`, `/google`, `/vk` | Регистрация и вход |
| POST | `/api/auth/forgot-password`, `/reset-password` | Сброс пароля |
| GET | `/api/catalog/search` | Поиск: `query, markId, modelId, groupId, year, partNumber, page` |
| GET | `/api/catalog/marks`, `/models`, `/groups`, `/years` | Справочники для фильтров |
| GET | `/api/orders/my?page=N` | Заказы пользователя (по 10 на страницу) |
| POST | `/api/orders` | Создать заказ |
| GET/POST | `/api/addresses` | Адреса доставки |
| POST | `/api/payments/create` | Платёж ЮKassa (карта РФ) |
| GET | `/api/payments/status/{orderId}` | Статус платежа |
| POST | `/api/payments/webhook` | Уведомления ЮKassa |
| GET/POST | `/api/admin/{marks,models,groups,partnumbers,zip,users,addresses,orders}` | Админ: просмотр и добавление записей (роль Admin) |
