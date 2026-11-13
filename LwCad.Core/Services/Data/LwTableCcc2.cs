// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_CCC2.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// CCC2 - call code SOPs by agency type.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableCcc2
{
    public class CCC2
    {
        public int CCC2_CODE;
        public char CCC2_TYPE;
        public string CCC2_SOP;
        public int CCC2_ENT_BY;
        public DateTime? CCC2_ENT_DATE;
        public int CCC2_MOD_BY;
        public DateTime? CCC2_MOD_DATE;
    }

    /// <summary>Reader for dbo.CCC2, keyed on CCC2_CODE + CCC2_TYPE.</summary>
    public class Ccc2_RW : LaTable<CCC2>
    {
        public Ccc2_RW() { }
        public Ccc2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "CCC2";

        protected override string[] KeyColumns => new[] { "CCC2_CODE", "CCC2_TYPE" };
    }
}
