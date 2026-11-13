using System.Text.Json.Serialization;
using LwCadMapping.Web.Geo;
using LwCadMapping.Web.Services;

namespace LwCadMapping.Web.State;

public enum GraphicKind { Call, Unit, Premise, Found, Mark }

/// <summary>One symbol on the map (the mock-up's MapGraphic). Serialised to the JS module as-is.</summary>
public sealed class MapGraphic
{
    public required string Id { get; init; }
    [JsonIgnore] public GraphicKind Kind { get; init; }
    [JsonPropertyName("kind")] public string KindName => Kind.ToString().ToLowerInvariant();
    public required string Layer { get; init; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string Label { get; init; } = "";
    public string Agency { get; init; } = "OTH";
    public string Status { get; init; } = "";
    public int Priority { get; init; } = 9;
    public int Stack { get; init; } = 1;

    [JsonIgnore] public string Title { get; init; } = "";
    [JsonIgnore] public string Subtitle { get; init; } = "";
    [JsonIgnore] public string Address { get; init; } = "";
    [JsonIgnore] public string Footer { get; init; } = "";
    [JsonIgnore] public object? Source { get; init; }
    [JsonIgnore] public GeoPoint Location => new(Lat, Lon);
}

public enum LayerKind { Graphics, Feature, Operational }

/// <summary>A Layers / Features tab row: legacy overlay id, visibility, labels, fade, size, count.</summary>
public sealed class MapLayerState
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    [JsonIgnore] public LayerKind Kind { get; init; }
    [JsonIgnore] public string Swatch { get; init; } = "--lw-accent";
    [JsonIgnore] public string Detail { get; init; } = "";
    public bool Visible { get; set; } = true;
    public bool Labels { get; set; }

    /// <summary>Opacity, 0.1 .. 1 (the legacy Fade slider).</summary>
    public double Fade { get; set; } = 1;

    /// <summary>Symbol scale, 0.5 .. 2 (the legacy Size slider).</summary>
    public double Size { get; set; } = 1;
    [JsonIgnore] public int Count { get; set; }

    /// <summary>For operational layers: the index into CadMappingOptions.OperationalLayers.</summary>
    public int OpIndex { get; init; } = -1;
}

/// <summary>A docked close-up (legacy FocusMap window).</summary>
public sealed class FocusMapState
{
    public required string Id { get; init; }
    public required string TargetId { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public string Subtitle { get; set; } = "";
    public GeoPoint Centre { get; set; }
    public double Zoom { get; set; } = 16;
}

public sealed record SelectionField(string Label, string Value, bool Emphasis = false);

public sealed record MapViewpoint(double Lat, double Lon, double Zoom)
{
    public GeoPoint Centre => new(Lat, Lon);
}

public sealed record RouteResult(double Miles, double Km, bool ByService, string? Error);

/// <summary>The legacy base-map list (CadMap.xaml.cs _dictBaseMaps) and the ArcGIS basemap style each now uses.</summary>
public static class BaseMaps
{
    public const string OpenStreetMap = "OpenStreetMap";

    public static readonly IReadOnlyList<(string Name, string? Style)> Legacy =
    [
        ("None", null),
        ("Default", "arcgis/streets"),
        ("Streets (A)", "arcgis/navigation"),
        ("Streets (B)", "arcgis/streets"),
        ("Streets (C)", "arcgis/streets-relief"),
        ("Streets (Night)", "arcgis/streets-night"),
        ("Topographic", "arcgis/topographic"),
        ("Light Gray Canvas", "arcgis/light-gray"),
        ("Dark Gray Canvas", "arcgis/dark-gray"),
        ("Imagery", "arcgis/imagery-standard"),
        ("Imagery (With Labels)", "arcgis/imagery"),
        ("Terrain (With Labels)", "arcgis/terrain"),
        ("National Geographic", "arcgis/outdoor"),
    ];

    public static string? StyleFor(string name, bool hasKey) =>
        !hasKey ? (name == "None" ? null : "osm") : Legacy.FirstOrDefault(b => b.Name == name).Style;
}

/// <summary>
/// The seam between the screens and whatever draws the map (ADR-007, the mock-up's
/// Services\IMapView): show call, show units, select and centre, zoom, fit, mark,
/// snapshot. CadMap implements it over the ArcGIS Maps SDK for JavaScript. It is
/// asynchronous because every call crosses to the browser.
/// </summary>
public interface IMapView
{
    /// <summary>Centres on a call at street zoom and selects its symbol.</summary>
    Task ShowCall(MapCall call);

    /// <summary>Fits the view to the given unit positions.</summary>
    Task ShowUnits(IEnumerable<GeoPoint> positions);

    /// <summary>Highlights a graphic and brings it into view without changing the zoom unless it is off screen.</summary>
    Task Select(MapGraphic? graphic, bool bringIntoView);

    Task Centre(GeoPoint point, double? zoom = null);
    Task ZoomBy(double levels);
    Task FullExtent();
    Task FitTo(IEnumerable<GeoPoint> points);
    Task<MapViewpoint?> GetViewpoint();

    /// <summary>Crosshair mode: the next click is reported as a mark.</summary>
    Task SetMarking(bool on);

    /// <summary>The map as a PNG data URL.</summary>
    Task<string?> Snapshot();
    Task<string> CopyToClipboard();
    Task Print();

    // Drawing state pushed from the session.
    Task Draw(IReadOnlyList<MapGraphic> graphics, IReadOnlyList<MapLayerState> layers, string markerTheme);
    Task SetCallout(MapGraphic? graphic);
    Task SetBaseMap(string? style);
    Task SetCoordinateFormats(bool dec, bool deg, bool map);
    Task<RouteResult> Route(GeoPoint from, GeoPoint to);
    Task ClearRoute();
    Task Refresh();
}
