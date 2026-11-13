using LwCadSystemUX.Models;

namespace LwCadSystem.Web.Components.Cad;

/// <summary>
/// The cell text per column key (Converters\CellConverters.cs) for the
/// monitors' DxGrid cell templates, and the agency badge class. The layout
/// job of Views\ColumnStrip.cs is done by DxGrid itself.
/// </summary>
public static class MonitorColumns
{
    /// <summary>CallCellConverter.</summary>
    public static string CallCell(ColumnSpec column, CadCall call) => column.Key switch
    {
        "Agency" => call.AgencyCode,
        "CallType" => call.CallType,
        "Jur" => call.Jurisdiction,
        "Area" => call.Area,
        "Location" => call.Location,
        "Time" => call.Time,
        "Duration" => call.Duration,
        "Priority" => call.PriorityText,
        "Crew" => call.CrewText,
        "Info" => call.Info,
        "CaseNo" => call.CaseNo,
        _ => string.Empty
    };

    /// <summary>UnitCellConverter.</summary>
    public static string UnitCell(ColumnSpec column, CadUnit unit) => column.Key switch
    {
        "Unit" => unit.UnitNo,
        "Station" => unit.Station,
        "Type" => unit.UnitType,
        "Status" => unit.Status,
        "VehId" => unit.VehicleId,
        "CallType" => unit.CallType,
        "Area" => unit.Area,
        "Location" => unit.Location,
        "Time" => unit.StatusTime,
        _ => string.Empty
    };

    /// <summary>The badge class for an agency code (PD / FD / EMS / OTH): AgencyCodeToBrushConverter.</summary>
    public static string AgencyClass(string? code) => "lw-ag-" + (string.IsNullOrWhiteSpace(code) ? "OTH" : code.Trim());
}
