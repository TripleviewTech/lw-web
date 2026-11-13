using LaConfig;
using LwCadMapping.Web.Geo;
using LwCadSystemUX;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;
using Microsoft.Extensions.Options;

namespace LwCadMapping.Web.Services;

/// <summary>
/// The one CAD reader for the whole server. It opens the back end through Core's
/// CadDataServiceFactory (live tables via La.cfg, or the demo board), calls Refresh()
/// every RefreshSeconds on its own thread, locates calls and units, and publishes an
/// immutable <see cref="BoardSnapshot"/>. Circuits only ever see snapshots.
///
/// READ-ONLY: this class calls nothing on ICadDataService but Refresh() and the
/// collection/identity getters. Dispatch, ApplyUnitChange, SelfInitiate, ActivateUnit,
/// AssignUnits and the alert writes are never called anywhere in this app.
/// </summary>
public sealed class CadBoard(
    IOptions<CadMappingOptions> options,
    LocationService locator,
    ILogger<CadBoard> logger) : BackgroundService
{
    private readonly SemaphoreSlim _refreshNow = new(0, 1);
    private ICadDataService? _service;
    private string _sourceStatus = "Opening the CAD back end...";
    private long _version;

    public BoardSnapshot Current { get; private set; } = BoardSnapshot.Empty;

    /// <summary>Raised on the poller thread after a new snapshot is published. Handlers must marshal (InvokeAsync).</summary>
    public event Action<BoardSnapshot>? Changed;

    /// <summary>Tools > Load Units: read the monitors again now (a read, never a write).</summary>
    public void RequestRefresh()
    {
        if (_refreshNow.CurrentCount == 0)
        {
            try { _refreshNow.Release(); } catch (SemaphoreFullException) { }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        CadHost.ForceDemoBoard = o.Demo;
        if (!string.IsNullOrWhiteSpace(o.LaCfgPath)) LaGetConfig.OverridePath = o.LaCfgPath;

        // The factory probes the database (up to 5 s) - keep it off the start-up path.
        await Task.Yield();
        ICadDataService service;
        try
        {
            var (svc, status) = await Task.Run(() =>
            {
                var created = CadDataServiceFactory.Create(out var factoryStatus);
                return (created, factoryStatus);
            }, stoppingToken);
            service = _service = svc;
            _sourceStatus = DescribeSource(svc, status);
            logger.LogInformation("CAD back end: {Status}", _sourceStatus);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not open the CAD back end");
            _sourceStatus = "Could not open the CAD back end: " + ex.Message;
            Publish(Current with { IsLoading = false, SourceStatus = _sourceStatus, LastError = ex.Message });
            return;
        }

        var first = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            string? error = null;
            List<MapCall> calls = [];
            List<MapUnit> units = [];
            try
            {
                // First read was done by the factory's probe on the live tables.
                if (!first) service.Refresh();
                (calls, units) = Copy(service);
                if (service is SqlCadDataService sql && !string.IsNullOrEmpty(sql.LastError)) error = sql.LastError;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                logger.LogWarning(ex, "CAD refresh failed");
            }
            first = false;

            if (error is null || calls.Count > 0 || units.Count > 0)
            {
                try
                {
                    await PublishBoardAsync(calls, units, error, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Locating calls failed");
                }
            }
            else
            {
                Publish(Current with { LastError = error, IsLoading = false });
            }

            try
            {
                await _refreshNow.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, o.RefreshSeconds)), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private static string DescribeSource(ICadDataService service, string factoryStatus)
    {
        if (service is SqlCadDataService)
        {
            // Core's status says the console writes - this app does not.
            var first = factoryStatus.Split(". ")[0].TrimEnd('.');
            return first + ". Read only - this app never writes to CAD.";
        }
        return factoryStatus;
    }

    /// <summary>Copies the bound collections into records. Runs on the poller thread only.</summary>
    private static (List<MapCall>, List<MapUnit>) Copy(ICadDataService s)
    {
        var calls = s.Calls.Where(c => c.Queue != CallQueue.Closed && !c.IsCompleted).Select(c => new MapCall
        {
            Key = c.EventKey.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EventNo = c.EventNo,
            ShortEventNo = ShortEvent(c),
            CallType = string.IsNullOrWhiteSpace(c.CallType) ? c.CallCode : c.CallType,
            Agency = AgencyCode(c.PrimaryAgency),
            AgencyName = AgencyName(c.PrimaryAgency),
            Priority = c.PriorityNumber,
            Status = c.Queue switch { CallQueue.Active => "ACTIVE", CallQueue.Delayed => "DELAYED", _ => "PENDING" },
            Jur = c.Jurisdiction,
            Area = c.Area,
            Location = c.Location,
            Premise = c.Premise,
            ReceivedAt = c.ReceivedAt,
            Units = c.AssignedUnits.Select(u => u.UnitNo).ToList(),
            Info = c.Info is "-" ? "" : c.Info,
            CaseNo = c.CaseNo,
            Notes = c.Notes,
            Origin = c.Origin,
        }).ToList();

        var byUnit = calls.SelectMany(c => c.Units.Select(u => (Unit: u, c.Key)))
            .GroupBy(x => x.Unit).ToDictionary(g => g.Key, g => g.First().Key);

        var units = s.Units.Select(u => new MapUnit
        {
            UnitNo = u.UnitNo,
            Agency = AgencyCode(u.Agency),
            UnitType = u.UnitType,
            Station = u.Station,
            VehId = u.VehicleId,
            Status = u.Status,
            StatusText = StatusText(u),
            IsAvailable = u.IsAvailable,
            IsOutOfService = u.Availability == UnitAvailability.OutOfService,
            CallType = u.CallType,
            Area = u.Area,
            Location = u.Location,
            Time = u.StatusTime,
            CallKey = u.AssignedCallKey != 0
                ? u.AssignedCallKey.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : byUnit.GetValueOrDefault(u.UnitNo),
        }).ToList();

        return (calls, units);
    }

    private async Task PublishBoardAsync(List<MapCall> calls, List<MapUnit> units, string? error, CancellationToken ct)
    {
        // Calls: CAD leaves STK1 X/Y at 0 (and Core does not surface them), so geocode the location.
        var located = new List<MapCall>(calls.Count);
        foreach (var c in calls)
        {
            var hit = await locator.LocateAsync(c.Location, ct);
            located.Add(hit is null ? c : c with { Point = hit.Value.Point, PointSource = hit.Value.Source });
        }

        var callsByKey = located.ToDictionary(c => c.Key);
        var placedUnits = new List<MapUnit>(units.Count);
        foreach (var u in units)
        {
            // No AVL in Lowell (ADR-007): a unit is drawn at its call, else its location, else its station.
            if (u.CallKey is not null && callsByKey.TryGetValue(u.CallKey, out var call) && call.Point is not null)
            {
                placedUnits.Add(u with { Point = call.Point, PointSource = "at its call " + call.ShortEventNo });
                continue;
            }

            var where = !string.IsNullOrWhiteSpace(u.Location) && !u.Location.Contains("..") ? u.Location : u.Station;
            var hit = string.IsNullOrWhiteSpace(where) || where.Contains("..") ? null : await locator.LocateAsync(where, ct);
            placedUnits.Add(hit is null ? u : u with
            {
                Point = hit.Value.Point,
                PointSource = (where == u.Station ? "station " : "location ") + where + " (" + hit.Value.Source + ")",
            });
        }

        var demo = _service is not SqlCadDataService;
        // Fictional demo premises only on the demo board; live data shows the premises of calls on the board.
        var premises = demo
            ? DemoPremises.Build()
            : located.Where(c => !string.IsNullOrWhiteSpace(c.Premise) && c.Point is not null)
                .GroupBy(c => c.Premise)
                .Select((g, i) => new Premise
                {
                    No = i + 1, Name = g.Key, Address = g.First().Location, Point = g.First().Point!.Value,
                    Hazards = "Premise on file in CAD (PMM1). Hazard notes open in CAD.", Contact = "-",
                })
                .ToList();

        var s = _service!;
        var snapshot = new BoardSnapshot
        {
            Version = Interlocked.Increment(ref _version),
            TakenAt = DateTime.Now,
            Calls = located,
            Units = placedUnits,
            Premises = premises,
            IsLive = !demo,
            IsLoading = false,
            AgencyName = string.IsNullOrWhiteSpace(s.AgencyName) ? "LARIMORE ASSOCIATES" : s.AgencyName.Trim(),
            OperatorId = s.OperatorId ?? "",
            SessionId = s.SessionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceStatus = _sourceStatus,
            GeocodeStatus = locator.UsesArcGis
                ? "Calls located with the ArcGIS World geocoder (cached)."
                : "No ArcGIS key - calls placed on approximate demo geography.",
            LastError = error,
        };

        if (!SameBoard(Current, snapshot)) Publish(snapshot);
        else Current = Current with { TakenAt = snapshot.TakenAt };
    }

    private static bool SameBoard(BoardSnapshot a, BoardSnapshot b) =>
        a.Calls.SequenceEqual(b.Calls, MapCallComparer.Instance) &&
        a.Units.SequenceEqual(b.Units) && a.LastError == b.LastError && a.SourceStatus == b.SourceStatus && !a.IsLoading;

    private void Publish(BoardSnapshot snapshot)
    {
        Current = snapshot;
        try { Changed?.Invoke(snapshot); }
        catch (Exception ex) { logger.LogWarning(ex, "A board subscriber failed"); }
    }

    private static string ShortEvent(CadCall c) =>
        c.EventNo2 > 0 ? $"{c.EventType}/{c.EventNo2}" : c.EventNo;

    private static string AgencyCode(Agency a) => a switch
    {
        Agency.Fire => "FD",
        Agency.Ems => "EMS",
        Agency.Other => "OTH",
        _ => "PD",
    };

    private static string AgencyName(Agency a) => a switch
    {
        Agency.Fire => "FIRE",
        Agency.Ems => "EMS",
        Agency.Other => "OTHER",
        _ => "POLICE",
    };

    private static string StatusText(CadUnit u) => u.Status switch
    {
        "AVL" or "AV" => "Available",
        "ENR" or "ER" => "En route",
        "ONS" or "OS" => "On scene",
        "DSP" or "DP" => "Dispatched",
        _ => u.Availability switch
        {
            UnitAvailability.Available => string.IsNullOrWhiteSpace(u.Status) ? "Available" : u.Status,
            UnitAvailability.OutOfService => "Out of service" + (string.IsNullOrWhiteSpace(u.Status) ? "" : $" ({u.Status})"),
            _ => string.IsNullOrWhiteSpace(u.Status) ? "Assigned" : u.Status,
        },
    };

    /// <summary>Record equality, but the units list compared by content.</summary>
    private sealed class MapCallComparer : IEqualityComparer<MapCall>
    {
        public static readonly MapCallComparer Instance = new();
        private static readonly string[] NoUnits = [];

        public bool Equals(MapCall? x, MapCall? y) =>
            x is not null && y is not null &&
            (x with { Units = NoUnits }) == (y with { Units = NoUnits }) && x.Units.SequenceEqual(y.Units);

        public int GetHashCode(MapCall obj) => obj.Key.GetHashCode();
    }
}
