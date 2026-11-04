# LibraryApi

REST API городской библиотеки: .NET 10 (C# 14), Clean Architecture, EF Core + PostgreSQL, Identity + JWT, SMTP, фоновый сервис просрочки, кеш, xUnit.

Структура: `Domain` → `Application` → `Infrastructure` → `API`, плюс `Tests` (ссылки только на `Application` и `Domain`).

## 1. Что нужно поднять

**PostgreSQL 14+**

```bash
docker run -d --name library-pg -p 5432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=library_api postgres:16
```

**SMTP-сервер** (для проверки писем, например smtp4dev; веб-интерфейс на http://localhost:3000)

```bash
docker run -d --name library-smtp -p 3000:80 -p 2525:25 rnwood/smtp4dev
```

Если SMTP недоступен, API всё равно работает: данные сохраняются, ответ 200, ошибка отправки пишется в лог.

Redis не нужен.

## 2. Настройки (`API/appsettings.json`)

| Секция | Параметры |
|---|---|
| `ConnectionStrings:DefaultConnection` | строка подключения к PostgreSQL |
| `Jwt` | `Key` (не короче 32 символов), `Issuer`, `Audience`, `ExpiresMinutes` |
| `Email` | `Host`, `Port`, `UseSsl`, `UserName`, `Password`, `From` |
| `Loan` | `LoanDays` (14), `MaxActiveLoans` (3), `FinePerDay` (0.50), `OverdueCheckSeconds` (30) |

## 3. Порядок запуска

Нужен .NET 10 SDK и инструмент EF: `dotnet tool install --global dotnet-ef --version 10.*`

```bash
dotnet build
dotnet ef database update --project Infrastructure --startup-project API
dotnet test
dotnet run --project API
```

Приложение миграции само не применяет. Swagger (кнопка **Authorize**): http://localhost:5080/swagger

При запуске в Development сидер создаёт роли и учётные записи:

| Роль | Email | Пароль |
|---|---|---|
| Admin | `admin@mail.com` | `Admin123` |
| Librarian | `librarian@mail.com` | `Librarian123` |

Новые пользователи через `POST /api/auth/register` получают роль `Reader`.

## 4. Кеш

Выбран `IMemoryCache`: приложение одно, отдельный сервер не нужен.

Кешируются DTO для `GET /api/books/{id}` и `GET /api/books`, ключ списка учитывает все параметры. У записей абсолютный TTL 5 минут и скользящий 1 минута. Инвалидация: версия ключа. После создания и изменения книги, а также после выдачи и возврата версия увеличивается, и пользователь сразу получает актуальные данные. В лог пишется `Cache HIT key=...` / `Cache MISS key=...`.

## 5. Тесты

```bash
dotnet test
```

30 тестовых методов, PostgreSQL и SMTP не нужны.

## 6. Что выполнено

- 13 эндпоинтов из задания, Result-паттерн, пагинация / фильтрация / сортировка.
- Identity (`IdentityUser<int>`, роли, блокировка 5 попыток / 5 минут) + JWT.
- Книги и выдачи с проверками в заданном порядке, расчёт штрафа (`FineCalculator`).
- Фоновый сервис просрочки (`BackgroundService` + `IServiceScopeFactory`).
- Письма по SMTP после сохранения в БД, сбой почты не ломает запрос.
- `ExceptionHandlingMiddleware`, `RequestLoggingMiddleware`.
- Кеш книг, Swagger с Authorize, миграция `InitialCreate`, 30 модульных тестов.
