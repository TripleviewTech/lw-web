using System.Globalization;
using LwCadMapping.Web.Geo;
using LwCadMapping.Web.Services;
using Microsoft.Extensions.Options;
using GeoMath = LwCadMapping.Web.Geo.Geo;

namespace LwCadMapping.Web.State;

/// <summary>
/// One user's CAD Mapping window - the port of the mock-up's ShellViewModel: menu,
/// toolbar, focus maps, controls, monitors and the Map Selection panel. Everything
/// that moves the map goes through <see cref="IMapView"/>. Scoped per circuit; all
/// mutations run on the circuit's dispatcher (the board's Changed event is marshalled
/// by the page with InvokeAsync).
/// </summary>
public sealed class MappingSession : IDisposable
{
    private readonly CadBoard _board;
    private readonly LocationService _locator;
    private readonly CadMappingOptions _options;
    private readonly Random _random = new(11);
    private readonly Dictionary<string, SimUnit> _sim = new();
    private IMapView? _map;
    private int _foundSeq;
    private MapGraphic? _found;
    private MapGraphic? _mark;
    private string? _selectedId;
    private object? _selectedSource;
    private string? _routedUnit;

    public MappingSession(CadBoard board, LocationService locator, IOptions<CadMappingOptions> options)
    {
        _board = board;
        _locator = locator;
        _options = options.Value;
        Board = board.Current;
        BuildLayers();
        SelectedBaseMap = HasApiKey ? "Streets (B)" : BaseMaps.OpenStreetMap;
        SelectedLayer = Layers[0];
        RebuildGraphics();
    }

    /// <summary>Raised after any change the page should render. The page re-renders its tree.</summary>
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();

    // ================================================================ identity

    public BoardSnapshot Board { get; private set; }
    public CadMappingOptions Options => _options;
    public bool HasApiKey => _options.HasApiKey;
    public bool IsDemoBoard => !Board.IsLive;

    /// <summary>"LARIMORE" and "ASSOCIATES" - the top bar's two-weight name.</summary>
    public (string Primary, string Secondary) AgencyName
    {
        get
        {
            var parts = Board.AgencyName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
        }
    }

    // ================================================================ map seam

    public bool IsMapReady => _map is not null;

    /// <summary>Called by CadMap once the ArcGIS view exists.</summary>
    public async Task AttachMap(IMapView map)
    {
        _map = map;
        var e = _options.AgencyExtent;
        HomeCentre = new GeoPoint((e[1] + e[3]) / 2, (e[0] + e[2]) / 2);
        HomeZoom = null;
        ZoomExtentText = "Agency extent (" + _options.AgencyId + ")";
        await map.SetBaseMap(BaseMaps.StyleFor(SelectedBaseMap, HasApiKey));
        await map.SetCoordinateFormats(ShowLatLongDec, ShowLatLongDeg, ShowMapCoords);
        await PushGraphics();
        if (!HasApiKey)
            Status = "No ArcGIS API key - OpenStreetMap base map, demo geography for searching and locating calls, straight-line routes.";
        NotifyChanged();
    }

    public void DetachMap() => _map = null;

    // ==================================================================== data

    /// <summary>A new board from the poller (already on this circuit's dispatcher).</summary>
    public async Task OnBoardChanged(BoardSnapshot snapshot)
    {
        Board = snapshot;
        RebuildGraphics();
        // Keep the selection on the same call/unit across polls.
        if (_selectedId is not null)
        {
            var g = Graphics.FirstOrDefault(x => x.Id == _selectedId) ?? FindGraphicFor(_selectedSource);
            if (g is null) await SelectGraphic(null, null, false);
            else
            {
                _selectedId = g.Id;
                _selectedSource = RefreshSource(_selectedSource) ?? g.Source;
                RefreshSelectionPanel();
                if (IsCalloutOpen && _map is not null) await _map.SetCallout(g);
            }
        }
        foreach (var fm in FocusMaps)
        {
            var g = Graphics.FirstOrDefault(x => x.Id == fm.TargetId);
            if (g is not null) fm.Centre = g.Location;
        }
        await PushGraphics();
        NotifyChanged();
    }

    private object? RefreshSource(object? source) => source switch
    {
        MapCall c => Board.Calls.FirstOrDefault(x => x.Key == c.Key),
        MapUnit u => Board.Units.FirstOrDefault(x => x.UnitNo == u.UnitNo),
        _ => source,
    };

    public IReadOnlyList<MapCall> ActiveCalls =>
        Board.Calls.Where(c => !c.IsPending).OrderBy(c => c.Priority).ThenBy(c => c.ReceivedAt).ToList();

    public IReadOnlyList<MapCall> PendingCalls =>
        Board.Calls.Where(c => c.IsPending).OrderBy(c => c.Priority).ThenBy(c => c.ReceivedAt).ToList();

    public IReadOnlyList<MapUnit> Units => Board.Units;

    public int ActiveCount => Board.Calls.Count(c => !c.IsPending);
    public int PendingCount => Board.Calls.Count(c => c.IsPending);
    public int UnitCount => Board.Units.Count;
    public int UnlocatedCalls => Board.Calls.Count(c => c.Point is null);

    // ================================================================== layers

    public List<MapLayerState> Layers { get; } = new();
    public List<MapLayerState> FeatureLayers { get; } = new();
    public IEnumerable<MapLayerState> AllLayers => Layers.Concat(FeatureLayers);
    public MapLayerState SelectedLayer { get; set; }

    private void BuildLayers()
    {
        void G(string id, string title, string swatch, string detail, bool labels = false) =>
            Layers.Add(new MapLayerState { Id = id, Title = title, Kind = LayerKind.Graphics, Swatch = swatch, Detail = detail, Labels = labels });

        G("Stacked_Calls", "Stacked Calls", "--lw-danger", "More than one call at one location");
        G("Active_Calls", "Active Calls", "--lw-agency-police", "Dispatched calls");
        G("Delayed_Calls", "Delayed Calls", "--lw-warning", "Calls held - no unit free");
        G("Pending_Calls", "Pending Calls", "--lw-text-muted", "Received, not yet dispatched");
        G("Found_Locations", "Found Locations", "--lw-accent", "Search results and marked points", true);
        G("Premise_Locations", "Premise Locations", "--lw-agency-other", "Premises with hazards on file");
        G("Police_Units", "Police Units", "--lw-agency-police", "At their call or station - no AVL in Lowell");
        G("Fire_Units", "Fire Units", "--lw-agency-fire", "At their call or station - no AVL in Lowell");
        G("EMS_Units", "EMS Units", "--lw-agency-ems", "At their call or station - no AVL in Lowell");
        G("Support_Units", "Support Units", "--lw-agency-other", "DPW, tow and other support");

        void F(string id, string title, string swatch, string detail, bool visible, bool labels) =>
            FeatureLayers.Add(new MapLayerState { Id = id, Title = title, Kind = LayerKind.Feature, Swatch = swatch, Detail = detail, Visible = visible, Labels = labels });

        F("Police_Sectors", "Police Sectors", "--lw-agency-police", "Demo geography - approximate sector lines, not the agency's", false, true);
        F("Fire_Districts", "Fire Districts", "--lw-agency-fire", "Demo geography - approximate district lines", false, true);
        F("Hydrants", "Hydrants", "--lw-agency-fire", "Demo geography - synthetic, shown from street zoom (15) in", false, false);
        F("Schools", "Schools", "--lw-accent", "Demo geography - approximate positions", true, false);

        for (var i = 0; i < _options.OperationalLayers.Count; i++)
        {
            var op = _options.OperationalLayers[i];
            FeatureLayers.Add(new MapLayerState
            {
                Id = "op" + i.ToString(CultureInfo.InvariantCulture), Title = op.Title, Kind = LayerKind.Operational, OpIndex = i,
                Swatch = "--lw-success", Visible = op.Visible,
                Detail = $"{op.Type} layer from configuration" + (op.MinScale > 0 ? $", from 1:{op.MinScale:#,0}" : ""),
            });
        }

        FeatureLayers.First(l => l.Id == "Police_Sectors").Count = DemoGeography.PoliceSectors.Count;
        FeatureLayers.First(l => l.Id == "Fire_Districts").Count = DemoGeography.FireDistricts.Count;
        FeatureLayers.First(l => l.Id == "Hydrants").Count = DemoGeography.Hydrants.Count;
        FeatureLayers.First(l => l.Id == "Schools").Count = DemoGeography.Schools.Count;
    }

    private MapLayerState Layer(string id) => AllLayers.First(l => l.Id == id);

    public async Task LayerChanged()
    {
        if (_map is not null) await _map.Draw(VisibleGraphics(), AllLayers.ToList(), MarkerTheme);
        NotifyChanged();
    }

    public async Task ClearSelectedLayer()
    {
        if (SelectedLayer.Id != "Found_Locations")
        {
            Status = $"{SelectedLayer.Id} is filled from CAD and refills on the next poll - only Found_Locations clears.";
            return;
        }
        if (_selectedId is not null && (_selectedId == _found?.Id || _selectedId == _mark?.Id)) await SelectGraphic(null, null, false);
        _found = null;
        _mark = null;
        RebuildGraphics();
        await PushGraphics();
        Status = "Found_Locations cleared.";
    }

    // ================================================================ graphics

    public List<MapGraphic> Graphics { get; } = new();

    private void RebuildGraphics()
    {
        Graphics.Clear();

        foreach (var p in Board.Premises)
        {
            Graphics.Add(new MapGraphic
            {
                Id = $"PMM-{p.No}", Kind = GraphicKind.Premise, Layer = "Premise_Locations", Source = p,
                Lat = p.Point.Latitude, Lon = p.Point.Longitude, Label = GeoMath.TitleCase(p.Name), Agency = "OTH",
                Title = $"PREMISE: {p.Name}", Subtitle = p.Hazards, Address = _locator.FullAddress(p.Address), Footer = p.Contact,
            });
        }

        foreach (var group in Board.Calls.Where(c => c.Point is not null).GroupBy(c => c.Location.Trim().ToUpperInvariant()))
        {
            var list = group.OrderBy(c => c.Priority).ToList();
            var first = list[0];
            var stacked = list.Count > 1;
            var pt = first.Point!.Value;
            Graphics.Add(new MapGraphic
            {
                Id = "CALL-" + first.Key, Kind = GraphicKind.Call, Source = first, Lat = pt.Latitude, Lon = pt.Longitude,
                Layer = stacked ? "Stacked_Calls" : LayerFor(first), Status = stacked ? "ACTIVE" : first.Status,
                Agency = first.Agency, Priority = list.Min(c => c.Priority), Stack = list.Count,
                Label = stacked ? $"{list.Count} calls" : first.CallType,
                Title = stacked ? $"{list.Count} calls at this location" : $"{first.AgencyName} Event: {first.ShortEventNo}",
                Subtitle = stacked ? string.Join(" · ", list.Select(c => $"{c.ShortEventNo} {c.CallType}")) : first.CallType,
                Address = _locator.FullAddress(first.Location),
                Footer = $"Unit(s) Assigned: {string.Join(", ", list.SelectMany(c => c.Units).DefaultIfEmpty("none"))}",
            });
        }

        SyncSimulation();
        foreach (var u in Board.Units)
        {
            var at = UnitPosition(u);
            if (at is null) continue;
            var call = u.CallKey is null ? null : Board.Calls.FirstOrDefault(c => c.Key == u.CallKey);
            Graphics.Add(new MapGraphic
            {
                Id = "UNIT-" + u.UnitNo, Kind = GraphicKind.Unit, Layer = LayerFor(u), Source = u,
                Lat = at.Value.Latitude, Lon = at.Value.Longitude, Label = u.UnitNo, Agency = u.Agency, Status = UnitStatusText(u),
                Title = $"UNIT {u.UnitNo} - {AgencyWord(u.Agency)} {u.UnitType}".TrimEnd(),
                Subtitle = call is null ? UnitStatusText(u) : $"{UnitStatusText(u)} - {call.ShortEventNo} {call.CallType}",
                Address = call is not null ? _locator.FullAddress(call.Location) : string.IsNullOrWhiteSpace(u.Station) ? u.Location : $"Station: {u.Station}",
                Footer = IsGpsTracking ? $"Simulated fix: {LastFixText(u)}  ·  Veh {u.VehId}" : $"Shown {u.PointSource}  ·  Veh {u.VehId}",
            });
        }

        if (_found is not null) Graphics.Add(_found);
        if (_mark is not null) Graphics.Add(_mark);

        foreach (var l in Layers) l.Count = Graphics.Count(g => g.Layer == l.Id);
    }

    private MapGraphic? FindGraphicFor(object? source) => source switch
    {
        MapCall c => Graphics.FirstOrDefault(g => g.Kind == GraphicKind.Call &&
            (g.Source is MapCall gc && (gc.Key == c.Key || (gc.Location.Trim().Equals(c.Location.Trim(), StringComparison.OrdinalIgnoreCase) && c.Point is not null)))),
        MapUnit u => Graphics.FirstOrDefault(g => g.Id == "UNIT-" + u.UnitNo),
        Premise p => Graphics.FirstOrDefault(g => g.Id == $"PMM-{p.No}"),
        _ => null,
    };

    public MapGraphic? GraphicFor(object? row) => FindGraphicFor(row);

    /// <summary>Calls above the priority limit are hidden from the map (not from the monitors).</summary>
    private List<MapGraphic> VisibleGraphics() =>
        Graphics.Where(g => g.Kind != GraphicKind.Call || g.Priority <= PriorityLimit).ToList();

    private async Task PushGraphics()
    {
        if (_map is null) return;
        await _map.Draw(VisibleGraphics(), AllLayers.ToList(), MarkerTheme);
        var sel = SelectedGraphic;
        await _map.Select(sel, false);
    }

    private static string LayerFor(MapCall c) =>
        c.Status == "PENDING" ? "Pending_Calls" : c.Status == "DELAYED" ? "Delayed_Calls" : "Active_Calls";

    private static string LayerFor(MapUnit u) =>
        u.Agency == "PD" ? "Police_Units" : u.Agency == "FD" ? "Fire_Units" : u.Agency == "EMS" ? "EMS_Units" : "Support_Units";

    private static string AgencyWord(string agency) =>
        agency == "PD" ? "POLICE" : agency == "FD" ? "FIRE" : agency == "EMS" ? "EMS" : "SUPPORT";

    // =============================================================== base maps

    public IReadOnlyList<string> BaseMapNames =>
        HasApiKey ? BaseMaps.Legacy.Select(b => b.Name).ToList() : [BaseMaps.OpenStreetMap, "None"];

    public string SelectedBaseMap { get; private set; }

    public async Task SetBaseMap(string name)
    {
        if (!HasApiKey && name != BaseMaps.OpenStreetMap && name != "None")
        {
            Status = $"{name} is an Esri basemap style and needs an ArcGIS API key - showing OpenStreetMap.";
            name = BaseMaps.OpenStreetMap;
        }
        else
        {
            Status = $"Base map: {name}.";
        }
        SelectedBaseMap = name;
        if (_map is not null) await _map.SetBaseMap(BaseMaps.StyleFor(name, HasApiKey));
        NotifyChanged();
    }

    // ================================================================= options

    public IReadOnlyList<string> MarkerThemes { get; } = ["Pins", "Dots"];
    public string MarkerTheme { get; private set; } = "Pins";

    public async Task SetMarkerTheme(string theme)
    {
        MarkerTheme = theme;
        await PushGraphics();
        NotifyChanged();
    }

    public IReadOnlyList<int> PriorityLevels { get; } = Enumerable.Range(1, 9).ToList();
    public int PriorityLimit { get; private set; } = 9;

    public async Task SetPriorityLimit(int value)
    {
        PriorityLimit = value;
        await PushGraphics();
        Status = value >= 9 ? "All priorities shown." : $"Calls with priority {value + 1}-9 hidden from the map.";
        NotifyChanged();
    }

    /// <summary>Legacy "Popup Overview": the callout over the selected symbol.</summary>
    public bool PopupOverview { get; private set; } = true;

    public async Task SetPopupOverview(bool value)
    {
        PopupOverview = value;
        await SetCalloutOpen(value && SelectedGraphic is not null);
    }

    /// <summary>Legacy "Auto Select Graphics": picking a monitor row pans the map to it.</summary>
    public bool AutoSelectGraphics { get; set; } = true;

    /// <summary>Legacy "Mark Location When Selected": Show on Main Map also drops the stick-pin.</summary>
    public bool MarkLocationWhenSelected { get; set; } = true;

    public GeoPoint HomeCentre { get; private set; }
    public double? HomeZoom { get; private set; }
    public string ZoomExtentText { get; private set; } = "";

    /// <summary>Options > Set Zoom Extent: the current view becomes the one Reset Map returns to.</summary>
    public async Task SetZoomExtent()
    {
        if (_map is null) return;
        var vp = await _map.GetViewpoint();
        if (vp is null) return;
        HomeCentre = vp.Centre;
        HomeZoom = vp.Zoom;
        ZoomExtentText = $"{HomeCentre.ToDecimal()} @ {vp.Zoom:0.0}";
        Status = "Zoom extent saved - Reset Map now returns here.";
    }

    // ============================================================ coordinates

    public bool ShowLatLongDec { get; private set; } = true;
    public bool ShowLatLongDeg { get; private set; }
    public bool ShowMapCoords { get; private set; }

    public async Task SetCoordinateFormats(bool? dec = null, bool? deg = null, bool? map = null)
    {
        ShowLatLongDec = dec ?? ShowLatLongDec;
        ShowLatLongDeg = deg ?? ShowLatLongDeg;
        ShowMapCoords = map ?? ShowMapCoords;
        if (_map is not null) await _map.SetCoordinateFormats(ShowLatLongDec, ShowLatLongDeg, ShowMapCoords);
        NotifyChanged();
    }

    // ================================================================== search

    public string SearchText { get; set; } = "";
    public bool IsSearching { get; private set; }

    /// <summary>Toolbar Search Location (legacy ZoomToLocation): geocode, drop the blue star, zoom to it.</summary>
    public async Task Search(string? text = null)
    {
        text ??= SearchText;
        if (string.IsNullOrWhiteSpace(text))
        {
            Status = "Type an address, an intersection (Chelmsford St & Stevens St), a premise, a place or a lat / long.";
            NotifyChanged();
            return;
        }

        IsSearching = true;
        NotifyChanged();
        LocationResult? hit;
        try
        {
            hit = await _locator.SearchAsync(text, Board.Premises, CancellationToken.None);
        }
        finally
        {
            IsSearching = false;
        }

        if (hit is null)
        {
            Status = $"\"{text}\" could not be plotted." + (HasApiKey ? "" : " (Searching the approximate demo geography - add an ArcGIS key for real addresses.)");
            NotifyChanged();
            return;
        }

        var address = hit.MatchType == "Lat/Long"
            ? (await _locator.ReverseAsync(hit.Point, CancellationToken.None)).Display
            : hit.Source == LocationService.ArcGisSource ? hit.Display : _locator.FullAddress(hit.Display.Split(" - ").Last());

        _found = new MapGraphic
        {
            Id = $"FOUND-{++_foundSeq}", Kind = GraphicKind.Found, Layer = "Found_Locations",
            Lat = hit.Point.Latitude, Lon = hit.Point.Longitude, Label = hit.Display, Agency = "OTH",
            Title = $"Found: {hit.Display}",
            Subtitle = $"{hit.MatchType} match" + (hit.Sector.Length > 0 ? $"  ·  Sector {hit.Sector}" : "") + $"  ·  {hit.Source}",
            Address = address, Footer = hit.Point.ToDecimal(), Source = hit,
        };
        RebuildGraphics();
        Layer("Found_Locations").Visible = true;
        await PushGraphics();
        await SelectGraphic(_found, hit, fromMap: true);
        if (_map is not null)
        {
            var vp = await _map.GetViewpoint();
            await _map.Centre(hit.Point, Math.Max(vp?.Zoom ?? 16, 16));
        }
        Status = $"Found {hit.Display} ({hit.MatchType.ToLowerInvariant()}, {hit.Source}).";
        NotifyChanged();
    }

    // ================================================================= marking

    public bool IsMarking { get; private set; }

    public async Task SetMarking(bool on)
    {
        IsMarking = on;
        if (_map is not null) await _map.SetMarking(on);
        Status = on ? "Mark Location - click the map to drop a pin. Esc cancels." : null;
        NotifyChanged();
    }

    private async Task<MapGraphic> DropMark(GeoPoint p, string why)
    {
        var near = await _locator.ReverseAsync(p, CancellationToken.None);
        var sector = _locator.SectorOf(p);
        _mark = new MapGraphic
        {
            Id = $"MARK-{++_foundSeq}", Kind = GraphicKind.Mark, Layer = "Found_Locations",
            Lat = p.Latitude, Lon = p.Longitude, Label = near.Display, Agency = "OTH",
            Title = "Marked location",
            Subtitle = why + (sector.Length > 0 && !why.StartsWith("Sector", StringComparison.Ordinal) ? $"  ·  Sector {sector}" : ""),
            Address = near.MatchType == "Address"
                ? (near.Source == LocationService.ArcGisSource ? near.Display : $"≈ {_locator.FullAddress(near.Display)} ({LocationService.DemoSource})")
                : near.Display,
            Footer = p.ToDecimal(), Source = near,
        };
        Layer("Found_Locations").Visible = true;
        RebuildGraphics();
        await PushGraphics();
        return _mark;
    }

    /// <summary>A click on the main map or a focus map (graphic id when a symbol was hit).</summary>
    public async Task OnMapClick(double lat, double lon, string? graphicId, bool fromFocusMap)
    {
        var p = new GeoPoint(lat, lon);
        if (IsMarking && !fromFocusMap)
        {
            var sector = _locator.SectorOf(p);
            var g = await DropMark(p, sector.Length > 0 ? $"Sector {sector}" : "Reverse geocoded");
            IsMarking = false;
            if (_map is not null) await _map.SetMarking(false);
            await SelectGraphic(g, g.Source, fromMap: true);
            Status = $"Marked {g.Label}.";
            NotifyChanged();
            return;
        }

        var hit = graphicId is null ? null : Graphics.FirstOrDefault(x => x.Id == graphicId);
        await SelectGraphic(hit, hit?.Source, fromMap: true);
        NotifyChanged();
    }

    // =============================================================== selection

    public MapGraphic? SelectedGraphic => _selectedId is null ? null : Graphics.FirstOrDefault(g => g.Id == _selectedId);
    public MapCall? SelectedCall => _selectedSource as MapCall;
    public MapUnit? SelectedUnit => _selectedSource as MapUnit;
    public Premise? SelectedPremise => _selectedSource as Premise;
    public bool HasSelection => SelectedGraphic is not null || _selectedSource is MapCall or MapUnit;

    /// <summary>The selected monitor row, whatever its kind - what the header actions act on.</summary>
    public object? CurrentRow => (object?)SelectedCall ?? SelectedUnit;

    public async Task SelectGraphic(MapGraphic? g, object? source, bool fromMap)
    {
        _selectedSource = source ?? g?.Source;
        _selectedId = g?.Id;

        if (fromMap && SelectedCall is not null) MonitorTab = SelectedCall.IsPending ? "Pending" : "Active";
        if (fromMap && SelectedUnit is not null) MonitorTab = "Units";

        if (SelectedUnit is null || _routedUnit is null || _routedUnit != SelectedUnit.UnitNo) await ClearRoute();

        IsCalloutOpen = PopupOverview && g is not null;
        if (_map is not null)
        {
            await _map.Select(g, !fromMap && g is not null && AutoSelectGraphics);
            await _map.SetCallout(IsCalloutOpen ? g : null);
        }
        RefreshSelectionPanel();
    }

    /// <summary>A monitor row was clicked.</summary>
    public async Task SelectRow(object row)
    {
        var g = FindGraphicFor(row);
        await SelectGraphic(g, row, fromMap: false);
        if (g is null) Status = row is MapCall c ? $"{c.ShortEventNo} could not be located on the map ({c.Location})." : $"{(row as MapUnit)?.UnitNo} has no known position.";
        NotifyChanged();
    }

    public bool IsCalloutOpen { get; private set; }

    public async Task SetCalloutOpen(bool open)
    {
        IsCalloutOpen = open && SelectedGraphic is not null;
        if (_map is not null) await _map.SetCallout(IsCalloutOpen ? SelectedGraphic : null);
        NotifyChanged();
    }

    // ---------------------------------------------------- map selection panel

    public IReadOnlyList<SelectionField> SelectionFields { get; private set; } = [];
    public string? SelectionNotes { get; private set; }
    public string? SelectionHeading { get; private set; }
    public string? SelectionKind { get; private set; }

    private void RefreshSelectionPanel()
    {
        var g = SelectedGraphic;
        var f = new List<SelectionField>();
        string? notes = null, heading = null, kind = null;

        if (SelectedCall is { } c)
        {
            kind = c.AgencyName;
            heading = $"{c.EventNo}  ·  {c.CallType}";
            f.Add(new("Event No", c.EventNo, true));
            f.Add(new("Call Type", c.CallType, true));
            f.Add(new("Location", c.Location, true));
            f.Add(new("Premise", Dash(c.Premise)));
            f.Add(new("Units Assigned", c.Units.Count == 0 ? "-" : c.UnitsText));
            f.Add(new("Case No.", Dash(c.CaseNo)));
            f.Add(new("Priority", c.Priority.ToString(CultureInfo.InvariantCulture)));
            f.Add(new("Status", GeoMath.TitleCase(c.Status)));
            f.Add(new("Received", c.ReceivedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture)));
            f.Add(new("Area", c.Area.Length == 0 ? "-" : c.Area));
            f.Add(new("Map position", c.Point is null ? "Not located" : c.Point.Value.ToDecimal()));
            f.Add(new("Located by", Dash(c.PointSource)));
            notes = c.Notes;
            if (g is not null && g.Stack > 1) notes = $"{g.Stack} calls at this location - {g.Subtitle}.  {notes}";
        }
        else if (SelectedUnit is { } u)
        {
            var call = u.CallKey is null ? null : Board.Calls.FirstOrDefault(x => x.Key == u.CallKey);
            kind = "UNIT";
            heading = $"{u.UnitNo}  ·  {UnitStatusText(u)}";
            f.Add(new("Unit", u.UnitNo, true));
            f.Add(new("Type", Dash(u.UnitType), true));
            f.Add(new("Status", UnitStatusText(u), true));
            f.Add(new("Station", Dash(u.Station)));
            f.Add(new("Event", call?.EventNo ?? "-"));
            f.Add(new("Call Type", call?.CallType ?? Dash(u.CallType)));
            f.Add(new("Location", Dash(u.Location)));
            f.Add(new("Veh ID", Dash(u.VehId)));
            f.Add(new("Last Fix", LastFixText(u)));
            f.Add(new("Position", g is null ? "Unknown" : g.Location.ToDecimal()));
            notes = call?.Notes;
            if (g is not null && !IsGpsTracking) notes = $"Shown {u.PointSource} - Lowell has no AVL feed (ADR-007).  {notes}";
        }
        else if (SelectedPremise is { } p)
        {
            kind = "PREMISE";
            heading = p.Name;
            f.Add(new("Premise", p.Name, true));
            f.Add(new("Address", p.Address, true));
            f.Add(new("Premise No", p.No.ToString(CultureInfo.InvariantCulture)));
            f.Add(new("Contact", p.Contact));
            var sector = _locator.SectorOf(p.Point);
            if (sector.Length > 0) f.Add(new("Area", $"Sector {sector}"));
            notes = p.Hazards + (p.IsDemo ? "  (Fictional demo premise.)" : "");
        }
        else if (g is not null)
        {
            kind = g.Kind == GraphicKind.Mark ? "MARK" : "FOUND";
            heading = g.Label;
            f.Add(new(g.Kind == GraphicKind.Mark ? "Nearest" : "Location", g.Label, true));
            f.Add(new("Lat / Long (Dec)", g.Location.ToDecimal()));
            f.Add(new("Lat / Long (Deg)", g.Location.ToDegrees()));
            f.Add(new("Map Coords", g.Location.ToMapCoordinates()));
            var sector = _locator.SectorOf(g.Location);
            if (sector.Length > 0) f.Add(new("Area", $"Sector {sector} (demo)"));
            f.Add(new("Match", g.Subtitle));
        }

        SelectionFields = f;
        SelectionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        SelectionHeading = heading;
        SelectionKind = kind;
    }

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "-" : s;

    public async Task ShowPremiseForSelection()
    {
        var g = SelectedGraphic;
        if (g is null) return;
        var premise = Board.Premises.OrderBy(p => p.Point.DistanceTo(g.Location)).FirstOrDefault(p => p.Point.DistanceTo(g.Location) < 60);
        if (premise is null)
        {
            Status = "No premise on file at this location.";
            return;
        }
        Layer("Premise_Locations").Visible = true;
        await PushGraphics();
        await SelectGraphic(FindGraphicFor(premise), premise, fromMap: true);
        Status = $"Premise {premise.No}: {premise.Name}.";
    }

    /// <summary>Legacy View History (WinCadCall_ShowHist): calls at this location on the current board.</summary>
    public void ShowHistory()
    {
        var g = SelectedGraphic;
        if (g is null) return;
        var here = Board.Calls.Where(c => c.Point is not null && c.Point.Value.DistanceTo(g.Location) < 40).ToList();
        Status = here.Count == 0
            ? "No calls on the board at this location. (Full call history opens in CAD - not part of this app.)"
            : $"{here.Count} call(s) on the board here: {string.Join(", ", here.Select(c => $"{c.ShortEventNo} {c.CallType}"))}. (Full history opens in CAD.)";
    }

    public string? GoogleMapsUrl => SelectedGraphic is { } g
        ? string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps?q={g.Lat:0.000000},{g.Lon:0.000000}")
        : null;

    // ================================================================ monitors

    /// <summary>Active, Pending or Units - the legacy Live, Pending and Unit Status monitors.</summary>
    public string MonitorTab { get; set; } = "Active";

    public async Task ShowOnMainMap(object? row = null)
    {
        row ??= CurrentRow;
        var g = FindGraphicFor(row);
        if (g is null || _map is null)
        {
            if (row is not null) Status = "That row has no position on the map.";
            NotifyChanged();
            return;
        }

        await SelectGraphic(g, row, fromMap: false);
        if (row is MapCall call) await _map.ShowCall(call);
        else
        {
            var vp = await _map.GetViewpoint();
            await _map.Centre(g.Location, Math.Max(vp?.Zoom ?? 15.5, 15.5));
        }

        if (MarkLocationWhenSelected && row is MapCall c2 && c2.Point is not null)
        {
            await DropMark(c2.Point.Value, $"{c2.ShortEventNo} {c2.CallType}");
            await _map.Select(g, false);
        }
        NotifyChanged();
    }

    public async Task ZoomToRow(object? row = null)
    {
        row ??= CurrentRow;
        var g = FindGraphicFor(row);
        if (g is null || _map is null) return;
        await SelectGraphic(g, row, fromMap: false);
        var vp = await _map.GetViewpoint();
        await _map.Centre(g.Location, Math.Min(19, Math.Max((vp?.Zoom ?? 15) + 2, 17)));
        NotifyChanged();
    }

    // ============================================================== focus maps

    public List<FocusMapState> FocusMaps { get; } = new();
    private int _focusSeq;

    /// <summary>Legacy Show Focus Map: a docked close-up that stays on its call or follows its unit. Up to four.</summary>
    public void OpenFocusMap(object? row = null)
    {
        row ??= CurrentRow;
        var g = FindGraphicFor(row);
        if (g is null)
        {
            Status = "Pick a located call or unit for a focus map.";
            NotifyChanged();
            return;
        }

        var fm = row switch
        {
            MapCall c => new FocusMapState { Id = "fm" + ++_focusSeq, TargetId = g.Id, Kind = "CALL", Title = c.ShortEventNo, Subtitle = $"{c.CallType} - {c.Location}" },
            MapUnit u => new FocusMapState { Id = "fm" + ++_focusSeq, TargetId = g.Id, Kind = "UNIT", Title = u.UnitNo, Subtitle = $"{UnitStatusText(u)} - {u.Location}" },
            _ => null,
        };
        if (fm is null) return;
        fm.Centre = g.Location;

        FocusMaps.RemoveAll(x => x.TargetId == fm.TargetId);
        FocusMaps.Insert(0, fm);
        while (FocusMaps.Count > 4) FocusMaps.RemoveAt(FocusMaps.Count - 1);
        IsFocusPanelExpanded = true;
        Status = $"Focus map opened on {fm.Title}.";
        NotifyChanged();
    }

    public void CloseFocusMap(FocusMapState fm)
    {
        FocusMaps.Remove(fm);
        NotifyChanged();
    }

    public async Task FocusToMain(FocusMapState fm)
    {
        var g = Graphics.FirstOrDefault(x => x.Id == fm.TargetId);
        if (g is null || _map is null) return;
        await SelectGraphic(g, g.Source, fromMap: true);
        var vp = await _map.GetViewpoint();
        await _map.Centre(g.Location, Math.Max(vp?.Zoom ?? 15.5, 15.5));
        NotifyChanged();
    }

    // ===================================================================== GPS

    /// <summary>Controls > GPS > Show Unit Location. Simulated, and only on the demo board: Lowell has no AVL feed (ADR-007).</summary>
    public bool IsGpsTracking { get; private set; }

    public bool CanSimulateGps => IsDemoBoard;

    private sealed class SimUnit
    {
        public GeoPoint Position;
        public GeoPoint Home;
        public double SpeedMph;
        public DateTime LastFix;
        public bool Arrived;
    }

    public async Task SetGpsTracking(bool on)
    {
        if (on && !CanSimulateGps)
        {
            Status = "No AVL feed in Lowell (ADR-007) - units are shown at their call or station. Simulated GPS runs on the demo board only.";
            NotifyChanged();
            return;
        }

        IsGpsTracking = on;
        if (on)
        {
            foreach (var id in new[] { "Police_Units", "Fire_Units", "EMS_Units", "Support_Units" }) Layer(id).Visible = true;
            foreach (var s in _sim.Values) s.Arrived = false;
            GpsTick();
            Status = "GPS tracking on - SIMULATED fixes every second (demo board). En-route units drive to their calls.";
        }
        else
        {
            foreach (var s in _sim.Values) s.SpeedMph = 0;
            Status = "GPS tracking off - units stay at their last known position.";
        }
        RebuildGraphics();
        await PushGraphics();
        NotifyChanged();
    }

    private void SyncSimulation()
    {
        foreach (var u in Board.Units)
        {
            if (u.Point is null || _sim.ContainsKey(u.UnitNo)) continue;
            // Start en-route units a little way off so the drive is visible.
            var start = u.Point.Value;
            if (IsEnRoute(u))
            {
                var h = Math.Abs(u.UnitNo.GetHashCode()) % 360 * Math.PI / 180;
                start = GeoMath.Offset(start, Math.Cos(h) * 1200, Math.Sin(h) * 1200);
            }
            _sim[u.UnitNo] = new SimUnit { Position = start, Home = start, LastFix = DateTime.Now };
        }
    }

    private static bool IsEnRoute(MapUnit u) => u.CallKey is not null && (u.Status is "ENR" or "ER" or "DSP" or "DP");

    private GeoPoint? UnitPosition(MapUnit u) =>
        IsGpsTracking && _sim.TryGetValue(u.UnitNo, out var s) ? s.Position : u.Point;

    private string UnitStatusText(MapUnit u) =>
        IsGpsTracking && _sim.TryGetValue(u.UnitNo, out var s) && s.Arrived ? "On scene (simulated)" : u.StatusText;

    public string LastFixText(MapUnit u) =>
        IsGpsTracking && _sim.TryGetValue(u.UnitNo, out var s) ? s.LastFix.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "no AVL";

    public double SpeedOf(MapUnit u) => IsGpsTracking && _sim.TryGetValue(u.UnitNo, out var s) ? s.SpeedMph : 0;

    /// <summary>One simulated fix per second (the page's timer calls this while tracking).</summary>
    public async Task OnGpsTimer()
    {
        if (!IsGpsTracking) return;
        GpsTick();
        RebuildGraphics();
        foreach (var fm in FocusMaps)
        {
            var g = Graphics.FirstOrDefault(x => x.Id == fm.TargetId);
            if (g is not null) fm.Centre = g.Location;
        }
        await PushGraphics();
        if (_routedUnit is not null && Board.Units.FirstOrDefault(u => u.UnitNo == _routedUnit) is { } ru) await RouteUnit(ru, quiet: true);
        if (SelectedUnit is not null) RefreshSelectionPanel();
        if (IsCalloutOpen && SelectedGraphic is { Kind: GraphicKind.Unit } sg && _map is not null) await _map.SetCallout(sg);
        NotifyChanged();
    }

    private void GpsTick()
    {
        var now = DateTime.Now;
        foreach (var u in Board.Units)
        {
            if (!_sim.TryGetValue(u.UnitNo, out var s)) continue;
            var from = s.Position;
            var to = from;
            var call = u.CallKey is null ? null : Board.Calls.FirstOrDefault(c => c.Key == u.CallKey);

            if (IsEnRoute(u) && call?.Point is { } target && !s.Arrived)
            {
                // 6x time-lapse at about 30 mph so the drive is watchable.
                var stop = GeoMath.Offset(target, 22, -18);
                to = from.StepToward(stop, 80);
                if (to.DistanceTo(stop) < 1)
                {
                    s.Arrived = true;
                    Status = $"{u.UnitNo} arrived on scene at {call.ShortEventNo} {call.Location} (simulated).";
                }
            }
            else if (u.IsAvailable)
            {
                var heading = _random.NextDouble() * Math.PI * 2;
                to = GeoMath.Offset(from, Math.Cos(heading) * 25, Math.Sin(heading) * 25);
                if (to.DistanceTo(s.Home) > 350) to = from.StepToward(s.Home, 40);
            }

            s.SpeedMph = Math.Round(from.DistanceTo(to) / 6.0 * 2.23694, 0);
            s.Position = to;
            s.LastFix = now;
        }
    }

    // =================================================================== route

    public bool HasRoute { get; private set; }
    public string RouteMiles { get; private set; } = "";
    public string RouteKm { get; private set; } = "";
    public string RouteTitle { get; private set; } = "";
    public string RouteNote { get; private set; } = "";

    public bool CanRoute => SelectedUnit?.CallKey is not null;

    /// <summary>Legacy Route to Destination: the selected unit to its call, with the distance box.</summary>
    public async Task RouteSelectedUnit()
    {
        if (SelectedUnit is { } u) await RouteUnit(u, quiet: false);
        NotifyChanged();
    }

    private async Task RouteUnit(MapUnit u, bool quiet)
    {
        var call = u.CallKey is null ? null : Board.Calls.FirstOrDefault(c => c.Key == u.CallKey);
        var from = Graphics.FirstOrDefault(g => g.Id == "UNIT-" + u.UnitNo)?.Location;
        if (call?.Point is null || from is null || _map is null)
        {
            if (!quiet) Status = $"{u.UnitNo} has no call with a known position to route to.";
            return;
        }

        var a = from.Value;
        var b = call.Point.Value;
        if (a.DistanceTo(b) < 30)
        {
            if (!quiet) Status = $"{u.UnitNo} is already at {call.ShortEventNo} - nothing to route. (No AVL: units are drawn at their call.)";
            if (!quiet) return;
        }

        var result = await _map.Route(a, b);
        _routedUnit = u.UnitNo;
        HasRoute = true;
        RouteMiles = result.Miles.ToString("0.00", CultureInfo.InvariantCulture);
        RouteKm = result.Km.ToString("0.00", CultureInfo.InvariantCulture);
        RouteTitle = $"{u.UnitNo} to {call.ShortEventNo}";
        RouteNote = result.ByService ? "ArcGIS routing service" : "straight line";

        if (!quiet)
        {
            await _map.FitTo([a, b]);
            Status = result.ByService
                ? $"Route {u.UnitNo} to {call.Location}: {RouteMiles} mi by street (ArcGIS routing service)."
                : $"Route {u.UnitNo} to {call.Location}: {RouteMiles} mi straight-line distance. " +
                  (result.Error ?? "Street routing needs an ArcGIS API key.");
        }
    }

    private async Task ClearRoute()
    {
        if (!HasRoute) return;
        _routedUnit = null;
        HasRoute = false;
        if (_map is not null) await _map.ClearRoute();
    }

    // ============================================================ menu: tools

    /// <summary>Tools > Load Units: re-reads the board now (read only).</summary>
    public void LoadUnits()
    {
        _sim.Clear();
        _board.RequestRefresh();
        Status = $"Reloading units from {(Board.IsLive ? "CAD" : "the demo board")}... ({Board.Units.Count} on the board).";
        NotifyChanged();
    }

    /// <summary>Tools > Load Premises: turns the premise layer on and recounts it.</summary>
    public async Task LoadPremises()
    {
        Layer("Premise_Locations").Visible = true;
        RebuildGraphics();
        await PushGraphics();
        Status = $"Loaded {Board.Premises.Count} premises" + (Board.Premises.Any(p => p.IsDemo) ? " (fictional demo premises)." : " (premises of calls on the board).");
        NotifyChanged();
    }

    public async Task CopyMap()
    {
        if (_map is null) return;
        Status = await _map.CopyToClipboard();
        NotifyChanged();
    }

    public async Task PrintMap()
    {
        if (_map is null) return;
        Status = "Printing the map (browser print).";
        await _map.Print();
        NotifyChanged();
    }

    public async Task ResetMap()
    {
        if (IsMarking) await SetMarking(false);
        await ClearRoute();
        if (_map is not null)
        {
            if (HomeZoom is null) await _map.FullExtent();
            else await _map.Centre(HomeCentre, HomeZoom);
        }
        Status = "Map reset to the zoom extent.";
        NotifyChanged();
    }

    public async Task ZoomBy(double levels)
    {
        if (_map is not null) await _map.ZoomBy(levels);
    }

    public async Task FullExtent()
    {
        if (_map is not null) await _map.FullExtent();
    }

    public async Task ShowAllUnits()
    {
        var pts = Graphics.Where(g => g.Kind == GraphicKind.Unit).Select(g => g.Location).ToList();
        if (_map is null || pts.Count == 0)
        {
            Status = "No unit positions to show.";
            NotifyChanged();
            return;
        }
        await _map.ShowUnits(pts);
        Status = $"Showing {pts.Count} units.";
        NotifyChanged();
    }

    public async Task Escape()
    {
        if (IsAboutOpen) IsAboutOpen = false;
        else if (IsMarking) await SetMarking(false);
        else if (IsCalloutOpen) await SetCalloutOpen(false);
        else if (HasSelection) await SelectGraphic(null, null, false);
        else Status = null;
        NotifyChanged();
    }

    // ================================================================== chrome

    public bool IsFocusPanelExpanded { get; set; } = true;
    public bool IsControlsPanelExpanded { get; set; } = true;
    public int ControlsTab { get; set; }
    public bool IsAboutOpen { get; set; }
    public bool IsDark { get; set; }
    public string? Status { get; set; }

    public void NotImplemented(string what)
    {
        Status = $"{what} - not part of this app (shown as in the mock-up).";
        NotifyChanged();
    }

    public void Dispose()
    {
        _map = null;
        Changed = null;
    }
}
