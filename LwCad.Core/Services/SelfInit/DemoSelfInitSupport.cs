using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.Services.SelfInit
{
    /// <summary>
    /// The demo board's answers: the roster is the Unit Monitor, and a small unit
    /// file (UNT1) holds two units that are not logged on - P77 (police) and E9
    /// (EMS) - so the "not active" prompt and Activate Unit can be tried.
    /// </summary>
    public sealed class DemoSelfInitSupport : ISelfInitSupport
    {
        private readonly Func<IEnumerable<CadUnit>> _units;
        private readonly Func<IEnumerable<CadCall>> _calls;

        private static readonly Dictionary<int, string> People = new Dictionary<int, string>
        {
            { 4101, "SMITH, JOHN" },
            { 4102, "JONES, MARY" },
            { 4103, "BROWN, ALAN" },
            { 4104, "GARCIA, ELENA" }
        };

        private static readonly Dictionary<string, UnitDefaults> UnitFile = new Dictionary<string, UnitDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["P77"] = new UnitDefaults { UnitNo = "P77", Agency = "LWPD", AgencyType = 'P', Jurisdiction = "LW", UnitType = 1, Employee1 = 4101, Areas = new[] { "9", "12" } },
            ["E9"] = new UnitDefaults { UnitNo = "E9", Agency = "LWEM", AgencyType = 'E', Jurisdiction = "LW", UnitType = 4, Station = "STA 3" }
        };

        private static readonly Dictionary<int, string[]> Profiles = new Dictionary<int, string[]>
        {
            [60] = new[] { "WHITE", "BLACK", "HISPANIC", "ASIAN", "OTHER" },
            [61] = new[] { "MALE", "FEMALE" },
            [62] = new[] { "SECTOR 1", "SECTOR 2", "SECTOR 3", "SECTOR 4" },
            [63] = new[] { "DISCRETIONARY", "NON-DISCRETIONARY" },
            [64] = new[] { "RESIDENT", "NON-RESIDENT" },
            [65] = new[] { "CITATION", "WARNING", "NONE" },
            [66] = new[] { "NO SEARCH", "CONSENT", "PROBABLE CAUSE" }
        };

        public DemoSelfInitSupport(Func<IEnumerable<CadUnit>> units, Func<IEnumerable<CadCall>> calls)
        {
            _units = units;
            _calls = calls;
        }

        /// <summary>CAD7_SELF_E 'P' asks whether the unit has arrived; Traffic Stop is en route.</summary>
        public SelfInitSettings Settings { get; } = new SelfInitSettings { SelfArrive = 'P', TrafficArrive = 'E' };

        public IReadOnlyList<AgencyItem> Agencies { get; } = new[]
        {
            new AgencyItem("LWPD", 'P', true),
            new AgencyItem("LWFD", 'F', false),
            new AgencyItem("LWEM", 'E', false)
        };

        public IReadOnlyList<LookupItem> UnitTypes { get; } = new[]
        {
            new LookupItem("1", "CRUISER"), new LookupItem("2", "WAGON"), new LookupItem("3", "ENGINE"), new LookupItem("4", "AMBULANCE")
        };

        public IReadOnlyList<LookupItem> Stations { get; } = new[]
        {
            new LookupItem("1", "STA 1"), new LookupItem("2", "STA 2"), new LookupItem("3", "STA 3")
        };

        public IReadOnlyList<LookupItem> UnitJurisdictions { get; } = new[] { new LookupItem("LW", "LOWELL") };

        public RosterUnit FindUnit(string unitNo)
        {
            var unit = _units().FirstOrDefault(u => string.Equals(u.UnitNo, (unitNo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
            if (unit == null) return null;

            var call = unit.AssignedCallKey != 0 ? _calls().FirstOrDefault(c => c.EventKey == unit.AssignedCallKey) : null;
            if (!_crews.TryGetValue(unit.UnitNo, out var officer)) officer = 4101 + Math.Abs(unit.UnitNo.GetHashCode()) % 4;

            return new RosterUnit
            {
                UnitNo = unit.UnitNo,
                AgencyType = AgencyType(unit.Agency),
                Agency = unit.Agency == Agency.Fire ? "LWFD" : unit.Agency == Agency.Ems ? "LWEM" : unit.Agency == Agency.Other ? "LWDPW" : "LWPD",
                Jurisdiction = unit.Jurisdiction,
                Officer1 = unit.Agency == Agency.Police ? officer : 0,
                Officer1Name = unit.Agency == Agency.Police ? PersonName(officer) : string.Empty,
                DispNo2 = call != null ? 1 : 0,
                EventText = call?.EventNo ?? string.Empty
            };
        }

        private readonly Dictionary<string, int> _crews = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The crew an activation put on the unit, so the roster shows it.</summary>
        public void RememberCrew(string unitNo, int employee) => _crews[unitNo ?? string.Empty] = employee;

        public UnitDefaults UnitDefaults(string unitNo) =>
            UnitFile.TryGetValue((unitNo ?? string.Empty).Trim(), out var defaults) ? defaults : null;

        public string PersonName(int employeeId) => People.TryGetValue(employeeId, out var name) ? name : string.Empty;

        public IReadOnlyList<string> ProfileList(int library)
        {
            var list = new List<string> { string.Empty };
            if (Profiles.TryGetValue(library, out var values)) list.AddRange(values);
            return list;
        }

        public string TrafficStopCallCode() => "540300";

        public static char AgencyType(Agency agency) =>
            agency == Agency.Fire ? 'F' : agency == Agency.Ems ? 'E' : agency == Agency.Other ? 'O' : 'P';
    }
}
