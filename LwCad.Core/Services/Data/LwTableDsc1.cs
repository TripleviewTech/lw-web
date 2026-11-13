// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DSC1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDsc1
{
    public class DSC1
    {
        public short DSC1_YEAR;
        public int DSC1_GLOBAL_NO;
        public int DSC1_EVENT_NO_P;
        public int DSC1_EVENT_NO_F;
        public int DSC1_EVENT_NO_E;
        public int DSC1_EVENT_NO_O;
        public int DSC1_DISP_NO;
        public int DSC1_TRANS_NO;
        public int DSC1_BULL_NO;
        public int DSC1_MSG_NO;
        public int DSC1_TOW_NO;
        public int DSC1_MOD_BY;
        public DateTime? DSC1_MOD_DATE;
        public DateTime? DSC1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.DSC1, keyed on DSC1_YEAR.</summary>
    public class Dsc1_RW : LaTable<DSC1>
    {
        public Dsc1_RW() { }
        public Dsc1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DSC1";

        protected override string[] KeyColumns => new[] { "DSC1_YEAR" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DSC1 InitializeDsc1(DSC1 record) => Initialize(record);
    }
}
