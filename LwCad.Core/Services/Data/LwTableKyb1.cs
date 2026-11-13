// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_KYB1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// KYB1 - the keyboard function catalogue (maintained by LwKybFunctions).

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableKyb1
{
    public class KYB1
    {
        public string KYB1_FUNC;
        public string KYB1_DESC;
        public string KYB1_KEY_LIT;
        public char KYB1_AVL_TO;
        public string KYB1_PRG_ID;
        public char KYB1_ASSIGN;
        public int KYB1_ENT_BY;
        public DateTime? KYB1_ENT_DATE;
        public int KYB1_MOD_BY;
        public DateTime? KYB1_MOD_DATE;
    }

    /// <summary>Reader for dbo.KYB1, keyed on KYB1_FUNC. Read only in this build.</summary>
    public class Kyb1_RW : LaTable<KYB1>
    {
        public Kyb1_RW() { }
        public Kyb1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "KYB1";

        protected override string[] KeyColumns => new[] { "KYB1_FUNC" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public KYB1 InitializeKyb1(KYB1 record) => Initialize(record);
    }
}
