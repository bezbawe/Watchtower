namespace Watchtower.LogGenerator;

// Чистый детерминированный движок генерации: по конфигу и «текущему времени» строит
// список событий = нормальный фон + управляемые вкрапления аномалий. Никакого I/O —
// поэтому наличие аномалий в потоке проверяется unit-тестом без живого API.
//
// Каждое аномальное событие помечено Fields["scenario"] (brute_force | off_hours |
// geo_anomaly) — это упрощает и проверку, и последующую детекцию/объяснимость.
public static class SyntheticLogStream
{
    private static readonly string[] Users = ["alice", "bob", "carol", "dave", "erin"];
    private static readonly string[] AdminUsers = ["admin", "root", "svc_backup"];
    private static readonly string[] Sources = ["auth-service", "api-gateway", "file-server", "admin-console"];

    // Внутренние адреса (stub-резолвер помечает их Internal/LAN) — «нормальный» трафик.
    private static readonly string[] InternalIps = ["10.0.0.11", "10.0.0.12", "192.168.1.20", "192.168.1.21"];

    // Публичные IP атакующих для brute-force (первый октет распознаётся stub-резолвером).
    private static readonly string[] AttackerIps = ["77.240.1.10", "203.0.113.55"]; // RU, AU

    // Пары гео-удалённых IP для impossible travel (разные страны у stub-резолвера).
    private static readonly (string From, string To)[] GeoPairs =
    [
        ("8.8.8.8", "123.10.20.30"),   // US -> CN
        ("203.0.113.9", "77.75.10.5"), // AU -> RU
    ];

    public static List<SyntheticEvent> Generate(GeneratorOptions options, DateTimeOffset now)
    {
        var rng = new Random(options.Seed);
        var start = now.AddHours(-Math.Max(1, options.PeriodHours));
        var events = new List<SyntheticEvent>();

        AddNormalBackground(events, rng, options, start, now);

        if (options.BruteForce.Enabled)
            AddBruteForce(events, rng, options.BruteForce, start, now);

        if (options.OffHours.Enabled)
            AddOffHours(events, rng, options.OffHours, start, now);

        if (options.GeoAnomaly.Enabled)
            AddGeoAnomaly(events, rng, options.GeoAnomaly, start, now);

        // Отдаём в хронологическом порядке — так поток похож на реальную ленту.
        return events.OrderBy(e => e.Timestamp).ToList();
    }

    private static void AddNormalBackground(
        List<SyntheticEvent> events, Random rng, GeneratorOptions options,
        DateTimeOffset start, DateTimeOffset now)
    {
        var total = Math.Max(0, options.PeriodHours * options.NormalEventsPerHour);
        for (var i = 0; i < total; i++)
        {
            var type = PickNormalEventType(rng);
            var user = Users[rng.Next(Users.Length)];
            var ip = InternalIps[rng.Next(InternalIps.Length)];
            var ts = RandomTime(rng, start, now, minHour: 8, maxHour: 18);

            events.Add(new SyntheticEvent
            {
                Timestamp = ts,
                Source = Sources[rng.Next(Sources.Length)],
                Severity = SeverityFor(type),
                EventType = type,
                Message = $"{type} for {user}",
                Actor = user,
                SourceIp = ip,
            });
        }
    }

    private static void AddBruteForce(
        List<SyntheticEvent> events, Random rng, BruteForceScenario cfg,
        DateTimeOffset start, DateTimeOffset now)
    {
        for (var inc = 0; inc < cfg.Incidents; inc++)
        {
            var ip = AttackerIps[inc % AttackerIps.Length];
            var target = Users[rng.Next(Users.Length)];
            var baseTime = RandomTime(rng, start, now, minHour: 0, maxHour: 23);

            for (var a = 0; a < cfg.Attempts; a++)
            {
                var ts = baseTime.AddSeconds(rng.NextDouble() * cfg.WithinMinutes * 60);
                events.Add(new SyntheticEvent
                {
                    Timestamp = ts,
                    Source = "auth-service",
                    Severity = "Warning",
                    EventType = "login_failed",
                    Message = $"failed password for {target} from {ip}",
                    Actor = target,
                    SourceIp = ip,
                    Fields = new Dictionary<string, string>
                    {
                        ["scenario"] = "brute_force",
                        ["attempt"] = (a + 1).ToString(),
                    },
                });
            }

            // Пробитие: один успешный логин сразу после серии неудач (для correlation в будущем).
            events.Add(new SyntheticEvent
            {
                Timestamp = baseTime.AddMinutes(cfg.WithinMinutes).AddSeconds(rng.Next(30)),
                Source = "auth-service",
                Severity = "Error",
                EventType = "login_success",
                Message = $"accepted password for {target} from {ip}",
                Actor = target,
                SourceIp = ip,
                Fields = new Dictionary<string, string>
                {
                    ["scenario"] = "brute_force",
                    ["outcome"] = "success",
                },
            });
        }
    }

    private static void AddOffHours(
        List<SyntheticEvent> events, Random rng, OffHoursScenario cfg,
        DateTimeOffset start, DateTimeOffset now)
    {
        for (var inc = 0; inc < cfg.Incidents; inc++)
        {
            var user = AdminUsers[rng.Next(AdminUsers.Length)];
            var ts = RandomTime(rng, start, now, minHour: 1, maxHour: 5); // глубокая ночь
            var privileged = rng.Next(2) == 0;

            events.Add(new SyntheticEvent
            {
                Timestamp = ts,
                Source = "admin-console",
                Severity = privileged ? "Error" : "Warning",
                EventType = privileged ? "privilege_action" : "config_change",
                Message = $"{user} performed admin action at {ts:HH:mm}",
                Actor = user,
                SourceIp = InternalIps[rng.Next(InternalIps.Length)],
                Fields = new Dictionary<string, string>
                {
                    ["scenario"] = "off_hours",
                    ["hour"] = ts.Hour.ToString(),
                },
            });
        }
    }

    private static void AddGeoAnomaly(
        List<SyntheticEvent> events, Random rng, GeoAnomalyScenario cfg,
        DateTimeOffset start, DateTimeOffset now)
    {
        for (var inc = 0; inc < cfg.Incidents; inc++)
        {
            var user = Users[rng.Next(Users.Length)];
            var (fromIp, toIp) = GeoPairs[inc % GeoPairs.Length];
            var first = RandomTime(rng, start, now, minHour: 8, maxHour: 18);
            var second = first.AddMinutes(rng.Next(5, 30)); // слишком быстро для смены страны

            events.Add(GeoLogin(user, fromIp, first, leg: 1));
            events.Add(GeoLogin(user, toIp, second, leg: 2));
        }
    }

    private static SyntheticEvent GeoLogin(string user, string ip, DateTimeOffset ts, int leg) =>
        new()
        {
            Timestamp = ts,
            Source = "auth-service",
            Severity = "Info",
            EventType = "login_success",
            Message = $"accepted password for {user} from {ip}",
            Actor = user,
            SourceIp = ip,
            Fields = new Dictionary<string, string>
            {
                ["scenario"] = "geo_anomaly",
                ["leg"] = leg.ToString(),
            },
        };

    private static string PickNormalEventType(Random rng) => rng.Next(100) switch
    {
        < 55 => "login_success",
        < 80 => "data_access",
        < 90 => "logout",
        < 98 => "login_failed", // редкие «нормальные» опечатки пароля
        _ => "config_change",
    };

    private static string SeverityFor(string eventType) => eventType switch
    {
        "login_failed" => "Warning",
        "config_change" => "Warning",
        _ => "Info",
    };

    // Случайный момент в [start, end], в будний день и в заданном диапазоне часов.
    private static DateTimeOffset RandomTime(
        Random rng, DateTimeOffset start, DateTimeOffset end, int minHour, int maxHour)
    {
        var totalDays = Math.Max(1, (int)Math.Ceiling((end - start).TotalDays));
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var day = start.AddDays(rng.Next(totalDays + 1)).Date;
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            var ts = new DateTimeOffset(day, TimeSpan.Zero)
                .AddHours(rng.Next(minHour, maxHour + 1))
                .AddMinutes(rng.Next(60))
                .AddSeconds(rng.Next(60));

            if (ts >= start && ts <= end)
                return ts;
        }

        return end;
    }
}
