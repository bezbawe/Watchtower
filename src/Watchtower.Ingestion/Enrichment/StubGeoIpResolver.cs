using System.Net;
using System.Net.Sockets;

namespace Watchtower.Ingestion.Enrichment;

// Оффлайн-заглушка гео-резолвера: не требует внешней БД/лицензии (MaxMind и т.п.).
// Приватные/loopback адреса помечаются как Internal/LAN; для нескольких публичных
// диапазонов отдаётся демо-гео; остальное — не распознано. Реальный резолвер
// (MaxMind GeoLite2) можно подключить позже за тем же IGeoIpResolver.
public class StubGeoIpResolver : IGeoIpResolver
{
    // Демо-таблица по первому октету публичного IPv4 → (страна, город). Только для показа.
    private static readonly Dictionary<byte, GeoLocation> DemoRanges = new()
    {
        [8] = new GeoLocation("US", "Mountain View"),
        [77] = new GeoLocation("RU", "Moscow"),
        [88] = new GeoLocation("DE", "Berlin"),
        [123] = new GeoLocation("CN", "Beijing"),
        [203] = new GeoLocation("AU", "Sydney"),
    };

    public bool TryResolve(string ip, out GeoLocation location)
    {
        location = default;
        if (!IPAddress.TryParse(ip, out var addr))
            return false;

        if (IPAddress.IsLoopback(addr) || IsPrivate(addr))
        {
            location = new GeoLocation("Internal", "LAN");
            return true;
        }

        if (addr.AddressFamily == AddressFamily.InterNetwork
            && DemoRanges.TryGetValue(addr.GetAddressBytes()[0], out var demo))
        {
            location = demo;
            return true;
        }

        return false;
    }

    private static bool IsPrivate(IPAddress addr)
    {
        if (addr.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var b = addr.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
               || (b[0] == 192 && b[1] == 168);
    }
}
