using System.Collections.Generic;
using System.Collections.ObjectModel;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.SelfInit;
using LwCadSystemUX.Services.TakeCall;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Everything the dashboard needs from the CAD back end. The demo
    /// implementation keeps it all in memory; a production implementation maps
    /// the same members onto the DSE1 / STK1 / DSU1 tables the legacy
    /// LwCadSystem talks to, and the UI does not change.
    /// </summary>
    public interface ICadDataService
    {
        string AgencyName { get; }
        string OperatorId { get; }
        int SessionId { get; }
        int E911Position { get; }
        string DefaultJurisdiction { get; }
        string DefaultCity { get; }
        string DefaultState { get; }

        ObservableCollection<CadCall> Calls { get; }
        ObservableCollection<CadUnit> Units { get; }

        IReadOnlyList<CallCodeItem> CallCodes { get; }
        IReadOnlyList<LookupItem> Origins { get; }
        IReadOnlyList<LookupItem> Jurisdictions { get; }
        IReadOnlyList<string> States { get; }
        IReadOnlyList<string> ForceFlags { get; }

        /// <summary>Selectable unit statuses, from UST1.</summary>
        IReadOnlyList<UnitStatusOption> UnitStatuses { get; }

        /// <summary>Take Call's own lookups: CAD7 switches, location validation, libraries, indicators, E911.</summary>
        ITakeCallSupport TakeCallSupport { get; }

        /// <summary>Self Initiate / Traffic Stop / Activate Unit lookups: RST1, UNT1, AGC1, PER1, the libraries.</summary>
        ISelfInitSupport SelfInitSupport { get; }

        /// <summary>
        /// The session's keyboard template - the Standard Functions bar (KYB2) and
        /// the Quick Menu (KYB3), keys resolved through KYB1. See KeymapLoader.
        /// </summary>
        Keymap Keymap { get; }

        /// <summary>
        /// Re-reads the monitors. The demo board does nothing; the SQL service
        /// re-queries STK1 and RST1 and reconciles the bound collections in place.
        /// </summary>
        void Refresh();

        /// <summary>
        /// The event number Take Call shows while the call is being taken. Lowell
        /// assigns the real number from DSC1 when the call is saved
        /// (TakeCall_AssignEKey), so on the live tables this is the number the call
        /// will most likely get, not a reservation.
        /// </summary>
        string ReserveEventNo();

        /// <summary>
        /// Commits a completed Take Call draft and returns the new stacked event.
        /// The live service writes DSC1, DSE1, DSN1, DSV1 and STK1, and throws when
        /// the call could not be saved - in which case nothing was written.
        /// </summary>
        CadCall Dispatch(NewCallDraft draft);

        /// <summary>
        /// Unit Status, Location Change or Release. The live service writes RST1,
        /// DSU1, DST1 and, on a release, STK1 and DSE1 (LwCad135 / LwCad108).
        /// </summary>
        WriteReport ApplyUnitChange(UnitChange change);

        /// <summary>
        /// Commits a Self Initiate or Traffic Stop and returns the new event, already
        /// Active with the unit on it (or closed, with a disposition). The live
        /// service writes DSC1, DSE1, DSU1, DSV1, RST1 and STK1 in one transaction -
        /// releasing the unit from its current call first - and throws when the call
        /// could not be saved, in which case nothing was written.
        /// </summary>
        CadCall SelfInitiate(SelfInitDraft draft);

        /// <summary>Activate Unit (LwCad129): the unit onto the roster - RST1 and its RST2 report areas.</summary>
        WriteReport ActivateUnit(UnitActivation unit);

        /// <summary>
        /// What one agency's assign screen offers for a call, grouped the way that
        /// screen groups its roster - LwCad131 / LwCad134 flat, LwCad132 / LwCad133
        /// by apparatus type.
        /// </summary>
        AssignCandidateSet GetAssignCandidates(CadCall call, Agency agency);

        /// <summary>
        /// Puts the picked units on the call. The live service writes DSC1, DSU1,
        /// RST1, STK1 and DSE1 per unit (LwCad131-134); units it refuses are named
        /// in the report and left off the call.
        /// </summary>
        WriteReport AssignUnits(UnitAssignment assignment);

        // ------------------------------------------------------ CAD alerts

        /// <summary>Calls with an alert waiting, as of the last Refresh - the top bar's badge.</summary>
        int PendingAlertCallCount { get; }

        /// <summary>Every stacked or active call with pending alerts (LwCad301's call list).</summary>
        IReadOnlyList<PendingAlerts> GetPendingAlerts();

        /// <summary>A call's alerts, pending first (LwCad301 TabA_LoadAlerts).</summary>
        IReadOnlyList<CallAlert> GetAlerts(CadCall call);

        /// <summary>
        /// Acknowledges the ticked alerts. The live service writes DST1 ALERT_SW 'X',
        /// ACK_DATE and ACK_BY, a FatPot acknowledge for CADfusion alerts, and TSK1 301.
        /// </summary>
        WriteReport AcknowledgeAlerts(CadCall call, IReadOnlyCollection<long> alertKeys);

        /// <summary>
        /// Sends a new alert. The live service writes one pending DST1 row for every
        /// event on the call's global number (LwCad302), then TSK1 301.
        /// </summary>
        WriteReport AddAlert(CadCall call, string remarks);
    }
}
