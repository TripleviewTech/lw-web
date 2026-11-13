using System;
using System.Collections.Generic;
using System.Linq;
using LaTableAlc1;
using LaTableGeo1;
using LaTableGeo2;
using LaTableStr1;
using LwCadSystemUX.Services.Data;
using LwTablePmm1;
using LwTablePmm2;
using LwTablePmm4;
using LwTablePmm6;
using LwTableRun1;
using Microsoft.Data.SqlClient;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// The live geobase and premise tables. Every query is parameterised - the
    /// legacy LaGeo04x screens concatenated what the operator typed into their SQL
    /// - and bounded, since the engine only needs candidates to apply its rules to.
    /// </summary>
    public sealed class SqlGeoStore : IGeoStore
    {
        private const int Bound = 2000;

        private readonly string _connectString;
        private readonly Geo1_RW _geo1;
        private readonly Geo2_RW _geo2;
        private readonly Str1_RW _str1;
        private readonly Pmm1_RW _pmm1;
        private readonly Pmm2_RW _pmm2;
        private readonly Pmm4_RW _pmm4;
        private readonly Pmm6_RW _pmm6;
        private readonly Run1_RW _run1;
        private readonly Alc1_RW _alc1;

        public SqlGeoStore(string connectString)
        {
            _connectString = connectString;
            _geo1 = new Geo1_RW(connectString);
            _geo2 = new Geo2_RW(connectString);
            _str1 = new Str1_RW(connectString);
            _pmm1 = new Pmm1_RW(connectString);
            _pmm2 = new Pmm2_RW(connectString);
            _pmm4 = new Pmm4_RW(connectString);
            _pmm6 = new Pmm6_RW(connectString);
            _run1 = new Run1_RW(connectString);
            _alc1 = new Alc1_RW(connectString);
        }

        /// <summary>The last read that failed - surfaced so a broken geobase is not mistaken for "no match".</summary>
        public string LastError { get; private set; } = string.Empty;

        public IReadOnlyList<STR1> StreetNames(string asortPrefix, bool exact, string jurisdiction)
        {
            var where = exact ? "STR1_ASORT_NM = @name" : "STR1_ASORT_NM LIKE @name";
            if (!string.IsNullOrWhiteSpace(jurisdiction)) where += " AND STR1_STR_JUR = @jur";
            return Note(_str1, _str1.GetList(where, new { name = exact ? asortPrefix : Like(asortPrefix), jur = Trim(jurisdiction) }, top: Bound));
        }

        public IReadOnlyList<GEO1> SegmentsByStreet(string zipPrefix) =>
            Note(_geo1, _geo1.GetList("GEO1_LOC_STR_ZIP LIKE @p", new { p = Like(zipPrefix) }, "GEO1_LOC_STR, GEO1_LOC_ADD_LO_L", Bound));

        public IReadOnlyList<GEO1> SegmentsFromStreet(string zipFrom, int top) =>
            Note(_geo1, _geo1.GetList("GEO1_LOC_STR_ZIP >= @p", new { p = zipFrom ?? string.Empty },
                                      "GEO1_LOC_STR_ZIP, GEO1_LOC_ADD_LO_L", top));

        public bool AnySegmentStreet(string zipPrefix) =>
            _geo1.GetList("GEO1_LOC_STR_ZIP LIKE @p", new { p = Like(zipPrefix) }, top: 1).Count > 0;

        public IReadOnlyList<GEO1> SegmentsByCommonName(string zipPrefix) =>
            Note(_geo1, _geo1.GetList("GEO1_C_NAME_ZIP LIKE @p AND GEO1_C_NAME_ZIP > ''", new { p = Like(zipPrefix) },
                                      "GEO1_C_NAME_ZIP", Bound));

        public GEO1 Segment(int key)
        {
            if (key <= 0) return null;
            var row = _geo1.GetRow(new GEO1 { GEO1_P_KEY = key });
            return _geo1.RecordStatus == 0 ? row : null;
        }

        public IReadOnlyList<GEO1> Segments(IEnumerable<int> keys) =>
            InBatches(keys, batch => _geo1.GetList("GEO1_P_KEY IN (" + batch + ")"));

        public IReadOnlyList<GEO2> CrossStreets(IEnumerable<int> segmentKeys) =>
            InBatches(segmentKeys, batch => _geo2.GetList("GEO2_P_KEY IN (" + batch + ")"));

        public IReadOnlyList<GEO2> CrossStreetsByName(string zipPrefix) =>
            Note(_geo2, _geo2.GetList("GEO2_NAME_ZIP LIKE @p", new { p = Like(zipPrefix) }, top: Bound));

        public IReadOnlyList<PMM1> PremisesByName(string spacelessPrefix) =>
            Note(_pmm1, _pmm1.GetList("PMM1_SPACELESS LIKE @p", new { p = Like(spacelessPrefix) }, "PMM1_SPACELESS", Bound));

        public IReadOnlyList<PMM1> PremisesFromName(string spacelessFrom, int top) =>
            Note(_pmm1, _pmm1.GetList("PMM1_INACTIVE <> 'Y' AND PMM1_SPACELESS >= @p", new { p = spacelessFrom ?? string.Empty },
                                      "PMM1_SPACELESS", top));

        public PMM1 Premise(int fop)
        {
            if (fop <= 0) return null;
            var row = _pmm1.GetRow(new PMM1 { PMM1_FOP = fop });
            return _pmm1.RecordStatus == 0 ? row : null;
        }

        public IReadOnlyList<PMM1> PremisesAt(string jurisdiction, string street, decimal streetNo) =>
            Note(_pmm1, _pmm1.GetList(
                "PMM1_LOC_JUR = @jur AND PMM1_LOC_STR = @str AND RTRIM(PMM1_LOC_STR) <> '' AND PMM1_LOC_ADDR = @no AND PMM1_INACTIVE <> 'Y'",
                new { jur = Trim(jurisdiction), str = Trim(street), no = streetNo }, top: 50));

        public IReadOnlyList<PMM1> PremisesAtCorner(string jurisdiction, string street, string cross) =>
            Note(_pmm1, _pmm1.GetList(
                "PMM1_LOC_JUR = @jur AND PMM1_LOC_STR = @str AND RTRIM(PMM1_LOC_STR) <> '' AND PMM1_LOC_X_ST = @x AND PMM1_INACTIVE <> 'Y'",
                new { jur = Trim(jurisdiction), str = Trim(street), x = Trim(cross) }, top: 50));

        public IReadOnlyList<PMM6> Alarms(string routingPrefix, bool exact) =>
            Note(_pmm6, _pmm6.GetList(exact ? "PMM6_ROUTING_ID = @r" : "PMM6_ROUTING_ID LIKE @r",
                                      new { r = exact ? Trim(routingPrefix) : Like(Trim(routingPrefix)) }, "PMM6_ROUTING_ID", Bound));

        public IReadOnlyList<PMM6> AlarmsFrom(string routingFrom, int top) =>
            Note(_pmm6, _pmm6.GetList("PMM6_ROUTING_ID >= @r", new { r = routingFrom ?? string.Empty }, "PMM6_ROUTING_ID", top));

        public RUN1 RunCard(string runNo) =>
            _run1.GetList("RUN1_RUN_NO = @r", new { r = Trim(runNo) }, top: 1).FirstOrDefault();

        public RUN1 RunCard(string runNo, char coverage) =>
            _run1.GetList("RUN1_RUN_NO = @r AND RUN1_COVERAGE = @c", new { r = Trim(runNo), c = coverage.ToString() }, top: 1).FirstOrDefault();

        public ALC1 AlarmCompany(short companyNo)
        {
            var row = _alc1.GetRow(new ALC1 { ALC1_CO_NO = companyNo });
            return _alc1.RecordStatus == 0 ? row : null;
        }

        public (decimal Lat, decimal Long)? PremiseCoordinates(int fop)
        {
            var row = _pmm4.GetList("PMM4_FOP = @f", new { f = fop }, top: 1).FirstOrDefault();
            return row == null ? null : (row.PMM4_LAT, row.PMM4_LONG);
        }

        public int HazardCount(int fop) => _pmm2.GetList("PMM2_FOP = @f", new { f = fop }, top: 1).Count;

        /// <summary>dbo.la_fn_GetPMM1Name when the database has it, else the premise's own name.</summary>
        public string PremiseName(int fop)
        {
            if (fop <= 0) return string.Empty;

            try
            {
                using var connection = new SqlConnection(_connectString);
                connection.Open();
                using var command = new SqlCommand("SELECT dbo.[la_fn_GetPMM1Name](@f)", connection);
                command.Parameters.AddWithValue("@f", fop);
                var value = command.ExecuteScalar();
                if (value != null && value != DBNull.Value) return Convert.ToString(value).Trim();
            }
            catch (SqlException)
            {
                // The function is missing on test copies of the database; fall through.
            }

            var p = Premise(fop);
            if (p == null) return string.Empty;
            return string.IsNullOrWhiteSpace(p.PMM1_NAME_F_M)
                ? Trim(p.PMM1_NAME_L)
                : Trim(p.PMM1_NAME_L) + ", " + Trim(p.PMM1_NAME_F_M);
        }

        // ---------------------------------------------------------------- helpers

        private IReadOnlyList<T> Note<T>(LaTable<T> table, List<T> rows) where T : class, new()
        {
            if (table.RecordStatus == 1 && !string.IsNullOrEmpty(table.ErrorMsg)) LastError = table.ErrorMsg;
            return rows;
        }

        private static IReadOnlyList<T> InBatches<T>(IEnumerable<int> keys, Func<string, List<T>> fetch)
        {
            var list = (keys ?? Enumerable.Empty<int>()).Where(k => k > 0).Distinct().ToList();
            var rows = new List<T>();

            // Integers only, so the IN list cannot carry anything but numbers.
            for (int i = 0; i < list.Count; i += 500)
            {
                rows.AddRange(fetch(string.Join(",", list.Skip(i).Take(500))));
            }

            return rows;
        }

        /// <summary>A literal LIKE prefix: the SQL wildcards in the text are escaped, then % appended.</summary>
        private static string Like(string prefix) =>
            (prefix ?? string.Empty).Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";

        private static string Trim(string value) => (value ?? string.Empty).Trim();
    }
}
