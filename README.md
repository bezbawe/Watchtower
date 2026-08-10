# Watchtower

Учебный SIEM-lite на .NET 9: приём логов → нормализация → детекция аномалий (L1 rule-based) →
алерты с объяснением → Blazor-дашборд с live-обновлением (SignalR) и уведомления в Telegram.

Спецификация — [`docs/tz.md`](docs/tz.md); план по фазам — [`docs/plan.md`](docs/plan.md).

## Запуск (демо Фазы 5)

Единый хост — **`Watchtower.Web`**: приём событий (`POST /api/events`), детекция, алертинг и дашборд
в одном процессе.

1. **PostgreSQL** (docker-compose):
   ```bash
   docker compose up -d
   ```
   БД/пользователь/пароль — всё `watchtower`, порт `5432`.

2. **Запустить хост** (профиль `http`; миграции применяются автоматически в Development):
   ```bash
   dotnet run --project src/Watchtower.Web --launch-profile http
   ```
   Дашборд: <http://localhost:5005>. SignalR-хаб: `/alertsHub`.

   > Используйте именно профиль `http` — POST на https-профиль даёт 307/ошибку сертификата dev-cert.

3. **Сгенерировать трафик** (нормальный фон + аномалии brute-force / off-hours / geo):
   ```bash
   dotnet run --project src/Watchtower.LogGenerator
   ```
   На дашборде в реальном времени появятся алерты (без перезагрузки страницы).

4. **Telegram (опционально).** Канал включается, когда заданы токен и чат — иначе no-op.
   Заполните секцию `Telegram` в `src/Watchtower.Web/appsettings.json` (или через user-secrets / env):
   ```json
   "Telegram": { "BotToken": "<токен от @BotFather>", "ChatId": "<id чата>" }
   ```
   После этого каждый новый алерт дублируется сообщением в Telegram.

## Тесты

```bash
dotnet test src/Watchtower.sln
```

Интеграционные тесты поднимают PostgreSQL через Testcontainers — нужен запущенный Docker.

## Пороги детекции

Вынесены в конфигурацию (секция `Detection` в `appsettings.json`): brute-force (N/окно),
off-hours (рабочие часы), privilege-escalation (список авторизованных), impossible-travel (окно).
