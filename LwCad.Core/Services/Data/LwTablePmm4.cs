// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesPMM/PMM4.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// PMM4 - premise coordinates.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTablePmm4
{
    public class PMM4
    {
        public int PMM4_FOP;
        public decimal PMM4_LAT;
        public decimal PMM4_LONG;
    }

    /// <summary>Reader for dbo.PMM4, keyed on PMM4_FOP.</summary>
    public class Pmm4_RW : LaTable<PMM4>
    {
        public Pmm4_RW() { }
        public Pmm4_RW(string connectString) : base(connectString) { }

        protected override string TableName => "PMM4";

        protected override string[] KeyColumns => new[] { "PMM4_FOP" };
    }
}
