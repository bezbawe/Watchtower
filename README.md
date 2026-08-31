# Watchtower

Учебный SIEM-lite на .NET 9: приём логов → нормализация → детекция аномалий (L1 rule-based +
L2 статистика + L3 ML.NET) → алерты с объяснением → Blazor-дашборд с live-обновлением (SignalR)
и уведомления в Telegram. Батчевая L2/L3-детекция — по расписанию через Hangfire.

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

5. **L2 статистика + Hangfire.** Дашборд Hangfire: <http://localhost:5005/hangfire> (по умолчанию
   только локальные запросы). Recurring job `l2-statistical` считает число событий по часам, строит
   baseline (скользящее среднее + std, EWMA для контекста) и флагает последний завершённый час по
   z-score. Запускается ежечасно; для демо жмите **«Trigger now»** на job'е после искусственного
   всплеска трафика. Пороги — секция `Detection:Statistical` (`WindowHours`, `ZScoreThreshold`, …).

6. **L3 ML.NET + Hangfire.** Recurring job `l3-ml-spike` (та же часовая агрегация, что и L2) гоняет
   ряд через ML.NET SSA spike detection (`DetectSpikeBySsa`) — модель сама учит структуру ряда, без
   ручных порогов. Флагает последний завершённый час алертом `ml_spike` с raw score/p-value в
   объяснении. Запускается ежечасно; для демо — **«Trigger now»** на `/hangfire` после всплеска.
   Пороги — секция `Detection:Ml` (`WindowHours`, `MinBaselinePoints`, `Confidence`, …).

7. **MITRE ATT&CK.** Каждый алерт несёт список техник (`Alert.MitreTechniques`); на дашборде
   бейдж с id (напр. `T1110`) показывает человекочитаемое название по наведению (tooltip) —
   `MitreAttackCatalog`.

8. **Correlation rule.** Детектор `account_compromise_chain` ловит цепочку «неудачный логин →
   успешный логин с того же IP → тот же актор обращается к данным» в пределах короткого окна
   (секция `Detection:Correlation:WindowMinutes`) — один Critical-алерт с `T1110`+`T1005` и всей
   цепочкой событий в `RelatedEventIds`, а не три разрозненных сигнала.

9. **PDF-отчёт по инцидентам.** Карточка «Incident report» на дашборде — выбрать период и
   скачать PDF (`GET /api/reports/incidents?from=&to=`, `IncidentReportService` на QuestPDF):
   список алертов за период с severity, объяснением и техниками MITRE.

## Тесты

```bash
dotnet test src/Watchtower.sln
```

Интеграционные тесты поднимают PostgreSQL через Testcontainers — нужен запущенный Docker.

## Пороги детекции

Вынесены в конфигурацию (секция `Detection` в `appsettings.json`): brute-force (N/окно),
off-hours (рабочие часы), privilege-escalation (список авторизованных), impossible-travel (окно),
statistical (окно baseline, порог z-score), ml (окно ряда, confidence SSA-модели), correlation
(окно между звеньями цепочки).
