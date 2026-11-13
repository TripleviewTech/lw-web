using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwTableCcc1;
using LwTableDse1;
using LwTableDsu1;
using LwTableDsv1;
using LwTableRst1;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Self Initiate and Traffic Stop (LwCadSystem SelfInit.cs / TraffStop.cs,
    /// LwCad107 / LwCad112) and Activate Unit (LwCad129).
    ///
    /// | Function      | Legacy                         | Tables written                                   |
    /// |---------------|--------------------------------|--------------------------------------------------|
    /// | Self Initiate | SelfInit_Save                  | DSC1, DSE1, DSU1 (U_TYPE 'S'), DSV1, RST1, STK1 'A' |
    /// | Traffic Stop  | TraffStop_Save                 | the same, DSU1 with the racial profile           |
    /// | (busy unit)   | LwCad123 Main_ReleaseUnit      | DSU1 05/01 disposition, RST1, STK1, DSE1         |
    /// | Activate Unit | LwCad129 AddUnitToRoster       | RST1 (TEMP_UNIT 'A'), RST2                       |
    ///
    /// The legacy forms wrote each table on its own connection with retry prompts,
    /// took the DSC1 numbers optimistically and carried on after a failed release;
    /// here each function is one transaction, so a failure part-way writes nothing.
    /// </summary>
    public sealed partial class CadWriteBack
    {
        /// <summary>What a Self Initiate created.</summary>
        public sealed class SelfInitResult
        {
            public CreatedEvent Event;
            public short DispNo1;
            public int DispNo2;

            /// <summary>"Released from P 2026-0000012 first (...)." when the unit was busy; else "".</summary>
            public string Released = string.Empty;
        }

        /// <summary>
        /// SelfInit_Save / TraffStop_Save: PrepareRST1 (releasing the unit from the
        /// call it is on, LwCad123), CreateDSE1, SaveDSU1, SaveOtherRecords.
        ///
        /// - The event is the unit's agency (RST1_AGC_TYPE) at the one priority.
        /// - DSU1 is U_TYPE 'S', dispatched and en route at the time the form was
        ///   opened; arrived too when CAD7_SELF_E / TRAF_E says so.
        /// - Without a disposition the unit goes on the call (RST1 status 3 or 4,
        ///   ASGN_TYPE 'S') and the event goes straight onto Active (STK1 'A').
        ///   With one the call is closed as it is entered and the unit stays free.
        /// </summary>
        public SelfInitResult SelfInitiate(SelfInitDraft draft, CCC1 code, short originCode)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (code == null) throw new CadWriteException("Call Code must be entered.");
            if (draft.Priority <= 0) throw new CadWriteException("Priority must be specified.");

            var unitNo = Text(draft.UnitNo);
            if (unitNo.Length == 0) throw new CadWriteException("Unit/Apparatus ID must be entered.");

            var place = draft.Place;
            var completed = draft.Disposition > 0;
            var result = new SelfInitResult();

            using (var tx = new CadTransaction(_connectString))
            {
                var now = ServerNow(tx);

                // --- SelfInit_PrepareRST1
                var rst1 = LockUnit(tx, unitNo);
                var type = char.ToUpperInvariant(rst1.RST1_AGC_TYPE);

                if (type != 'P' && type != 'F' && type != 'E' && type != 'O')
                {
                    throw new CadWriteException("Cannot assign keys, agency type unknown.");
                }

                if (draft.Mode == SelfInitMode.TrafficStop && type != 'P')
                {
                    throw new CadWriteException("Traffic Stop is a function reserved for police.");
                }

                if (rst1.RST1_DISP_NO_2 != 0)
                {
                    var from = rst1.RST1_EVENT_TYPE + " " + FormatEvent(rst1.RST1_EVENT_NO_1, rst1.RST1_EVENT_NO_2);
                    var outcome = ReleaseCore(tx, rst1, now, complete: false, disposition: draft.ReleaseDisposition);
                    result.Released = "Released from " + from + " first (" + Describe(outcome) + ").";
                    rst1 = _rst1.GetRowForUpdate(new RST1 { RST1_UNIT_NO = rst1.RST1_UNIT_NO }, tx);
                }

                // The form's open time is RCV, DSP and ENR, as the legacy stamps them.
                var received = draft.ReceivedAt > now || draft.ReceivedAt == default ? now : draft.ReceivedAt;
                var year = (short)received.Year;

                // --- CreateDSE1
                var dse1 = _dse1.Initialize(new DSE1());
                ApplyPlace(dse1, place, draft.Jurisdiction, draft.Location);

                dse1.DSE1_EVENT_TYPE = type;
                dse1.DSE1_EVENT_NO_1 = year;
                dse1.DSE1_G_EVENT_1 = year;
                dse1.DSE1_G_EVENT_2 = NextCounter(tx, year, "DSC1_GLOBAL_NO");
                dse1.DSE1_EVENT_NO_2 = NextCounter(tx, year, "DSC1_EVENT_NO_" + type);

                dse1.DSE1_CALL_CODE = code.CCC1_CODE;
                dse1.DSE1_PRIORITY = draft.Priority;
                dse1.DSE1_DOW = Dow(received);
                dse1.DSE1_RCV_DATE = received;
                dse1.DSE1_RCV_ID = _session.EmpId;
                dse1.DSE1_DSP_DATE = received;
                dse1.DSE1_ENR_DATE = received;
                if (draft.Arrived) dse1.DSE1_ARV_DATE = received;

                // SelfInit_SetDefaults: origin 05/02/0008.
                dse1.DSE1_ORIGIN_S = 5;
                dse1.DSE1_ORIGIN_L = 2;
                dse1.DSE1_ORIGIN_C = originCode > 0 ? originCode : (short)8;

                if (completed)
                {
                    dse1.DSE1_DISPO_S = 5;
                    dse1.DSE1_DISPO_L = 1;
                    dse1.DSE1_DISPO_C = draft.Disposition;
                    dse1.DSE1_CMP_DATE = received;
                    dse1.DSE1_RECLASS = code.CCC1_CODE;
                }

                dse1.DSE1_POLICE_1 = Text(draft.PoliceCode);
                dse1.DSE1_FIRE_1 = Text(draft.FireCode);
                dse1.DSE1_EMS_1 = Text(draft.EmsCode);
                dse1.DSE1_OTHER_1 = Text(draft.SupportCode);
                dse1.DSE1_TOW_ZONE = Text(draft.TowCode);
                dse1.DSE1_RESP_AREA = Text(draft.FireCode);

                dse1.DSE1_FORCED = place != null
                    ? (place.Forced == 'Y' || place.Forced == 'X' ? place.Forced : 'N')
                    : string.Equals(draft.Force, "Y", StringComparison.OrdinalIgnoreCase) ? 'Y' : 'N';
                dse1.DSE1_PMM_NO = place?.PremiseNo ?? 0;

                // The legacy put the narrative on DSU1 only, leaving the event blank.
                dse1.DSE1_REMARKS = draft.Narrative ?? string.Empty;
                dse1.DSE1_911_POS = _session.E911Pos;
                dse1.DSE1_PC_NAME = _session.PcName;

                _dse1.InsertRow(dse1, tx);
                Fat2(tx, "DSE1", Dse1Key(dse1), FatInsert);

                // --- SaveDSU1
                var dsu1 = _dsu1.Initialize(new DSU1());
                dsu1.DSU1_DISP_NO_1 = year;
                do
                {
                    dsu1.DSU1_DISP_NO_2 = NextCounter(tx, year, "DSC1_DISP_NO");
                    _dsu1.GetRowForUpdate(new DSU1 { DSU1_DISP_NO_1 = dsu1.DSU1_DISP_NO_1, DSU1_DISP_NO_2 = dsu1.DSU1_DISP_NO_2 }, tx);
                }
                while (_dsu1.RecordStatus == 0);

                CopyEventToDsu1(dse1, dsu1);
                dsu1.DSU1_FIRE_2 = dse1.DSE1_FIRE_2;
                dsu1.DSU1_EMS_2 = dse1.DSE1_EMS_2;
                dsu1.DSU1_OTHER_2 = dse1.DSE1_OTHER_2;
                dsu1.DSU1_TOW_ZONE = dse1.DSE1_TOW_ZONE;

                dsu1.DSU1_U_TYPE = 'S';
                dsu1.DSU1_UNIT_NO = rst1.RST1_UNIT_NO;
                dsu1.DSU1_AGENCY = rst1.RST1_AGENCY;
                dsu1.DSU1_OFFICER_1 = rst1.RST1_OFCR_1;
                dsu1.DSU1_OFFICER_2 = rst1.RST1_OFCR_2;
                dsu1.DSU1_DSP_ID = dsu1.DSU1_ENR_ID = _session.EmpId;
                dsu1.DSU1_DSP_DATE = dsu1.DSU1_ENR_DATE = received;

                if (draft.Arrived)
                {
                    dsu1.DSU1_ARV_ID = _session.EmpId;
                    dsu1.DSU1_ARV_DATE = received;
                }

                if (completed)
                {
                    dsu1.DSU1_DISPO_S = 5;
                    dsu1.DSU1_DISPO_L = 1;
                    dsu1.DSU1_DISPO_C = draft.Disposition;
                    dsu1.DSU1_CMP_ID = _session.EmpId;
                    dsu1.DSU1_CMP_DATE = received;
                    dsu1.DSU1_RECLASS = code.CCC1_CODE;
                }

                dsu1.DSU1_REMARKS = draft.Narrative ?? string.Empty;
                dsu1.DSU1_XMIT_MDC = 'N';
                dsu1.DSU1_911_POS = _session.E911Pos;
                dsu1.DSU1_PC_NAME = _session.PcName;

                if (draft.Profile != null)
                {
                    dsu1.DSU1_R_PROF_RACE = Text(draft.Profile.Race);
                    dsu1.DSU1_R_PROF_SEX = Text(draft.Profile.Sex);
                    dsu1.DSU1_R_PROF_SECTOR = Text(draft.Profile.Sector);
                    dsu1.DSU1_R_PROF_DISC = Text(draft.Profile.Discretion);
                    dsu1.DSU1_R_PROF_RESIDENT = Text(draft.Profile.Resident);
                    dsu1.DSU1_R_PROF_CIT_WARN = Text(draft.Profile.CitationWarning);
                    dsu1.DSU1_R_PROF_SEARCH = Text(draft.Profile.Search);
                    dsu1.DSU1_R_PROF_OFCR = draft.Profile.Officer;
                }

                _dsu1.InsertRow(dsu1, tx);
                Fat2(tx, "DSU1", Dsu1Key(dsu1), FatInsert);

                // --- SaveDSV1: type 'U', keyed to the dispatch as well as the event.
                SaveUnitVehicle(tx, dsu1, draft.Vehicle);

                long stackKey = 0;
                if (!completed)
                {
                    // --- UpdateRST1
                    rst1.RST1_STATUS = draft.Arrived ? Arrived : EnRoute;
                    rst1.RST1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
                    rst1.RST1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
                    rst1.RST1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
                    rst1.RST1_DISP_NO_1 = dsu1.DSU1_DISP_NO_1;
                    rst1.RST1_DISP_NO_2 = dsu1.DSU1_DISP_NO_2;
                    rst1.RST1_TRANS_NO_1 = 0;
                    rst1.RST1_TRANS_NO_2 = 0;
                    rst1.RST1_ASGN_TYPE = 'S';
                    rst1.RST1_CALL_CODE = dse1.DSE1_CALL_CODE;
                    rst1.RST1_PRIORITY = dse1.DSE1_PRIORITY;
                    rst1.RST1_LAST_HR = (short)received.Hour;
                    rst1.RST1_LAST_MN = (short)received.Minute;
                    if (Text(rst1.RST1_JUR).Length == 0) rst1.RST1_JUR = dse1.DSE1_LOC_JUR;

                    // Every agency is flagged outside its own jurisdiction here - the
                    // Assign screens only flag police and support.
                    if (Text(rst1.RST1_JUR) != Text(dse1.DSE1_LOC_JUR)) rst1.RST1_TEMP_JUR = dse1.DSE1_LOC_JUR;

                    var timeout = draft.Arrived ? ArrivedTimeout(code, type) : TravelTimeout(code, type);
                    rst1.RST1_T_O_MN = timeout;
                    rst1.RST1_T_O_F_HR = timeout > 0 ? (short)received.Hour : (short)0;
                    rst1.RST1_T_O_F_MN = timeout > 0 ? (short)received.Minute : (short)0;
                    rst1.RST1_T_O_FLAG = 0;
                    rst1.RST1_LOCK_ID = 0;

                    // --- CreateSTK1: onto Active, the unit on it, never timed out.
                    var extra = place == null ? string.Empty
                        : place.PremiseNo > 0 ? Text(place.PremiseName) : Text(place.CommonName);
                    var stk1 = CreateStk1(tx, dse1, code, extra, now, status: 'A', units: Text(rst1.RST1_UNIT_NO));
                    rst1.RST1_LAST_LOC = Text(stk1.STK1_LOCATION);
                    _rst1.UpdateRow(rst1, tx);

                    stackKey = stk1.STK1_S_KEY;
                }

                tx.Commit();

                result.Event = new CreatedEvent
                {
                    EventType = dse1.DSE1_EVENT_TYPE,
                    EventNo1 = dse1.DSE1_EVENT_NO_1,
                    EventNo2 = dse1.DSE1_EVENT_NO_2,
                    StackKey = stackKey,
                    Priority = dse1.DSE1_PRIORITY
                };
                result.DispNo1 = dsu1.DSU1_DISP_NO_1;
                result.DispNo2 = dsu1.DSU1_DISP_NO_2;
            }

            // SelfInit_Save, then SelfInit_UpdateMonitors when the unit went on the call.
            if (completed) SendTasks((301, null), (401, null), (403, null));
            else SendTasks((301, null), (401, null), (403, null), (202, unitNo), (404, unitNo), (402, null));

            return result;
        }

        /// <summary>SelfInit_SaveDSV1 - written only when a plate, make, model, style or colour was entered.</summary>
        private void SaveUnitVehicle(CadTransaction tx, DSU1 dsu1, VehicleInfo vehicle)
        {
            if (vehicle == null || vehicle.IsEmpty) return;

            var dsv1 = _dsv1.Initialize(new DSV1());

            dsv1.DSV1_TYPE = 'U';
            dsv1.DSV1_EVENT_TYPE = dsu1.DSU1_EVENT_TYPE;
            dsv1.DSV1_EVENT_NO_1 = dsu1.DSU1_EVENT_NO_1;
            dsv1.DSV1_EVENT_NO_2 = dsu1.DSU1_EVENT_NO_2;
            dsv1.DSV1_DISP_NO_1 = dsu1.DSU1_DISP_NO_1;
            dsv1.DSV1_DISP_NO_2 = dsu1.DSU1_DISP_NO_2;
            dsv1.DSV1_CALL_CODE = dsu1.DSU1_CALL_CODE;
            dsv1.DSV1_JUR = dsu1.DSU1_LOC_JUR;
            dsv1.DSV1_RCV_DATE = dsu1.DSU1_RCV_DATE;

            dsv1.DSV1_VEH_PLATE = Text(vehicle.Plate).ToUpperInvariant();
            dsv1.DSV1_ZIP_PLATE = dsv1.DSV1_VEH_PLATE.Replace(" ", string.Empty);
            dsv1.DSV1_VEH_STATE = Text(vehicle.PlateState).ToUpperInvariant();
            dsv1.DSV1_VEH_P_TYPE = Text(vehicle.PlateType).ToUpperInvariant();
            dsv1.DSV1_VEH_MAKE = Text(vehicle.Make).ToUpperInvariant();
            dsv1.DSV1_VEH_MODEL = Text(vehicle.Model).ToUpperInvariant();
            dsv1.DSV1_VEH_STYLE = Text(vehicle.Style).ToUpperInvariant();
            dsv1.DSV1_VEH_COLOR = Text(vehicle.Color).ToUpperInvariant();
            if (short.TryParse(Text(vehicle.Year), out var plateYear)) dsv1.DSV1_VEH_P_YR = plateYear;

            _dsv1.InsertRow(dsv1, tx);
        }

        private static short ArrivedTimeout(CCC1 code, char agencyType)
        {
            switch (agencyType)
            {
                case 'P': return code.CCC1_PT_O_ARRV;
                case 'F': return code.CCC1_FT_O_ARRV;
                case 'E': return code.CCC1_ET_O_ARRV;
                default: return code.CCC1_OT_O_ARRV;
            }
        }

        // ============================================================ activate

        /// <summary>
        /// LwCad129 AddUnitToRoster: a new RST1 row - in service, TEMP_UNIT 'A' (a
        /// unit activated from the console rather than logged on at the start of a
        /// tour) - and one RST2 row per report area.
        ///
        /// Three things the legacy did not do: the RST1 insert and the RST2 rows are
        /// one transaction; RST2 rows left behind by an earlier activation of the
        /// same unit are removed first; and TSK1 202 tells the other unit monitors.
        /// </summary>
        public UnitResult ActivateUnit(UnitActivation unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            var unitNo = Text(unit.UnitNo);
            var result = new UnitResult { UnitNo = unitNo };

            try
            {
                if (unitNo.Length == 0) throw new CadWriteException("A Unit/Apparatus must be entered.");

                using (var tx = new CadTransaction(_connectString))
                {
                    var existing = _rst1.GetList("RTRIM(RST1_UNIT_NO) = @unit", new { unit = unitNo }, tx, forUpdate: true);
                    if (existing.Count > 0) throw new CadWriteException("Unit/Apparatus has already been logged onto CAD.");

                    var type = char.ToUpperInvariant(unit.AgencyType);
                    var rst1 = _rst1.Initialize(new RST1());

                    rst1.RST1_UNIT_NO = unitNo;
                    rst1.RST1_AGENCY = Text(unit.Agency);
                    rst1.RST1_AGC_TYPE = type;
                    rst1.RST1_JUR = Text(unit.Jurisdiction);
                    rst1.RST1_DISPLAY = 'N';
                    rst1.RST1_OFCR_1 = unit.Employee1;
                    rst1.RST1_OFCR_2 = unit.Employee2;

                    // The MLB1 05/10 description, as LwCad129 and LwCad171 store it; the
                    // columns hold six characters.
                    if (type == 'F' || type == 'E')
                    {
                        rst1.RST1_A_STATION = Text(unit.Station);
                        rst1.RST1_C_STATION = Text(unit.Station);
                    }

                    if (unit.UnitType > 0)
                    {
                        rst1.RST1_U_TYPE_S = 5;
                        rst1.RST1_U_TYPE_L = 4;
                        rst1.RST1_U_TYPE_C = unit.UnitType;
                    }

                    // AddAssignment: the non-blank areas, packed from the first - police only.
                    // The legacy also saved the UNT1 areas it had put in the disabled boxes
                    // of a fire, EMS or support unit.
                    var areas = type != 'P' ? new List<string>()
                        : (unit.Areas ?? Array.Empty<string>()).Select(Text).Where(a => a.Length > 0).Take(5).ToList();
                    if (areas.Count > 0) rst1.RST1_R_AREA_1 = areas[0];
                    if (areas.Count > 1) rst1.RST1_R_AREA_2 = areas[1];
                    if (areas.Count > 2) rst1.RST1_R_AREA_3 = areas[2];
                    if (areas.Count > 3) rst1.RST1_R_AREA_4 = areas[3];
                    if (areas.Count > 4) rst1.RST1_R_AREA_5 = areas[4];

                    var police = type == 'P';
                    rst1.RST1_RECOMMEND = police ? 'Y' : 'N';
                    rst1.RST1_DSPL_SW = police ? 'Y' : 'N';
                    rst1.RST1_STATUS = InService;
                    rst1.RST1_LOCK_ID = 0;
                    rst1.RST1_TEMP_UNIT = 'A';

                    _rst1.InsertRow(rst1, tx);

                    tx.Execute("DELETE FROM dbo.RST2 WHERE RTRIM(RST2_UNIT_NO) = @unit", ("@unit", unitNo));
                    for (int i = 0; i < areas.Count; i++)
                    {
                        tx.Execute("INSERT INTO dbo.RST2 (RST2_UNIT_NO, RST2_JUR, RST2_R_AREA, RST2_AREA_POS) VALUES (@unit, @jur, @area, @pos)",
                                   ("@unit", unitNo), ("@jur", rst1.RST1_JUR), ("@area", areas[i]), ("@pos", (short)(i + 1)));
                    }

                    tx.Commit();
                }

                result.Written = true;
                result.Message = "Unit " + unitNo + " activated and in service.";
                SendTasks((202, unitNo));
            }
            catch (CadWriteException ex)
            {
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                LaLog.Error("CadWriteBack", "Activate " + unitNo + " failed.", ex);
                result.Message = "Unit/Apparatus could not be created. " + ex.Message;
            }

            return result;
        }
    }
}
