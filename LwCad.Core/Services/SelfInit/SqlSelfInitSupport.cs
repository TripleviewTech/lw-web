using System;
using System.Collections.Generic;
using System.Linq;
using LaTableMlb1;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwTableCad7;
using LwTableRst1;
using Microsoft.Data.SqlClient;

namespace LwCadSystemUX.Services.SelfInit
{
    /// <summary>
    /// Self Initiate / Traffic Stop / Activate Unit lookups on the live tables. The
    /// legacy forms concatenated field text into their SQL; everything here is
    /// parameterised. A table or function missing on a test copy reads as "not
    /// found" and is logged.
    /// </summary>
    public sealed class SqlSelfInitSupport : ISelfInitSupport
    {
        private readonly string _connectString;
        private readonly Rst1_RW _rst1;

        public SqlSelfInitSupport(string connectString)
        {
            _connectString = connectString;
            _rst1 = new Rst1_RW(connectString);

            var cad7 = new Cad7_RW(connectString).GetList(top: 1).FirstOrDefault() ?? new CAD7();
            Settings = new SelfInitSettings
            {
                SelfArrive = Switch(cad7.CAD7_SELF_E),
                TrafficArrive = Switch(cad7.CAD7_TRAF_E)
            };

            var mlb1 = new Mlb1_RW(connectString);
            UnitTypes = mlb1.GetLibrary(5, 4).OrderBy(p => p.Key).Select(p => new LookupItem(p.Key.ToString(), p.Value)).ToList();
            Stations = mlb1.GetLibrary(5, 10).OrderBy(p => p.Key).Select(p => new LookupItem(p.Key.ToString(), p.Value)).ToList();

            Agencies = Query("SELECT AGC1_AGC_CODE, AGC1_AGC_TYPE, AGC1_RST_DFLT FROM dbo.AGC1 (NOLOCK) " +
                             "WHERE AGC1_LIC_AGC = 'Y' ORDER BY AGC1_AGC_CODE",
                             Array.Empty<(string, object)>(),
                             r => new AgencyItem(Str(r, 0), Chr(r, 1), Chr(r, 2) == 'Y'));

            // Cad129 GeneralInitialization: GEO or E911 jurisdictions that take units.
            UnitJurisdictions = Query("SELECT JUR1_JUR_CODE, JUR1_P_NAME, JUR1_CAD_DFLT FROM dbo.JUR1 (NOLOCK) " +
                                      "WHERE (JUR1_GEO_JUR = 'Y' OR JUR1_E911_JUR = 'Y') AND JUR1_UNITS = 'Y' " +
                                      "ORDER BY CASE WHEN JUR1_CAD_DFLT = 'Y' THEN 0 ELSE 1 END, JUR1_JUR_CODE",
                                      Array.Empty<(string, object)>(),
                                      r => new LookupItem(Str(r, 0), Str(r, 1)));
        }

        public SelfInitSettings Settings { get; }
        public IReadOnlyList<AgencyItem> Agencies { get; }
        public IReadOnlyList<LookupItem> UnitTypes { get; }
        public IReadOnlyList<LookupItem> Stations { get; }
        public IReadOnlyList<LookupItem> UnitJurisdictions { get; }

        public RosterUnit FindUnit(string unitNo)
        {
            var unit = (unitNo ?? string.Empty).Trim();
            if (unit.Length == 0) return null;

            var row = _rst1.GetList("RTRIM(RST1_UNIT_NO) = @unit", new { unit }, top: 1).FirstOrDefault();
            if (row == null) return null;

            return new RosterUnit
            {
                UnitNo = T(row.RST1_UNIT_NO),
                AgencyType = char.ToUpperInvariant(row.RST1_AGC_TYPE),
                Agency = T(row.RST1_AGENCY),
                Jurisdiction = T(row.RST1_JUR),
                Officer1 = row.RST1_OFCR_1,
                Officer1Name = row.RST1_OFCR_1 > 0 ? PersonName(row.RST1_OFCR_1) : string.Empty,
                Officer2 = row.RST1_OFCR_2,
                Officer2Name = row.RST1_OFCR_2 > 0 ? PersonName(row.RST1_OFCR_2) : string.Empty,
                Status = row.RST1_STATUS,
                EventType = row.RST1_EVENT_TYPE,
                EventNo1 = row.RST1_EVENT_NO_1,
                EventNo2 = row.RST1_EVENT_NO_2,
                DispNo2 = row.RST1_DISP_NO_2,
                LockId = row.RST1_LOCK_ID
            };
        }

        /// <summary>SelfInit_ValUnitType and Cad129's UNT1 pre-fill, in one read.</summary>
        public UnitDefaults UnitDefaults(string unitNo)
        {
            var unit = (unitNo ?? string.Empty).Trim();
            if (unit.Length == 0) return null;

            return Query(
                "SELECT UNT1_AGCY, ISNULL(AGC1_AGC_TYPE, ' '), UNT1_DFLT_JUR, UNT1_STATION, UNT1_U_TYPE_C, " +
                "UNT1_EMP_ID_1, UNT1_EMP_ID_2, UNT1_R_AREA_1, UNT1_R_AREA_2, UNT1_R_AREA_3, UNT1_R_AREA_4, UNT1_R_AREA_5 " +
                "FROM dbo.UNT1 (NOLOCK) LEFT JOIN dbo.AGC1 (NOLOCK) ON AGC1_AGC_CODE = UNT1_AGCY " +
                "WHERE RTRIM(UNT1_UNIT_NO) = @unit",
                new (string, object)[] { ("@unit", unit) },
                r => new UnitDefaults
                {
                    UnitNo = unit,
                    Agency = Str(r, 0),
                    AgencyType = char.ToUpperInvariant(Chr(r, 1)),
                    Jurisdiction = Str(r, 2),
                    Station = Str(r, 3),
                    UnitType = r.IsDBNull(4) ? (short)0 : Convert.ToInt16(r.GetValue(4)),
                    Employee1 = r.IsDBNull(5) ? 0 : Convert.ToInt32(r.GetValue(5)),
                    Employee2 = r.IsDBNull(6) ? 0 : Convert.ToInt32(r.GetValue(6)),
                    Areas = Enumerable.Range(7, 5).Select(i => Str(r, i)).Where(a => a.Length > 0).ToArray()
                }).FirstOrDefault();
        }

        public string PersonName(int employeeId)
        {
            if (employeeId <= 0) return string.Empty;

            var name = Scalar("SELECT dbo.la_fn_GetPerName(@id)", ("@id", employeeId)) as string;
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

            // A copy without the LA functions: the name from PER1 itself.
            name = Scalar("SELECT RTRIM(PER1_NM_L) + ', ' + RTRIM(PER1_NM_F) FROM dbo.PER1 (NOLOCK) WHERE PER1_CODE = @id",
                          ("@id", employeeId)) as string;
            return (name ?? string.Empty).Trim().TrimEnd(',');
        }

        public IReadOnlyList<string> ProfileList(int library)
        {
            // TraffStop_Loaded, with the blank first on every list - the legacy put
            // Sector's blank on the Sex list by mistake (TraffStop.cs:150).
            var list = new List<string> { string.Empty };
            list.AddRange(Query("SELECT MLB2_CODE_DESC FROM dbo.MLB2 (NOLOCK) WHERE MLB2_SYS_NO = 5 AND MLB2_LIB_NO = @lib " +
                                "ORDER BY MLB2_CODE",
                                new (string, object)[] { ("@lib", library) },
                                r => Str(r, 0))
                              .Where(s => s.Length > 0));
            return list;
        }

        public string TrafficStopCallCode()
        {
            // TraffStop_SetInitialize took TOP 1 with no order; the lowest code is at
            // least the same one every time.
            var code = Scalar("SELECT TOP 1 CCC1_CODE FROM dbo.CCC1 (NOLOCK) WHERE CCC1_TS_CODE = 'Y' ORDER BY CCC1_CODE");
            return code == null ? string.Empty : Convert.ToString(code);
        }

        // ================================================================ helpers

        private static char Switch(char value) => value == 'A' || value == 'P' ? value : 'E';

        private object Scalar(string sql, params (string Name, object Value)[] args)
        {
            try
            {
                using var connection = new SqlConnection(_connectString);
                connection.Open();
                using var command = new SqlCommand(sql, connection);
                foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                var result = command.ExecuteScalar();
                return result == DBNull.Value ? null : result;
            }
            catch (SqlException ex)
            {
                LaLog.Error("SelfInit", "Lookup failed: " + sql, ex);
                return null;
            }
        }

        private IReadOnlyList<T> Query<T>(string sql, (string Name, object Value)[] args, Func<SqlDataReader, T> map)
        {
            var list = new List<T>();
            try
            {
                using var connection = new SqlConnection(_connectString);
                connection.Open();
                using var command = new SqlCommand(sql, connection);
                foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                using var reader = command.ExecuteReader();
                while (reader.Read()) list.Add(map(reader));
            }
            catch (SqlException ex)
            {
                LaLog.Error("SelfInit", "Query failed: " + sql, ex);
            }

            return list;
        }

        private static string Str(SqlDataReader r, int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i)).Trim();

        private static char Chr(SqlDataReader r, int i)
        {
            var s = r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i));
            return s.Length > 0 ? s[0] : ' ';
        }

        private static string T(string value) => (value ?? string.Empty).Trim();
    }
}
