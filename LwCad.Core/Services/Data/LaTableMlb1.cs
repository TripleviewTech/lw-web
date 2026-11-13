// MLB1 is the multi-level code library - a (system, library, code) triple
// resolving to a description. In LaTables it is one of the LINQ to SQL tables,
// which has no .NET 8 equivalent, so it is re-expressed here on SqlClient.
// Columns and types come from LaTables/MLB1.designer.cs.

using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Services.Data;

namespace LaTableMlb1
{
    public class MLB1
    {
        public short MLB1_SYS_NO;
        public short MLB1_LIB_NO;
        public short MLB1_CODE;
        public string MLB1_CODE_DESC;
        public short MLB1_SRCH_KEY;
        public char MLB1_DISPLAY;
        public char MLB1_CODE_LOCK;
        public int MLB1_ENT_BY;
        public DateTime? MLB1_ENT_DATE;
        public int MLB1_MOD_BY;
        public DateTime? MLB1_MOD_DATE;
    }

    /// <summary>Reader/writer for dbo.MLB1, keyed on MLB1_SYS_NO, MLB1_LIB_NO, MLB1_CODE.</summary>
    public class Mlb1_RW : LaTable<MLB1>
    {
        public Mlb1_RW() { }
        public Mlb1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "MLB1";

        protected override string[] KeyColumns =>
            new[] { "MLB1_SYS_NO", "MLB1_LIB_NO", "MLB1_CODE" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public MLB1 InitializeMlb1(MLB1 record) => Initialize(record);

        /// <summary>
        /// Equivalent of the la_fn_MLB1_GetDesc SQL function the legacy monitors
        /// call inline. Returns an empty string rather than throwing when the code
        /// is 0 or unknown, which is what the screens expect.
        /// </summary>
        public string GetDescription(short sysNo, short libNo, short code)
        {
            if (code == 0) return string.Empty;

            var row = GetRow(new MLB1
            {
                MLB1_SYS_NO = sysNo,
                MLB1_LIB_NO = libNo,
                MLB1_CODE = code
            });

            return RecordStatus == 0 ? (row.MLB1_CODE_DESC ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>
        /// Loads a whole library in one round trip. The monitors resolve the same
        /// handful of codes on every refresh, so caching a library beats a GetRow
        /// per unit per tick.
        /// </summary>
        public Dictionary<short, string> GetLibrary(short sysNo, short libNo)
        {
            var rows = GetList(
                where: "MLB1_SYS_NO = @MLB1_SYS_NO AND MLB1_LIB_NO = @MLB1_LIB_NO",
                parameters: new { MLB1_SYS_NO = sysNo, MLB1_LIB_NO = libNo },
                orderBy: "MLB1_CODE");

            return rows
                .GroupBy(r => r.MLB1_CODE)
                .ToDictionary(g => g.Key, g => (g.First().MLB1_CODE_DESC ?? string.Empty).Trim());
        }
    }
}
