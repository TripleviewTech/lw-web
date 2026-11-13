using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwTableCcc1;
using LwTableDat1;
using LwTableDse1;
using LwTableDsn1;
using LwTableDst1;
using LwTableDsu1;
using LwTableDsv1;
using LwTableRst1;
using LwTableStk1;
using LwTableTsk1;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Writes the console's work back to the Lowell CAD tables - the half of the
    /// legacy programs the board used to only imitate.
    ///
    /// | Function        | Legacy                                   | Tables written                                   |
    /// |-----------------|------------------------------------------|--------------------------------------------------|
    /// | Dispatch        | LwCadSystem TakeCall_SaveDSE1            | DSC1, DSE1 (one per agency), DSN1, DSV1, STK1    |
    /// | Assign / Assist | LwCad131-134 TabA_AssignUnit / Apparatus | DSC1, DSU1, RST1, STK1, DSE1                     |
    /// | Unit Status     | LwCad135 TabA_ProcessUnit                | RST1, DSU1, DSE1, DST1                           |
    /// | Location Change | LwCad135 TabA_ProcessLocEnr              | RST1, DST1                                       |
    /// | Release         | LwCad108 TabA_SaveDSU1 / SaveOtherRecords| DSU1, RST1, STK1 (restack / units / delete), DSE1 |
    ///
    /// Every function also queues FAT2 rows for the FatPot interface where the
    /// legacy calls FpComm.CommInterface, and afterwards writes the TSK1 task
    /// messages (202 / 301 / 401 / 403) that tell the other consoles to refresh.
    ///
    /// Two deliberate differences from the legacy programs. Each function runs in
    /// one transaction - the legacy wrote every table on its own connection, so a
    /// failure part-way left an event half-written. And rows are read with UPDLOCK
    /// inside that transaction; the RST1_LOCK_ID / STK1_LOCK_ID a legacy console
    /// sets is still honoured, so an LW console holding a unit or a call blocks
    /// this one exactly as it blocks another LW console.
    /// </summary>
    public sealed partial class CadWriteBack
    {
        // Legacy RST1_STATUS values (LwCad131-135, LwCad108).
        private const short InService = 1;
        private const short Dispatched = 2;
        private const short EnRoute = 3;
        private const short Arrived = 4;
        private const short CompleteAtScene = 5;
        private const short LocationEnRoute = 10;
        private const short LocationArrived = 11;
        private const short MdcSent = 12;
        private const short OffCallEnRoute = 20;
        private const short OffCallArrived = 21;
        private const short OffCallComplete = 22;

        // FatPotComm.ActionType.
        private const char FatInsert = '0';
        private const char FatUpdate = '1';

        private readonly string _connectString;
        private readonly CadSession _session;

        private readonly Dse1_RW _dse1;
        private readonly Stk1_RW _stk1;
        private readonly Dsu1_RW _dsu1;
        private readonly Dst1_RW _dst1;
        private readonly Rst1_RW _rst1;
        private readonly Dsn1_RW _dsn1;
        private readonly Dsv1_RW _dsv1;
        private readonly Tsk1_RW _tsk1;
        private readonly Dat1_RW _dat1;
        private readonly Ccc1_RW _ccc1;

        private bool? _hasFat2;

        public CadWriteBack(string connectString, CadSession session)
        {
            _connectString = connectString;
            _session = session ?? new CadSession();

            _dse1 = new Dse1_RW(connectString);
            _stk1 = new Stk1_RW(connectString);
            _dsu1 = new Dsu1_RW(connectString);
            _dst1 = new Dst1_RW(connectString);
            _rst1 = new Rst1_RW(connectString);
            _dsn1 = new Dsn1_RW(connectString);
            _dsv1 = new Dsv1_RW(connectString);
            _tsk1 = new Tsk1_RW(connectString);
            _dat1 = new Dat1_RW(connectString);
            _ccc1 = new Ccc1_RW(connectString);
        }

        /// <summary>
        /// Reads the session row, as CadMain does after the logon: DAT1_911_POS, and
        /// the keyboard template the menus are built from - DAT1_KBRD_ID,
        /// DAT1_OP_TYPE, DAT1_ACC_LEVEL and DAT1_AGC_P/F/E/O. A value given on the
        /// command line (--kbrd / --optype / --sec) wins over the row. True when a
        /// row was found.
        /// </summary>
        public bool LoadSession()
        {
            var row = _dat1.GetRow(new DAT1 { DAT1_EMP_ID = _session.EmpId, DAT1_SESS_ID = _session.SessId });
            if (_dat1.RecordStatus != 0) return false;

            _session.E911Pos = row.DAT1_911_POS;

            if (!_session.KeyboardIdOverridden && !string.IsNullOrWhiteSpace(row.DAT1_KBRD_ID))
            {
                _session.KeyboardId = row.DAT1_KBRD_ID.Trim().ToUpperInvariant();
            }

            if (!_session.OperatorTypeOverridden && row.DAT1_OP_TYPE != ' ')
            {
                _session.OperatorType = char.ToUpperInvariant(row.DAT1_OP_TYPE);
            }

            if (!_session.SecurityOverridden)
            {
                _session.SecurityLevel = KeymapLoader.SecurityDigit(row.DAT1_ACC_LEVEL);
            }

            _session.AgencyPolice = row.DAT1_AGC_P;
            _session.AgencyFire = row.DAT1_AGC_F;
            _session.AgencyEms = row.DAT1_AGC_E;
            _session.AgencyOther = row.DAT1_AGC_O;
            return true;
        }

        // =============================================================== dispatch

        /// <summary>One event a dispatch created - there is one per agency the call code tasks.</summary>
        public sealed class CreatedEvent
        {
            public char EventType;
            public short EventNo1;
            public int EventNo2;
            public long StackKey;
            public short Priority;
        }

        /// <summary>
        /// Commits a Take Call. TakeCall_SaveDSE1: a global number from DSC1, then for
        /// each priority box above 0 (seeded from CCC1_PRTY_P / F / E / O, and the
        /// operator's to change), an event number from DSC1, a DSE1 row, the DSN1
        /// reporting-party and DSV1 vehicle rows, and - unless a disposition closes
        /// the call on entry - the STK1 stack row that puts it on the Stacked monitor.
        /// </summary>
        public IReadOnlyList<CreatedEvent> Dispatch(NewCallDraft draft, CCC1 code, short originCode, int premiseNo)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (code == null) throw new CadWriteException("Call code must be specified.");

            var tasked = (draft.HasPriorities
                    ? new[] { ('P', draft.PriorityPolice), ('F', draft.PriorityFire), ('E', draft.PriorityEms), ('O', draft.PriorityOther) }
                    : new[] { ('P', code.CCC1_PRTY_P), ('F', code.CCC1_PRTY_F), ('E', code.CCC1_PRTY_E), ('O', code.CCC1_PRTY_O) })
                .Where(t => t.Item2 > 0)
                .ToList();

            // TakeCall_ValidateMore: not all four priorities may be zero.
            if (tasked.Count == 0) throw new CadWriteException("Priority must be specified.");

            var place = draft.Place;
            var completed = draft.Disposition > 0;

            var created = new List<CreatedEvent>();

            using (var tx = new CadTransaction(_connectString))
            {
                var now = ServerNow(tx);
                var received = draft.ReceivedAt > now || draft.ReceivedAt == default ? now : draft.ReceivedAt;
                var year = (short)received.Year;

                var dse1 = _dse1.Initialize(new DSE1());

                ApplyPlace(dse1, place, draft.Jurisdiction, draft.Location);

                // TakeCall_LoadE911Call: the call's arrival and, with no premise
                // coordinates, the handset's position.
                if (draft.E911 != null)
                {
                    dse1.DSE1_E911_DATE = draft.E911.Received;
                    if (dse1.DSE1_XLOW == 0 && draft.E911.XLow != 0)
                    {
                        dse1.DSE1_XLOW = draft.E911.XLow;
                        dse1.DSE1_YLOW = draft.E911.YLow;
                        dse1.DSE1_XHIGH = draft.E911.XHigh;
                        dse1.DSE1_YHIGH = draft.E911.YHigh;
                    }
                }

                dse1.DSE1_CALL_CODE = code.CCC1_CODE;
                dse1.DSE1_RECLASS = 0;
                dse1.DSE1_DOW = Dow(received);
                dse1.DSE1_RCV_DATE = received;
                dse1.DSE1_CMP_DATE = null;

                // TakeCall_SaveDSE1: a disposition completes the call as it is entered.
                if (completed)
                {
                    dse1.DSE1_DISPO_S = 5;
                    dse1.DSE1_DISPO_L = 1;
                    dse1.DSE1_DISPO_C = draft.Disposition;
                    dse1.DSE1_CMP_DATE = now;
                    dse1.DSE1_RECLASS = code.CCC1_CODE;
                }

                // TakeCall_SaveDSE1 defaults an unset origin to 5/2/20 in Lowell.
                dse1.DSE1_ORIGIN_S = 5;
                dse1.DSE1_ORIGIN_L = 2;
                dse1.DSE1_ORIGIN_C = originCode > 0 ? originCode : (short)20;

                dse1.DSE1_POLICE_1 = Text(draft.PoliceCode);
                dse1.DSE1_FIRE_1 = Text(draft.FireCode);
                dse1.DSE1_EMS_1 = Text(draft.EmsCode);
                dse1.DSE1_OTHER_1 = Text(draft.SupportCode);
                dse1.DSE1_TOW_ZONE = Text(draft.TowCode);
                // LwCad132 keys the fire run card on DSE1_FIRE_1 and copies it into
                // DSE1_RESP_AREA when that is blank; doing it here saves the round trip.
                dse1.DSE1_RESP_AREA = Text(draft.FireCode);

                dse1.DSE1_RCV_ID = _session.EmpId;
                dse1.DSE1_FORCED = place != null
                    ? (place.Forced == 'Y' || place.Forced == 'X' ? place.Forced : 'N')
                    : string.Equals(draft.Force, "Y", StringComparison.OrdinalIgnoreCase) ? 'Y' : 'N';
                dse1.DSE1_PMM_NO = place != null ? place.PremiseNo : premiseNo;
                dse1.DSE1_OTHER_NAME = Text(draft.OtherComment);

                // Lowell does not uppercase the narrative (EH does).
                dse1.DSE1_REMARKS = draft.Narrative ?? string.Empty;

                dse1.DSE1_911_POS = _session.E911Pos;
                dse1.DSE1_PC_NAME = _session.PcName;

                dse1.DSE1_G_EVENT_1 = year;
                dse1.DSE1_G_EVENT_2 = NextCounter(tx, year, "DSC1_GLOBAL_NO");

                foreach (var (type, priority) in tasked)
                {
                    dse1.DSE1_EVENT_TYPE = type;
                    dse1.DSE1_EVENT_NO_1 = year;
                    dse1.DSE1_EVENT_NO_2 = NextCounter(tx, year, "DSC1_EVENT_NO_" + type);
                    dse1.DSE1_PRIORITY = priority;

                    _dse1.InsertRow(dse1, tx);
                    Fat2(tx, "DSE1", Dse1Key(dse1), FatInsert);

                    SaveDsn1(tx, dse1, draft);
                    SaveDsv1(tx, dse1, draft.Vehicle);

                    // STK1_LOCATION brackets the premise name, else the segment's common
                    // name, else the other name - TakeCall_CreateSTK1.
                    var extra = place == null ? Text(draft.Premise)
                        : place.PremiseNo > 0 ? Text(place.PremiseName) : Text(place.CommonName);
                    var stk1 = completed ? null : CreateStk1(tx, dse1, code, premiseName: extra, now: now);

                    created.Add(new CreatedEvent
                    {
                        EventType = type,
                        EventNo1 = dse1.DSE1_EVENT_NO_1,
                        EventNo2 = dse1.DSE1_EVENT_NO_2,
                        StackKey = stk1?.STK1_S_KEY ?? 0,
                        Priority = priority
                    });
                }

                // The in-flight map record goes with the save (TakeCall_SaveDSE1).
                if (draft.MapRecordKey > 0)
                {
                    tx.Execute("DELETE FROM dbo.STK2 WHERE STK2_S_KEY = @k", ("@k", draft.MapRecordKey));
                }

                tx.Commit();
            }

            // A call closed on entry never reaches the monitors, so nobody is told.
            if (completed) SendTasks((403, null));
            else SendTasks((301, null), (401, null), (403, null));

            return created;
        }

        /// <summary>
        /// What LaGeo040 and TakeCall_ValidateLocation put on the event: the parts of
        /// the validated location, its geocode, routing, campus, second areas and
        /// coordinates. Without a validated location the text is only split up.
        /// </summary>
        private static void ApplyPlace(DSE1 dse1, Location.LocationResult place, string jurisdiction, string location)
        {
            if (place != null)
            {
                // What LaGeo040 and TakeCall_ValidateLocation put on the event.
                dse1.DSE1_LOC_JUR = Text(place.Jurisdiction).Length > 0 ? Text(place.Jurisdiction) : Text(jurisdiction);
                dse1.DSE1_LOC_ST_NO = place.StreetNo;
                dse1.DSE1_LOC_ST_NAM = Text(place.Street);
                dse1.DSE1_LOC_SUB_NO = Text(place.Sub);
                dse1.DSE1_LOC_UNIT = Text(place.Unit);
                dse1.DSE1_LOC_X_ST = Text(place.Cross);
                dse1.DSE1_GEOCODE_NO = place.GeoKey;
                dse1.DSE1_ROUTING_ID = Text(place.RoutingId);
                dse1.DSE1_CAMPUS_ID = place.CampusId;
                dse1.DSE1_POLICE_2 = Text(place.Police2);
                dse1.DSE1_FIRE_2 = Text(place.Fire2);
                dse1.DSE1_EMS_2 = Text(place.Ems2);
                dse1.DSE1_OTHER_2 = Text(place.Other2);
                dse1.DSE1_XLOW = dse1.DSE1_XHIGH = place.X;
                dse1.DSE1_YLOW = dse1.DSE1_YHIGH = place.Y;
            }
            else
            {
                var parts = LocationParts.Parse(location);
                dse1.DSE1_LOC_JUR = Text(jurisdiction);
                dse1.DSE1_LOC_ST_NO = parts.StreetNo;
                dse1.DSE1_LOC_ST_NAM = parts.StreetName;
                dse1.DSE1_LOC_SUB_NO = parts.SubNo;
                dse1.DSE1_LOC_UNIT = parts.Unit;
                dse1.DSE1_LOC_X_ST = parts.CrossStreet;
            }
        }

        /// <summary>TSK1 403 - the maps re-read the calls being taken (STK2).</summary>
        public void NotifyMaps() => SendTasks((403, null));

        /// <summary>TakeCall_SaveDSN1: the reporting party, written only when one was entered.</summary>
        private void SaveDsn1(CadTransaction tx, DSE1 dse1, NewCallDraft draft)
        {
            var dsn1 = _dsn1.Initialize(new DSN1());

            dsn1.DSN1_TYPE = 'E';
            dsn1.DSN1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
            dsn1.DSN1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
            dsn1.DSN1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
            dsn1.DSN1_CALL_CODE = dse1.DSE1_CALL_CODE;
            dsn1.DSN1_LOC_JUR = dse1.DSE1_LOC_JUR;
            dsn1.DSN1_RCV_DATE = dse1.DSE1_RCV_DATE;

            dsn1.DSN1_PER_NAME_L = Text(draft.LastName);
            dsn1.DSN1_PER_NAME_F = Text(draft.FirstName);
            dsn1.DSN1_PER_NAME_M = Text(draft.MiddleName);
            dsn1.DSN1_PER_ADDR = Text(draft.Address);
            dsn1.DSN1_PER_CITY = Text(draft.City);
            dsn1.DSN1_PER_STATE = Text(draft.State);

            var phone = new string((draft.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            if (phone.Length == 11 && phone[0] == '1') phone = phone.Substring(1);
            if (phone.Length == 10)
            {
                dsn1.DSN1_PER_PHONE_AC = short.Parse(phone.Substring(0, 3));
                phone = phone.Substring(3);
            }

            if (phone.Length == 7)
            {
                dsn1.DSN1_PER_PHONE_1 = short.Parse(phone.Substring(0, 3));
                dsn1.DSN1_PER_PHONE_2 = int.Parse(phone.Substring(3));
            }

            var entered = dsn1.DSN1_PER_NAME_L.Length > 0 ||
                          dsn1.DSN1_PER_NAME_F.Length > 0 ||
                          dsn1.DSN1_PER_PHONE_2 > 0 ||
                          dsn1.DSN1_PER_ADDR.Length > 0 ||
                          dsn1.DSN1_PER_CITY.Length > 0;

            if (entered) _dsn1.InsertRow(dsn1, tx);
        }

        /// <summary>TakeCall_SaveDSV1: the vehicle, written only when one was entered.</summary>
        private void SaveDsv1(CadTransaction tx, DSE1 dse1, VehicleInfo vehicle)
        {
            if (vehicle == null || vehicle.IsEmpty) return;

            var dsv1 = _dsv1.Initialize(new DSV1());

            dsv1.DSV1_TYPE = 'E';
            dsv1.DSV1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
            dsv1.DSV1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
            dsv1.DSV1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
            dsv1.DSV1_CALL_CODE = dse1.DSE1_CALL_CODE;
            dsv1.DSV1_JUR = dse1.DSE1_LOC_JUR;
            dsv1.DSV1_RCV_DATE = dse1.DSE1_RCV_DATE;

            dsv1.DSV1_VEH_PLATE = Text(vehicle.Plate).ToUpperInvariant();
            dsv1.DSV1_ZIP_PLATE = dsv1.DSV1_VEH_PLATE.Replace(" ", string.Empty);
            dsv1.DSV1_VEH_STATE = Text(vehicle.PlateState).ToUpperInvariant();
            dsv1.DSV1_VEH_P_TYPE = Text(vehicle.PlateType).ToUpperInvariant();
            dsv1.DSV1_VEH_MAKE = Text(vehicle.Make).ToUpperInvariant();
            dsv1.DSV1_VEH_MODEL = Text(vehicle.Model).ToUpperInvariant();
            dsv1.DSV1_VEH_STYLE = Text(vehicle.Style).ToUpperInvariant();
            dsv1.DSV1_VEH_COLOR = Text(vehicle.Color).ToUpperInvariant();
            if (short.TryParse(Text(vehicle.Year), out var year)) dsv1.DSV1_VEH_P_YR = year;

            _dsv1.InsertRow(dsv1, tx);
        }

        /// <summary>
        /// TakeCall_CreateSTK1: the stack row, which is what the monitors read.
        /// SelfInit_CreateSTK1 files straight onto Active ('A') with the unit on it
        /// and no stack timeout - "Self Initiated Calls are Never Stacked".
        /// </summary>
        private STK1 CreateStk1(CadTransaction tx, DSE1 dse1, CCC1 code, string premiseName, DateTime now,
                                char status = 'S', string units = "")
        {
            var stk1 = _stk1.Initialize(new STK1());

            switch (dse1.DSE1_EVENT_TYPE)
            {
                case 'P': stk1.STK1_AREA = dse1.DSE1_POLICE_1; stk1.STK1_T_O_MN = code.CCC1_PT_O_STCK; break;
                case 'F': stk1.STK1_AREA = dse1.DSE1_FIRE_1; stk1.STK1_T_O_MN = code.CCC1_FT_O_STCK; break;
                case 'E': stk1.STK1_AREA = dse1.DSE1_EMS_1; stk1.STK1_T_O_MN = code.CCC1_ET_O_STCK; break;
                default: stk1.STK1_AREA = dse1.DSE1_OTHER_1; stk1.STK1_T_O_MN = code.CCC1_OT_O_STCK; break;
            }

            stk1.STK1_STATUS = 'S';
            stk1.STK1_PRIORITY = dse1.DSE1_PRIORITY;
            stk1.STK1_RCV_DATE = dse1.DSE1_RCV_DATE;
            stk1.STK1_RCV_ID = dse1.DSE1_RCV_ID;
            stk1.STK1_CALL_CODE = dse1.DSE1_CALL_CODE;
            stk1.STK1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
            stk1.STK1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
            stk1.STK1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
            stk1.STK1_G_EVENT_1 = dse1.DSE1_G_EVENT_1;
            stk1.STK1_G_EVENT_2 = dse1.DSE1_G_EVENT_2;

            stk1.STK1_LOCATION = StackLocation(dse1, premiseName);
            stk1.STK1_JUR = dse1.DSE1_LOC_JUR;
            stk1.STK1_LOC_JUR = dse1.DSE1_LOC_JUR;
            stk1.STK1_LOC_ST_NAM = dse1.DSE1_LOC_ST_NAM;
            stk1.STK1_LOC_ST_NO = dse1.DSE1_LOC_ST_NO;
            stk1.STK1_LOC_SUB_NO = dse1.DSE1_LOC_SUB_NO;
            stk1.STK1_LOC_UNIT = dse1.DSE1_LOC_UNIT;
            stk1.STK1_LOC_X_ST = dse1.DSE1_LOC_X_ST;
            stk1.STK1_FORCED = dse1.DSE1_FORCED;
            stk1.STK1_X_CORD = dse1.DSE1_XLOW;
            stk1.STK1_Y_CORD = dse1.DSE1_YLOW;

            // The legacy files an intersection under whichever street sorts first.
            if (stk1.STK1_LOC_X_ST.Trim().Length > 0 && stk1.STK1_FORCED != 'X' &&
                string.CompareOrdinal(stk1.STK1_LOC_X_ST, stk1.STK1_LOC_ST_NAM) < 0)
            {
                (stk1.STK1_LOC_ST_NAM, stk1.STK1_LOC_X_ST) = (stk1.STK1_LOC_X_ST, stk1.STK1_LOC_ST_NAM);
            }

            // Nobody is on a call that has just been taken - except a self-initiated one.
            stk1.STK1_UNITS = Text(units);

            if (status == 'A')
            {
                stk1.STK1_STATUS = 'A';
                stk1.STK1_T_O_MN = 0;
                var dispatched = dse1.DSE1_DSP_DATE ?? now;
                stk1.STK1_DSP_HR = (short)dispatched.Hour;
                stk1.STK1_DSP_MN = (short)dispatched.Minute;
            }

            if (stk1.STK1_T_O_MN > 0)
            {
                stk1.STK1_T_O_F_HR = (short)now.Hour;
                stk1.STK1_T_O_F_MN = (short)now.Minute;
            }

            _stk1.InsertRow(stk1, tx);
            return stk1;
        }

        /// <summary>
        /// The monitor's location text: the address, plus the premise name - or the
        /// other name when there is no premise - in brackets, merged into a bracket
        /// already there, cut to 50 characters. TakeCall_CreateSTK1.
        /// </summary>
        private static string StackLocation(DSE1 dse1, string premiseName)
        {
            var location = Location.LocationFormatter.Format(dse1.DSE1_LOC_ST_NO, dse1.DSE1_LOC_ST_NAM,
                                                             dse1.DSE1_LOC_SUB_NO, dse1.DSE1_LOC_UNIT, dse1.DSE1_LOC_X_ST);
            var extra = Text(premiseName);
            if (extra.Length == 0) extra = Text(dse1.DSE1_OTHER_NAME);

            if (extra.Length > 0)
            {
                location = location.EndsWith(")")
                    ? location.TrimEnd(')') + " - " + extra + ")"
                    : location + " (" + extra + ")";
            }

            location = location.Replace("()", string.Empty).Trim();
            return location.Length > 50 ? location.Substring(0, 50) : location;
        }

        /// <summary>
        /// Corrects the event's fire box or EMS area to the premise's - the write
        /// LwCad132 / LwCad133 make on open when PMM1_FIRE / PMM1_EMS differs.
        /// </summary>
        public void SetEventArea(char eventType, short eventNo1, int eventNo2, bool fire, string area)
        {
            using var tx = new CadTransaction(_connectString);

            var dse1 = _dse1.GetRowForUpdate(new DSE1
            {
                DSE1_EVENT_TYPE = eventType,
                DSE1_EVENT_NO_1 = eventNo1,
                DSE1_EVENT_NO_2 = eventNo2
            }, tx);

            if (_dse1.RecordStatus != 0) return;

            if (fire) dse1.DSE1_FIRE_1 = area;
            else dse1.DSE1_EMS_1 = area;

            _dse1.UpdateRow(dse1, tx);
            Fat2(tx, "DSE1", Dse1Key(dse1), FatUpdate);
            tx.Commit();
        }

        // ================================================================= assign

        /// <summary>What happened to one unit an assign screen was asked to send.</summary>
        public sealed class UnitResult
        {
            public string UnitNo;
            public bool Written;
            public string Message;
        }

        /// <summary>
        /// Puts units on a call - LwCad131 / 134 TabA_AssignUnit, LwCad132 / 133
        /// TabA_AssignApparatus. Each unit is its own transaction, the way the
        /// legacy assigns them one at a time: one unit being refused does not undo
        /// the others.
        ///
        /// A unit is always assigned to its own agency's event. When it is not the
        /// agency of the call it was picked for - a fire unit named on the police
        /// board - it goes on the linked event for that agency (same DSE1_G_EVENT),
        /// which is what the legacy's Assign Fire Unit button switches to.
        /// </summary>
        public IReadOnlyList<UnitResult> Assign(long stackKey, IEnumerable<string> unitNos,
                                                DispatchAction action, int timeoutMinutes)
        {
            var results = new List<UnitResult>();
            var sent = new List<(int, string)>();

            foreach (var unitNo in unitNos.Select(Text).Where(u => u.Length > 0))
            {
                var result = new UnitResult { UnitNo = unitNo };
                results.Add(result);

                try
                {
                    string jurisdiction;
                    using (var tx = new CadTransaction(_connectString))
                    {
                        result.Message = AssignOne(tx, stackKey, unitNo, action, timeoutMinutes, out jurisdiction);
                        tx.Commit();
                    }

                    result.Written = true;
                    sent.Add((202, unitNo));
                }
                catch (CadWriteException ex)
                {
                    result.Message = ex.Message;
                }
                catch (Exception ex)
                {
                    LaLog.Error("CadWriteBack", "Assign " + unitNo + " failed.", ex);
                    result.Message = "Unit " + unitNo + " was not assigned: " + ex.Message;
                }
            }

            if (sent.Count > 0)
            {
                sent.Add((301, null));
                sent.Add((401, null));
                SendTasks(sent.ToArray());
            }

            return results;
        }

        private string AssignOne(CadTransaction tx, long stackKey, string unitNo, DispatchAction action,
                                 int timeoutMinutes, out string jurisdiction)
        {
            jurisdiction = string.Empty;

            var picked = _stk1.GetRowForUpdate(new STK1 { STK1_S_KEY = stackKey }, tx);
            if (_stk1.RecordStatus != 0)
            {
                throw new CadWriteException("The event is no longer on the stack (STK1 " + stackKey + ").");
            }

            var rst1 = LockUnit(tx, unitNo);
            var agencyType = char.ToUpperInvariant(rst1.RST1_AGC_TYPE);

            var stk1 = agencyType == picked.STK1_EVENT_TYPE ? picked : LinkedStack(tx, picked, agencyType);
            if (stk1 == null)
            {
                throw new CadWriteException("Unit " + unitNo + " is a " + AgencyName(agencyType) +
                                            " unit and event " + EventText(picked) + " has no " +
                                            AgencyName(agencyType) + " event to assign it to.");
            }

            if (stk1.STK1_STATUS == 'D')
            {
                throw new CadWriteException("Assign is not valid for Delayed calls.");
            }

            if (stk1.STK1_LOCK_ID != 0 && stk1.STK1_LOCK_ID != _session.EmpId)
            {
                throw new CadWriteException("Event " + EventText(stk1) + " is being updated by " + stk1.STK1_LOCK_ID + ".");
            }

            var dse1 = _dse1.GetRowForUpdate(new DSE1
            {
                DSE1_EVENT_TYPE = stk1.STK1_EVENT_TYPE,
                DSE1_EVENT_NO_1 = stk1.STK1_EVENT_NO_1,
                DSE1_EVENT_NO_2 = stk1.STK1_EVENT_NO_2
            }, tx);

            if (_dse1.RecordStatus != 0)
            {
                throw new CadWriteException("Event " + EventText(stk1) + " has no DSE1 record.");
            }

            if (rst1.RST1_EVENT_TYPE == dse1.DSE1_EVENT_TYPE &&
                rst1.RST1_EVENT_NO_1 == dse1.DSE1_EVENT_NO_1 &&
                rst1.RST1_EVENT_NO_2 == dse1.DSE1_EVENT_NO_2 &&
                rst1.RST1_EVENT_NO_2 != 0)
            {
                throw new CadWriteException("Unit " + unitNo + " is already on event " + EventText(stk1) + ".");
            }

            var now = ServerNow(tx);
            var released = string.Empty;

            // A unit working another call comes off it first - the legacy runs
            // LwCad108 in release mode before the assign, and so does this.
            if (rst1.RST1_DISP_NO_2 != 0)
            {
                var from = FormatEvent(rst1.RST1_EVENT_NO_1, rst1.RST1_EVENT_NO_2);
                var outcome = ReleaseCore(tx, rst1, now, complete: false);
                released = " Released from " + from + " first (" + Describe(outcome) + ").";
                rst1 = _rst1.GetRowForUpdate(new RST1 { RST1_UNIT_NO = rst1.RST1_UNIT_NO }, tx);
            }

            var code = _ccc1.GetRow(new CCC1 { CCC1_CODE = dse1.DSE1_CALL_CODE });
            if (_ccc1.RecordStatus != 0) code = _ccc1.Initialize(new CCC1());

            // --- DSU1, the unit's dispatch record (TabA_AssignDKey, then WriteRow)
            var dsu1 = _dsu1.Initialize(new DSU1());
            dsu1.DSU1_DISP_NO_1 = (short)now.Year;
            do
            {
                dsu1.DSU1_DISP_NO_2 = NextCounter(tx, dsu1.DSU1_DISP_NO_1, "DSC1_DISP_NO");
                _dsu1.GetRowForUpdate(new DSU1 { DSU1_DISP_NO_1 = dsu1.DSU1_DISP_NO_1, DSU1_DISP_NO_2 = dsu1.DSU1_DISP_NO_2 }, tx);
            }
            while (_dsu1.RecordStatus == 0);

            CopyEventToDsu1(dse1, dsu1);

            var police = agencyType == 'P' || agencyType == 'O';

            dsu1.DSU1_DSP_ID = _session.EmpId;
            dsu1.DSU1_DSP_DATE = now;

            if (police || action != DispatchAction.Dispatch)
            {
                // Police and support have no Dispatch step: dispatch is en route.
                dsu1.DSU1_ENR_ID = _session.EmpId;
                dsu1.DSU1_ENR_DATE = now;
            }

            if (action == DispatchAction.Arrived)
            {
                dsu1.DSU1_ARV_ID = _session.EmpId;
                dsu1.DSU1_ARV_DATE = now;
            }

            dsu1.DSU1_UNIT_NO = rst1.RST1_UNIT_NO;
            dsu1.DSU1_U_TYPE = dse1.DSE1_DSP_DATE != null ? 'A' : 'P';   // additional vs primary
            dsu1.DSU1_OFFICER_1 = rst1.RST1_OFCR_1;
            dsu1.DSU1_OFFICER_2 = rst1.RST1_OFCR_2;
            dsu1.DSU1_AGENCY = rst1.RST1_AGENCY;
            dsu1.DSU1_XMIT_MDC = 'N';

            if (agencyType == 'P')
            {
                dsu1.DSU1_911_POS = _session.E911Pos;
                dsu1.DSU1_PC_NAME = _session.PcName;
            }

            _dsu1.InsertRow(dsu1, tx);
            Fat2(tx, "DSU1", Dsu1Key(dsu1), FatInsert);

            // --- RST1 (TabA_UbdateRST1)
            short status;
            if (action == DispatchAction.Arrived) status = Arrived;
            else if (police || action == DispatchAction.EnRoute) status = EnRoute;
            else status = Dispatched;

            rst1.RST1_STATUS = status;
            rst1.RST1_EVENT_TYPE = dsu1.DSU1_EVENT_TYPE;
            rst1.RST1_EVENT_NO_1 = dsu1.DSU1_EVENT_NO_1;
            rst1.RST1_EVENT_NO_2 = dsu1.DSU1_EVENT_NO_2;
            rst1.RST1_DISP_NO_1 = dsu1.DSU1_DISP_NO_1;
            rst1.RST1_DISP_NO_2 = dsu1.DSU1_DISP_NO_2;
            rst1.RST1_TRANS_NO_1 = 0;
            rst1.RST1_TRANS_NO_2 = 0;

            // Police and support flag a unit working outside its own jurisdiction.
            if (police && Text(rst1.RST1_JUR) != Text(dse1.DSE1_LOC_JUR))
            {
                rst1.RST1_TEMP_JUR = dse1.DSE1_LOC_JUR;
            }

            rst1.RST1_ASGN_TYPE = dsu1.DSU1_U_TYPE;
            rst1.RST1_CALL_CODE = dsu1.DSU1_CALL_CODE;
            rst1.RST1_PRIORITY = dsu1.DSU1_PRIORITY;
            rst1.RST1_LAST_HR = (short)now.Hour;
            rst1.RST1_LAST_MN = (short)now.Minute;
            rst1.RST1_LAST_LOC = Text(stk1.STK1_LOCATION);

            // The legacy police board shows a Timeout field and never reads it; this
            // honours it when the dispatcher fills it in, and otherwise uses the call
            // code's travel timeout for the unit's agency, as the legacy does.
            var timeout = timeoutMinutes > 0 ? (short)timeoutMinutes : TravelTimeout(code, agencyType);
            rst1.RST1_T_O_MN = timeout;
            rst1.RST1_T_O_F_HR = timeout > 0 ? (short)now.Hour : (short)0;
            rst1.RST1_T_O_F_MN = timeout > 0 ? (short)now.Minute : (short)0;
            rst1.RST1_T_O_FLAG = 0;
            rst1.RST1_LOCK_ID = 0;

            _rst1.UpdateRow(rst1, tx);

            // --- STK1 (TabA_UpdateSTK1): onto the Active monitor, and the unit list
            if (stk1.STK1_STATUS != 'A')
            {
                stk1.STK1_STATUS = 'A';
                stk1.STK1_DSP_HR = (short)now.Hour;
                stk1.STK1_DSP_MN = (short)now.Minute;
                stk1.STK1_T_O_F_HR = 0;
                stk1.STK1_T_O_F_MN = 0;
                stk1.STK1_T_O_MN = 0;
                stk1.STK1_T_O_FLAG = 0;
            }

            stk1.STK1_UNITS = UnitsOnEvent(tx, stk1.STK1_EVENT_TYPE, stk1.STK1_EVENT_NO_1, stk1.STK1_EVENT_NO_2);
            _stk1.UpdateRow(stk1, tx);

            // --- DSE1 (TabA_UpdateDSE1): the event's first dispatch / en route / arrive
            var changed = false;
            if (dse1.DSE1_DSP_DATE == null && dsu1.DSU1_DSP_DATE != null) { dse1.DSE1_DSP_DATE = dsu1.DSU1_DSP_DATE; changed = true; }
            if (dse1.DSE1_ENR_DATE == null && dsu1.DSU1_ENR_DATE != null) { dse1.DSE1_ENR_DATE = dsu1.DSU1_ENR_DATE; changed = true; }
            if (dse1.DSE1_ARV_DATE == null && dsu1.DSU1_ARV_DATE != null) { dse1.DSE1_ARV_DATE = dsu1.DSU1_ARV_DATE; changed = true; }

            if (changed)
            {
                _dse1.UpdateRow(dse1, tx);
                Fat2(tx, "DSE1", Dse1Key(dse1), FatUpdate);
            }

            jurisdiction = Text(rst1.RST1_JUR);

            return "Unit " + Text(rst1.RST1_UNIT_NO) + " assigned to " + EventText(stk1) +
                   " (dispatch " + dsu1.DSU1_DISP_NO_1 + "-" + dsu1.DSU1_DISP_NO_2 + ")." + released;
        }

        private static void CopyEventToDsu1(DSE1 dse1, DSU1 dsu1)
        {
            dsu1.DSU1_EVENT_TYPE = dse1.DSE1_EVENT_TYPE;
            dsu1.DSU1_EVENT_NO_1 = dse1.DSE1_EVENT_NO_1;
            dsu1.DSU1_EVENT_NO_2 = dse1.DSE1_EVENT_NO_2;
            dsu1.DSU1_LOC_JUR = dse1.DSE1_LOC_JUR;
            dsu1.DSU1_LOC_ST_NAM = dse1.DSE1_LOC_ST_NAM;
            dsu1.DSU1_LOC_ST_NO = dse1.DSE1_LOC_ST_NO;
            dsu1.DSU1_LOC_SUB_NO = dse1.DSE1_LOC_SUB_NO;
            dsu1.DSU1_LOC_UNIT = dse1.DSE1_LOC_UNIT;
            dsu1.DSU1_LOC_X_ST = dse1.DSE1_LOC_X_ST;
            dsu1.DSU1_RCV_ID = dse1.DSE1_RCV_ID;
            dsu1.DSU1_RCV_DATE = dse1.DSE1_RCV_DATE;
            dsu1.DSU1_DOW = dse1.DSE1_DOW;
            dsu1.DSU1_CALL_CODE = dse1.DSE1_CALL_CODE;
            dsu1.DSU1_PRIORITY = dse1.DSE1_PRIORITY;
            dsu1.DSU1_ORIGIN_S = dse1.DSE1_ORIGIN_S;
            dsu1.DSU1_ORIGIN_L = dse1.DSE1_ORIGIN_L;
            dsu1.DSU1_ORIGIN_C = dse1.DSE1_ORIGIN_C;
            dsu1.DSU1_FORCED = dse1.DSE1_FORCED;
            dsu1.DSU1_GEOCODE_NO = dse1.DSE1_GEOCODE_NO;
            dsu1.DSU1_POLICE_1 = dse1.DSE1_POLICE_1;
            dsu1.DSU1_POLICE_2 = dse1.DSE1_POLICE_2;
            dsu1.DSU1_FIRE_1 = dse1.DSE1_FIRE_1;
            dsu1.DSU1_EMS_1 = dse1.DSE1_EMS_1;
            dsu1.DSU1_OTHER_1 = dse1.DSE1_OTHER_1;
            dsu1.DSU1_PMM_NO = dse1.DSE1_PMM_NO;
        }

        /// <summary>The same global event's stack row for another agency.</summary>
        private STK1 LinkedStack(CadTransaction tx, STK1 picked, char agencyType)
        {
            if (picked.STK1_G_EVENT_2 == 0) return null;

            return _stk1.GetList(
                    "STK1_G_EVENT_1 = @g1 AND STK1_G_EVENT_2 = @g2 AND STK1_EVENT_TYPE = @type",
                    new { g1 = picked.STK1_G_EVENT_1, g2 = picked.STK1_G_EVENT_2, type = agencyType.ToString() },
                    tx, forUpdate: true)
                .FirstOrDefault();
        }

        // ========================================================= unit functions

        /// <summary>
        /// Unit Status - LwCad135 TabA_ProcessUnit. The legacy screen offers five
        /// radio buttons; this panel offers the UST1 list, so the chosen status is
        /// mapped onto the branch that reaches it from where the unit is now:
        ///
        ///   2 / 12 -> 3        En Route   DSU1_ENR, DSE1_ENR_DATE, RST1
        ///   2 / 3  -> 4        Arrive     DSU1_ARV, DSE1_ARV_DATE, RST1
        ///   on a call -> 5     Cmp/Scene  DSU1_CS,  DSE1_CS_DATE,  RST1
        ///   1 -> 20, 20 -> 21, 10 -> 11   off-call steps, logged to DST1
        ///   20 / 21 -> 22      Complete   DST1, then the roster row is reset
        ///   on a call -> an available status   Complete the call (LwCad108 mode "k")
        ///
        /// Anything else sets the status and logs it to DST1, which the legacy screen
        /// has no way to do; it is marked in the DST1 remarks.
        /// </summary>
        public UnitResult ChangeStatus(string unitNo, UnitStatusOption status, string location,
                                       int timeoutMinutes, string remarks)
        {
            if (status == null) throw new CadWriteException("A status must be chosen.");

            return RunUnit(unitNo, (tx, rst1, now) =>
            {
                var from = rst1.RST1_STATUS;
                var target = status.Code;
                var onCall = rst1.RST1_DISP_NO_2 > 0;

                if (target == EnRoute && (from == Dispatched || from == MdcSent) && onCall)
                {
                    StampDispatch(tx, rst1, now, (d, t) => { d.DSU1_ENR_ID = _session.EmpId; d.DSU1_ENR_DATE = t; },
                                  e => e.DSE1_ENR_DATE, (e, t) => e.DSE1_ENR_DATE = t);
                    rst1.RST1_STATUS = EnRoute;
                    Tail(rst1, now, timeoutMinutes);
                    _rst1.UpdateRow(rst1, tx);
                    return "set to En Route";
                }

                if (target == Arrived && (from == Dispatched || from == EnRoute) && onCall)
                {
                    StampDispatch(tx, rst1, now, (d, t) => { d.DSU1_ARV_ID = _session.EmpId; d.DSU1_ARV_DATE = t; },
                                  e => e.DSE1_ARV_DATE, (e, t) => e.DSE1_ARV_DATE = t);
                    rst1.RST1_STATUS = Arrived;
                    Tail(rst1, now, timeoutMinutes);
                    _rst1.UpdateRow(rst1, tx);
                    return "arrived on scene";
                }

                if (target == CompleteAtScene && onCall &&
                    new short[] { Dispatched, EnRoute, Arrived, 6, LocationEnRoute, LocationArrived, MdcSent }.Contains(from))
                {
                    StampDispatch(tx, rst1, now, (d, t) => { d.DSU1_CS_ID = _session.EmpId; d.DSU1_CS_DATE = t; },
                                  e => e.DSE1_CS_DATE, (e, t) => e.DSE1_CS_DATE = t);
                    rst1.RST1_STATUS = CompleteAtScene;
                    Tail(rst1, now, timeoutMinutes);
                    _rst1.UpdateRow(rst1, tx);
                    return "complete at scene";
                }

                if ((target == OffCallEnRoute && from == InService) ||
                    (target == OffCallArrived && from == OffCallEnRoute) ||
                    (target == LocationArrived && from == LocationEnRoute))
                {
                    rst1.RST1_STATUS = target;
                    var prefix = target == OffCallEnRoute ? "Enr - " : "Arv - ";
                    rst1.RST1_TRANS_NO_2 = (int)CreateDst1(tx, rst1, now, prefix, location, remarks);
                    if (Text(location).Length > 0) rst1.RST1_LAST_LOC = Text(location);
                    Tail(rst1, now, timeoutMinutes);
                    _rst1.UpdateRow(rst1, tx);
                    return "set to " + status.Abbreviation;
                }

                if (target == OffCallComplete && (from == OffCallEnRoute || from == OffCallArrived))
                {
                    rst1.RST1_STATUS = OffCallComplete;
                    CreateDst1(tx, rst1, now, "Cmp - ", location, remarks);
                    ResetRoster(rst1, now);
                    _rst1.UpdateRow(rst1, tx);
                    return "completed and back in service";
                }

                if (status.IsAvailable && onCall)
                {
                    var outcome = ReleaseCore(tx, rst1, now, complete: true);
                    return "completed and back in service; " + Describe(outcome);
                }

                if (status.IsAvailable &&
                    (from == LocationEnRoute || from == LocationArrived ||
                     from == OffCallEnRoute || from == OffCallArrived))
                {
                    CreateDst1(tx, rst1, now, "Cmp - ", location, remarks);
                    ResetRoster(rst1, now);
                    _rst1.UpdateRow(rst1, tx);
                    return "back in service";
                }

                // A step the legacy radio group has no button for.
                rst1.RST1_STATUS = target;
                rst1.RST1_TRANS_NO_2 = (int)CreateDst1(tx, rst1, now, string.Empty, location,
                    ("Status " + from + " to " + target + ". " + (remarks ?? string.Empty)).Trim());
                if (Text(location).Length > 0) rst1.RST1_LAST_LOC = Text(location);
                Tail(rst1, now, timeoutMinutes);
                _rst1.UpdateRow(rst1, tx);
                return "set to " + status.Abbreviation;
            });
        }

        /// <summary>
        /// Location Change - LwCad135 TabA_ProcessLocEnr: status 10 on a call, 20 off
        /// one, a DST1 transaction for the move, and the new last location. The
        /// legacy does this without locking RST1; this locks it like every other write.
        /// </summary>
        public UnitResult ChangeLocation(string unitNo, string location, int timeoutMinutes, string remarks)
        {
            if (Text(location).Length == 0) throw new CadWriteException("A location must be entered.");

            return RunUnit(unitNo, (tx, rst1, now) =>
            {
                rst1.RST1_STATUS = rst1.RST1_DISP_NO_2 > 0 ? LocationEnRoute : OffCallEnRoute;
                rst1.RST1_TRANS_NO_2 = (int)CreateDst1(tx, rst1, now, "Enr - ", location, remarks);
                rst1.RST1_LAST_LOC = Text(location);
                Tail(rst1, now, timeoutMinutes);
                _rst1.UpdateRow(rst1, tx);
                return "location set to " + Text(location);
            });
        }

        /// <summary>
        /// Release Unit - LwCad108 in release mode. The unit comes off its call and
        /// back into service; CCC1_RESTACK decides the call once the last unit is off.
        /// </summary>
        public UnitResult Release(string unitNo)
        {
            return RunUnit(unitNo, (tx, rst1, now) =>
            {
                if (rst1.RST1_DISP_NO_2 == 0)
                {
                    throw new CadWriteException("Unit " + unitNo + " is not on a call.");
                }

                var outcome = ReleaseCore(tx, rst1, now, complete: false);
                return "released and back in service; " + Describe(outcome);
            });
        }

        private UnitResult RunUnit(string unitNo, Func<CadTransaction, RST1, DateTime, string> work)
        {
            unitNo = Text(unitNo);
            var result = new UnitResult { UnitNo = unitNo };

            try
            {
                using (var tx = new CadTransaction(_connectString))
                {
                    var rst1 = LockUnit(tx, unitNo);
                    var now = ServerNow(tx);
                    result.Message = "Unit " + unitNo + " " + work(tx, rst1, now) + ".";
                    tx.Commit();
                }

                result.Written = true;
                SendTasks((202, unitNo), (301, null), (401, null));
            }
            catch (CadWriteException ex)
            {
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                LaLog.Error("CadWriteBack", "Unit " + unitNo + " update failed.", ex);
                result.Message = "Unit " + unitNo + " was not updated: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Writes a transition onto the unit's DSU1 dispatch record, and onto the
        /// event when it is the event's first - TabA_ProcessUnitEnr / Arv / Lwc.
        /// </summary>
        private void StampDispatch(CadTransaction tx, RST1 rst1, DateTime now,
                                   Action<DSU1, DateTime> stampUnit,
                                   Func<DSE1, DateTime?> eventStamp, Action<DSE1, DateTime> setEvent)
        {
            var dsu1 = _dsu1.GetRowForUpdate(new DSU1 { DSU1_DISP_NO_1 = rst1.RST1_DISP_NO_1, DSU1_DISP_NO_2 = rst1.RST1_DISP_NO_2 }, tx);
            if (_dsu1.RecordStatus == 0)
            {
                stampUnit(dsu1, now);
                _dsu1.UpdateRow(dsu1, tx);
                Fat2(tx, "DSU1", Dsu1Key(dsu1), FatUpdate);
            }

            var dse1 = _dse1.GetRowForUpdate(new DSE1
            {
                DSE1_EVENT_TYPE = rst1.RST1_EVENT_TYPE,
                DSE1_EVENT_NO_1 = rst1.RST1_EVENT_NO_1,
                DSE1_EVENT_NO_2 = rst1.RST1_EVENT_NO_2
            }, tx);

            if (_dse1.RecordStatus == 0 && eventStamp(dse1) == null)
            {
                setEvent(dse1, now);
                _dse1.UpdateRow(dse1, tx);
            }
        }

        /// <summary>The RST1 fields every LwCad135 branch sets before its WriteRow.</summary>
        private static void Tail(RST1 rst1, DateTime now, int timeoutMinutes)
        {
            rst1.RST1_LAST_HR = (short)now.Hour;
            rst1.RST1_LAST_MN = (short)now.Minute;
            rst1.RST1_T_O_F_HR = (short)now.Hour;
            rst1.RST1_T_O_F_MN = (short)now.Minute;
            rst1.RST1_T_O_MN = (short)Math.Max(0, Math.Min(short.MaxValue, timeoutMinutes));
            rst1.RST1_T_O_FLAG = 0;
            rst1.RST1_LOCK_ID = 0;
        }

        /// <summary>TabA_CreateDST1: one unit transaction, returning its DST1_T_KEY.</summary>
        private long CreateDst1(CadTransaction tx, RST1 rst1, DateTime now, string prefix, string location, string remarks)
        {
            var dst1 = _dst1.Initialize(new DST1());

            dst1.DST1_DISP_NO_1 = rst1.RST1_DISP_NO_1;
            dst1.DST1_DISP_NO_2 = rst1.RST1_DISP_NO_2;
            dst1.DST1_EVENT_TYPE = rst1.RST1_EVENT_TYPE;
            dst1.DST1_EVENT_NO_1 = rst1.RST1_EVENT_NO_1;
            dst1.DST1_EVENT_NO_2 = rst1.RST1_EVENT_NO_2;
            dst1.DST1_RCD_TYPE = 'U';
            dst1.DST1_TRAN_DATE = now;
            dst1.DST1_DOW = Dow(now);
            dst1.DST1_UNIT_NO = rst1.RST1_UNIT_NO;
            dst1.DST1_AGENCY = rst1.RST1_AGENCY;
            dst1.DST1_OFFICER_1 = rst1.RST1_OFCR_1;
            dst1.DST1_OFFICER_2 = rst1.RST1_OFCR_2;
            dst1.DST1_ENT_BY = _session.EmpId;
            dst1.DST1_REMARKS = remarks ?? string.Empty;
            dst1.DST1_LOCATION = (prefix + Text(location)).Trim();
            dst1.DST1_U_STATUS = rst1.RST1_STATUS;

            _dst1.InsertRow(dst1, tx);
            Fat2(tx, "DST1", dst1.DST1_T_KEY.ToString(), FatInsert);

            return dst1.DST1_T_KEY;
        }

        // ================================================================ release

        /// <summary>What a release did to the call the unit came off.</summary>
        public enum ReleaseOutcome
        {
            /// <summary>Other units are still on the call.</summary>
            CallUnaffected,

            /// <summary>Last unit off and CCC1_RESTACK is not 'N': back on the stack.</summary>
            CallRestacked,

            /// <summary>Last unit off and the call was completed or does not restack.</summary>
            CallClosed
        }

        /// <summary>
        /// LwCad108 TabA_SaveDSU1 + TabA_SaveOtherRecords. Completes the unit's DSU1,
        /// resets its roster row, then decides the call. <paramref name="complete"/>
        /// is LwCad108's mode "k" - Complete from the status screen - which always
        /// closes the call once the last unit is off; a release (mode "r" / "l")
        /// restacks it unless CCC1_RESTACK is 'N'.
        ///
        /// <paramref name="disposition"/> is LwCad123's: Self Initiate releasing a
        /// busy unit closes its dispatch record 05/01/code and reclassifies it as
        /// its own call code, so the code lands on DSE1 too if the call closes.
        /// </summary>
        private ReleaseOutcome ReleaseCore(CadTransaction tx, RST1 rst1, DateTime now, bool complete, short disposition = 0)
        {
            var eventType = rst1.RST1_EVENT_TYPE;
            var eventNo1 = rst1.RST1_EVENT_NO_1;
            var eventNo2 = rst1.RST1_EVENT_NO_2;

            var dsu1 = _dsu1.GetRowForUpdate(new DSU1 { DSU1_DISP_NO_1 = rst1.RST1_DISP_NO_1, DSU1_DISP_NO_2 = rst1.RST1_DISP_NO_2 }, tx);
            var haveDsu1 = _dsu1.RecordStatus == 0;

            if (haveDsu1)
            {
                if (disposition > 0)
                {
                    dsu1.DSU1_DISPO_S = 5;
                    dsu1.DSU1_DISPO_L = 1;
                    dsu1.DSU1_DISPO_C = disposition;
                }

                if (dsu1.DSU1_DISPO_C > 0 && dsu1.DSU1_RECLASS == 0) dsu1.DSU1_RECLASS = dsu1.DSU1_CALL_CODE;
                dsu1.DSU1_CMP_ID = _session.EmpId;
                dsu1.DSU1_CMP_DATE = now;
                _dsu1.UpdateRow(dsu1, tx);
                Fat2(tx, "DSU1", Dsu1Key(dsu1), FatUpdate);
            }

            ResetRoster(rst1, now);
            _rst1.UpdateRow(rst1, tx);

            if (eventNo2 == 0) return ReleaseOutcome.CallUnaffected;

            var stack = _stk1.GetList(
                    "STK1_EVENT_TYPE = @t AND STK1_EVENT_NO_1 = @n1 AND STK1_EVENT_NO_2 = @n2",
                    new { t = eventType.ToString(), n1 = eventNo1, n2 = eventNo2 }, tx, forUpdate: true)
                .FirstOrDefault();

            var units = UnitsOnEvent(tx, eventType, eventNo1, eventNo2);

            if (units.Length > 0)
            {
                if (stack != null)
                {
                    stack.STK1_UNITS = units;
                    _stk1.UpdateRow(stack, tx);
                }

                return ReleaseOutcome.CallUnaffected;
            }

            var dse1 = _dse1.GetRowForUpdate(new DSE1
            {
                DSE1_EVENT_TYPE = eventType,
                DSE1_EVENT_NO_1 = eventNo1,
                DSE1_EVENT_NO_2 = eventNo2
            }, tx);
            var haveDse1 = _dse1.RecordStatus == 0;

            var code = _ccc1.GetRow(new CCC1 { CCC1_CODE = haveDse1 ? dse1.DSE1_CALL_CODE : 0 });
            var restack = _ccc1.RecordStatus == 0 && char.ToUpperInvariant(code.CCC1_RESTACK) != 'N';

            if (!complete && restack && stack != null)
            {
                // TabA_RestackCall.
                stack.STK1_STATUS = 'S';
                stack.STK1_T_O_FLAG = 0;
                stack.STK1_T_O_MN = StackTimeout(code, eventType);
                stack.STK1_T_O_F_HR = (short)now.Hour;
                stack.STK1_T_O_F_MN = (short)now.Minute;
                stack.STK1_UNITS = string.Empty;
                _stk1.UpdateRow(stack, tx);
                return ReleaseOutcome.CallRestacked;
            }

            // TabA_DeleteStack, TabA_CompleteDSE1, TabA_DeleteEmg2.
            if (stack != null) _stk1.DeleteRow(stack, tx);

            if (haveDse1)
            {
                dse1.DSE1_CMP_DATE = now;

                if (haveDsu1)
                {
                    dse1.DSE1_DISPO_S = dsu1.DSU1_DISPO_S;
                    dse1.DSE1_DISPO_L = dsu1.DSU1_DISPO_L;
                    dse1.DSE1_DISPO_C = dsu1.DSU1_DISPO_C;
                    if (dsu1.DSU1_RECLASS > 0) dse1.DSE1_RECLASS = dsu1.DSU1_RECLASS;
                }

                _dse1.UpdateRow(dse1, tx);
                Fat2(tx, "DSE1", Dse1Key(dse1), FatUpdate);
            }

            tx.Execute("DELETE FROM dbo.EMG2 WHERE EMG2_EVENT_TYPE = @t AND EMG2_EVENT_NO_1 = @n1 AND EMG2_EVENT_NO_2 = @n2",
                       ("@t", eventType), ("@n1", eventNo1), ("@n2", eventNo2));

            return ReleaseOutcome.CallClosed;
        }

        /// <summary>The roster reset LwCad108 TabA_UpdateRST1 and LwCad135 TabA_CheckResetRoster share.</summary>
        private static void ResetRoster(RST1 rst1, DateTime now)
        {
            if (rst1.RST1_TEMP_UNIT == 'A')
            {
                rst1.RST1_DISPLAY = 'N';
                rst1.RST1_RECOMMEND = Text(rst1.RST1_R_AREA_1).Length == 0 ? 'N' : 'Y';
            }

            rst1.RST1_STATUS = InService;
            rst1.RST1_EVENT_TYPE = ' ';
            rst1.RST1_EVENT_NO_1 = 0;
            rst1.RST1_EVENT_NO_2 = 0;
            rst1.RST1_DISP_NO_1 = 0;
            rst1.RST1_DISP_NO_2 = 0;
            rst1.RST1_TRANS_NO_1 = 0;
            rst1.RST1_TRANS_NO_2 = 0;
            rst1.RST1_ASGN_TYPE = ' ';
            rst1.RST1_CALL_CODE = 0;
            rst1.RST1_PRIORITY = 0;
            rst1.RST1_LAST_LOC = string.Empty;
            rst1.RST1_LAST_HR = 0;
            rst1.RST1_LAST_MN = 0;
            rst1.RST1_T_O_F_HR = 0;
            rst1.RST1_T_O_F_MN = 0;
            rst1.RST1_T_O_MN = rst1.RST1_TMOUT_MINS;
            rst1.RST1_T_O_FLAG = 0;

            if (rst1.RST1_T_O_MN > 0)
            {
                rst1.RST1_LAST_HR = rst1.RST1_T_O_F_HR = (short)now.Hour;
                rst1.RST1_LAST_MN = rst1.RST1_T_O_F_MN = (short)now.Minute;
            }

            rst1.RST1_T_TYPE_C = 0;
            rst1.RST1_T_TYPE_L = 0;
            rst1.RST1_T_TYPE_S = 0;
            rst1.RST1_TEMP_UNIT = ' ';
            rst1.RST1_TEMP_JUR = string.Empty;
            rst1.RST1_LOCK_ID = 0;
        }

        // ================================================================ helpers

        /// <summary>
        /// Reads the unit's roster row with an update lock, refusing it when an LW
        /// console holds it - TabA_ReadLockRst1. The legacy asks "Continue?" at that
        /// point; a board that cannot see the other screen refuses instead.
        /// </summary>
        private RST1 LockUnit(CadTransaction tx, string unitNo)
        {
            var rst1 = _rst1.GetList("RTRIM(RST1_UNIT_NO) = @unit", new { unit = unitNo }, tx, forUpdate: true)
                            .FirstOrDefault();

            if (rst1 == null)
            {
                throw new CadWriteException("Unit " + unitNo + " is not on the roster (RST1).");
            }

            if (rst1.RST1_LOCK_ID != 0 && rst1.RST1_LOCK_ID != _session.EmpId)
            {
                throw new CadWriteException("Unit " + unitNo + " is being updated by " + rst1.RST1_LOCK_ID + ".");
            }

            return rst1;
        }

        /// <summary>
        /// Takes the next number from a DSC1 counter for the year, creating the
        /// year's row if it is the first. One atomic UPDATE ... OUTPUT replaces the
        /// legacy read-then-compare-and-set loop (TakeCall_AssignEKey / GKey,
        /// TabA_AssignDKey).
        /// </summary>
        private static int NextCounter(CadTransaction tx, short year, string column)
        {
            switch (column)
            {
                case "DSC1_GLOBAL_NO":
                case "DSC1_EVENT_NO_P":
                case "DSC1_EVENT_NO_F":
                case "DSC1_EVENT_NO_E":
                case "DSC1_EVENT_NO_O":
                case "DSC1_DISP_NO":
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(column), column, "Not a DSC1 counter.");
            }

            tx.Execute(
                "IF NOT EXISTS (SELECT 1 FROM dbo.DSC1 WITH (UPDLOCK, HOLDLOCK) WHERE DSC1_YEAR = @year) " +
                "INSERT INTO dbo.DSC1 (DSC1_YEAR, DSC1_GLOBAL_NO, DSC1_EVENT_NO_P, DSC1_EVENT_NO_F, DSC1_EVENT_NO_E, " +
                "DSC1_EVENT_NO_O, DSC1_DISP_NO, DSC1_TRANS_NO, DSC1_BULL_NO, DSC1_MSG_NO, DSC1_TOW_NO, DSC1_MOD_BY) " +
                "VALUES (@year, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)",
                ("@year", year));

            // SET @next = column = column + 1 rather than OUTPUT inserted.column: the
            // CAD tables carry TRIGGER_DATE triggers, and SQL Server refuses a bare
            // OUTPUT clause on a table with enabled triggers.
            var next = tx.Scalar(
                "DECLARE @next int; " +
                "UPDATE dbo.DSC1 SET @next = " + column + " = ISNULL(" + column + ", 0) + 1 WHERE DSC1_YEAR = @year; " +
                "SELECT @next",
                ("@year", year));

            if (next == null) throw new CadWriteException("Unable to assign a number from DSC1 for " + year + ".");
            return Convert.ToInt32(next);
        }

        /// <summary>"U1 U2" - RST1 units on the event, as GetUnitsOnCallList builds STK1_UNITS.</summary>
        private string UnitsOnEvent(CadTransaction tx, char type, short no1, int no2)
        {
            var units = _rst1.GetList(
                    "RST1_EVENT_TYPE = @t AND RST1_EVENT_NO_1 = @n1 AND RST1_EVENT_NO_2 = @n2",
                    new { t = type.ToString(), n1 = no1, n2 = no2 }, tx)
                .Select(r => Text(r.RST1_UNIT_NO))
                .Where(u => u.Length > 0)
                .OrderBy(u => u, StringComparer.Ordinal);

            return string.Join(" ", units);
        }

        /// <summary>Queues an FAT2 row for the FatPot interface, when this database has one.</summary>
        private void Fat2(CadTransaction tx, string table, string key, char action)
        {
            _hasFat2 ??= tx.Scalar("SELECT OBJECT_ID('dbo.FAT2')") != null;
            if (_hasFat2 != true) return;

            tx.Execute("INSERT INTO dbo.FAT2 (FAT2_TBL_NAME, FAT2_TBL_KEY, FAT2_ACTION) VALUES (@name, @key, @action)",
                       ("@name", table), ("@key", key), ("@action", action));
        }

        private static string Dse1Key(DSE1 r) =>
            r.DSE1_EVENT_TYPE + r.DSE1_EVENT_NO_1.ToString() + r.DSE1_EVENT_NO_2.ToString().PadLeft(8, '0');

        private static string Dsu1Key(DSU1 r) =>
            r.DSU1_DISP_NO_1.ToString() + r.DSU1_DISP_NO_2.ToString().PadLeft(8, '0');

        /// <summary>
        /// TSK1 task messages, written after the transaction commits - LwCadTsk001
        /// TaskMsg. 2xx go to every console with a unit monitor (DAT1_TS_MON &gt; 0),
        /// 3xx to every session, 4xx to every map (DAT1_TS_MAP set); each is one TSK1
        /// row listing the bracketed employee and session ids. 404 is suppressed in
        /// Lowell, as the legacy suppresses it. A failure here is logged and not
        /// reported: the write itself has landed, and the next refresh cycle of every
        /// console picks it up regardless.
        /// </summary>
        private void SendTasks(params (int Action, string UnitNo)[] tasks)
        {
            foreach (var (action, unitNo) in tasks.Distinct())
            {
                try
                {
                    SendTask((short)action, unitNo);
                }
                catch (Exception ex)
                {
                    LaLog.Error("TSK1", "Task message " + action + " not sent.", ex);
                }
            }
        }

        private void SendTask(short action, string unitNo)
        {
            string where;
            char taskType;
            short priority;

            if (action >= 200 && action < 300) { where = "DAT1_TS_MON > 0"; taskType = 'U'; priority = 2; }
            else if (action >= 300 && action < 400) { where = null; taskType = 'S'; priority = 2; }
            else if (action >= 400 && action < 500 && action != 404) { where = "DAT1_TS_MAP IS NOT NULL"; taskType = 'P'; priority = 0; }
            else return;

            using var tx = new CadTransaction(_connectString);

            var sessions = _dat1.GetList(where, null, tx);
            if (sessions.Count == 0) return;

            var tsk1 = _tsk1.Initialize(new TSK1());
            tsk1.TSK1_EMP_ID = string.Concat(sessions.Select(s => "[" + s.DAT1_EMP_ID + "]"));
            tsk1.TSK1_SESS_ID = string.Concat(sessions.Select(s => "[" + s.DAT1_SESS_ID + "]"));
            tsk1.TSK1_TASK_TYPE = taskType;
            tsk1.TSK1_PRIORITY = priority;
            tsk1.TSK1_ACTION = action;
            tsk1.TSK1_UNIT_NO = taskType == 'S' ? string.Empty : Text(unitNo);
            tsk1.TSK1_DATE_TIME = null;
            tsk1.TSK1_TRIGGER_DATE = null;

            _tsk1.InsertRow(tsk1, tx);
            tx.Commit();
        }

        private static short TravelTimeout(CCC1 code, char agencyType)
        {
            switch (agencyType)
            {
                case 'P': return code.CCC1_PT_O_TRVL;
                case 'F': return code.CCC1_FT_O_TRVL;
                case 'E': return code.CCC1_ET_O_TRVL;
                default: return code.CCC1_OT_O_TRVL;
            }
        }

        private static short StackTimeout(CCC1 code, char eventType)
        {
            switch (eventType)
            {
                case 'P': return code.CCC1_PT_O_STCK;
                case 'F': return code.CCC1_FT_O_STCK;
                case 'E': return code.CCC1_ET_O_STCK;
                default: return code.CCC1_OT_O_STCK;
            }
        }

        private static DateTime ServerNow(CadTransaction tx) => Convert.ToDateTime(tx.Scalar("SELECT GETDATE()"));

        private static string Dow(DateTime date) => date.DayOfWeek.ToString().ToUpperInvariant().Substring(0, 3);

        private static string Text(string value) => (value ?? string.Empty).Trim();

        private static string FormatEvent(short no1, int no2) => no1.ToString("0000") + "-" + no2.ToString("0000000");

        private static string EventText(STK1 stk1) => FormatEvent(stk1.STK1_EVENT_NO_1, stk1.STK1_EVENT_NO_2);

        private static string AgencyName(char type)
        {
            switch (type)
            {
                case 'P': return "police";
                case 'F': return "fire";
                case 'E': return "EMS";
                default: return "support";
            }
        }

        public static string Describe(ReleaseOutcome outcome)
        {
            switch (outcome)
            {
                case ReleaseOutcome.CallRestacked: return "last unit off, so the call is back on the stack";
                case ReleaseOutcome.CallClosed: return "last unit off, so the call is closed";
                default: return "the call is unchanged";
            }
        }
    }

    /// <summary>A refusal the dispatcher should read, as opposed to a failure.</summary>
    public sealed class CadWriteException : Exception
    {
        public CadWriteException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Splits a typed location into the DSE1_LOC_* parts. The legacy screen
    /// geocodes against GEO1 (LaGeo040), which this build does not carry, so this
    /// takes the address the way a dispatcher types it: "316 ACCABONAC RD",
    /// "MAIN ST / ELM ST" or "MAIN ST &amp; ELM ST" for an intersection, and "#4" or
    /// "APT 4" for a unit.
    /// </summary>
    public sealed class LocationParts
    {
        public decimal StreetNo;
        public string StreetName = string.Empty;
        public string SubNo = string.Empty;
        public string Unit = string.Empty;
        public string CrossStreet = string.Empty;

        public static LocationParts Parse(string location)
        {
            var parts = new LocationParts();
            var text = (location ?? string.Empty).Trim().ToUpperInvariant();
            if (text.Length == 0) return parts;

            var cross = text.IndexOfAny(new[] { '/', '&' });
            if (cross > 0)
            {
                parts.CrossStreet = text.Substring(cross + 1).Trim();
                text = text.Substring(0, cross).Trim();
            }

            foreach (var marker in new[] { " APT ", " UNIT ", " #" })
            {
                var at = text.IndexOf(marker, StringComparison.Ordinal);
                if (at <= 0) continue;

                parts.Unit = text.Substring(at + marker.Length).Trim();
                text = text.Substring(0, at).Trim();
                break;
            }

            var space = text.IndexOf(' ');
            if (space > 0)
            {
                var head = text.Substring(0, space);
                var number = new string(head.TakeWhile(char.IsDigit).ToArray());

                if (number.Length > 0 && decimal.TryParse(number, out var value) && value < 1000000m)
                {
                    parts.StreetNo = value;
                    parts.SubNo = head.Substring(number.Length).TrimStart('-');   // "12A" -> 12, "A"
                    text = text.Substring(space + 1).Trim();
                }
            }

            parts.StreetName = text;
            return parts;
        }

        /// <summary>The address as the monitors print it.</summary>
        public static string Format(DSE1 dse1)
        {
            var street = (dse1.DSE1_LOC_ST_NAM ?? string.Empty).Trim();
            var text = dse1.DSE1_LOC_ST_NO > 0
                ? dse1.DSE1_LOC_ST_NO.ToString("0.#") + (dse1.DSE1_LOC_SUB_NO ?? string.Empty).Trim() + " " + street
                : street;

            var unit = (dse1.DSE1_LOC_UNIT ?? string.Empty).Trim();
            if (unit.Length > 0) text += " #" + unit;

            var cross = (dse1.DSE1_LOC_X_ST ?? string.Empty).Trim();
            if (cross.Length > 0) text += " / " + cross;

            return text.Trim();
        }
    }
}
