using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwCadSystemUX.Services.TakeCall;
using LwTableCcc1;
using LwTableDsc1;
using LwTableDse1;
using LwTableDst1;
using LwTableRst1;
using LwTableStk1;
using LwTableUst1;
using LaConfig;
using LaTableGeo1;
using LaTableMlb1;
using LwTablePmm1;
using LwTableBmp1;
using LwTableBmp2;
using LwTableCad7;
using LwTableRst2;
using LwTableRun1;
using LwTableRun5;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// The live CAD back end, reading the production tables through the .NET 8
    /// readers in Services/Data.
    ///
    /// Sources match the legacy screens: the call monitors come from STK1 joined
    /// to CCC1 and DSE1 (CadMain WinA.cs), split by STK1_STATUS - 'S' stacked,
    /// 'A' active, 'D' delayed. The unit monitor comes from RST1 joined to UST1.
    ///
    /// Take Call, Assign and the unit functions are written back to the tables
    /// through CadWriteBack, and the board is then re-read from them - what the
    /// monitors show after a change is what the database holds, not a local copy.
    /// </summary>
    public partial class SqlCadDataService : ICadDataService
    {
        private readonly Ccc1_RW _ccc1;
        private readonly Dsc1_RW _dsc1;
        private readonly Stk1_RW _stk1;
        private readonly Dse1_RW _dse1;
        private readonly Rst1_RW _rst1;
        private readonly Ust1_RW _ust1;
        private readonly Mlb1_RW _mlb1;
        private readonly Dst1_RW _dst1;
        private readonly Pmm1_RW _pmm1;
        private readonly Geo1_RW _geo1;

        // The unit recommendation the assign screens make; see CadRecommendation.
        private readonly Cad7_RW _cad7;
        private readonly Bmp1_RW _bmp1;
        private readonly Bmp2_RW _bmp2;
        private readonly Rst2_RW _rst2;
        private readonly Run1_RW _run1;
        private readonly Run5_RW _run5;

        /// <summary>
        /// CAD7_PLAN_TYPE - 'D' recommends police and support off a deployment
        /// plan, 'G' off the call's own report area. Read once at start-up, as the
        /// legacy screens read it once on open.
        /// </summary>
        private char _planType = 'G';

        private readonly Dictionary<int, CCC1> _callCodesByCode = new Dictionary<int, CCC1>();
        private readonly Dictionary<short, UST1> _statusByCode = new Dictionary<short, UST1>();

        // MLB1 libraries the unit monitor resolves on every refresh, cached by
        // (system, library) so a board of 60 units is not 120 extra round trips.
        private readonly Dictionary<(short, short), Dictionary<short, string>> _codeLibraries =
            new Dictionary<(short, short), Dictionary<short, string>>();

        // Event detail is a round trip per stack row, so it is fetched once per
        // event rather than on every monitor cycle.
        private readonly HashSet<long> _detailLoaded = new HashSet<long>();

        // Units assigned per event, read once per refresh instead of once per row.
        private readonly Dictionary<(char, short, int), List<AssignedUnit>> _unitsByEvent =
            new Dictionary<(char, short, int), List<AssignedUnit>>();

        // Take Call, Assign and the unit functions.
        private readonly CadWriteBack _writer;

        public SqlCadDataService() : this(null)
        {
        }

        public SqlCadDataService(string connectString)
        {
            var config = new LaGetConfig();
            ConnectString = string.IsNullOrWhiteSpace(connectString)
                ? config.GetConnectString()
                : connectString;

            AgencyName = string.IsNullOrWhiteSpace(config.PrintTitle) ? "LARIMORE ASSOCIATES" : config.PrintTitle;

            _ccc1 = new Ccc1_RW(ConnectString);
            _dsc1 = new Dsc1_RW(ConnectString);
            _stk1 = new Stk1_RW(ConnectString);
            _dse1 = new Dse1_RW(ConnectString);
            _rst1 = new Rst1_RW(ConnectString);
            _ust1 = new Ust1_RW(ConnectString);
            _mlb1 = new Mlb1_RW(ConnectString);
            _dst1 = new Dst1_RW(ConnectString);
            _pmm1 = new Pmm1_RW(ConnectString);
            _geo1 = new Geo1_RW(ConnectString);

            _cad7 = new Cad7_RW(ConnectString);
            _bmp1 = new Bmp1_RW(ConnectString);
            _bmp2 = new Bmp2_RW(ConnectString);
            _rst2 = new Rst2_RW(ConnectString);
            _run1 = new Run1_RW(ConnectString);
            _run5 = new Run5_RW(ConnectString);

            _writer = new CadWriteBack(ConnectString, CadSession.Current);
            OperatorId = CadSession.Current.EmpId.ToString();
            SessionId = CadSession.Current.SessId;

            LoadReferenceData();
            LoadSession();
            LoadKeymap();
            LoadTakeCallSupport();
            LoadSelfInitSupport();
        }

        /// <summary>
        /// CAD7 switches, JUR1 jurisdictions and the location engine for Take Call.
        /// The Take Call picker offers the JUR1 GEO_JUR jurisdictions, as the legacy
        /// screen does; the roster's jurisdictions are the fallback.
        /// </summary>
        private void LoadTakeCallSupport()
        {
            try
            {
                var support = new SqlTakeCallSupport(ConnectString, _writer);
                TakeCallSupport = support;

                var jurisdictions = support.Jurisdictions();
                if (jurisdictions.Count > 0) Jurisdictions = jurisdictions;

                if (support.Settings.DefaultJurisdiction.Length > 0) DefaultJurisdiction = support.Settings.DefaultJurisdiction;
                DefaultCity = support.Settings.DefaultCity;
                DefaultState = support.Settings.DefaultState;
                if (support.States.Count > 0) States = support.States.Select(s => s.Code).ToList();
            }
            catch (Exception ex)
            {
                LaLog.Error("TakeCall", "Take Call lookups not loaded.", ex);
            }
        }

        /// <summary>Take Call's lookups on the live tables.</summary>
        public ITakeCallSupport TakeCallSupport { get; private set; }

        /// <summary>Self Initiate / Traffic Stop / Activate Unit lookups on the live tables.</summary>
        public SelfInit.ISelfInitSupport SelfInitSupport { get; private set; }

        private void LoadSelfInitSupport()
        {
            try
            {
                SelfInitSupport = new SelfInit.SqlSelfInitSupport(ConnectString);
            }
            catch (Exception ex)
            {
                LaLog.Error("SelfInit", "Self Initiate lookups not loaded.", ex);
            }
        }

        /// <summary>
        /// SelfInit_Save / TraffStop_Save. The new event comes back as the board
        /// shows it after the refresh - on Active with the unit - or, closed on
        /// entry with a disposition, as a completed call that is on no monitor.
        /// </summary>
        public CadCall SelfInitiate(SelfInitDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            CCC1 code = null;
            if (draft.CallCode != null && int.TryParse(draft.CallCode.Code, out var codeNo))
            {
                _callCodesByCode.TryGetValue(codeNo, out code);
            }

            if (code == null) throw new CadWriteException("Call Code must be entered.");

            short.TryParse(draft.Origin?.Code, out var origin);

            var result = _writer.SelfInitiate(draft, code, origin);
            LastSelfInitRelease = result.Released;

            Refresh();

            var created = result.Event;
            var call = created.StackKey > 0 ? Calls.FirstOrDefault(c => c.EventKey == created.StackKey) : null;

            return call ?? new CadCall
            {
                EventKey = created.StackKey,
                EventType = created.EventType,
                EventNo1 = created.EventNo1,
                EventNo2 = created.EventNo2,
                EventNo = FormatEventNo(created.EventNo1, created.EventNo2),
                CallType = Description(code),
                Location = draft.Place?.Formatted ?? draft.Location,
                Queue = created.StackKey > 0 ? CallQueue.Active : CallQueue.Closed,
                IsCompleted = created.StackKey == 0
            };
        }

        /// <summary>The release the last Self Initiate had to make first; "" when the unit was free.</summary>
        public string LastSelfInitRelease { get; private set; } = string.Empty;

        public WriteReport ActivateUnit(UnitActivation unit)
        {
            var result = _writer.ActivateUnit(unit);
            if (result.Written) Refresh();

            return new WriteReport
            {
                Saved = result.Written,
                Failed = !result.Written,
                Message = result.Message
            };
        }

        /// <summary>
        /// DAT1_911_POS and the keyboard template for the session, when a DAT1 row
        /// exists for it. Without one the template comes from --kbrd / --optype, or
        /// DEFAULT / D.
        /// </summary>
        private void LoadSession()
        {
            try
            {
                _hasSessionRow = _writer.LoadSession();
                E911Position = CadSession.Current.E911Pos;
            }
            catch (Exception ex)
            {
                LaLog.Error("DAT1", "Session row not read.", ex);
            }
        }

        private bool _hasSessionRow;

        /// <summary>
        /// Reads KYB1-3 for the session. A missing or unreadable template leaves the
        /// menus blank and says why on the status line - it does not set LastError,
        /// because the board itself is fine and should not fall back to the demo.
        /// </summary>
        private void LoadKeymap()
        {
            try
            {
                Keymap = KeymapLoader.Load(ConnectString, CadSession.Current);
            }
            catch (Exception ex)
            {
                LaLog.Error("KYB2", "Keyboard template not read.", ex);
                Keymap = Keymap.Empty("Keyboard template could not be read: " + ex.Message);
            }

            if (!_hasSessionRow && string.IsNullOrEmpty(Keymap.Problem))
            {
                Keymap = new Keymap(Keymap.Standard, Keymap.Quick)
                {
                    TemplateId = Keymap.TemplateId,
                    OperatorType = Keymap.OperatorType,
                    Source = Keymap.Source + ", no DAT1 row for this session",
                    Problem = Keymap.Problem
                };
            }
        }

        /// <summary>The session's keyboard template, from KYB1-3.</summary>
        public Keymap Keymap { get; private set; } = Keymap.Empty(string.Empty);

        public string ConnectString { get; }

        /// <summary>Last failure reported by any of the readers, empty when healthy.</summary>
        public string LastError { get; private set; } = string.Empty;

        // --------------------------------------------------------- identity

        public string AgencyName { get; }
        public string OperatorId { get; set; } = "0";
        public int SessionId { get; set; } = 1;
        public int E911Position { get; set; } = 1;
        public string DefaultJurisdiction { get; set; } = string.Empty;
        public string DefaultCity { get; set; } = string.Empty;
        public string DefaultState { get; set; } = string.Empty;

        /// <summary>
        /// Agency types this console monitors, as the session globals gl_UnitTypeP/F/E/O
        /// carry them after logon. Defaults to all four until a logon sets them.
        /// </summary>
        public string MonitoredAgencyTypes { get; set; } = "PFEO";

        // ------------------------------------------------------------ state

        public ObservableCollection<CadCall> Calls { get; } = new ObservableCollection<CadCall>();
        public ObservableCollection<CadUnit> Units { get; } = new ObservableCollection<CadUnit>();

        public IReadOnlyList<CallCodeItem> CallCodes { get; private set; } = new List<CallCodeItem>();
        public IReadOnlyList<LookupItem> Origins { get; private set; } = new List<LookupItem>();
        public IReadOnlyList<LookupItem> Jurisdictions { get; private set; } = new List<LookupItem>();
        public IReadOnlyList<string> States { get; private set; } = new List<string>();
        public IReadOnlyList<string> ForceFlags { get; } = new[] { "Y", "N" };
        public IReadOnlyList<UnitStatusOption> UnitStatuses { get; private set; } = new List<UnitStatusOption>();

        // ------------------------------------------------------- reference

        private void LoadReferenceData()
        {
            // Active call codes only, the way the Take Call lookup filters them.
            var codes = _ccc1.GetList(
                where: "(CCC1_INACTIVE <> 'Y' OR CCC1_EFF_DATE > GETDATE()) AND CCC1_HIDE <> 'Y'",
                orderBy: "CCC1_DESC");
            Note(_ccc1);

            _callCodesByCode.Clear();
            foreach (var row in codes) _callCodesByCode[row.CCC1_CODE] = row;

            CallCodes = codes.Select(ToCallCodeItem).ToList();

            var statuses = _ust1.GetList(orderBy: "UST1_STATUS");
            Note(_ust1);
            _statusByCode.Clear();
            foreach (var row in statuses) _statusByCode[row.UST1_STATUS] = row;

            // UST1_N_STATUS is what each site has configured to follow a status, and
            // is what the Unit Status screen opens on.
            UnitStatuses = statuses
                .Select(r => new UnitStatusOption(
                    r.UST1_STATUS, r.UST1_ABRV, r.UST1_DESC, r.UST1_AVL_SW == 'A', r.UST1_N_STATUS))
                .Where(o => o.Abbreviation.Length > 0)
                .ToList();

            // Jurisdictions as actually in use on the roster, so the picker only
            // offers codes this console can dispatch to.
            Jurisdictions = _rst1
                .GetList(orderBy: "RST1_JUR")
                .Select(r => (r.RST1_JUR ?? string.Empty).Trim())
                .Where(j => j.Length > 0)
                .Distinct()
                .Select(j => new LookupItem(j, j))
                .ToList();
            Note(_rst1);

            if (string.IsNullOrWhiteSpace(DefaultJurisdiction) && Jurisdictions.Count > 0)
            {
                DefaultJurisdiction = Jurisdictions[0].Code;
            }

            Origins = BuildOrigins();
            States = BuildStates();

            LoadPlanType();
        }

        /// <summary>
        /// Which way police and support units are recommended. The legacy reads
        /// CAD7 on open and falls back to nothing if the row is missing; this
        /// falls back to 'G', the simpler of the two, so a site without a
        /// deployment plan still gets a recommendation off the call's own area.
        /// </summary>
        private void LoadPlanType()
        {
            var rows = _cad7.GetList(top: 1);
            Note(_cad7);

            var row = rows.FirstOrDefault();
            if (row == null || row.CAD7_PLAN_TYPE == '\0' || row.CAD7_PLAN_TYPE == ' ') return;

            _planType = row.CAD7_PLAN_TYPE;
        }

        private static CallCodeItem ToCallCodeItem(CCC1 row)
        {
            var agency = AgencyForCode(row);
            var item = new CallCodeItem(
                row.CCC1_CODE.ToString(),
                (row.CCC1_DESC ?? string.Empty).Trim(),
                Description(row),
                agency,
                BandFor(PrimaryPriority(row)),
                PrimaryPriority(row));

            item.PromptsAlert = row.CCC1_ALERT == 'Y';
            item.OffersAssist = row.CCC1_ASSGN_ASST == 'Y';

            item.PolicePriority = row.CCC1_PRTY_P;
            item.FirePriority = row.CCC1_PRTY_F;
            item.EmsPriority = row.CCC1_PRTY_E;
            item.OtherPriority = row.CCC1_PRTY_O;
            item.RestacksOnRelease = Restacks(row);

            return item;
        }

        /// <summary>
        /// What the monitors put in the Call Type column. The legacy lists all call
        /// dbo.la_fn_CCC1_GetDesc, which returns CCC1_DESC - the full description,
        /// "STRUCTURE FIRE", not the CCC1_ABRV short form "SIG 13".
        /// </summary>
        private static string Description(CCC1 row) => (row.CCC1_DESC ?? string.Empty).Trim();

        /// <summary>
        /// Whether releasing the last unit restacks the call. LwCad108 closes the
        /// call only when CCC1_RESTACK is 'N' and restacks it otherwise - a blank
        /// restacks - so the test is "not N", not "is Y".
        /// </summary>
        private static bool Restacks(CCC1 row) => char.ToUpperInvariant(row.CCC1_RESTACK) != 'N';

        private static Agency AgencyForCode(CCC1 row)
        {
            // The agency with the strongest (lowest non-zero) priority owns the code.
            if (row.CCC1_PRTY_P > 0) return Agency.Police;
            if (row.CCC1_PRTY_F > 0) return Agency.Fire;
            if (row.CCC1_PRTY_E > 0) return Agency.Ems;
            return Agency.Other;
        }

        private static short PrimaryPriority(CCC1 row)
        {
            var set = new[] { row.CCC1_PRTY_P, row.CCC1_PRTY_F, row.CCC1_PRTY_E, row.CCC1_PRTY_O }
                .Where(p => p > 0)
                .ToArray();

            return set.Length == 0 ? (short)5 : set.Min();
        }

        internal static CallPriority BandFor(int priority)
        {
            if (priority <= 0) return CallPriority.Medium;
            if (priority <= 3) return CallPriority.High;
            if (priority <= 6) return CallPriority.Medium;
            return CallPriority.Low;
        }

        /// <summary>
        /// Call origins are MLB1 system 5, library 2 - the ORIGIN_S / ORIGIN_L the
        /// legacy writes on every event - so the picker offers this site's codes and
        /// DSE1_ORIGIN_C gets a real one. The fixed list is only for a database with
        /// no such library.
        /// </summary>
        private IReadOnlyList<LookupItem> BuildOrigins()
        {
            var library = _mlb1.GetLibrary(5, 2);
            Note(_mlb1);

            if (library.Count == 0) return DefaultOrigins();

            return library
                .OrderBy(p => p.Key)
                .Select(p => new LookupItem(p.Key.ToString(), (p.Value ?? string.Empty).Trim()))
                .ToList();
        }

        private static IReadOnlyList<LookupItem> DefaultOrigins() => new List<LookupItem>
        {
            new LookupItem("10", "911 Line"),
            new LookupItem("20", "Business Line"),
            new LookupItem("30", "Walk In"),
            new LookupItem("40", "Radio"),
            new LookupItem("50", "Officer Initiated"),
            new LookupItem("60", "Alarm Company"),
            new LookupItem("70", "Mutual Aid"),
            new LookupItem("80", "Text To 911")
        };

        private static IReadOnlyList<string> BuildStates() =>
            new[] { "MA", "NH", "NY", "CT", "RI", "VT", "ME", "TX" };

        // --------------------------------------------------------- monitors

        /// <summary>Re-reads the call and unit monitors. Call this from the refresh timer.</summary>
        public void Refresh()
        {
            // Units first: the call rows read the assignment map the roster builds.
            RefreshUnits();
            RefreshCalls();
            RefreshAlertCount();
        }

        private void RefreshCalls()
        {
            var stack = _stk1.GetList(
                where: "STK1_STATUS IN ('S', 'A', 'D')",
                orderBy: "STK1_PRIORITY, STK1_RCV_DATE");
            Note(_stk1);

            var wanted = new List<CadCall>();

            foreach (var row in stack)
            {
                var call = Calls.FirstOrDefault(c => c.EventKey == row.STK1_S_KEY) ?? new CadCall();
                MapCall(call, row, loadDetail: !_detailLoaded.Contains(row.STK1_S_KEY));
                wanted.Add(call);
            }

            Reconcile(Calls, wanted);

            _detailLoaded.IntersectWith(wanted.Select(c => c.EventKey));
        }

        private void MapCall(CadCall call, STK1 row, bool loadDetail)
        {
            call.EventKey = row.STK1_S_KEY;
            call.EventType = row.STK1_EVENT_TYPE;
            call.EventNo1 = row.STK1_EVENT_NO_1;
            call.EventNo2 = row.STK1_EVENT_NO_2;
            call.EventNo = FormatEventNo(row.STK1_EVENT_NO_1, row.STK1_EVENT_NO_2);
            call.CallCode = row.STK1_CALL_CODE.ToString();
            call.Jurisdiction = (row.STK1_JUR ?? string.Empty).Trim();
            call.Area = (row.STK1_AREA ?? string.Empty).Trim();
            call.Location = (row.STK1_LOCATION ?? string.Empty).Trim();
            call.PriorityNumber = row.STK1_PRIORITY;
            call.Priority = BandFor(row.STK1_PRIORITY);
            call.PrimaryAgency = AgencyForEventType(row.STK1_EVENT_TYPE);
            call.ReceivedAt = row.STK1_RCV_DATE ?? DateTime.Now;
            call.Queue = QueueFor(row.STK1_STATUS);
            call.HasAlert = row.STK1_T_O_FLAG > 0 || row.STK1_T_T_FLAG > 0;

            call.CallType = _callCodesByCode.TryGetValue(row.STK1_CALL_CODE, out var code)
                ? Description(code)
                : call.CallCode;

            // What the assign screens show across the top of the call, and the rule
            // the release screen quotes: CCC1 decides both, per call code.
            call.PolicePriority = code != null ? code.CCC1_PRTY_P : (short)0;
            call.FirePriority = code != null ? code.CCC1_PRTY_F : (short)0;
            call.EmsPriority = code != null ? code.CCC1_PRTY_E : (short)0;
            call.OtherPriority = code != null ? code.CCC1_PRTY_O : (short)0;
            call.RestackOnRelease = code != null && Restacks(code);

            SyncAssignedUnits(call, row);

            // Additional Info, as LwCadSystem WinA.cs fills it per list: Active shows
            // STK1_UNITS - the unit list the assign and release programs keep on the
            // stack row - Delayed shows when the call activates, and Stacked shows
            // nothing (Easthampton's restacking-unit text is not in LW's STK1).
            switch (call.Queue)
            {
                case CallQueue.Active:
                    var units = (row.STK1_UNITS ?? string.Empty).Trim();
                    call.Info = units.Length > 0
                        ? units
                        : call.AssignedUnits.Count > 0
                            ? string.Join(" ", call.AssignedUnits.Select(u => u.UnitNo))
                            : "-";
                    break;

                case CallQueue.Delayed:
                    call.Info = row.STK1_ACT_DATE.HasValue
                        ? "Activate-" + row.STK1_ACT_DATE.Value.ToString("MM/dd HH:mm")
                        : "-";
                    break;

                default:
                    call.Info = "-";
                    break;
            }

            if (loadDetail && MapEventDetail(call, row))
            {
                _detailLoaded.Add(row.STK1_S_KEY);
            }
        }

        /// <summary>Units the roster shows assigned to this event.</summary>
        private void SyncAssignedUnits(CadCall call, STK1 row)
        {
            _unitsByEvent.TryGetValue(
                (row.STK1_EVENT_TYPE, row.STK1_EVENT_NO_1, row.STK1_EVENT_NO_2), out var units);
            units ??= new List<AssignedUnit>();

            if (call.AssignedUnits.Count == units.Count &&
                !call.AssignedUnits.Where((u, i) => u.UnitNo != units[i].UnitNo).Any())
            {
                return;
            }

            call.AssignedUnits.Clear();
            foreach (var unit in units) call.AssignedUnits.Add(unit);
        }

        /// <summary>Pulls the DSE1 detail the expanded row shows. False if the read failed.</summary>
        private bool MapEventDetail(CadCall call, STK1 row)
        {
            var key = new DSE1
            {
                DSE1_EVENT_TYPE = row.STK1_EVENT_TYPE,
                DSE1_EVENT_NO_1 = row.STK1_EVENT_NO_1,
                DSE1_EVENT_NO_2 = row.STK1_EVENT_NO_2
            };

            var detail = _dse1.GetRow(key);
            if (_dse1.RecordStatus != 0)
            {
                Note(_dse1);
                return false;
            }

            call.Notes = (detail.DSE1_REMARKS ?? string.Empty).Trim();
            call.ReportedBy = (detail.DSE1_OTHER_NAME ?? string.Empty).Trim();
            call.CaseNo = FormatCaseNo(detail.DSE1_CASE_NO_1_01, detail.DSE1_CASE_NO_2_01);

            // The overview shows the call code's description, not the code itself.
            call.CallCodeText = _callCodesByCode.TryGetValue(detail.DSE1_CALL_CODE, out var code)
                ? Description(code)
                : detail.DSE1_CALL_CODE.ToString();

            call.Premise = ResolvePremise(detail);

            // What the assign screens key their recommendation off.
            call.PoliceArea = (detail.DSE1_POLICE_1 ?? string.Empty).Trim();
            call.ResponseArea = (detail.DSE1_RESP_AREA ?? string.Empty).Trim();
            call.FireArea = (detail.DSE1_FIRE_1 ?? string.Empty).Trim();
            call.EmsArea = (detail.DSE1_EMS_1 ?? string.Empty).Trim();
            call.PremiseNo = detail.DSE1_PMM_NO;
            call.LocationJurisdiction = (detail.DSE1_LOC_JUR ?? string.Empty).Trim();
            call.CallCodeNumber = detail.DSE1_CALL_CODE;

            return true;
        }

        /// <summary>
        /// Case numbers are a year/sequence pair, formatted as the legacy overview
        /// formats them - four digits, dash, seven. Blank when none is assigned;
        /// the original printed 0000-0000000 there instead.
        /// </summary>
        private static string FormatCaseNo(short year, int sequence)
        {
            if (year == 0 && sequence == 0) return string.Empty;
            return year.ToString("0000") + "-" + sequence.ToString("0000000");
        }

        /// <summary>
        /// Premise name, following the legacy overview's three steps: the premise
        /// file name, then the geobase common name when the event has no premise,
        /// then the geobase subdivision name if both come back empty.
        /// </summary>
        private string ResolvePremise(DSE1 detail)
        {
            if (detail.DSE1_PMM_NO > 0)
            {
                var premise = _pmm1.GetRow(new PMM1 { PMM1_FOP = detail.DSE1_PMM_NO });
                if (_pmm1.RecordStatus == 0)
                {
                    var name = (premise.PMM1_NAME_L ?? string.Empty).Trim();
                    if (name.Length > 0) return name;
                }
            }

            if (detail.DSE1_GEOCODE_NO <= 0) return string.Empty;

            var geo = _geo1.GetRow(new GEO1 { GEO1_P_KEY = detail.DSE1_GEOCODE_NO });
            if (_geo1.RecordStatus != 0) return string.Empty;

            var common = (geo.GEO1_C_NAME ?? string.Empty).Trim();
            if (detail.DSE1_PMM_NO == 0 && common.Length > 0) return common;

            return (geo.GEO1_SUB_NM ?? string.Empty).Trim();
        }

        private void RefreshUnits()
        {
            var roster = _rst1.GetList(where: UnitVisibilityFilter());
            Note(_rst1);

            // Ordered here rather than in the ORDER BY, because the legacy orders
            // here too - see UnitSortOrder.
            roster.Sort(CompareForMonitor);

            var wanted = new List<CadUnit>();
            _unitsByEvent.Clear();

            foreach (var row in roster)
            {
                if (row.RST1_EVENT_NO_2 > 0)
                {
                    var key = (row.RST1_EVENT_TYPE, row.RST1_EVENT_NO_1, row.RST1_EVENT_NO_2);
                    if (!_unitsByEvent.TryGetValue(key, out var assigned))
                    {
                        assigned = new List<AssignedUnit>();
                        _unitsByEvent[key] = assigned;
                    }

                    assigned.Add(new AssignedUnit(
                        (row.RST1_UNIT_NO ?? string.Empty).Trim(),
                        AgencyForEventType(row.RST1_AGC_TYPE)));
                }

                var unitNo = (row.RST1_UNIT_NO ?? string.Empty).Trim();
                var unit = Units.FirstOrDefault(u => u.UnitNo == unitNo) ?? new CadUnit { UnitNo = unitNo };
                MapUnit(unit, row);
                wanted.Add(unit);
            }

            Reconcile(Units, wanted);
        }

        /// <summary>
        /// The monitor's row order: police first, then fire, then EMS, then
        /// everything else, and by unit number inside each. This is the LwCadSystem
        /// SortOrder column - '1' to '4' by agency type only. Easthampton also groups
        /// by RST1_AGENCY inside each type; Lowell does not.
        ///
        /// Sorted in memory rather than by the database, and that is deliberate.
        /// The legacy builds the same expression into its SELECT but then re-sorts
        /// the grid with <c>DataView.Sort</c>, which is a .NET culture-aware
        /// comparison - and the two disagree on punctuation. This database collates
        /// SQL_Latin1_General_CP1_CI_AS, which weighs '-' as a character before the
        /// digits, so SQL puts P-DAN at the head of the P units; .NET treats the
        /// hyphen as ignorable, so the legacy puts it after P420. Sorting the same
        /// way the legacy actually sorts is what keeps the two monitors in step.
        /// </summary>
        private static int CompareForMonitor(RST1 left, RST1 right)
        {
            var byGroup = MonitorCompare(MonitorSortKey(left), MonitorSortKey(right));

            return byGroup != 0
                ? byGroup
                : MonitorCompare((left.RST1_UNIT_NO ?? string.Empty).Trim(),
                                 (right.RST1_UNIT_NO ?? string.Empty).Trim());
        }

        /// <summary>
        /// Compares two unit numbers the way the legacy grid compares them.
        ///
        /// IgnoreSymbols is the point. The legacy runs on .NET Framework, whose
        /// string comparison goes through Windows NLS, and NLS treats punctuation
        /// as ignorable - "P-DAN" collates as "PDAN", which lands after "P420"
        /// because digits sort before letters. .NET 8 collates through ICU, which
        /// weighs the hyphen as a character and puts P-DAN at the head of the P
        /// units instead. IgnoreSymbols asks ICU for the NLS answer rather than
        /// switching the whole process back to NLS for one list.
        /// </summary>
        private static int MonitorCompare(string left, string right) =>
            CultureInfo.CurrentCulture.CompareInfo.Compare(
                left ?? string.Empty, right ?? string.Empty, CompareOptions.IgnoreSymbols);

        private static string MonitorSortKey(RST1 row)
        {
            switch (char.ToUpperInvariant(row.RST1_AGC_TYPE))
            {
                case 'P': return "1";
                case 'F': return "2";
                case 'E': return "3";
                default: return "4";
            }
        }

        /// <summary>
        /// The unit monitor's visibility rule, as LwCadSystem WinA.cs loads it: a
        /// unit is shown unless it is flagged RST1_DISPLAY = 'Y' and is not on a
        /// call, and only for the agency types this console monitors.
        ///
        /// Easthampton's rule is wider - it always shows recommended units and keeps
        /// a display-flagged unit that is off a call but not in service. Lowell has
        /// neither clause, so a display-flagged unit given a location off a call
        /// leaves the LW monitor; that is the Lowell behaviour and is kept.
        /// </summary>
        private string UnitVisibilityFilter()
        {
            var agencies = (MonitoredAgencyTypes ?? string.Empty)
                .ToUpperInvariant()
                .Where(c => "PFEO".IndexOf(c) >= 0)
                .Select(c => "'" + c + "'")
                .ToArray();

            var agencyClause = agencies.Length == 0
                ? "1 = 0"
                : "RST1_AGC_TYPE IN (" + string.Join(", ", agencies) + ")";

            return
                "(RST1_DISPLAY <> 'Y' OR (RST1_DISPLAY = 'Y' AND RST1_EVENT_NO_2 > 0)) AND " +
                agencyClause;
        }

        private void MapUnit(CadUnit unit, RST1 row)
        {
            // Column sources follow the legacy unit monitor SELECT in WinA.cs.
            unit.Station = (row.RST1_C_STATION ?? string.Empty).Trim();
            unit.Agency = AgencyForEventType(row.RST1_AGC_TYPE);
            // The assign screens narrow their recommendation to the call's jurisdiction.
            unit.Jurisdiction = (row.RST1_JUR ?? string.Empty).Trim();
            unit.Location = (row.RST1_LAST_LOC ?? string.Empty).Trim();
            unit.Area = BuildArea(row);
            unit.AssignedCallKey = row.RST1_DISP_NO_2;
            unit.DispNo1 = row.RST1_DISP_NO_1;
            unit.DispNo2 = row.RST1_DISP_NO_2;
            unit.VehicleId = (row.RST1_VEH_ID ?? string.Empty).Trim();
            unit.IsRecommended = char.ToUpperInvariant(row.RST1_RECOMMEND) == 'Y';

            unit.UnitType = Describe(row.RST1_U_TYPE_S, row.RST1_U_TYPE_L, row.RST1_U_TYPE_C);
            unit.CallType = BuildUnitCallType(row);

            if (_statusByCode.TryGetValue(row.RST1_STATUS, out var status))
            {
                unit.Status = (status.UST1_ABRV ?? string.Empty).Trim();
            }
            else
            {
                unit.Status = row.RST1_STATUS.ToString();
            }

            // The legacy monitor counts a unit available when its status abbreviation
            // is AVL, and overdue from the RST1 timeout flag - not from the clock.
            unit.Availability = unit.Status == "AVL"
                ? UnitAvailability.Available
                : UnitAvailability.Assigned;

            unit.IsOverdue = row.RST1_T_O_FLAG == 1;

            unit.HasStatusTime = row.RST1_LAST_HR > 0 || row.RST1_LAST_MN > 0;
            unit.StatusSince = AtTimeToday(row.RST1_LAST_HR, row.RST1_LAST_MN);
            unit.TimeoutMinutes = row.RST1_T_O_MN;
        }

        /// <summary>Response areas 1-5, joined the way the monitor joins them.</summary>
        private static string BuildArea(RST1 row)
        {
            var areas = new[]
                {
                    row.RST1_R_AREA_1, row.RST1_R_AREA_2, row.RST1_R_AREA_3,
                    row.RST1_R_AREA_4, row.RST1_R_AREA_5
                }
                .Select(a => (a ?? string.Empty).Trim())
                .ToArray();

            int last = Array.FindLastIndex(areas, a => a.Length > 0);
            return last < 0 ? string.Empty : string.Join("/", areas.Take(last + 1));
        }

        /// <summary>
        /// The monitor's "Call" column: a transaction type while the unit is not on a
        /// dispatch, otherwise the call code description.
        /// </summary>
        private string BuildUnitCallType(RST1 row)
        {
            if (row.RST1_T_TYPE_C > 0 && row.RST1_DISP_NO_2 == 0)
            {
                return Describe(row.RST1_T_TYPE_S, row.RST1_T_TYPE_L, row.RST1_T_TYPE_C);
            }

            if (row.RST1_CALL_CODE > 0 && _callCodesByCode.TryGetValue(row.RST1_CALL_CODE, out var code))
            {
                return Description(code);
            }

            return string.Empty;
        }

        /// <summary>Resolves an MLB1 code, caching the library it belongs to.</summary>
        private string Describe(short sysNo, short libNo, short code)
        {
            if (code == 0) return string.Empty;

            var key = (sysNo, libNo);
            if (!_codeLibraries.TryGetValue(key, out var library))
            {
                library = _mlb1.GetLibrary(sysNo, libNo);
                Note(_mlb1);
                _codeLibraries[key] = library;
            }

            return library.TryGetValue(code, out var description) ? description : string.Empty;
        }

        private static DateTime AtTimeToday(short hour, short minute)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return DateTime.Now;

            var stamp = DateTime.Today.AddHours(hour).AddMinutes(minute);

            // A stamp later than now belongs to yesterday's shift.
            return stamp > DateTime.Now ? stamp.AddDays(-1) : stamp;
        }

        private static CallQueue QueueFor(char status)
        {
            switch (char.ToUpperInvariant(status))
            {
                case 'A': return CallQueue.Active;
                case 'D': return CallQueue.Delayed;
                case 'S': return CallQueue.Stacked;
                default: return CallQueue.Closed;
            }
        }

        private static Agency AgencyForEventType(char eventType)
        {
            switch (char.ToUpperInvariant(eventType))
            {
                case 'F': return Agency.Fire;
                case 'E': return Agency.Ems;
                case 'O': return Agency.Other;
                default: return Agency.Police;
            }
        }

        private static string FormatEventNo(short no1, int no2) =>
            no1.ToString("0000") + "-" + no2.ToString("0000000");

        /// <summary>
        /// Brings the bound collection in line with the query result in place,
        /// so rows that survived a refresh keep their container - and therefore
        /// the dispatcher's selection and expander state.
        /// </summary>
        private static void Reconcile<T>(ObservableCollection<T> target, List<T> wanted) where T : class
        {
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(target[i])) target.RemoveAt(i);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                int current = target.IndexOf(wanted[i]);
                if (current < 0) target.Insert(i, wanted[i]);
                else if (current != i) target.Move(current, i);
            }
        }

        // ------------------------------------------------------- take call

        /// <summary>
        /// The police event number the call will most likely be given. Lowell
        /// assigns event numbers when the call is saved, per agency, from
        /// DSC1_EVENT_NO_P/F/E/O (TakeCall_AssignEKey) - reserving one on open
        /// would burn a number every time a Take Call is cancelled. So this reads
        /// the counter without moving it; Dispatch reports the number actually given.
        /// </summary>
        public string ReserveEventNo()
        {
            var year = (short)DateTime.Now.Year;
            var rows = _dsc1.GetList(where: "DSC1_YEAR = @year", parameters: new { year });
            Note(_dsc1);

            var next = rows.Count > 0 ? rows[0].DSC1_EVENT_NO_P + 1 : 1;
            return FormatEventNo(year, next);
        }

        /// <summary>
        /// Saves the call - DSC1, a DSE1 per agency the call code tasks, DSN1, DSV1
        /// and STK1, in one transaction (CadWriteBack.Dispatch) - then re-reads the
        /// monitors and returns the new event, the highest-priority one when the
        /// call created several. Throws when the call could not be saved.
        /// </summary>
        public CadCall Dispatch(NewCallDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            CCC1 code = null;
            if (draft.CallCode != null && int.TryParse(draft.CallCode.Code, out var codeNo))
            {
                _callCodesByCode.TryGetValue(codeNo, out code);
            }

            if (code == null) throw new CadWriteException("Call code must be specified.");

            short.TryParse(draft.Origin?.Code, out var origin);

            var created = _writer.Dispatch(draft, code, origin, draft.Place != null ? draft.Place.PremiseNo : PremiseNumber(draft.Premise));

            Refresh();

            // TakeCall_SaveDSE1P/F/E/O: the lowest priority number wins; on a tie Fire
            // beats Police, and both beat EMS and Other.
            var primary = created
                .OrderBy(e => e.Priority)
                .ThenBy(e => "FPEO".IndexOf(e.EventType))
                .First();

            LastDispatchedEvents = created
                .Select(e => e.EventType + " " + FormatEventNo(e.EventNo1, e.EventNo2))
                .ToList();

            var call = primary.StackKey > 0 ? Calls.FirstOrDefault(c => c.EventKey == primary.StackKey) : null;

            // A call closed on entry (a disposition) has no stack row to find.
            return call ?? new CadCall
            {
                EventKey = primary.StackKey,
                EventType = primary.EventType,
                EventNo1 = primary.EventNo1,
                EventNo2 = primary.EventNo2,
                EventNo = FormatEventNo(primary.EventNo1, primary.EventNo2),
                CallType = Description(code),
                Location = draft.Place?.Formatted ?? draft.Location,
                Queue = CallQueue.Stacked,
                IsCompleted = primary.StackKey == 0
            };
        }

        /// <summary>
        /// Every event the last dispatch created - "P 2026-0001234", "F 2026-0000456" -
        /// since a call code tasking several agencies creates one per agency.
        /// </summary>
        public IReadOnlyList<string> LastDispatchedEvents { get; private set; } = new List<string>();

        /// <summary>The premise file entry whose name was typed, 0 when none matches.</summary>
        private int PremiseNumber(string premise)
        {
            var name = (premise ?? string.Empty).Trim();
            if (name.Length == 0) return 0;

            var rows = _pmm1.GetList(where: "RTRIM(PMM1_NAME_L) = @name", parameters: new { name }, top: 1);
            return rows.Count > 0 ? rows[0].PMM1_FOP : 0;
        }


        /// <summary>
        /// What one agency's assign screen offers for a call, as LwCad131 / 132 /
        /// 133 / 134 work it out - a deployment plan for police and support, a run
        /// card for fire and EMS. See CadRecommendation.
        ///
        /// When the tables have nothing to say - no active plan, no run card, no
        /// RST2 rows - the roster fallback in CadAssignment takes over and the
        /// panel says the list was widened, rather than the screen coming up empty.
        /// </summary>
        public AssignCandidateSet GetAssignCandidates(CadCall call, Agency agency)
        {
            if (call == null) return AssignCandidateSet.Empty;

            List<CadRecommendation.Recommended> recommended;
            string basis;

            switch (agency)
            {
                case Agency.Fire:
                    // LwCad132 keys RUN1 on the fire box, DSE1_FIRE_1 - overridden by
                    // the premise's PMM1_FIRE - and falls back to DSE1_RESP_AREA.
                    var box = AreaFor(call, Agency.Fire);
                    recommended = CadRecommendation.ForFire(box, call.CallCodeNumber, _run1, NoteTable);
                    basis = "Fire run card for box " + Shown(box) + ".";
                    break;

                case Agency.Ems:
                    // LwCad133 keys RUN5 on DSE1_EMS_1, overridden by PMM1_EMS.
                    var emsArea = AreaFor(call, Agency.Ems);
                    recommended = CadRecommendation.ForEms(emsArea, call.CallCodeNumber, _run5, NoteTable);
                    basis = "EMS run card for area " + Shown(emsArea) + ".";
                    break;

                default:
                    var jur = call.LocationJurisdiction.Length > 0
                        ? call.LocationJurisdiction
                        : call.Jurisdiction;

                    recommended = CadRecommendation.ForDeployment(
                        _planType, jur, call.PoliceArea, _bmp2, _bmp1, _rst2, NoteTable);

                    basis = char.ToUpperInvariant(_planType) == 'D'
                        ? "Deployment plan for area " + Shown(call.PoliceArea) + " in " + Shown(jur) + "."
                        : "Report area " + Shown(call.PoliceArea) + " in " + Shown(jur) + ".";
                    break;
            }

            if (recommended.Count == 0)
            {
                // Nothing recommended. Fall back to the roster rather than an empty
                // screen, and let CadAssignment label it as widened.
                return CadAssignment.BuildCandidates(call, agency, Units);
            }

            return CadAssignment.BuildFromRecommendation(
                call, agency, RosterFor(recommended.Select(r => r.UnitNo)), recommended, basis);
        }

        /// <summary>
        /// The units a recommendation named, resolved against RST1 rather than
        /// against the Unit Monitor.
        ///
        /// The monitor is a filtered view - on this database 26 of 513 roster rows
        /// pass it - and a run card names apparatus that is sitting in quarters,
        /// which is exactly what that filter hides. The legacy screens re-read RST1
        /// per recommended unit for the same reason; this reads them in one query
        /// instead of one round trip each.
        /// </summary>
        private List<CadUnit> RosterFor(IEnumerable<string> unitNos)
        {
            var roster = new List<CadUnit>(Units);

            var known = new HashSet<string>(Units.Select(u => u.UnitNo), StringComparer.OrdinalIgnoreCase);
            var wanted = unitNos
                .Select(n => (n ?? string.Empty).Trim())
                .Where(n => n.Length > 0 && !known.Contains(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (wanted.Count == 0) return roster;

            var list = string.Join(", ", wanted.Select(n => "'" + n.Replace("'", "''") + "'"));
            var rows = _rst1.GetList(where: "RTRIM(RST1_UNIT_NO) IN (" + list + ")");
            Note(_rst1);

            foreach (var row in rows)
            {
                var unit = new CadUnit { UnitNo = (row.RST1_UNIT_NO ?? string.Empty).Trim() };
                MapUnit(unit, row);
                roster.Add(unit);
            }

            return roster;
        }

        /// <summary>
        /// The area a fire or EMS run card is read by. The premise wins: when the
        /// event has a premise whose PMM1_FIRE / PMM1_EMS differs from the event's
        /// own, LwCad132 / LwCad133 correct the event to it (DSE1_FIRE_1 /
        /// DSE1_EMS_1) before reading the card, and so does this. EMS's override is
        /// Lowell's; Easthampton comments it out.
        /// </summary>
        private string AreaFor(CadCall call, Agency agency)
        {
            var fire = agency == Agency.Fire;
            var area = fire ? call.FireArea : call.EmsArea;

            if (call.PremiseNo > 0)
            {
                var premise = _pmm1.GetRow(new PMM1 { PMM1_FOP = call.PremiseNo });
                if (_pmm1.RecordStatus == 0)
                {
                    var fromPremise = ((fire ? premise.PMM1_FIRE : premise.PMM1_EMS) ?? string.Empty).Trim();

                    if (fromPremise.Length > 0 &&
                        !string.Equals(fromPremise, area, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            _writer.SetEventArea(call.EventType, call.EventNo1, call.EventNo2, fire, fromPremise);
                        }
                        catch (Exception ex)
                        {
                            LaLog.Error("DSE1", "Premise area not written to the event.", ex);
                        }

                        area = fromPremise;
                        if (fire) call.FireArea = area;
                        else call.EmsArea = area;
                    }
                }
            }

            if (fire && string.IsNullOrWhiteSpace(area)) area = call.ResponseArea;
            return area;
        }

        private static string Shown(string value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        /// <summary>Lets CadRecommendation report a reader's failure through Note.</summary>
        private void NoteTable(object table)
        {
            switch (table)
            {
                case LaTable<BMP1> t: Note(t); break;
                case LaTable<BMP2> t: Note(t); break;
                case LaTable<RST2> t: Note(t); break;
                case LaTable<RUN1> t: Note(t); break;
                case LaTable<RUN5> t: Note(t); break;
            }
        }

        /// <summary>
        /// Puts the picked units on the call, writing DSC1, DSU1, RST1, STK1 and DSE1
        /// per unit the way LwCad131-134 do (CadWriteBack.Assign), then re-reads the
        /// monitors so the event re-files from what the tables now hold.
        /// </summary>
        public WriteReport AssignUnits(UnitAssignment assignment)
        {
            var report = new WriteReport();
            if (assignment?.Call == null || assignment.Units.Count == 0)
            {
                report.Failed = true;
                report.Message = "Nothing to assign.";
                return report;
            }

            var results = _writer.Assign(assignment.Call.EventKey,
                                         assignment.Units.Select(u => u.UnitNo),
                                         assignment.Action,
                                         assignment.TimeoutMinutes);

            foreach (var refused in results.Where(r => !r.Written)) report.RefusedUnits.Add(refused.UnitNo);

            report.Saved = results.Any(r => r.Written);
            report.Failed = !report.Saved;
            report.Message = report.Saved
                ? "Saved to the CAD tables." + Refusals(results)
                : string.Join(" ", results.Select(r => r.Message));

            RefreshAfterWrite(report);
            return report;
        }

        private static string Refusals(IEnumerable<CadWriteBack.UnitResult> results)
        {
            var refused = results.Where(r => !r.Written).Select(r => r.Message).ToList();
            return refused.Count == 0 ? string.Empty : " Not assigned: " + string.Join(" ", refused);
        }

        /// <summary>
        /// Unit Status, Location Change and Release, written the way LwCad135 and
        /// LwCad108 write them (CadWriteBack) - RST1, DSU1, DST1, and for a release
        /// the call's STK1 and DSE1 - then the monitors are re-read.
        /// </summary>
        public WriteReport ApplyUnitChange(UnitChange change)
        {
            if (change?.Unit == null) return new WriteReport { Failed = true, Message = "No unit was given." };

            CadWriteBack.UnitResult result;

            try
            {
                switch (change.Function)
                {
                    case UnitFunction.Location:
                        result = _writer.ChangeLocation(change.Unit.UnitNo, change.Location,
                                                        change.TimeoutMinutes, change.Remarks);
                        break;

                    case UnitFunction.Release:
                        result = _writer.Release(change.Unit.UnitNo);
                        break;

                    default:
                        result = _writer.ChangeStatus(change.Unit.UnitNo, change.Status, change.Location,
                                                      change.TimeoutMinutes, change.Remarks);
                        break;
                }
            }
            catch (CadWriteException ex)
            {
                return new WriteReport { Failed = true, Message = ex.Message };
            }

            var report = new WriteReport
            {
                Saved = result.Written,
                Failed = !result.Written,
                Message = result.Message
            };

            RefreshAfterWrite(report);
            return report;
        }

        /// <summary>
        /// Re-reads the board after a write. The write has already committed, so a
        /// failed re-read is reported alongside it rather than instead of it - the
        /// next refresh cycle catches the monitors up.
        /// </summary>
        private void RefreshAfterWrite(WriteReport report)
        {
            if (!report.Saved) return;

            try
            {
                Refresh();
            }
            catch (Exception ex)
            {
                report.Message += " The monitors could not be re-read: " + ex.Message;
            }
        }

        public const string LocationChangeLabel = "LOCATION CHANGE";

        // ------------------------------------------------------------ misc

        private void Note<T>(LaTable<T> table) where T : class, new()
        {
            if (table.RecordStatus == 1 && !string.IsNullOrEmpty(table.ErrorMsg))
            {
                LastError = table.ErrorMsg;
            }
        }
    }
}
