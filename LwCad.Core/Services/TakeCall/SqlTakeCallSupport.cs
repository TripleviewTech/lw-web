using System;
using System.Collections.Generic;
using System.Linq;
using LaTableJur1;
using LaTableMlb1;
using LaTableMlb2;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services.Data;
using LwCadSystemUX.Services.Location;
using LwTableCad7;
using LwTableCcc2;
using LwTableEmg2;
using LwTableStk2;
using Microsoft.Data.SqlClient;

namespace LwCadSystemUX.Services.TakeCall
{
    /// <summary>
    /// Take Call's lookups on the live tables. The legacy screen built its SQL by
    /// concatenating field text; every query here is parameterised.
    /// </summary>
    public sealed class SqlTakeCallSupport : ITakeCallSupport
    {
        private readonly string _connectString;
        private readonly CadWriteBack _writer;
        private readonly Mlb2_RW _mlb2;
        private readonly Jur1_RW _jur1;
        private readonly Ccc2_RW _ccc2;
        private readonly Emg2_RW _emg2;
        private readonly Stk2_RW _stk2;

        public SqlTakeCallSupport(string connectString, CadWriteBack writer)
        {
            _connectString = connectString;
            _writer = writer;
            _mlb2 = new Mlb2_RW(connectString);
            _jur1 = new Jur1_RW(connectString);
            _ccc2 = new Ccc2_RW(connectString);
            _emg2 = new Emg2_RW(connectString);
            _stk2 = new Stk2_RW(connectString);

            var cad7 = new Cad7_RW(connectString).GetList(top: 1).FirstOrDefault() ?? new CAD7();
            var defaultJur = _jur1.GetList("JUR1_GEO_JUR = 'Y' AND JUR1_CAD_DFLT = 'Y'", top: 1).FirstOrDefault();

            Settings = new TakeCallSettings
            {
                AllowPolice = cad7.CAD7_CAD_P == 'Y',
                AllowFire = cad7.CAD7_CAD_F == 'Y',
                AllowEms = cad7.CAD7_CAD_E == 'Y',
                AllowOther = cad7.CAD7_CAD_O == 'Y',
                DuplicateCheck = cad7.CAD7_DUP_SW == 'Y',
                DuplicateRange = cad7.CAD7_DUP_RANGE > 0 ? cad7.CAD7_DUP_RANGE : (short)0,
                E911Transfer = cad7.CAD7_E911_XFER == 'A' || cad7.CAD7_E911_XFER == 'P' ? cad7.CAD7_E911_XFER : 'N',
                Visual911 = cad7.CAD7_VISUAL_911 == 'Y',
                VisualHistory = cad7.CAD7_VISUAL_HIS == 'Y',
                VisualPatrol = cad7.CAD7_VISUAL_PAT == 'Y',
                Play911 = cad7.CAD7_PLAY_911 == 'Y',
                PlayHistory = cad7.CAD7_PLAY_HIS == 'Y',
                PlayPatrol = cad7.CAD7_PLAY_PAT == 'Y',
                AttachSinglePremise = cad7.CAD7_PREM_SW == 'Y',
                DefaultJurisdiction = T(defaultJur?.JUR1_JUR_CODE),
                DefaultCity = T(defaultJur?.JUR1_P_NAME),
                DefaultState = string.IsNullOrWhiteSpace(defaultJur?.JUR1_STATE) ? "MA" : T(defaultJur.JUR1_STATE)
            };

            Locations = new LocationEngine(new SqlGeoStore(connectString), new LocationSettings
            {
                AttachSinglePremise = Settings.AttachSinglePremise,
                DefaultJurisdiction = Settings.DefaultJurisdiction
            });

            var mlb1 = new Mlb1_RW(connectString);
            Dispositions = mlb1.GetLibrary(5, 1)
                .OrderBy(p => p.Key)
                .Select(p => new LookupItem(p.Key.ToString(), p.Value))
                .ToList();

            States = Library(50, 1);
            PlateTypes = Library(50, 22);
        }

        public TakeCallSettings Settings { get; }
        public LocationEngine Locations { get; }
        public IReadOnlyList<LookupItem> Dispositions { get; }
        public IReadOnlyList<LookupItem> States { get; }
        public IReadOnlyList<LookupItem> PlateTypes { get; }

        /// <summary>GEO_JUR jurisdictions, for the picker - Take Call's JUR1 list (TC:48-73).</summary>
        public IReadOnlyList<LookupItem> Jurisdictions() =>
            _jur1.GetList("JUR1_GEO_JUR = 'Y'", orderBy: "JUR1_JUR_CODE")
                 .Select(j => new LookupItem(T(j.JUR1_JUR_CODE), T(j.JUR1_P_NAME)))
                 .ToList();

        public string CityFor(string jurisdiction)
        {
            var row = _jur1.GetRow(new JUR1 { JUR1_JUR_CODE = T(jurisdiction) });
            return _jur1.RecordStatus == 0 ? T(row.JUR1_P_NAME) : string.Empty;
        }

        public string VehicleError(string make, string model, string style, string color)
        {
            if (!Known("MK", make, model, style, color, make)) return "Vehicle Make is invalid.";
            if (!Known("MD", make, model, style, color, model)) return "Vehicle Model is invalid.";
            if (!Known("ST", make, model, style, color, style)) return "Vehicle Style is invalid.";
            if (!Known("CL", make, model, style, color, color)) return "Vehicle Color is invalid.";
            return string.Empty;
        }

        /// <summary>dbo.la_fn_GetMMC1Desc when present, else the MMC1 table itself.</summary>
        private bool Known(string type, string make, string model, string style, string color, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;

            var desc = Scalar("SELECT dbo.[la_fn_GetMMC1Desc](@t, @mk, @md, @st, @cl)",
                              ("@t", type), ("@mk", T(make)), ("@md", T(model)), ("@st", T(style)), ("@cl", T(color)));
            if (desc != null) return !string.IsNullOrWhiteSpace(Convert.ToString(desc));

            string where;
            switch (type)
            {
                case "MK": where = "MMC1_MAKE = @mk"; break;
                case "MD": where = "MMC1_MAKE = @mk AND MMC1_MODEL = @md"; break;
                case "ST": where = "MMC1_STYLE = @st"; break;
                default: where = "MMC1_COLOR = @cl"; break;
            }

            var count = Scalar("SELECT COUNT(*) FROM dbo.MMC1 (NOLOCK) WHERE " + where,
                               ("@mk", T(make)), ("@md", T(model)), ("@st", T(style)), ("@cl", T(color)));

            // No MMC1 table at all on a test copy: do not block the call on it.
            return count == null || Convert.ToInt32(count) > 0;
        }

        /// <summary>
        /// CheckForDuplicate: stacked calls in the jurisdiction on the same street
        /// within CAD7_DUP_RANGE of the number, or at the same corner in either order.
        /// </summary>
        public IReadOnlyList<CallSummary> Duplicates(LocationResult place)
        {
            if (!Settings.DuplicateCheck || place == null || place.Forced == 'X' || place.Street.Length == 0)
            {
                return Array.Empty<CallSummary>();
            }

            string where;
            var args = new List<(string, object)> { ("@jur", place.Jurisdiction), ("@st", place.Street) };

            if (place.Cross.Length == 0)
            {
                var low = Math.Max(0, place.StreetNo - Settings.DuplicateRange);
                var high = place.StreetNo + Settings.DuplicateRange;
                where = "RTRIM(STK1_LOC_JUR) = @jur AND RTRIM(STK1_LOC_ST_NAM) = @st AND STK1_LOC_ST_NO BETWEEN @lo AND @hi";
                args.Add(("@lo", low));
                args.Add(("@hi", high));
            }
            else
            {
                // STK1 files a corner under whichever street sorts first.
                var a = place.Street;
                var b = place.Cross;
                if (string.CompareOrdinal(b, a) < 0) (a, b) = (b, a);
                where = "RTRIM(STK1_LOC_JUR) = @jur AND RTRIM(STK1_LOC_ST_NAM) = @a AND RTRIM(STK1_LOC_X_ST) = @b";
                args.Add(("@a", a));
                args.Add(("@b", b));
            }

            return Query(
                "SELECT TOP 50 STK1_EVENT_TYPE, STK1_EVENT_NO_1, STK1_EVENT_NO_2, STK1_RCV_DATE, " +
                "ISNULL(RTRIM(CCC1_DESC), CAST(STK1_CALL_CODE AS varchar(10))), STK1_LOCATION, STK1_STATUS " +
                "FROM dbo.STK1 (NOLOCK) LEFT JOIN dbo.CCC1 (NOLOCK) ON CCC1_CODE = STK1_CALL_CODE WHERE " + where +
                " ORDER BY STK1_RCV_DATE DESC",
                args.ToArray(),
                r => new CallSummary
                {
                    EventNo = r.GetString(0).Trim() + " " + Convert.ToInt32(r.GetValue(1)).ToString("0000") + "-" +
                              Convert.ToInt32(r.GetValue(2)).ToString("00000000"),
                    Received = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                    CallType = r.GetString(4).Trim(),
                    Location = r.IsDBNull(5) ? string.Empty : r.GetString(5).Trim(),
                    Status = StackStatus(r.IsDBNull(6) ? " " : r.GetString(6))
                });
        }

        /// <summary>
        /// LwCad030: prior calls at the address (street and number) or at the corner
        /// (either order), for call codes marked CCC1_DISP_HIST.
        /// </summary>
        public IReadOnlyList<CallSummary> History(LocationResult place)
        {
            if (place == null || place.Street.Length == 0 || place.Forced == 'X') return Array.Empty<CallSummary>();
            if (place.StreetNo <= 0 && place.Cross.Length == 0) return Array.Empty<CallSummary>();

            var where = place.Cross.Length == 0
                ? "DSE1_LOC_ST_NAM = @st AND DSE1_LOC_ST_NO = @no"
                : "((DSE1_LOC_ST_NAM = @st AND DSE1_LOC_X_ST = @x) OR (DSE1_LOC_ST_NAM = @x AND DSE1_LOC_X_ST = @st))";

            return Query(
                "SELECT TOP 50 DSE1_EVENT_TYPE, DSE1_EVENT_NO_1, DSE1_EVENT_NO_2, DSE1_RCV_DATE, " +
                "ISNULL(RTRIM(CCC1_DESC), CAST(DSE1_CALL_CODE AS varchar(10))), DSE1_LOC_ST_NO, DSE1_LOC_ST_NAM, DSE1_LOC_X_ST, DSE1_CMP_DATE " +
                "FROM dbo.DSE1 (NOLOCK) JOIN dbo.CCC1 (NOLOCK) ON CCC1_CODE = DSE1_CALL_CODE " +
                "WHERE DSE1_LOC_JUR = @jur AND " + where + " AND CCC1_DISP_HIST = 'Y' " +   // LwCad030 TabA:188 - only codes flagged for history
                "ORDER BY DSE1_RCV_DATE DESC",
                new (string, object)[] { ("@jur", place.Jurisdiction), ("@st", place.Street), ("@no", place.StreetNo), ("@x", place.Cross) },
                r => new CallSummary
                {
                    EventNo = r.GetString(0).Trim() + " " + Convert.ToInt32(r.GetValue(1)).ToString("0000") + "-" +
                              Convert.ToInt32(r.GetValue(2)).ToString("00000000"),
                    Received = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3),
                    CallType = r.GetString(4).Trim(),
                    Location = LocationFormatter.Format(r.IsDBNull(5) ? 0 : r.GetDecimal(5), r.IsDBNull(6) ? string.Empty : r.GetString(6),
                                                        string.Empty, string.Empty, r.IsDBNull(7) ? string.Empty : r.GetString(7)),
                    Status = r.IsDBNull(8) ? "Open" : "Closed"
                });
        }

        public PatrolHit Patrol(LocationResult place, string lastName, string firstName, string middleName,
                                string plate, string plateState)
        {
            var hasPlace = place != null && place.Jurisdiction.Length > 0 && place.Street.Length > 0 && place.StreetNo > 0;
            if (!hasPlace && string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(plate)) return new PatrolHit();

            var level = Scalar("SELECT dbo.[la_fn_ValidatePTL1HitLevel](@jur, @st, @no, @sub, @unit, @l, @f, @m, @plate, @ps)",
                               ("@jur", place?.Jurisdiction ?? string.Empty), ("@st", place?.Street ?? string.Empty),
                               ("@no", place?.StreetNo ?? 0m), ("@sub", place?.Sub ?? string.Empty), ("@unit", place?.Unit ?? string.Empty),
                               ("@l", T(lastName)), ("@f", T(firstName)), ("@m", T(middleName)),
                               ("@plate", T(plate)), ("@ps", T(plateState)));

            return PatrolHit.FromLevel(level == null ? 0 : Convert.ToInt32(level));
        }

        public IReadOnlyList<SopEntry> Sops(int callCode, bool police, bool fire, bool ems, bool other)
        {
            if (callCode <= 0) return Array.Empty<SopEntry>();

            return _ccc2.GetList("CCC2_CODE = @c", new { c = callCode })
                .Where(s => (police && s.CCC2_TYPE == 'P') || (fire && s.CCC2_TYPE == 'F') ||
                            (ems && s.CCC2_TYPE == 'E') || (other && s.CCC2_TYPE == 'O'))
                .Select(s => new SopEntry { Type = s.CCC2_TYPE, Text = T(s.CCC2_SOP) })
                .ToList();
        }

        /// <summary>
        /// TakeCall_CheckForE911 / TakeCall_LoadE911Call: the EMG2 row for the 911
        /// position, with the jurisdiction from JUR3 by ESN or JUR1 by the city alias.
        /// </summary>
        public E911Call PendingE911(short position)
        {
            if (position <= 0) return null;

            var row = _emg2.GetList("EMG2_CAD_POS = @p AND EMG2_EVENT_NO_1 = 0", new { p = position }, top: 1).FirstOrDefault();
            if (row == null) return null;

            var jur = string.Empty;
            if (row.EMG2_ESN > 0)
            {
                jur = Convert.ToString(Scalar("SELECT TOP 1 JUR3_JUR FROM dbo.JUR3 (NOLOCK) WHERE JUR3_ESN = @e", ("@e", row.EMG2_ESN)));
            }
            else if (!string.IsNullOrWhiteSpace(row.EMG2_CITY))
            {
                jur = Convert.ToString(Scalar("SELECT TOP 1 JUR1_JUR_CODE FROM dbo.JUR1 (NOLOCK) WHERE JUR1_ALIAS = @c", ("@c", T(row.EMG2_CITY))));
            }

            // "LAST, FIRST MIDDLE"
            var name = (row.EMG2_NAME ?? string.Empty).Split(',');
            var last = name.Length > 0 ? name[0].Trim() : string.Empty;
            var rest = name.Length > 1 ? name[1].Trim().Split(' ') : Array.Empty<string>();

            return new E911Call
            {
                CadPosition = row.EMG2_CAD_POS,
                Position = row.EMG2_POS,
                Phone1 = row.EMG2_PHONE_1,
                Phone2 = row.EMG2_PHONE_2,
                Phone3 = row.EMG2_PHONE_3,
                LastName = last,
                FirstName = rest.Length > 0 ? rest[0].Trim() : string.Empty,
                MiddleName = rest.Length > 1 ? rest[1].Trim() : string.Empty,
                StreetNo = row.EMG2_ST_NO,
                Street = T(row.EMG2_ST_NAM),
                Unit = T(row.EMG2_UNIT),
                City = T(row.EMG2_CITY),
                State = T(row.EMG2_STATE) == "XX" ? string.Empty : T(row.EMG2_STATE),
                Jurisdiction = T(jur),
                Received = row.EMG2_RCV_DATE,
                XLow = row.EMG2_XLOW,
                YLow = row.EMG2_YLOW,
                XHigh = row.EMG2_XHIGH,
                YHigh = row.EMG2_YHIGH
            };
        }

        public void ClearE911(E911Call call)
        {
            if (call == null) return;

            Execute("DELETE FROM dbo.EMG2 WHERE EMG2_POS = @pos AND EMG2_PHONE_1 = @p1 AND EMG2_PHONE_2 = @p2 AND EMG2_PHONE_3 = @p3",
                    ("@pos", call.Position), ("@p1", call.Phone1), ("@p2", call.Phone2), ("@p3", call.Phone3));
        }

        /// <summary>TakeCall_CreateSTK2 - WriteRow, then TSK1 403 so the maps show the call being taken.</summary>
        public long SaveMapRecord(MapRecord record, long existingKey)
        {
            if (record?.Place == null) return existingKey;

            var place = record.Place;
            var stk2 = _stk2.Initialize(new STK2());
            stk2.STK2_S_KEY = existingKey;
            stk2.STK2_POSITION = record.Position;
            stk2.STK2_RCV_DATE = record.Received;
            stk2.STK2_RCV_ID = record.ReceivedBy;
            stk2.STK2_PRIORITY = record.Priority;
            stk2.STK2_CALL_CODE = record.CallCode;
            stk2.STK2_AREA = record.Area;
            stk2.STK2_LOCATION = LocationFormatter.Format(place.StreetNo, place.Street, place.Sub, place.Unit, place.Cross);
            stk2.STK2_LOC_JUR = place.Jurisdiction;
            stk2.STK2_LOC_ST_NAM = place.Street;
            stk2.STK2_LOC_ST_NO = place.StreetNo;
            stk2.STK2_LOC_SUB_NO = place.Sub;
            stk2.STK2_LOC_UNIT = place.Unit;
            stk2.STK2_LOC_X_ST = place.Cross;
            stk2.STK2_FORCED = place.Forced;
            stk2.STK2_PREMISE = place.PremiseNo > 0 ? place.PremiseName : string.Empty;
            stk2.STK2_X_CORD = place.X;
            stk2.STK2_Y_CORD = place.Y;
            stk2.STK2_PER_NAME_L = record.LastName;
            stk2.STK2_PER_NAME_F = record.FirstName;
            stk2.STK2_PER_NAME_M = record.MiddleName;
            stk2.STK2_PER_PHONE_AC = record.PhoneArea;
            stk2.STK2_PER_PHONE_1 = record.Phone1;
            stk2.STK2_PER_PHONE_2 = record.Phone2;
            stk2.STK2_PC_NAME = record.PcName;

            if (existingKey > 0)
            {
                _stk2.UpdateRow(stk2);
                if (_stk2.RecordStatus != 0) return existingKey;
            }
            else
            {
                _stk2.InsertRow(stk2);
                if (_stk2.RecordStatus != 0) return 0;
            }

            _writer.NotifyMaps();
            return stk2.STK2_S_KEY;
        }

        public void DeleteMapRecord(long key)
        {
            if (key <= 0) return;

            _stk2.DeleteRow(new STK2 { STK2_S_KEY = key });
            _writer.NotifyMaps();
        }

        // ---------------------------------------------------------------- helpers

        private IReadOnlyList<LookupItem> Library(short sys, short lib) =>
            _mlb2.GetList("MLB2_SYS_NO = @s AND MLB2_LIB_NO = @l", new { s = sys, l = lib }, "MLB2_CODE")
                 .Select(r => new LookupItem(T(r.MLB2_CODE), T(r.MLB2_CODE_DESC)))
                 .Where(i => i.Code.Length > 0)
                 .ToList();

        private static string StackStatus(string status)
        {
            switch ((status ?? " ").Trim())
            {
                case "S": return "Stacked";
                case "A": return "Active";
                case "D": return "Delayed";
                default: return status?.Trim() ?? string.Empty;
            }
        }

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
                // A function or table missing on a test copy - treated as "nothing found".
                LaLog.Error("TakeCall", "Lookup failed: " + sql, ex);
                return null;
            }
        }

        private void Execute(string sql, params (string Name, object Value)[] args)
        {
            try
            {
                using var connection = new SqlConnection(_connectString);
                connection.Open();
                using var command = new SqlCommand(sql, connection);
                foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                LaLog.Error("TakeCall", "Statement failed: " + sql, ex);
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
                LaLog.Error("TakeCall", "Query failed: " + sql, ex);
            }

            return list;
        }

        private static string T(string value) => (value ?? string.Empty).Trim();
    }
}
