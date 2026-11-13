// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesPMM/PMM2.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// PMM2 - premise hazards (a count turns the premise flag red).

using System;
using LwCadSystemUX.Services.Data;

namespace LwTablePmm2
{
    public class PMM2
    {
        public int PMM2_P_KEY;
        public int PMM2_FOP;
        public short PMM2_HAZ_TYPE_S;
        public short PMM2_HAZ_TYPE_L;
        public short PMM2_HAZ_TYPE_C;
        public string PMM2_UNUSED;
        public string PMM2_OTH_TYPE;
        public string PMM2_QTY;
        public string PMM2_FLOOR;
        public string PMM2_HAZ_LOC;
        public string PMM2_NOTES;
        public int PMM2_ENT_BY;
        public DateTime? PMM2_ENT_DATE;
        public int PMM2_MOD_BY;
        public DateTime? PMM2_MOD_DATE;
        public string PMM2_ID_NO;
    }

    /// <summary>Reader for dbo.PMM2, keyed on PMM2_P_KEY.</summary>
    public class Pmm2_RW : LaTable<PMM2>
    {
        public Pmm2_RW() { }
        public Pmm2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "PMM2";

        protected override string[] KeyColumns => new[] { "PMM2_P_KEY" };

        protected override string IdentityColumn => "PMM2_P_KEY";
    }
}
