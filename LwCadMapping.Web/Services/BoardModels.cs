using LwCadMapping.Web.Geo;

namespace LwCadMapping.Web.Services;

/// <summary>
/// One CAD call as the mapping screens use it: an immutable copy of a Core CadCall
/// taken on the poller thread, plus where it was located on the map.
/// </summary>
public sealed record MapCall
{
    /// <summary>Stable key (the STK1 S_KEY).</summary>
    public required string Key { get; init; }
    public required string EventNo { get; init; }
    public string ShortEventNo { get; init; } = "";
    public string CallType { get; init; } = "";

    /// <summary>PD, FD, EMS or OTH.</summary>
    public string Agency { get; init; } = "PD";
    public string AgencyName { get; init; } = "POLICE";
    public int Priority { get; init; } = 5;

    /// <summary>ACTIVE, DELAYED or PENDING (stacked, not yet dispatched).</summary>
    public string Status { get; init; } = "PENDING";
    public bool IsPending => Status == "PENDING";
    public string Jur { get; init; } = "";
    public string Area { get; init; } = "";
    public string Location { get; init; } = "";
    public string Premise { get; init; } = "";
    public DateTime ReceivedAt { get; init; }
    public string Time => ReceivedAt.ToString("HH:mm");
    public IReadOnlyList<string> Units { get; init; } = [];
    public string UnitsText => Units.Count == 0 ? "" : string.Join(", ", Units);
    public string Info { get; init; } = "";
    public string CaseNo { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Origin { get; init; } = "";

    /// <summary>Where the call sits on the map; null when it could not be located.</summary>
    public GeoPoint? Point { get; init; }

    /// <summary>How <see cref="Point"/> was found - e.g. "ArcGIS World geocoder" or "approximate demo geography".</summary>
    public string PointSource { get; init; } = "";
}

/// <summary>One roster unit, as the mapping screens use it.</summary>
public sealed record MapUnit
{
    public required string UnitNo { get; init; }
    public string Agency { get; init; } = "PD";
    public string UnitType { get; init; } = "";
    public string Station { get; init; } = "";
    public string VehId { get; init; } = "";

    /// <summary>The CAD status abbreviation (UST1), e.g. AVL, ENR, ONS.</summary>
    public string Status { get; init; } = "";
    public string StatusText { get; init; } = "";
    public bool IsAvailable { get; init; }
    public bool IsOutOfService { get; init; }
    public string CallType { get; init; } = "";
    public string Area { get; init; } = "";
    public string Location { get; init; } = "";
    public string Time { get; init; } = "";

    /// <summary>Key of the call the unit is on, or null.</summary>
    public string? CallKey { get; init; }

    /// <summary>Where the unit is drawn: its call, its location or its station. Lowell has no AVL (ADR-007).</summary>
    public GeoPoint? Point { get; init; }
    public string PointSource { get; init; } = "";
}

/// <summary>An immutable board, published to every circuit after each poll.</summary>
public sealed record BoardSnapshot
{
    public static readonly BoardSnapshot Empty = new();

    public long Version { get; init; }
    public DateTime TakenAt { get; init; } = DateTime.Now;
    public IReadOnlyList<MapCall> Calls { get; init; } = [];
    public IReadOnlyList<MapUnit> Units { get; init; } = [];
    public IReadOnlyList<Premise> Premises { get; init; } = [];
    public bool IsLive { get; init; }
    public bool IsLoading { get; init; } = true;
    public string AgencyName { get; init; } = "LARIMORE ASSOCIATES";
    public string OperatorId { get; init; } = "";
    public string SessionId { get; init; } = "";

    /// <summary>One line on where the data comes from (database, demo board, why).</summary>
    public string SourceStatus { get; init; } = "Starting...";

    /// <summary>How calls are located: ArcGIS geocoder or approximate demo geography.</summary>
    public string GeocodeStatus { get; init; } = "";
    public string? LastError { get; init; }
}
