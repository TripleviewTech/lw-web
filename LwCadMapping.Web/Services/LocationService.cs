using System.Text.RegularExpressions;
using LwCadMapping.Web.Geo;
using Microsoft.Extensions.Options;

namespace LwCadMapping.Web.Services;

/// <summary>A located place: search hit, marked point or geocoded call.</summary>
public sealed record LocationResult(GeoPoint Point, string Display, string MatchType, string Sector, string Source);

/// <summary>
/// The geocoder service of ADR-007 (the mock-up's LocationSearch): search, reverse and
/// call locating. With an ArcGIS key it uses the ArcGIS World geocoder server-side;
/// without one it falls back to the mock-up's approximate demo geography of Lowell,
/// and says so wherever a result is shown.
/// </summary>
public sealed partial class LocationService(ArcGisLocator arcgis, IOptions<CadMappingOptions> options)
{
    public const string DemoSource = "approximate demo geography";
    public const string ArcGisSource = "ArcGIS World geocoder";

    private readonly DemoLocationSearch _demo = new(() => DemoPremises.Build());

    public bool UsesArcGis => arcgis.IsEnabled;

    public string GeocoderName => UsesArcGis ? ArcGisSource : DemoSource;

    private CadMappingOptions O => options.Value;

    /// <summary>"16 Main St, Lowell, MA, 01852" - the legacy callout's address line.</summary>
    public string FullAddress(string street) =>
        string.IsNullOrWhiteSpace(street) ? "" : $"{Geo.Geo.TitleCase(street.Trim())}, {O.DefaultCity}, {O.DefaultState}, {O.DefaultZip}";

    /// <summary>Places a CAD location (call, unit location or station) on the map.</summary>
    public async Task<(GeoPoint Point, string Source)?> LocateAsync(string location, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        if (GeoPoint.TryParse(location, out var ll)) return (ll, "coordinates");

        if (UsesArcGis)
        {
            var hit = await arcgis.GeocodeForStorageAsync($"{CleanCadLocation(location)}, {O.DefaultCity}, {O.DefaultState}", ct);
            return hit is null ? null : (new GeoPoint(hit.Lat, hit.Lon), ArcGisSource);
        }

        var demo = _demo.Find(location);
        if (demo is not null) return (demo.Point, DemoSource);
        return (HashPlace(location), DemoSource);
    }

    /// <summary>Toolbar Search Location: a lat/long, a premise, then the geocoder.</summary>
    public async Task<LocationResult?> SearchAsync(string text, IEnumerable<Premise> premises, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (GeoPoint.TryParse(text, out var ll))
            return new LocationResult(ll, ll.ToDecimal(), "Lat/Long", SectorOf(ll), "coordinates");

        var q = DemoLocationSearch.Normalise(text);
        var premise = premises.FirstOrDefault(p =>
            DemoLocationSearch.Normalise(p.Name).Contains(q) || DemoLocationSearch.Normalise(p.Address) == q);
        if (premise is not null)
            return new LocationResult(premise.Point, $"{premise.Name} - {premise.Address}", "Premise", SectorOf(premise.Point),
                premise.IsDemo ? DemoSource : "CAD premise");

        if (UsesArcGis)
        {
            var hits = await arcgis.SearchAsync(text.Contains(',') ? text : $"{text}, {O.DefaultCity}, {O.DefaultState}", ct);
            var best = hits.FirstOrDefault();
            if (best is null) return null;
            var type = best.Type switch
            {
                "StreetInt" => "Intersection",
                "PointAddress" or "StreetAddress" or "Subaddress" => "Address",
                "POI" => "Place",
                "StreetName" => "Street",
                "" => "Address",
                _ => best.Type,
            };
            var p = new GeoPoint(best.Lat, best.Lon);
            return new LocationResult(p, best.Address, type, "", ArcGisSource);
        }

        var demo = _demo.Find(text);
        return demo is null ? null : new LocationResult(demo.Point, demo.Display, demo.MatchType, demo.Sector ?? "", DemoSource);
    }

    /// <summary>Mark Location: the nearest address (reverse geocode).</summary>
    public async Task<LocationResult> ReverseAsync(GeoPoint p, CancellationToken ct)
    {
        if (UsesArcGis)
        {
            var address = await arcgis.ReverseAsync(p.Longitude, p.Latitude, ct);
            return address is null
                ? new LocationResult(p, p.ToDecimal(), "Lat/Long", "", ArcGisSource)
                : new LocationResult(p, address, "Address", "", ArcGisSource);
        }

        var near = _demo.Reverse(p);
        return new LocationResult(p, near.Display, near.MatchType, near.Sector ?? "", DemoSource);
    }

    /// <summary>Police sector from the demo sectors - only meaningful on the demo geography.</summary>
    public string SectorOf(GeoPoint p) => UsesArcGis ? "" : DemoGeography.SectorOf(p);

    /// <summary>Takes the CAD's location text down to what a geocoder reads ("120 BRIDGE ST, APT 2" keeps "120 BRIDGE ST, APT 2").</summary>
    private static string CleanCadLocation(string location) => Spaces().Replace(location.Replace('/', '&'), " ").Trim();

    /// <summary>
    /// Demo fallback for addresses the demo street file does not know (the Core demo
    /// board uses generic streets): a stable spot on one of the named demo streets.
    /// </summary>
    private static GeoPoint HashPlace(string text)
    {
        var norm = DemoLocationSearch.Normalise(text);
        uint h = 2166136261;
        foreach (var ch in norm) h = (h ^ ch) * 16777619;

        var roads = DemoGeography.Roads.Where(r => r.Name != null && r.MaxNumber > 0).ToList();
        var road = roads[(int)(h % (uint)roads.Count)];
        var m = Number().Match(norm);
        var no = m.Success ? int.Parse(m.Groups[1].Value) : (int)(h / 7 % (uint)Math.Max(1, road.MaxNumber));
        return DemoLocationSearch.Geocode(road, Math.Max(1, no % Math.Max(2, road.MaxNumber)));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(\d+)")]
    private static partial Regex Number();
}
