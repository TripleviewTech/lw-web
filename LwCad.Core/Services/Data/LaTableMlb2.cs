// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/MLB2.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// MLB2 - the alpha code libraries (states 50/01, plate types 50/22).

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableMlb2
{
    public class MLB2
    {
        public short MLB2_SYS_NO;
        public short MLB2_LIB_NO;
        public string MLB2_CODE;
        public string MLB2_CODE_DESC;
        public short MLB2_SRCH_KEY;
        public char MLB2_DISPLAY;
        public char MLB2_CODE_LOCK;
        public int MLB2_ENT_BY;
        public DateTime? MLB2_ENT_DATE;
        public int MLB2_MOD_BY;
        public DateTime? MLB2_MOD_DATE;
    }

    /// <summary>Reader for dbo.MLB2, keyed on MLB2_SYS_NO + MLB2_LIB_NO + MLB2_CODE.</summary>
    public class Mlb2_RW : LaTable<MLB2>
    {
        public Mlb2_RW() { }
        public Mlb2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "MLB2";

        protected override string[] KeyColumns => new[] { "MLB2_SYS_NO", "MLB2_LIB_NO", "MLB2_CODE" };
    }
}
