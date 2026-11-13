namespace LwCadMapping.Web;

/// <summary>
/// The "CadMapping" configuration section. Keys and secrets (ArcGisApiKey) belong in
/// user secrets or environment variables only - never in appsettings or code.
/// </summary>
public sealed class CadMappingOptions
{
    public const string Section = "CadMapping";

    /// <summary>True to skip the database and run on the demo board (sets CadHost.ForceDemoBoard).</summary>
    public bool Demo { get; set; }

    /// <summary>Full path of La.cfg (sets LaGetConfig.OverridePath). Empty: Core's default search.</summary>
    public string LaCfgPath { get; set; } = "";

    /// <summary>
    /// The CAD database connection, from <c>ConnectionStrings:LwCad</c> (Azure app setting or user
    /// secret - never appsettings). When set it replaces La.cfg's [DATABASE] section. Filled in
    /// Program.cs; not bound from the CadMapping section.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Picks the initial extent from <see cref="Extents"/>.</summary>
    public string AgencyId { get; set; } = "LW";

    /// <summary>ArcGIS location services key (basemaps, geocoding, routing). Sent to the browser - scope it and restrict by referrer.</summary>
    public string ArcGisApiKey { get; set; } = "";

    public string GeocodeUrl { get; set; } = "https://geocode-api.arcgis.com/arcgis/rest/services/World/GeocodeServer";

    public string RouteUrl { get; set; } = "https://route-api.arcgis.com/arcgis/rest/services/World/Route/NAServer/Route_World";

    /// <summary>Seconds between reads of the CAD monitors (one server poll for all users).</summary>
    public int RefreshSeconds { get; set; } = 5;

    /// <summary>Appended to call locations before geocoding when CAD has no city.</summary>
    public string DefaultCity { get; set; } = "Lowell";
    public string DefaultState { get; set; } = "MA";
    public string DefaultZip { get; set; } = "01852";

    /// <summary>Initial extent per agency: [xmin, ymin, xmax, ymax] in WGS84.</summary>
    public Dictionary<string, double[]> Extents { get; set; } = new();

    /// <summary>Replacements for the desktop LocalServer .mpk layers once they are republished.</summary>
    public List<OperationalLayer> OperationalLayers { get; set; } = new();

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ArcGisApiKey);

    /// <summary>The agency extent, or Lowell's legacy setExtent.</summary>
    public double[] AgencyExtent =>
        Extents.TryGetValue(AgencyId, out var e) && e is { Length: 4 } ? e : [-71.382294, 42.601802, -71.231232, 42.669491];
}

public sealed class OperationalLayer
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>feature | map-image | vector-tile | tile</summary>
    public string Type { get; set; } = "feature";

    public bool Visible { get; set; } = true;

    /// <summary>Hidden when zoomed out beyond this scale (0 = no limit). The desktop used 3000 for address layers.</summary>
    public double MinScale { get; set; }
}
