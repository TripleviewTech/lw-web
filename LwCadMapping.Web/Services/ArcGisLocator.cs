using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LwCadMapping.Web.Services;

/// <summary>A geocoder hit.</summary>
public sealed record GeocodeHit(double Lon, double Lat, string Address, double Score, string Type);

/// <summary>
/// Server-side ArcGIS World geocoder (ported from the LwCadMapping.Web spike's Geocoder):
/// call locations are geocoded once per server with forStorage=true (Esri's terms for
/// cached results) and cached; interactive search and reverse geocoding are not stored.
/// The token travels in a POST body, never in a URL, so request logging cannot leak it.
/// </summary>
public sealed class ArcGisLocator(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IOptions<CadMappingOptions> options,
    ILogger<ArcGisLocator> logger)
{
    private const int MinScore = 80;
    private readonly SemaphoreSlim _throttle = new(4);

    public bool IsEnabled => options.Value.HasApiKey;

    /// <summary>Geocodes a call location for storage (cached 7 days, misses 10 minutes).</summary>
    public async Task<GeocodeHit?> GeocodeForStorageAsync(string singleLine, CancellationToken ct)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(singleLine)) return null;

        var key = "geo:" + singleLine.Trim().ToUpperInvariant();
        if (cache.TryGetValue(key, out GeocodeHit? hit)) return hit;

        var result = await FindAsync(singleLine, forStorage: true, maxLocations: 1, ct);
        if (result is null) return null; // service error: not cached

        var best = result.FirstOrDefault(h => h.Score >= MinScore);
        cache.Set(key, best, best is null ? TimeSpan.FromMinutes(10) : TimeSpan.FromDays(7));
        return best;
    }

    /// <summary>Interactive Search Location: candidates near the agency extent (not stored).</summary>
    public async Task<IReadOnlyList<GeocodeHit>> SearchAsync(string singleLine, CancellationToken ct) =>
        (await FindAsync(singleLine, forStorage: false, maxLocations: 5, ct))?.Where(h => h.Score >= MinScore).ToList() ?? [];

    /// <summary>Mark Location: the nearest address to a point.</summary>
    public async Task<string?> ReverseAsync(double lon, double lat, CancellationToken ct)
    {
        if (!IsEnabled) return null;
        var form = new Dictionary<string, string>
        {
            ["location"] = string.Create(CultureInfo.InvariantCulture, $"{lon},{lat}"),
            ["outSR"] = "4326",
            ["f"] = "json",
        };
        using var doc = await PostAsync("reverseGeocode", form, ct);
        if (doc is null) return null;
        if (doc.RootElement.TryGetProperty("address", out var address) &&
            address.TryGetProperty("LongLabel", out var label))
            return label.GetString();
        if (doc.RootElement.TryGetProperty("address", out address) &&
            address.TryGetProperty("Match_addr", out label))
            return label.GetString();
        return null;
    }

    private async Task<List<GeocodeHit>?> FindAsync(string singleLine, bool forStorage, int maxLocations, CancellationToken ct)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(singleLine)) return null;
        var e = options.Value.AgencyExtent;
        var form = new Dictionary<string, string>
        {
            ["SingleLine"] = singleLine,
            ["maxLocations"] = maxLocations.ToString(CultureInfo.InvariantCulture),
            ["outSR"] = "4326",
            ["outFields"] = "Addr_type",
            ["location"] = string.Create(CultureInfo.InvariantCulture, $"{(e[0] + e[2]) / 2},{(e[1] + e[3]) / 2}"),
            ["f"] = "json",
        };
        // Required by Esri's terms when results are kept; billed at the stored-geocode rate.
        if (forStorage) form["forStorage"] = "true";

        using var doc = await PostAsync("findAddressCandidates", form, ct);
        if (doc is null) return null;

        var list = new List<GeocodeHit>();
        if (doc.RootElement.TryGetProperty("candidates", out var candidates))
        {
            foreach (var c in candidates.EnumerateArray())
            {
                var loc = c.GetProperty("location");
                var type = c.TryGetProperty("attributes", out var a) && a.TryGetProperty("Addr_type", out var t) ? t.GetString() ?? "" : "";
                list.Add(new GeocodeHit(loc.GetProperty("x").GetDouble(), loc.GetProperty("y").GetDouble(),
                    c.GetProperty("address").GetString() ?? singleLine, c.GetProperty("score").GetDouble(), type));
            }
        }
        return list;
    }

    private async Task<JsonDocument?> PostAsync(string operation, Dictionary<string, string> form, CancellationToken ct)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            var o = options.Value;
            form["token"] = o.ArcGisApiKey;
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClientFactory.CreateClient("arcgis")
                .PostAsync($"{o.GeocodeUrl.TrimEnd('/')}/{operation}", content, ct);
            response.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                // The error object carries a code and message only - never the token.
                logger.LogWarning("ArcGIS {Operation} error: {Code} {Message}", operation,
                    error.TryGetProperty("code", out var code) ? code.ToString() : "?",
                    error.TryGetProperty("message", out var msg) ? msg.GetString() : "");
                doc.Dispose();
                return null;
            }
            return doc;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("ArcGIS {Operation} failed: {Error}", operation, ex.Message);
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }
}
