// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesPMM/PMM6.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// PMM6 - premise alarm / box routing ids (A/ and B/ locations).

using System;
using LwCadSystemUX.Services.Data;

namespace LwTablePmm6
{
    public class PMM6
    {
        public int PMM6_FOP;
        public short PMM6_SEQ_NO;
        public string PMM6_ROUTING_ID;
        public string PMM6_PERM_NO;
        public DateTime? PMM6_ISSUED_DATE;
        public DateTime? PMM6_RENEW_DATE;
        public char PMM6_ALRM_BURG;
        public char PMM6_ALRM_HOLD;
        public char PMM6_ALRM_PANC;
        public char PMM6_ALRM_FIRE;
        public char PMM6_ALRM_MEDI;
        public char PMM6_EXEMPT;
        public char PMM6_RESPONSE;
        public int PMM6_CALL_CODE;
        public short PMM6_ALC_CO_NO;
        public char PMM6_INACTIVE;
        public int PMM6_INACT_BY;
        public DateTime? PMM6_INACT_DATE;
        public char PMM6_CHRG_LVL;
        public string PMM6_REMARKS;
        public int PMM6_ENT_BY;
        public DateTime? PMM6_ENT_DATE;
        public int PMM6_MOD_BY;
        public DateTime? PMM6_MOD_DATE;
    }

    /// <summary>Reader for dbo.PMM6, keyed on PMM6_FOP + PMM6_SEQ_NO.</summary>
    public class Pmm6_RW : LaTable<PMM6>
    {
        public Pmm6_RW() { }
        public Pmm6_RW(string connectString) : base(connectString) { }

        protected override string TableName => "PMM6";

        protected override string[] KeyColumns => new[] { "PMM6_FOP", "PMM6_SEQ_NO" };
    }
}
