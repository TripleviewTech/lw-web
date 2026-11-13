// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_UST1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableUst1
{
    public class UST1
    {
        public short UST1_STATUS;
        public string UST1_ABRV;
        public string UST1_DESC;
        public short UST1_N_STATUS;
        public short UST1_MON_COL_S;
        public short UST1_MON_COL_L;
        public string UST1_MON_COL_C;
        public string UST1_COLOR_LITERAL;
        public char UST1_AVL_SW;
        public char UST1_SEND_2_MDC;
        public DateTime? UST1_MOD_DATE;
        public int UST1_MOD_BY;
    }

    /// <summary>Reader/writer for dbo.UST1, keyed on UST1_STATUS.</summary>
    public class Ust1_RW : LaTable<UST1>
    {
        public Ust1_RW() { }
        public Ust1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "UST1";

        protected override string[] KeyColumns => new[] { "UST1_STATUS" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public UST1 InitializeUst1(UST1 record) => Initialize(record);
    }
}
