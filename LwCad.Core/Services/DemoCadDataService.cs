using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.TakeCall;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// In-memory CAD back end seeded with the sample board shown in the Figma
    /// screens, so the redesigned shell runs without the production database.
    /// </summary>
    public partial class DemoCadDataService : ICadDataService
    {
        private int _eventSequence = 4424242;
        private int _caseSequence = 55334565;
        private long _keySequence = 1;

        public string AgencyName => "LARIMORE ASSOCIATES";
        public string OperatorId => "443221";
        public int SessionId => 1;
        public int E911Position => 1;
        public string DefaultJurisdiction => "LW";
        public string DefaultCity => "Austin";
        public string DefaultState => "TX";

        public ObservableCollection<CadCall> Calls { get; } = new ObservableCollection<CadCall>();
        public ObservableCollection<CadUnit> Units { get; } = new ObservableCollection<CadUnit>();

        public IReadOnlyList<CallCodeItem> CallCodes { get; }
        public IReadOnlyList<LookupItem> Origins { get; }
        public IReadOnlyList<LookupItem> Jurisdictions { get; }
        public IReadOnlyList<string> States { get; }
        public IReadOnlyList<string> ForceFlags { get; }
        public IReadOnlyList<UnitStatusOption> UnitStatuses { get; }
        public Keymap Keymap { get; } = BuildKeymap();

        private ITakeCallSupport _takeCallSupport;

        /// <summary>The demo geobase and canned indicator answers - see DemoTakeCallSupport.</summary>
        public ITakeCallSupport TakeCallSupport =>
            _takeCallSupport ??= new DemoTakeCallSupport(() => Calls, DefaultCity, DefaultState);

        private SelfInit.ISelfInitSupport _selfInitSupport;

        /// <summary>The Unit Monitor as the roster, and a two-unit UNT1 - see DemoSelfInitSupport.</summary>
        public SelfInit.ISelfInitSupport SelfInitSupport =>
            _selfInitSupport ??= new SelfInit.DemoSelfInitSupport(() => Units, () => Calls);

        /// <summary>
        /// Self Initiate / Traffic Stop on the board: the unit comes off any call it
        /// is on, the event is created for the unit's agency and the unit is put on
        /// it en route or arrived - the event goes straight to Active. A disposition
        /// closes it on entry instead.
        /// </summary>
        public CadCall SelfInitiate(SelfInitDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            var unit = Units.FirstOrDefault(u => string.Equals(u.UnitNo, (draft.UnitNo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            if (unit == null) throw new CadWriteException("Unit " + draft.UnitNo + " is not on the roster (RST1).");
            if (draft.Mode == SelfInitMode.TrafficStop && unit.Agency != Agency.Police)
            {
                throw new CadWriteException("Traffic Stop is a function reserved for police.");
            }

            if (unit.AssignedCallKey != 0) ReleaseFromCall(unit);

            var p = unit.Agency == Agency.Police ? draft.Priority : (short)0;
            var f = unit.Agency == Agency.Fire ? draft.Priority : (short)0;
            var e = unit.Agency == Agency.Ems ? draft.Priority : (short)0;
            var o = unit.Agency == Agency.Other ? draft.Priority : (short)0;

            var call = Dispatch(new NewCallDraft
            {
                CallCode = draft.CallCode,
                PriorityPolice = p,
                PriorityFire = f,
                PriorityEms = e,
                PriorityOther = o,
                Priority = draft.Priority <= 3 ? CallPriority.High : draft.Priority <= 6 ? CallPriority.Medium : CallPriority.Low,
                Place = draft.Place,
                Location = draft.Location,
                Jurisdiction = draft.Jurisdiction,
                PoliceCode = draft.PoliceCode,
                FireCode = draft.FireCode,
                EmsCode = draft.EmsCode,
                SupportCode = draft.SupportCode,
                TowCode = draft.TowCode,
                Origin = draft.Origin,
                Disposition = draft.Disposition,
                Narrative = draft.Narrative,
                Vehicle = draft.Vehicle,
                ReceivedAt = draft.ReceivedAt == default ? DateTime.Now : draft.ReceivedAt
            });

            if (call.IsCompleted) return call;

            var assignment = new UnitAssignment(call, unit.Agency, AssignMode.Call)
            {
                Action = draft.Arrived ? DispatchAction.Arrived : DispatchAction.EnRoute
            };
            assignment.Units.Add(unit);
            CadAssignment.Apply(assignment, UnitStatuses, Calls);
            return call;
        }

        /// <summary>Activate Unit: the unit joins the Unit Monitor, in service.</summary>
        public WriteReport ActivateUnit(UnitActivation activation)
        {
            if (activation == null || string.IsNullOrWhiteSpace(activation.UnitNo))
            {
                return new WriteReport { Failed = true, Message = "A Unit/Apparatus must be entered." };
            }

            var unitNo = activation.UnitNo.Trim().ToUpperInvariant();
            if (Units.Any(u => string.Equals(u.UnitNo, unitNo, StringComparison.OrdinalIgnoreCase)))
            {
                return new WriteReport { Failed = true, Message = "Unit/Apparatus has already been logged onto CAD." };
            }

            var agency = activation.AgencyType == 'F' ? Agency.Fire : activation.AgencyType == 'E' ? Agency.Ems
                       : activation.AgencyType == 'O' ? Agency.Other : Agency.Police;
            var type = SelfInitSupport.UnitTypes.FirstOrDefault(t => t.Code == activation.UnitType.ToString())?.Description ?? string.Empty;

            (SelfInitSupport as SelfInit.DemoSelfInitSupport)?.RememberCrew(unitNo, activation.Employee1);
            AddUnit(unitNo, activation.Station, type, agency, string.Empty,
                    string.Join("/", activation.Areas ?? Array.Empty<string>()), string.Empty, 0,
                    UnitAvailability.Available, jurisdiction: activation.Jurisdiction);

            return WriteReport.BoardOnly("Unit " + unitNo + " activated and in service.");
        }

        public DemoCadDataService()
        {
            CallCodes = BuildCallCodes();
            Origins = BuildOrigins();
            Jurisdictions = BuildJurisdictions();
            States = new[] { "TX", "MA", "NH", "NY", "CT", "RI", "VT", "ME" };
            ForceFlags = new[] { "Y", "N" };
            UnitStatuses = BuildUnitStatuses();

            // Units first: the seeded calls link their assigned units back to the roster.
            SeedUnits();
            SeedCalls();
            SeedAlerts();
        }


        /// <summary>What one agency's assign screen offers for a call.</summary>
        public AssignCandidateSet GetAssignCandidates(CadCall call, Agency agency) =>
            CadAssignment.BuildCandidates(call, agency, Units);

        /// <summary>Puts the picked units on the call.</summary>
        public WriteReport AssignUnits(UnitAssignment assignment)
        {
            CadAssignment.Apply(assignment, UnitStatuses, Calls);
            return WriteReport.BoardOnly(string.Empty);
        }

        /// <summary>
        /// Applies a unit function to the board. The three functions land on the
        /// same fields the legacy LwCad135 screen writes - status, last location and
        /// the status timer - and Release also takes the unit off its call.
        /// </summary>
        public WriteReport ApplyUnitChange(UnitChange change)
        {
            if (change?.Unit == null) return new WriteReport { Failed = true, Message = "No unit was given." };

            var unit = change.Unit;
            unit.StatusSince = DateTime.Now;
            unit.HasStatusTime = true;
            unit.IsOverdue = false;

            switch (change.Function)
            {
                case UnitFunction.Status:
                    if (change.Status != null)
                    {
                        unit.Status = change.Status.Abbreviation;
                        unit.Availability = change.Status.IsAvailable
                            ? UnitAvailability.Available
                            : UnitAvailability.Assigned;
                    }

                    if (!string.IsNullOrWhiteSpace(change.Location)) unit.Location = change.Location.Trim();
                    unit.TimeoutMinutes = change.TimeoutMinutes;
                    break;

                case UnitFunction.Location:
                    // A location off a call: the unit keeps its status, takes the new
                    // location, and the transaction reads LOCATION CHANGE on the monitor.
                    unit.Location = (change.Location ?? string.Empty).Trim();
                    unit.CallType = LocationChangeLabel;
                    unit.TimeoutMinutes = change.TimeoutMinutes;
                    break;

                case UnitFunction.Release:
                    ReleaseFromCall(unit);
                    break;
            }

            unit.HasPendingChange = true;
            return WriteReport.BoardOnly(string.Empty);
        }

        /// <summary>
        /// Takes a unit off its call and returns it to service.
        ///
        /// What happens to the call is the rule CCC1 carries, not a blanket one:
        /// while other units are still on it the call is untouched, and it is only
        /// the last unit coming off that decides the event. Then CCC1_RESTACK says
        /// whether the call is significant enough to go back on the stack for
        /// another unit, or whether releasing the last unit closes it.
        /// </summary>
        private void ReleaseFromCall(CadUnit unit)
        {
            foreach (var call in Calls)
            {
                var assigned = call.AssignedUnits.FirstOrDefault(u => u.UnitNo == unit.UnitNo);
                if (assigned == null) continue;

                call.AssignedUnits.Remove(assigned);
                CadAssignment.RefreshInfo(call);

                if (call.AssignedUnits.Count > 0) continue;

                call.Queue = call.RestackOnRelease ? CallQueue.Stacked : CallQueue.Closed;
                call.Info = "-";   // LW Stacked shows no restacking unit
            }

            unit.AssignedCallKey = 0;
            unit.CallType = string.Empty;
            unit.Availability = UnitAvailability.Available;
            unit.TimeoutMinutes = 0;

            var available = UnitStatuses.FirstOrDefault(s => s.IsAvailable);
            unit.Status = available != null ? available.Abbreviation : "AVL";
        }

        /// <summary>What the monitor's Call column shows for a location given off a call.</summary>
        public const string LocationChangeLabel = "LOCATION CHANGE";

        /// <summary>
        /// The statuses, and what normally follows each - UST1_N_STATUS on the live
        /// table. The chain is the legacy LwCad135 transition set read in order:
        /// dispatched, en route, arrived, close scene, complete, back in service.
        ///
        /// Available has no next. A unit sitting in service is not part-way through
        /// anything, so the screen opens on its current status and the operator picks.
        /// </summary>
        private static IReadOnlyList<UnitStatusOption> BuildUnitStatuses() => new List<UnitStatusOption>
        {
            new UnitStatusOption(1, "AVL", "Available", true, nextStatusCode: 0),
            new UnitStatusOption(2, "DSP", "Dispatched", false, nextStatusCode: 3),
            new UnitStatusOption(3, "ENR", "En Route", false, nextStatusCode: 4),
            new UnitStatusOption(4, "ARV", "Arrived", false, nextStatusCode: 5),
            new UnitStatusOption(5, "C/S", "Close Scene", false, nextStatusCode: 6),
            new UnitStatusOption(6, "CMP", "Complete", true, nextStatusCode: 1),
            new UnitStatusOption(20, "O/S", "Out Of Service", false, nextStatusCode: 1)
        };

        /// <summary>The demo board is already in memory; nothing to re-read.</summary>
        public void Refresh()
        {
        }

        public string ReserveEventNo()
        {
            _eventSequence++;
            return "3222-" + _eventSequence.ToString("0000000");
        }

        public CadCall Dispatch(NewCallDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            var code = draft.CallCode;

            // The event the board shows is the highest-priority one the call creates -
            // lowest number, Fire winning a tie (TakeCall_SaveDSE1P/F/E/O).
            var tasked = new[] { (Agency.Fire, draft.PriorityFire), (Agency.Police, draft.PriorityPolice),
                                 (Agency.Ems, draft.PriorityEms), (Agency.Other, draft.PriorityOther) }
                .Where(t => t.Item2 > 0).OrderBy(t => t.Item2).ToList();
            var primary = tasked.Count > 0 ? tasked[0] : (code?.Agency ?? Agency.Police, (short)(code?.DefaultPriorityNumber ?? 5));

            var area = primary.Item1 == Agency.Fire ? draft.FireCode
                : primary.Item1 == Agency.Ems ? draft.EmsCode
                : primary.Item1 == Agency.Other ? draft.SupportCode
                : draft.PoliceCode;

            var call = new CadCall
            {
                EventKey = _keySequence++,
                EventNo = string.IsNullOrWhiteSpace(draft.EventNo) ? ReserveEventNo() : draft.EventNo,
                CallType = code != null ? code.CallType : "Other",
                CallCode = code != null ? code.Code : string.Empty,
                Jurisdiction = draft.Place?.Jurisdiction ?? draft.Jurisdiction,
                Area = string.IsNullOrWhiteSpace(area) ? FirstCode(draft.PoliceCode, draft.FireCode, draft.EmsCode, draft.SupportCode) : area.Trim(),
                Location = draft.Place?.Formatted ?? draft.Location,
                Premise = draft.Place != null ? draft.Place.PremiseName : draft.Premise,
                PremiseNo = draft.Place?.PremiseNo ?? 0,
                Origin = draft.Origin != null ? draft.Origin.Display : string.Empty,
                Priority = draft.Priority,
                PriorityNumber = primary.Item2,
                PrimaryAgency = primary.Item1,
                ReportedBy = draft.ReportedBy,
                ReportedPhone = draft.PhoneNumber,
                CaseNo = (++_caseSequence).ToString(),
                ReceivedAt = draft.ReceivedAt,
                Queue = CallQueue.Stacked,
                Info = BuildInfo(draft),
                Notes = BuildNotes(draft)
            };

            ApplyCallCode(call, code);

            // A disposition completes the call as it is entered - it never reaches the stack.
            if (draft.Disposition > 0)
            {
                call.IsCompleted = true;
                return call;
            }

            Calls.Insert(0, call);
            return call;
        }

        /// <summary>
        /// Copies what the call code says about the event onto it: the priority each
        /// agency responds at, which the assign screens show across the top, and
        /// whether releasing the last unit restacks the call rather than closing it.
        /// </summary>
        private static void ApplyCallCode(CadCall call, CallCodeItem code)
        {
            if (call == null || code == null) return;

            call.PolicePriority = code.PolicePriority;
            call.FirePriority = code.FirePriority;
            call.EmsPriority = code.EmsPriority;
            call.OtherPriority = code.OtherPriority;
            call.RestackOnRelease = code.RestacksOnRelease;
        }

        /// <summary>First agency code that was actually entered, for the Area column.</summary>
        private static string FirstCode(params string[] codes)
        {
            foreach (var code in codes)
            {
                if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
            }
            return string.Empty;
        }

        private static int BandToNumber(CallPriority band)
        {
            switch (band)
            {
                case CallPriority.High: return 1;
                case CallPriority.Low: return 7;
                default: return 4;
            }
        }

        private static string BuildInfo(NewCallDraft draft)
        {
            if (!string.IsNullOrWhiteSpace(draft.OtherComment)) return draft.OtherComment.Trim();
            if (!string.IsNullOrWhiteSpace(draft.Premise)) return draft.Premise.Trim();
            if (draft.Vehicle != null && !draft.Vehicle.IsEmpty) return draft.Vehicle.Summary;
            return "-";
        }

        private static string BuildNotes(NewCallDraft draft)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(draft.Narrative)) lines.Add(draft.Narrative.Trim());
            if (draft.Vehicle != null && !draft.Vehicle.IsEmpty) lines.Add("Vehicle: " + draft.Vehicle.Summary);
            if (!string.IsNullOrWhiteSpace(draft.ReportedBy))
            {
                var rp = "RP: " + draft.ReportedBy;
                if (!string.IsNullOrWhiteSpace(draft.PhoneNumber)) rp += " - " + draft.PhoneNumber;
                if (!string.IsNullOrWhiteSpace(draft.Address)) rp += ", " + draft.Address.Trim();
                lines.Add(rp);
            }
            return string.Join("   ", lines);
        }

        // ----------------------------------------------------------------- seed

        private void SeedCalls()
        {
            // Elapsed times match the sample board; seeding them relative to now
            // keeps the Duration column live and the Time column believable.
            var now = DateTime.Now;

            Add(CallQueue.Active, "Medical", "322255", "3", "145 Oak Ave.", now.AddSeconds(-341),
                2, CallPriority.High, Agency.Ems, "Chest pain",
                "Patient experiencing severe shortness of breath, transported to Mercy Hospital.",
                "Bridge at the Kenzie", new[] { "A204", "D403" });

            Add(CallQueue.Stacked, "Medical", "322255", "3", "145 Oak Ave.", now.AddSeconds(-341),
                2, CallPriority.High, Agency.Ems, "Chest pain",
                "Caller reports chest tightness and shortness of breath.", "Residence", new string[0]);
            Add(CallQueue.Stacked, "Fire Alarm", "410100", "2", "88 Elm St.", now.AddSeconds(-1087),
                1, CallPriority.High, Agency.Fire, "Commercial",
                "Master box activation, commercial occupancy, no smoke showing on arrival.",
                "Elm Street Mill", new string[0]);
            Add(CallQueue.Stacked, "Traffic Stop", "540300", "12", "5th & Pine", now.AddSeconds(-235),
                3, CallPriority.Medium, Agency.Police, "-",
                "Officer initiated traffic stop.", string.Empty, new string[0]);
            Add(CallQueue.Stacked, "Burglary", "220100", "9", "302 Cedar Ln.", now.AddSeconds(-1334),
                1, CallPriority.High, Agency.Police, "In progress",
                "Homeowner reports forced rear entry, suspect may still be inside.",
                "Single family", new string[0]);
            Add(CallQueue.Stacked, "Noise", "610200", "2", "77 Maple Dr.", now.AddSeconds(-512),
                4, CallPriority.Low, Agency.Police, "Residential",
                "Repeat complaint, loud music from rear deck.", "Residence", new string[0]);

            Add(CallQueue.Delayed, "Medical", "322255", "3", "145 Oak Ave.", now.AddSeconds(-341),
                2, CallPriority.High, Agency.Ems, "Chest pain",
                "Holding for transporting unit availability.", "Residence", new string[0]);
            Add(CallQueue.Delayed, "Fire Alarm", "410100", "2", "88 Elm St.", now.AddSeconds(-1087),
                1, CallPriority.High, Agency.Fire, "Commercial",
                "Delayed pending keyholder response.", "Elm Street Mill", new string[0]);
        }

        private void Add(CallQueue queue, string callType, string callCode, string area, string location,
                         DateTime received, int priorityNumber, CallPriority band, Agency agency,
                         string info, string notes, string premise, string[] units)
        {
            var call = new CadCall
            {
                EventKey = _keySequence++,
                EventNo = ReserveEventNo(),
                CallType = callType,
                CallCode = callCode,
                Jurisdiction = DefaultJurisdiction,
                Area = area,
                Location = location,
                Premise = premise,
                Origin = "20, Business Line",
                Priority = band,
                PriorityNumber = priorityNumber,
                PrimaryAgency = agency,
                ReceivedAt = received,
                Queue = queue,
                Info = info,
                Notes = notes,
                HasCrew = agency == Agency.Ems && queue == CallQueue.Active,
                CaseNo = (++_caseSequence).ToString()
            };

            ApplyCallCode(call, CallCodes.FirstOrDefault(c => c.Code == callCode));

            foreach (var unitNo in units)
            {
                call.AssignedUnits.Add(new AssignedUnit(unitNo, AgencyForUnitNo(unitNo)));

                // Tie the roster row back to the event, so the assign screens see the
                // unit as taken and the release screen can count what is left on it.
                var unit = Units.FirstOrDefault(u => u.UnitNo == unitNo);
                if (unit == null) continue;

                unit.AssignedCallKey = call.EventKey;
                unit.CallType = call.CallType;
                unit.Availability = UnitAvailability.Assigned;
            }

            Calls.Add(call);
        }

        /// <summary>
        /// The agency of a unit named on a seeded call, read off the roster rather
        /// than guessed from the unit number - the numeric unit numbers the fire and
        /// EMS rosters use carry nothing to guess from.
        /// </summary>
        private Agency AgencyForUnitNo(string unitNo)
        {
            var unit = Units.FirstOrDefault(u => u.UnitNo == unitNo);
            return unit != null ? unit.Agency : Agency.Police;
        }

        private void SeedUnits()
        {
            // Police first: 4 assigned (and past their 45 minute status timer) and
            // 16 available, which is the All / Available / Overdue split on the board.
            AddUnit("A204", "NOR..", "EMRG", Agency.Police, "Med Res..", "2/3/7", "42 Oak Ave", 62, UnitAvailability.Assigned);
            AddUnit("D403", "NOR..", "PATL", Agency.Police, "Lacerat..", "4/9/12", "61 Hazel St", 58, UnitAvailability.Assigned);
            AddUnit("C101", "GOR..", "PATL", Agency.Police, "911 Han..", "1/8/9", "16 Main St", 51, UnitAvailability.Assigned);
            AddUnit("B117", "WES..", "FIRE", Agency.Police, "Struct F..", "4/5/9", "88 Elm Dr", 47, UnitAvailability.Assigned);

            AddUnit("D312", "EAS..", "HAZM", Agency.Police, "Gas Lea..", "2/3/4", "15 Pine Rd", 44, UnitAvailability.Available);
            AddUnit("A108", "SOU..", "PATL", Agency.Police, "Cardiac..", "7/8/9", "201 Lake Bl", 41, UnitAvailability.Available);
            AddUnit("C055", "NOR..", "EMRG", Agency.Police, "MVA Inj..", "4/5/6", "77 River St", 38, UnitAvailability.Available);
            AddUnit("B223", "CEN..", "RESC", Agency.Police, "Water R..", "1/2/3", "5 Harbor Ln", 35, UnitAvailability.Available);
            AddUnit("A419", "WES..", "PATL", Agency.Police, "Fall Inj..", "6/7/8", "330 Hill Ct", 32, UnitAvailability.Available);
            AddUnit("D087", "EAS..", "FIRE", Agency.Police, "Alarm A..", "9/10/11", "12 Cedar Pl", 29, UnitAvailability.Available);
            AddUnit("C341", "SOU..", "HAZM", Agency.Police, "Spill Re..", "3/4/12", "99 Birch Wy", 26, UnitAvailability.Available);
            AddUnit("A156", "NOR..", "EMRG", Agency.Police, "Chest P..", "2/7/9", "64 Maple Dr", 24, UnitAvailability.Available);
            AddUnit("B072", "CEN..", "RESC", Agency.Police, "Elev Tr..", "5/6/12", "18 Spruce A..", 21, UnitAvailability.Available);
            AddUnit("D245", "WES..", "PATL", Agency.Police, "Seizure..", "1/4/7", "507 Ash Blvd", 19, UnitAvailability.Available);
            AddUnit("A330", "EAS..", "FIRE", Agency.Police, "Brush F..", "7/9/12", "23 Walnut St", 16, UnitAvailability.Available);
            AddUnit("C198", "SOU..", "EMRG", Agency.Police, "Allergic..", "2/3/9", "141 Ivy Ln", 14, UnitAvailability.Available);
            AddUnit("B461", "NOR..", "HAZM", Agency.Police, "Chem Sp..", "6/11/12", "82 Poplar Rd", 11, UnitAvailability.Available, jurisdiction: "LE");
            AddUnit("D034", "CEN..", "PATL", Agency.Police, "Diabeti..", "8/9/10", "36 Willow Ct", 8, UnitAvailability.Available);
            AddUnit("A512", "WES..", "RESC", Agency.Police, "Confine..", "3/5/7", "270 Fern Dr", 5, UnitAvailability.Available);
            AddUnit("C276", "EAS..", "FIRE", Agency.Police, "Vehicle..", "1/2/12", "9 Laurel Pl", 2, UnitAvailability.Available, jurisdiction: "LE");

            SeedFireApparatus();
            SeedEmsUnits();
            SeedOtherUnits();
        }

        /// <summary>
        /// The fire roster the LwCad132 board lays out - a row per apparatus type,
        /// with the unit numbers the Figma board carries. UnitType is the row
        /// heading the assign screen groups on, so these carry the apparatus names
        /// rather than the police roster's four letter unit codes.
        /// </summary>
        private void SeedFireApparatus()
        {
            AddUnit("9330", "STA 1", "Chief", Agency.Fire, "", "1/2/3", "Station 1", 96, UnitAvailability.Available, true);
            AddUnit("9331", "STA 2", "Chief", Agency.Fire, "", "2/3/4", "Station 2", 91, UnitAvailability.Available, true);
            AddUnit("9332", "STA 3", "Chief", Agency.Fire, "", "3/4/5", "Station 3", 88, UnitAvailability.Available);

            AddUnit("931", "STA 1", "Pumper", Agency.Fire, "", "1/2/3", "Station 1", 84, UnitAvailability.Available, true);
            AddUnit("933", "STA 3", "Pumper", Agency.Fire, "", "3/4/5", "Station 3", 79, UnitAvailability.Available, true);
            AddUnit("934", "STA 4", "Pumper", Agency.Fire, "", "4/5/6", "Station 4", 74, UnitAvailability.Available);

            AddUnit("9314", "STA 1", "Aer/Ladder", Agency.Fire, "", "1/2/3", "Station 1", 70, UnitAvailability.Available, true);
            AddUnit("932", "STA 2", "Rescue", Agency.Fire, "", "2/3/4", "Station 2", 66, UnitAvailability.Available, true);

            AddUnit("9321B", "STA 1", "Lieutenant", Agency.Fire, "", "1/2/3", "Station 1", 61, UnitAvailability.Available);
            AddUnit("9322B", "STA 2", "Lieutenant", Agency.Fire, "", "2/3/4", "Station 2", 57, UnitAvailability.Available);
            AddUnit("9325B", "STA 5", "Lieutenant", Agency.Fire, "", "5/6/7", "Station 5", 52, UnitAvailability.Available);

            AddUnit("9321", "STA 1", "Captain", Agency.Fire, "", "1/2/3", "Station 1", 48, UnitAvailability.Available, true);
            AddUnit("9322", "STA 2", "Captain", Agency.Fire, "", "2/3/4", "Station 2", 43, UnitAvailability.Available);
            AddUnit("9325", "STA 5", "Captain", Agency.Fire, "", "5/6/7", "Station 5", 39, UnitAvailability.Available);

            AddUnit("935", "STA 5", "Tankers", Agency.Fire, "", "5/6/7", "Station 5", 34, UnitAvailability.Available);
            AddUnit("937", "STA 7", "Tankers", Agency.Fire, "", "7/8/9", "Station 7", 30, UnitAvailability.Available);

            AddUnit("936", "STA 6", "Fire/Police", Agency.Fire, "", "6/7/8", "Station 6", 25, UnitAvailability.Available);

            AddUnit("938", "STA 8", "Brush Truck", Agency.Fire, "", "8/9/10", "Station 8", 21, UnitAvailability.Available);
            AddUnit("939", "STA 9", "Brush Truck", Agency.Fire, "", "9/10/11", "Station 9", 16, UnitAvailability.Available);
            AddUnit("9310", "STA 1", "Brush Truck", Agency.Fire, "", "1/2/3", "Station 1", 12, UnitAvailability.Available);
        }

        /// <summary>The EMS roster the LwCad133 board lays out, the same way.</summary>
        private void SeedEmsUnits()
        {
            AddUnit("9340", "STA 1", "Chief", Agency.Ems, "", "1/2/3", "Station 1", 93, UnitAvailability.Available, true);
            AddUnit("9341", "STA 2", "Chief", Agency.Ems, "", "2/3/4", "Station 2", 87, UnitAvailability.Available);

            AddUnit("941", "STA 1", "Ambulance", Agency.Ems, "", "1/2/3", "Station 1", 81, UnitAvailability.Available, true);
            AddUnit("943", "STA 3", "Ambulance", Agency.Ems, "", "3/4/5", "Station 3", 76, UnitAvailability.Available, true);
            AddUnit("944", "STA 4", "Ambulance", Agency.Ems, "", "4/5/6", "Station 4", 71, UnitAvailability.Available);

            AddUnit("9344", "STA 4", "AEMT", Agency.Ems, "", "4/5/6", "Station 4", 67, UnitAvailability.Available, true);

            AddUnit("9168", "STA 1", "EMT", Agency.Ems, "", "1/2/3", "Station 1", 62, UnitAvailability.Available);
            AddUnit("9169", "STA 1", "EMT", Agency.Ems, "", "1/2/3", "Station 1", 58, UnitAvailability.Available);
            AddUnit("9171", "STA 2", "EMT", Agency.Ems, "", "2/3/4", "Station 2", 53, UnitAvailability.Available);
            AddUnit("9172", "STA 2", "EMT", Agency.Ems, "", "2/3/4", "Station 2", 49, UnitAvailability.Available);
            AddUnit("9173", "STA 3", "EMT", Agency.Ems, "", "3/4/5", "Station 3", 44, UnitAvailability.Available);
            AddUnit("9374", "STA 4", "EMT", Agency.Ems, "", "4/5/6", "Station 4", 40, UnitAvailability.Available);

            AddUnit("9345", "STA 5", "Capt / Lt", Agency.Ems, "", "5/6/7", "Station 5", 35, UnitAvailability.Available);
            AddUnit("9346", "STA 6", "Capt / Lt", Agency.Ems, "", "6/7/8", "Station 6", 31, UnitAvailability.Available);

            AddUnit("9347", "STA 7", "1st Rspd", Agency.Ems, "", "7/8/9", "Station 7", 26, UnitAvailability.Available);
            AddUnit("9348", "STA 8", "1st Rspd", Agency.Ems, "", "8/9/10", "Station 8", 22, UnitAvailability.Available);
        }

        /// <summary>
        /// The support roster behind the SPT tab - LwCad134's Other Unit Assignment,
        /// which lays its units out in the same numbered slots as the police board.
        /// </summary>
        private void SeedOtherUnits()
        {
            AddUnit("T101", "PW", "Tow", Agency.Other, "", "1/2/3/4/5/6/7/8/9/10/11/12", "Public Works", 55, UnitAvailability.Available, true);
            AddUnit("T102", "PW", "Tow", Agency.Other, "", "1/2/3/4/5/6/7/8/9/10/11/12", "Public Works", 47, UnitAvailability.Available);
            AddUnit("H201", "DPW", "Highway", Agency.Other, "", "1/2/3/4/5/6/7/8/9/10/11/12", "Highway Barn", 36, UnitAvailability.Available, true);
            AddUnit("U301", "UTIL", "Utility", Agency.Other, "", "1/2/3/4/5/6/7/8/9/10/11/12", "Utility Yard", 28, UnitAvailability.Available);
            AddUnit("A401", "ACO", "Animal Ctl", Agency.Other, "", "1/2/3/4/5/6/7/8/9/10/11/12", "Shelter", 17, UnitAvailability.Available);
        }

        private void AddUnit(string unitNo, string station, string type, Agency agency, string callType,
                             string area, string location, int minutesAgo, UnitAvailability availability,
                             bool isRecommended = false, string jurisdiction = null)
        {
            Units.Add(new CadUnit
            {
                UnitNo = unitNo,
                Station = station,
                UnitType = type,
                Agency = agency,
                CallType = callType,
                Area = area,
                Location = location,
                StatusSince = DateTime.Now.AddMinutes(-minutesAgo),
                Availability = availability,
                Status = availability == UnitAvailability.Available ? "AVL" : "ENR",
                VehicleId = "V" + unitNo,
                IsRecommended = isRecommended,

                // The assign screens narrow their recommendation to the call's
                // jurisdiction, so the roster has to carry one. Two units sit in the
                // neighbouring jurisdiction, which is what keeps that visible on the
                // demo board rather than being a rule nothing ever exercises.
                Jurisdiction = jurisdiction ?? DefaultJurisdiction,

                // Units assigned to a call time out against that call; a unit given a
                // non-call location (meal break, motor pool) never times off the monitor.
                TimeoutMinutes = availability == UnitAvailability.Assigned ? 45 : 0
            });
        }

        private static IReadOnlyList<CallCodeItem> BuildCallCodes()
        {
            var list = new List<CallCodeItem>
            {
                new CallCodeItem("322255", "Medical - Breathing Problem", "Medical", Agency.Ems, CallPriority.High, 2),
                new CallCodeItem("322100", "Medical - Cardiac Arrest", "Medical", Agency.Ems, CallPriority.High, 1),
                new CallCodeItem("410100", "Fire Alarm - Commercial", "Fire Alarm", Agency.Fire, CallPriority.High, 1),
                new CallCodeItem("410200", "Structure Fire", "Structure Fire", Agency.Fire, CallPriority.High, 1),
                new CallCodeItem("220100", "Burglary - In Progress", "Burglary", Agency.Police, CallPriority.High, 1),
                new CallCodeItem("240300", "Larceny - Report", "Larceny", Agency.Police, CallPriority.Low, 7),
                new CallCodeItem("540300", "Traffic Stop", "Traffic Stop", Agency.Police, CallPriority.Medium, 3),
                new CallCodeItem("541000", "Motor Vehicle Accident", "MVA", Agency.Police, CallPriority.High, 2),
                new CallCodeItem("610200", "Noise Complaint", "Noise", Agency.Police, CallPriority.Low, 4),
                new CallCodeItem("620100", "Well Being Check", "Well Being", Agency.Police, CallPriority.Medium, 5),
                new CallCodeItem("700100", "Hazmat - Gas Leak", "Gas Leak", Agency.Fire, CallPriority.High, 1),
                new CallCodeItem("000653", "Assist Other Agency", "Assist", Agency.Other, CallPriority.Low, 6)
            };

            foreach (var item in list)
            {
                // CCC1 carries a priority per agency, 0 where the code does not task
                // it. The owning agency gets the code's own priority; a fire alarm and
                // a structure fire also send police, and a cardiac arrest sends fire.
                item.PolicePriority = item.Agency == Agency.Police ? item.DefaultPriorityNumber : 0;
                item.FirePriority = item.Agency == Agency.Fire ? item.DefaultPriorityNumber : 0;
                item.EmsPriority = item.Agency == Agency.Ems ? item.DefaultPriorityNumber : 0;
                item.OtherPriority = item.Agency == Agency.Other ? item.DefaultPriorityNumber : 0;
                if (item.Code == "410200") item.PolicePriority = 3;
                if (item.Code == "322100") item.FirePriority = 2;
                item.PromptsAlert = item.Code == "220100" || item.Code == "410200";

                // CCC1_RESTACK - the codes significant enough that releasing the last
                // unit puts the event back on the stack rather than closing it.
                item.RestacksOnRelease = item.DefaultPriorityNumber <= 2;
            }

            return list;
        }

        private static IReadOnlyList<LookupItem> BuildOrigins()
        {
            return new List<LookupItem>
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
        }

        private static IReadOnlyList<LookupItem> BuildJurisdictions()
        {
            return new List<LookupItem>
            {
                new LookupItem("LW", "Larimore West"),
                new LookupItem("LE", "Larimore East"),
                new LookupItem("NO", "Northfield"),
                new LookupItem("GO", "Gorham"),
                new LookupItem("WE", "Westbrook"),
                new LookupItem("SO", "Southport")
            };
        }

        // ---------------------------------------------------------- keyboard

        /// <summary>
        /// A demo keyboard template, not production data - nobody has the Lowell
        /// KYB1-3 rows on disk (Docs OQ-8). The Quick Menu keeps the rail's entries
        /// from the Figma board, each on the letter it already had; Take Call has
        /// moved to F1 of the Standard Functions bar, where the legacy console puts
        /// it. Ctrl F9 is a real LW program the catalogue has no notes for, so the
        /// "Uncatalogued program" stub can be seen. Captions keep the Figma wording
        /// even where it is longer than the legacy 12-character KYB literal - the
        /// console does not cut a caption; see the README.
        /// </summary>
        private static Keymap BuildKeymap()
        {
            var standard = new (string Literal, string Program)[]
            {
                // F1 - F9
                ("Take Call", "LWCAD101"), ("Assign Call", "LWCAD130"), ("Unit Status", "LWCAD135"),
                ("Release Unit", "LWCAD141"), ("Self Init", "LWCAD107"), ("Traffic Stop", "LWCAD112"),
                ("Delayed Call", "LWCAD105"), ("Station Call", "LWCADSTATIONCALL"), ("CAD Alert", "LWCAD301"),

                // Shift F1 - F9
                ("Replicate", "LWCAD121"), ("X-Ref Call", "LWCAD139"), ("Send Other", "LWCAD138"),
                ("Release Call", "LWCAD142"), ("Switch Unit", "LWCAD175"), ("Asgn. Case No", "LWCAD144"),
                ("Unit Trans", "LWCAD113"), ("Event Trans", "LWCAD117"), ("Trans Viewer", "LWCAD610"),

                // Ctrl F1 - F9
                ("CAD Search", "LWCADSEARCH"), ("Call Review", "LWCADCALLREVIEW"), ("Call Detail", "LWCADTRANSUNITS"),
                ("Call History", "LWCAD030"), ("Bulletins", "LWCADBULLETINS"), ("Dynamic M/up", "LWCAD171"),
                ("Fire M/Dwn", "LWCAD172"), ("R/Stk Tmout", "LWCAD137"), ("CAD Report", "LWCADRPT501")
            };

            var quick = new Dictionary<string, (string Literal, string Program)>
            {
                ["Q"] = ("Assign Call", "LWCAD130"),
                ["E"] = ("Release Call", "LWCAD142"),
                ["R"] = ("Unit Status", "LWCAD135"),
                ["Y"] = ("Station Call", "LWCADSTATIONCALL"),
                ["U"] = ("Send Other", "LWCAD138"),
                ["O"] = ("Trans Viewer", "LWCAD610"),
                ["P"] = ("Dynamic M/up", "LWCAD171"),
                ["L"] = ("Switch Unit", "LWCAD175"),
                ["M"] = ("Self Init", "LWCAD107"),
                ["G"] = ("Asgn. A/Units", "LWCAD140"),
                ["F"] = ("R/Stk Tmout", "LWCAD137"),
                ["D"] = ("Fire M/Dwn", "LWCAD172"),
                ["S"] = ("Xfer Case No", "LWCAD191"),
                ["Z"] = ("Vw Act Call", "LWCAD699"),
                ["X"] = ("CAD Alert", "LWCAD301"),
                ["V"] = ("Asgn. Case No", "LWCAD144"),
                ["B"] = ("Call Review", "LWCADCALLREVIEW"),
                ["N"] = ("Patrol File", "LAPTLSYSTEM"),
                ["J"] = ("Rolodex", "LARDXSYSTEM"),
                ["W"] = ("PreRoaster", "LWRSTPREPLAN"),
                ["H"] = ("M/Box", "LWMDCEMAIL")
            };

            var standardSlots = Keymap.AllStandardPositions()
                .Select((p, i) => KeymapLoader.BuildSlot(p.Bank, p.Position, 'P', standard[i].Literal, ' ', ' ',
                                                         standard[i].Program, null, null, alwaysThroughKyb1: false));

            var quickSlots = Keymap.QuickPositions()
                .Select(letter => quick.TryGetValue(letter, out var key)
                    ? KeymapLoader.BuildSlot(KeyBank.Quick, letter, 'P', key.Literal, ' ', ' ',
                                             key.Program, null, null, alwaysThroughKyb1: false)
                    : KeySlot.Blank(KeyBank.Quick, letter));

            return new Keymap(standardSlots, quickSlots)
            {
                TemplateId = "DEMO",
                OperatorType = 'D',
                Source = "demo keyboard template"
            };
        }
    }
}
