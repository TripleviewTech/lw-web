// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/JUR3.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// JUR3 - E911 ESN to jurisdiction.

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableJur3
{
    public class JUR3
    {
        public short JUR3_ESN;
        public string JUR3_JUR;
        public int JUR3_ENT_BY;
        public DateTime? JUR3_ENT_DATE;
        public int JUR3_MOD_BY;
        public DateTime? JUR3_MOD_DATE;
    }

    /// <summary>Reader for dbo.JUR3, keyed on JUR3_ESN.</summary>
    public class Jur3_RW : LaTable<JUR3>
    {
        public Jur3_RW() { }
        public Jur3_RW(string connectString) : base(connectString) { }

        protected override string TableName => "JUR3";

        protected override string[] KeyColumns => new[] { "JUR3_ESN" };
    }
}
