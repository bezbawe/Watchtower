namespace Watchtower.Ingestion.Enrichment;

public interface IGeoIpResolver
{
    // Пытается определить гео по IP. false — IP не распознан (гео не проставляется).
    bool TryResolve(string ip, out GeoLocation location);
}
