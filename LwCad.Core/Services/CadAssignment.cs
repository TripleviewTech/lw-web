using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// The assign screens' two pieces of shared behaviour: working out what a call
    /// may be assigned, and applying an assignment to the board. Both data services
    /// run the same code here so the demo board and the live tables cannot drift
    /// apart on which units an agency offers or what an assignment does.
    /// </summary>
    internal static class CadAssignment
    {
        /// <summary>
        /// Slots the numbered grid on LwCad131 / LwCad134 offers. The legacy screens
        /// carry F1 to F8, so the flat list stops at eight.
        /// </summary>
        public const int FlatSlotCount = 8;

        /// <summary>
        /// What one agency's assign screen offers for a call.
        ///
        /// A recommendation is the units that cover the call, not simply the units
        /// that happen to be free: the roster is narrowed to the call's
        /// jurisdiction (RST1_JUR against STK1_JUR) and then to the units whose
        /// response areas cover the call's area (RST1_R_AREA_1..5 against
        /// STK1_AREA), with RST1_RECOMMEND ordering what is left. Offering the
        /// first eight free units alphabetically, which is what this did before, is
        /// a roster listing rather than a recommendation.
        ///
        /// When nothing covers the call the search widens - first past the area,
        /// then past the jurisdiction - and says so, because a widened list read as
        /// a recommendation is how the wrong unit gets sent.
        ///
        /// NOTE: LwCad131/132/133/134 are not in this tree, so the rule above is
        /// reconstructed from the columns the tables carry rather than ported. If
        /// the real screens weight recommendations differently - by RST1_PRIORITY,
        /// by station, by the CCC1_RECOM_1ST/2ND/3RD response ladder - this is where
        /// that belongs.
        /// </summary>
        public static AssignCandidateSet BuildCandidates(
            CadCall call, Agency agency, IEnumerable<CadUnit> roster)
        {
            if (roster == null) return AssignCandidateSet.Empty;

            var onCall = call == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(call.AssignedUnits.Select(u => u.UnitNo),
                                      StringComparer.OrdinalIgnoreCase);

            // A unit already working another event is not offered; one already on
            // this call is, marked as assigned, so the whole response is visible.
            var pool = roster
                .Where(u => u.Agency == agency)
                .Where(u => onCall.Contains(u.UnitNo) || IsFree(u, call))
                .ToList();

            var jur = (call?.Jurisdiction ?? string.Empty).Trim();
            var area = (call?.Area ?? string.Empty).Trim();

            bool InJur(CadUnit u) =>
                jur.Length == 0 ||
                string.Equals((u.Jurisdiction ?? string.Empty).Trim(), jur, StringComparison.OrdinalIgnoreCase);

            // Always keep a unit that is already on the call, whatever it covers.
            bool Kept(CadUnit u) => onCall.Contains(u.UnitNo);

            // The recommendation proper: in the jurisdiction, covering the area.
            if (area.Length > 0)
            {
                var covering = pool.Where(u => Kept(u) || (InJur(u) && u.CoversArea(area))).ToList();
                if (covering.Any(u => !Kept(u)))
                {
                    return Build(covering, agency, onCall, Describe(area, jur), isWidened: false);
                }
            }

            // Nothing covers it. Widen to the jurisdiction, and say so.
            var inJurisdiction = pool.Where(u => Kept(u) || InJur(u)).ToList();
            if (inJurisdiction.Any(u => !Kept(u)))
            {
                var widened = area.Length > 0;
                return Build(inJurisdiction, agency, onCall,
                             widened
                                 ? "No free unit covers area " + area + ". Showing every free unit in " +
                                   (jur.Length == 0 ? "the roster" : jur) + "."
                                 : Describe(area, jur),
                             widened);
            }

            // Nothing in the jurisdiction either.
            return Build(pool, agency, onCall,
                         "No free unit in " + (jur.Length == 0 ? "this jurisdiction" : jur) +
                         ". Showing the whole roster.",
                         isWidened: true);
        }

        private static string Describe(string area, string jur)
        {
            if (area.Length == 0 && jur.Length == 0) return "Free units on the roster.";
            if (area.Length == 0) return "Free units in " + jur + ".";
            if (jur.Length == 0) return "Units covering area " + area + ".";
            return "Units covering area " + area + " in " + jur + ".";
        }

        /// <summary>
        /// Lays a chosen set out the way that agency's screen lays it out. Police and
        /// Other come back as one unnamed group - the numbered slots of LwCad131 /
        /// LwCad134; Fire and EMS come back one group per apparatus type, which is how
        /// LwCad132 / LwCad133 lay their rosters out.
        /// </summary>
        private static AssignCandidateSet Build(List<CadUnit> chosen, Agency agency,
                                                HashSet<string> onCall, string basis, bool isWidened)
        {
            var groups = new List<AssignGroup>();
            bool grouped = agency == Agency.Fire || agency == Agency.Ems;

            if (!grouped)
            {
                var flat = new AssignGroup(string.Empty);

                foreach (var unit in Order(chosen, onCall).Take(FlatSlotCount))
                {
                    flat.Candidates.Add(Candidate(unit, string.Empty, onCall));
                }

                if (flat.Candidates.Count > 0) groups.Add(flat);
                return new AssignCandidateSet(groups, basis, isWidened);
            }

            var byTypes = chosen.GroupBy(Heading)
                .OrderBy(g => GroupRank(g.Key, agency))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var byType in byTypes)
            {
                var group = new AssignGroup(byType.Key);

                foreach (var unit in Order(byType, onCall))
                {
                    group.Candidates.Add(Candidate(unit, byType.Key, onCall));
                }

                groups.Add(group);
            }

            return new AssignCandidateSet(groups, basis, isWidened);
        }

        /// <summary>
        /// Lays out a recommendation the deployment plan or the run card made.
        ///
        /// The recommendation decides membership and order; this only resolves the
        /// unit numbers against the roster and files them. A unit the plan names
        /// that is not on the monitor - off duty, or another jurisdiction's - is
        /// skipped, which is what the legacy does when its RST1 re-read fails.
        ///
        /// Fire and EMS get their apparatus row from the run card column that named
        /// the unit, not from the unit's own type: the card decides what a unit is
        /// being called as. Rows still print in the board's order.
        /// </summary>
        public static AssignCandidateSet BuildFromRecommendation(
            CadCall call, Agency agency, IEnumerable<CadUnit> roster,
            IReadOnlyList<CadRecommendation.Recommended> recommended, string basis)
        {
            var byUnitNo = new Dictionary<string, CadUnit>(StringComparer.OrdinalIgnoreCase);
            foreach (var unit in roster ?? Enumerable.Empty<CadUnit>())
            {
                byUnitNo[unit.UnitNo] = unit;
            }

            var onCall = call == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(call.AssignedUnits.Select(u => u.UnitNo),
                                      StringComparer.OrdinalIgnoreCase);

            var groups = new List<AssignGroup>();
            var grouped = agency == Agency.Fire || agency == Agency.Ems;
            var flat = grouped ? null : new AssignGroup(string.Empty);
            if (flat != null) groups.Add(flat);

            foreach (var entry in recommended.OrderBy(r => r.Rank))
            {
                if (!byUnitNo.TryGetValue(entry.UnitNo, out var unit)) continue;

                // Everything the plan or the card named is shown, busy or not. The
                // legacy screens print each recommendation's status beside it rather
                // than dropping the unit, because what the card says should respond
                // is what the dispatcher needs to see - including that it is already
                // out. The roster fallback is the one that filters on free.

                if (!grouped)
                {
                    if (flat.Candidates.Count >= FlatSlotCount) break;
                    flat.Candidates.Add(Candidate(unit, string.Empty, onCall));
                    continue;
                }

                var heading = entry.Group.Length > 0 ? entry.Group : Heading(unit);
                var group = groups.FirstOrDefault(
                    g => string.Equals(g.Name, heading, StringComparison.OrdinalIgnoreCase));

                if (group == null)
                {
                    group = new AssignGroup(heading);
                    groups.Add(group);
                }

                group.Candidates.Add(Candidate(unit, heading, onCall));
            }

            if (grouped)
            {
                groups = groups
                    .Where(g => g.Candidates.Count > 0)
                    .OrderBy(g => GroupRank(g.Name, agency))
                    .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else if (flat.Candidates.Count == 0)
            {
                groups.Clear();
            }

            return new AssignCandidateSet(groups, basis, isWidened: false);
        }

        private static AssignCandidate Candidate(CadUnit unit, string groupName, HashSet<string> onCall)
        {
            return new AssignCandidate(unit, groupName, onCall.Contains(unit.UnitNo))
            {
                IsRecommended = unit.IsRecommended
            };
        }

        /// <summary>Recommended units first, then by unit number, as the legacy slots fill.</summary>
        private static IEnumerable<CadUnit> Order(IEnumerable<CadUnit> units, HashSet<string> onCall)
        {
            return units
                .OrderByDescending(u => onCall.Contains(u.UnitNo))
                .ThenByDescending(u => u.IsRecommended)
                .ThenBy(u => u.UnitNo, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The row heading on the fire and EMS boards - the unit's apparatus type.</summary>
        private static string Heading(CadUnit unit)
        {
            var type = (unit.UnitType ?? string.Empty).Trim();
            return type.Length > 0 ? type : "Other";
        }

        /// <summary>
        /// Free to take this call. The legacy screens open with their Available box
        /// ticked and their Assigned box clear, so a unit working another event is
        /// not recommended - which needs the status as well as the event number,
        /// because a unit can be committed without an event on it.
        /// </summary>
        private static bool IsFree(CadUnit unit, CadCall call)
        {
            if (call != null && unit.AssignedCallKey == call.EventKey) return true;
            return unit.AssignedCallKey == 0 && unit.IsAvailable;
        }

        // The apparatus order the boards print. LwCad132 and LwCad133 lay their
        // apparatus out as fixed COLUMNS - Chief, Pumper, Aer/Ladder, Rescue,
        // Captian, Captian, Lieutenant, Lieutenant, Other, then a second band of
        // Engine, Tankers, Fire/Police, Brush Trck - and CAD_AssignFire.png
        // transposes that into rows for the redesign. These are the row orders the
        // Figma boards carry, which is the order they are reproduced in here.
        //
        // The two agencies' apparatus names are disjoint, so they get a list each
        // rather than one merged order that satisfies neither: the fire board puts
        // Other before Captain, the EMS board puts it before Capt / Lt.

        //
        // Lowell's fire run card is laid out by alarm (LwCad132: 1st to 9th Alarm,
        // then EMS, Haz, Still), so those groups lead. The apparatus names after them
        // are what the roster fallback groups on - the MLB1 unit type - when there is
        // no run card to read.

        private static readonly string[] FireGroupOrder =
        {
            "1st Alarm", "2nd Alarm", "3rd Alarm", "4th Alarm", "5th Alarm", "6th Alarm",
            "7th Alarm", "8th Alarm", "9th Alarm", "EMS", "Haz", "Still",
            "Chief", "Pumper", "Engine", "Aer/Ladder", "Rescue",
            "Lieutenant", "Other", "Captain", "Tankers", "Fire/Police", "Brush Truck"
        };

        // LwCad133: Chief, Ambulance, Capt/Lt, Rescue, AEMT x2, EMT x2, Other.
        private static readonly string[] EmsGroupOrder =
        {
            "Chief", "Ambulance", "Capt / Lt", "Rescue", "AEMT", "EMT", "Other", "1st Rspd"
        };

        /// <summary>
        /// Where an apparatus type sits on its agency's board. A type the board does
        /// not name sorts after the ones it does, alphabetically, so a site carrying
        /// its own apparatus names still gets a stable order.
        /// </summary>
        private static int GroupRank(string name, Agency agency)
        {
            var order = agency == Agency.Ems ? EmsGroupOrder : FireGroupOrder;

            for (int i = 0; i < order.Length; i++)
            {
                if (string.Equals(order[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return order.Length;
        }

        // ------------------------------------------------------------- apply

        /// <summary>
        /// Puts the picked units on the call. This is the board half of what
        /// LwCad131's Assign does: the unit takes the event's call code and
        /// location, moves to the chosen state, and the event moves from the
        /// stack to the Active monitor once it has a unit on it.
        /// </summary>
        public static void Apply(UnitAssignment assignment,
                                 IReadOnlyList<UnitStatusOption> statuses,
                                 ObservableCollection<CadCall> calls)
        {
            if (assignment?.Call == null || assignment.Units.Count == 0) return;

            var call = assignment.Call;
            var status = StatusFor(assignment.Action, statuses);

            foreach (var unit in assignment.Units)
            {
                if (call.AssignedUnits.All(a => !string.Equals(a.UnitNo, unit.UnitNo,
                                                               StringComparison.OrdinalIgnoreCase)))
                {
                    call.AssignedUnits.Add(new AssignedUnit(unit.UnitNo, unit.Agency));
                }

                unit.AssignedCallKey = call.EventKey;
                unit.CallType = call.CallType;
                unit.Location = call.Location;
                unit.Availability = UnitAvailability.Assigned;
                unit.StatusSince = DateTime.Now;
                unit.HasStatusTime = true;
                unit.IsOverdue = false;
                unit.HasPendingChange = true;

                if (status != null) unit.Status = status.Abbreviation;

                // A unit on a call times out against that call; the screen's own
                // timeout wins when the operator entered one.
                unit.TimeoutMinutes = assignment.TimeoutMinutes > 0 ? assignment.TimeoutMinutes : 45;
            }

            // An Assign Call takes the event off the stack; an Assign / Assist is run
            // against an event that is already active and leaves the monitor alone.
            if (call.Queue == CallQueue.Stacked || call.Queue == CallQueue.Delayed)
            {
                call.Queue = CallQueue.Active;
            }

            RefreshInfo(call);

            // Other events lose any unit that has just been taken off them.
            if (calls == null) return;

            foreach (var other in calls)
            {
                if (ReferenceEquals(other, call)) continue;

                var taken = other.AssignedUnits
                    .Where(a => assignment.Units.Any(u => string.Equals(u.UnitNo, a.UnitNo,
                                                                        StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (taken.Count == 0) continue;

                foreach (var unit in taken) other.AssignedUnits.Remove(unit);
                RefreshInfo(other);
            }
        }

        /// <summary>The Active list's Additional Info column is its assigned units.</summary>
        public static void RefreshInfo(CadCall call)
        {
            if (call == null || call.Queue != CallQueue.Active) return;

            call.Info = call.AssignedUnits.Count > 0
                ? string.Join(", ", call.AssignedUnits.Select(u => u.UnitNo))
                : "-";
        }

        /// <summary>
        /// Maps the screen's Dispatch / En Route / Arrive choice onto a UST1 status.
        /// Matched on the abbreviation the legacy statuses carry, falling back to the
        /// description so a site that spells them differently still resolves.
        /// </summary>
        private static UnitStatusOption StatusFor(DispatchAction action,
                                                  IReadOnlyList<UnitStatusOption> statuses)
        {
            if (statuses == null) return null;

            string abbreviation;
            string description;

            switch (action)
            {
                case DispatchAction.EnRoute:
                    abbreviation = "ENR";
                    description = "en route";
                    break;
                case DispatchAction.Arrived:
                    abbreviation = "ARV";
                    description = "arrive";
                    break;
                default:
                    abbreviation = "DSP";
                    description = "dispatch";
                    break;
            }

            return statuses.FirstOrDefault(
                       s => string.Equals(s.Abbreviation, abbreviation, StringComparison.OrdinalIgnoreCase))
                ?? statuses.FirstOrDefault(
                       s => s.Description.IndexOf(description, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? statuses.FirstOrDefault(s => !s.IsAvailable);
        }
    }
}
