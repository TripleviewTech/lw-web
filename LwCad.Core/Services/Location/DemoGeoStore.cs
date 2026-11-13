using System;
using System.Collections.Generic;
using System.Linq;
using LaTableAlc1;
using LaTableGeo1;
using LaTableGeo2;
using LaTableStr1;
using LwTablePmm1;
using LwTablePmm6;
using LwTableRun1;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// A small made-up geobase for the demo board, so every location form works
    /// without a database. It is also what Tools/LwCadUxTest-Geo.sql loads into the
    /// scratch LocalDB copy, so the demo and the SQL path are tested on the same data.
    ///
    ///   Streets     CEDAR LN, ELM ST, OAK AVE, MAPLE DR, BAKER ST, MAIN ST (two
    ///               segments, split at ELM), PINE ST, 5TH ST; "CENTRE ST" is an
    ///               STR1 alias of CENTER ST.
    ///   Premises    P/BA lists BAKER SCHOOL, BAY STATE MARKET and BAYSIDE
    ///               APARTMENTS; two premises share 145 OAK AVE ("Multiple Premises").
    ///   Alarm / box A/1234 and B/21 are BAY STATE MARKET; B/R7 is a run-card box.
    ///   Common name C/CITY HALL (an address) and C/TOWN COMMON (a corner).
    ///   Split       PINE ST / 5TH ST sits on a split segment and asks L or R.
    /// </summary>
    public sealed class DemoGeoStore : IGeoStore
    {
        public const string Jur = "LW";

        public List<GEO1> Geo1 { get; } = new List<GEO1>();
        public List<GEO2> Geo2 { get; } = new List<GEO2>();
        public List<STR1> Str1 { get; } = new List<STR1>();
        public List<PMM1> Pmm1 { get; } = new List<PMM1>();
        public List<PMM6> Pmm6 { get; } = new List<PMM6>();
        public List<RUN1> Run1 { get; } = new List<RUN1>();
        public List<ALC1> Alc1 { get; } = new List<ALC1>();
        public Dictionary<int, (decimal Lat, decimal Long)> Pmm4 { get; } = new Dictionary<int, (decimal, decimal)>();
        public Dictionary<int, int> Pmm2 { get; } = new Dictionary<int, int>();

        public DemoGeoStore()
        {
            // key, street, L range, R range, police/fire/ems L and R
            Segment(101, "CEDAR LN", 100, 398, 101, 399, "P9", "F2", "E3", "P9", "F2", "E3");
            Segment(102, "ELM ST", 2, 198, 1, 199, "P2", "F2", "E1", "P2", "F2", "E1");
            Segment(103, "OAK AVE", 100, 298, 101, 299, "P3", "F3", "E3", "P3", "F3", "E3");
            Segment(104, "MAPLE DR", 2, 150, 1, 149, "P2", "F1", "E2", "P2", "F1", "E2");
            Segment(105, "BAKER ST", 2, 98, 1, 99, "P1", "F1", "E1", "P1", "F1", "E1");
            Segment(106, "MAIN ST", 2, 498, 1, 499, "P1", "F1", "E1", "P1", "F1", "E1");
            Segment(107, "MAIN ST", 500, 998, 501, 999, "P4", "F4", "E4", "P5", "F4", "E4");
            Segment(108, "PINE ST", 2, 98, 1, 99, "P12", "F5", "E5", "P12", "F5", "E5");
            Segment(109, "5TH ST", 2, 98, 1, 99, "P12", "F5", "E5", "P12", "F5", "E5");
            Segment(110, "PINE ST", 100, 198, 101, 199, "P12", "F5", "E5", "P14", "F6", "E5", typeCode: 2);
            Segment(111, "CENTER ST", 2, 98, 1, 99, "P1", "F1", "E1", "P1", "F1", "E1");

            // Common names: an address range and a named corner.
            Segment(120, "MAIN ST", 375, 375, 0, 0, "P1", "F1", "E1", "P1", "F1", "E1", commonName: "CITY HALL");
            Segment(121, "MAIN ST", 0, 0, 0, 0, "P1", "F1", "E1", "P1", "F1", "E1", commonName: "TOWN COMMON");

            // Corners: MAIN ST's two segments meet ELM ST at 498/500 - different
            // police areas either side, so "MAIN / ELM" lists two rows.
            Corner(106, "ELM ST", 'H');
            Corner(107, "ELM ST", 'L');
            Corner(102, "MAIN ST", 'L');
            Corner(103, "MAPLE DR", 'H');
            Corner(104, "OAK AVE", 'L');
            Corner(110, "5TH ST", 'L');
            Corner(109, "PINE ST", 'H');
            Corner(121, "ELM ST", 'L');
            Corner(101, "BAKER ST", 'H');
            Corner(105, "CEDAR LN", 'L');

            Str1.Add(Street("CENTER ST", "CENTRE ST"));
            Str1.Add(Street("MAIN ST", "MAIN ST"));

            Premise(5001, "BAKER SCHOOL", 105, "BAKER ST", 40, "P1", "F1");
            Premise(5002, "BAY STATE MARKET", 106, "MAIN ST", 220, string.Empty, string.Empty);
            Premise(5003, "BAYSIDE APARTMENTS", 103, "OAK AVE", 145, string.Empty, string.Empty, sub: string.Empty, unit: "A");
            Premise(5004, "OAK AVE MEDICAL", 103, "OAK AVE", 145, string.Empty, string.Empty);
            Premise(5005, "MAPLE MANOR", 104, "MAPLE DR", 77, string.Empty, string.Empty);
            Pmm2[5001] = 2;
            Pmm4[5002] = (42.6334m, -71.3162m);

            Alc1.Add(new ALC1
            {
                ALC1_CO_NO = 7, ALC1_CO_NM = "SAFEGUARD ALARM CO", ALC1_ADDR_1 = "40 COMMERCE WAY",
                ALC1_CITY = "LOWELL", ALC1_STATE = "MA", ALC1_PHONE_1 = "978", ALC1_PHONE_2 = "5550199"
            });
            Alarm(5002, 1, "1234", 220100, 7);
            Alarm(5002, 2, "21", 410100, 7);

            Run1.Add(new RUN1
            {
                RUN1_RUN_NO = "R7", RUN1_CALL_CODE = 410100, RUN1_COVERAGE = '4', RUN1_LOC_JUR = Jur,
                RUN1_LOC_ST_NAM = "PINE ST", RUN1_LOC_ST_NO = 0, RUN1_LOC_X_ST = "5TH ST",
                RUN1_LOC_SUB_NO = string.Empty, RUN1_LOC_UNIT = string.Empty
            });
        }

        // ------------------------------------------------------------------ IGeoStore

        public IReadOnlyList<STR1> StreetNames(string asortPrefix, bool exact, string jurisdiction) =>
            Str1.Where(s => exact ? Eq(s.STR1_ASORT_NM, asortPrefix) : Starts(s.STR1_ASORT_NM, asortPrefix))
                .Where(s => string.IsNullOrWhiteSpace(jurisdiction) || Eq(s.STR1_STR_JUR, jurisdiction)).ToList();

        public IReadOnlyList<GEO1> SegmentsByStreet(string zipPrefix) => Geo1.Where(g => Starts(g.GEO1_LOC_STR_ZIP, zipPrefix)).ToList();

        public IReadOnlyList<GEO1> SegmentsFromStreet(string zipFrom, int top) =>
            Geo1.Where(g => string.Compare(g.GEO1_LOC_STR_ZIP, zipFrom ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(g => g.GEO1_LOC_STR_ZIP).ThenBy(g => g.GEO1_LOC_ADD_LO_L).Take(top).ToList();

        public bool AnySegmentStreet(string zipPrefix) => Geo1.Any(g => Starts(g.GEO1_LOC_STR_ZIP, zipPrefix));

        public IReadOnlyList<GEO1> SegmentsByCommonName(string zipPrefix) =>
            Geo1.Where(g => !string.IsNullOrWhiteSpace(g.GEO1_C_NAME_ZIP) && Starts(g.GEO1_C_NAME_ZIP, zipPrefix)).ToList();

        public GEO1 Segment(int key) => Geo1.FirstOrDefault(g => g.GEO1_P_KEY == key);

        public IReadOnlyList<GEO1> Segments(IEnumerable<int> keys)
        {
            var set = new HashSet<int>(keys ?? Enumerable.Empty<int>());
            return Geo1.Where(g => set.Contains(g.GEO1_P_KEY)).ToList();
        }

        public IReadOnlyList<GEO2> CrossStreets(IEnumerable<int> segmentKeys)
        {
            var set = new HashSet<int>(segmentKeys ?? Enumerable.Empty<int>());
            return Geo2.Where(x => set.Contains(x.GEO2_P_KEY)).ToList();
        }

        public IReadOnlyList<GEO2> CrossStreetsByName(string zipPrefix) => Geo2.Where(x => Starts(x.GEO2_NAME_ZIP, zipPrefix)).ToList();

        public IReadOnlyList<PMM1> PremisesByName(string spacelessPrefix) => Pmm1.Where(p => Starts(p.PMM1_SPACELESS, spacelessPrefix)).ToList();

        public IReadOnlyList<PMM1> PremisesFromName(string spacelessFrom, int top) =>
            Pmm1.Where(p => p.PMM1_INACTIVE != 'Y' &&
                            string.Compare(p.PMM1_SPACELESS, spacelessFrom ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p.PMM1_SPACELESS).Take(top).ToList();

        public PMM1 Premise(int fop) => Pmm1.FirstOrDefault(p => p.PMM1_FOP == fop);

        public IReadOnlyList<PMM1> PremisesAt(string jurisdiction, string street, decimal streetNo) =>
            Pmm1.Where(p => Eq(p.PMM1_LOC_JUR, jurisdiction) && Eq(p.PMM1_LOC_STR, street) &&
                            p.PMM1_LOC_ADDR == streetNo && p.PMM1_INACTIVE != 'Y').ToList();

        public IReadOnlyList<PMM1> PremisesAtCorner(string jurisdiction, string street, string cross) =>
            Pmm1.Where(p => Eq(p.PMM1_LOC_JUR, jurisdiction) && Eq(p.PMM1_LOC_STR, street) &&
                            Eq(p.PMM1_LOC_X_ST, cross) && p.PMM1_INACTIVE != 'Y').ToList();

        public IReadOnlyList<PMM6> Alarms(string routingPrefix, bool exact) =>
            Pmm6.Where(a => exact ? Eq(a.PMM6_ROUTING_ID, routingPrefix) : Starts(a.PMM6_ROUTING_ID, routingPrefix)).ToList();

        public IReadOnlyList<PMM6> AlarmsFrom(string routingFrom, int top) =>
            Pmm6.Where(a => string.Compare(a.PMM6_ROUTING_ID, routingFrom ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(a => a.PMM6_ROUTING_ID).Take(top).ToList();

        public RUN1 RunCard(string runNo) => Run1.FirstOrDefault(r => Eq(r.RUN1_RUN_NO, runNo));

        public RUN1 RunCard(string runNo, char coverage) =>
            Run1.FirstOrDefault(r => Eq(r.RUN1_RUN_NO, runNo) && r.RUN1_COVERAGE == coverage);

        public ALC1 AlarmCompany(short companyNo) => Alc1.FirstOrDefault(a => a.ALC1_CO_NO == companyNo);

        public (decimal Lat, decimal Long)? PremiseCoordinates(int fop) => Pmm4.TryGetValue(fop, out var xy) ? xy : null;

        public int HazardCount(int fop) => Pmm2.TryGetValue(fop, out var n) ? n : 0;

        public string PremiseName(int fop)
        {
            var p = Premise(fop);
            return p == null ? string.Empty : p.PMM1_NAME_L.Trim();
        }

        // ------------------------------------------------------------------ fixture

        private void Segment(int key, string street, decimal loL, decimal hiL, decimal loR, decimal hiR,
                             string pL, string fL, string eL, string pR, string fR, string eR,
                             short typeCode = 0, string commonName = "")
        {
            Geo1.Add(new GEO1
            {
                GEO1_P_KEY = key,
                GEO1_LOC_STR = street,
                GEO1_LOC_STR_ZIP = Zip(street),
                GEO1_LOC_JUR_L = Jur,
                GEO1_LOC_JUR_R = Jur,
                GEO1_LOC_ADD_LO_L = loL,
                GEO1_LOC_ADD_HI_L = hiL,
                GEO1_LOC_ADD_LO_R = loR,
                GEO1_LOC_ADD_HI_R = hiR,
                GEO1_C_NAME = commonName,
                GEO1_C_NAME_ZIP = Zip(commonName),
                GEO1_SUB_NM = string.Empty,
                GEO1_INT_PT_HI = 'Y',
                GEO1_INT_PT_LO = 'Y',
                GEO1_TYPE_CODE = typeCode,
                GEO1_ADDR_TYPE = 'A',
                GEO1_POLICE_1L = pL, GEO1_POLICE_2L = string.Empty,
                GEO1_FIRE_1L = fL, GEO1_FIRE_2L = string.Empty,
                GEO1_EMS_1L = eL, GEO1_EMS_2L = string.Empty,
                GEO1_OTHER_1L = "S1", GEO1_OTHER_2L = string.Empty, GEO1_TOW_L = "T1",
                GEO1_ZIP_CODE_1L = "01852", GEO1_ZIP_CODE_2L = string.Empty,
                GEO1_POLICE_1R = pR, GEO1_POLICE_2R = string.Empty,
                GEO1_FIRE_1R = fR, GEO1_FIRE_2R = string.Empty,
                GEO1_EMS_1R = eR, GEO1_EMS_2R = string.Empty,
                GEO1_OTHER_1R = "S1", GEO1_OTHER_2R = string.Empty, GEO1_TOW_R = "T1",
                GEO1_ZIP_CODE_1R = "01852", GEO1_ZIP_CODE_2R = string.Empty,
                GEO1_DFLT_SIDE = ' ',
                GEO1_REMARKS = string.Empty
            });
        }

        private void Corner(int key, string cross, char type) => Geo2.Add(new GEO2
        {
            GEO2_P_KEY = key, GEO2_NAME = cross, GEO2_TYPE = type, GEO2_NAME_ZIP = Zip(cross)
        });

        private static STR1 Street(string name, string alias) => new STR1
        {
            STR1_STR_JUR = Jur, STR1_STR_NM = name, STR1_SSORT_NM = Zip(name), STR1_ALIAS_NM = alias, STR1_ASORT_NM = Zip(alias),
            STR1_POLICE_1 = string.Empty, STR1_POLICE_2 = string.Empty, STR1_FIRE_1 = string.Empty, STR1_FIRE_2 = string.Empty,
            STR1_EMS_1 = string.Empty, STR1_EMS_2 = string.Empty, STR1_OTHER_1 = string.Empty, STR1_OTHER_2 = string.Empty,
            STR1_TOW = string.Empty, STR1_REMARKS = string.Empty, STR1_X_STR_PRF = string.Empty, STR1_X_STR_NM = string.Empty,
            STR1_X_STR_TYPE = string.Empty, STR1_X_STR_SUF = string.Empty, STR1_SEND_2_MDC = 'N'
        };

        private void Premise(int fop, string name, int geo, string street, decimal addr, string police, string fire,
                             string sub = "", string unit = "")
        {
            Pmm1.Add(new PMM1
            {
                PMM1_FOP = fop,
                PMM1_NAME_L = name,
                PMM1_NAME_F_M = string.Empty,
                PMM1_SPACELESS = Zip(name),
                PMM1_LOC_JUR = Jur,
                PMM1_LOC_STR = street,
                PMM1_LOC_ADDR = addr,
                PMM1_LOC_SUB_NO = sub,
                PMM1_LOC_UNI_NO = unit,
                PMM1_LOC_X_ST = string.Empty,
                PMM1_LOC_FORMAT = LocationFormatter.Format(addr, street, sub, unit, string.Empty),
                PMM1_POLICE = police,
                PMM1_FIRE = fire,
                PMM1_EMS = string.Empty,
                PMM1_OTHER = string.Empty,
                PMM1_TOW_ZONE = string.Empty,
                PMM1_GEO_NO = geo,
                PMM1_INACTIVE = 'N'
            });
        }

        private void Alarm(int fop, short seq, string routing, int callCode, short company) => Pmm6.Add(new PMM6
        {
            PMM6_FOP = fop, PMM6_SEQ_NO = seq, PMM6_ROUTING_ID = routing, PMM6_CALL_CODE = callCode,
            PMM6_ALC_CO_NO = company, PMM6_INACTIVE = 'N', PMM6_RESPONSE = 'Y', PMM6_PERM_NO = string.Empty,
            PMM6_REMARKS = string.Empty
        });

        private static string Zip(string text) => SearchPhrase.Squash(text);

        private static bool Starts(string value, string prefix) =>
            (value ?? string.Empty).StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        private static bool Eq(string a, string b) =>
            string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
