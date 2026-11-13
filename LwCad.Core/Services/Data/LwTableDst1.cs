// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DST1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDst1
{
    public class DST1
    {
        public long DST1_T_KEY;
        public short DST1_DISP_NO_1;
        public int DST1_DISP_NO_2;
        public char DST1_ALERT_SW;
        public char DST1_EVENT_TYPE;
        public short DST1_EVENT_NO_1;
        public int DST1_EVENT_NO_2;
        public DateTime? DST1_TRAN_DATE;
        public string DST1_DOW;
        public short DST1_T_TYPE_S;
        public short DST1_T_TYPE_L;
        public short DST1_T_TYPE_C;
        public string DST1_LOCATION;
        public string DST1_AGENCY;
        public string DST1_UNIT_NO;
        public int DST1_OFFICER_1;
        public int DST1_OFFICER_2;
        public decimal DST1_MILEAGE;
        public long DST1_TOW_KEY;
        public int DST1_ENT_BY;
        public short DST1_U_STATUS;
        public decimal DST1_XLOW;
        public decimal DST1_YLOW;
        public decimal DST1_XHIGH;
        public decimal DST1_YHIGH;
        public char DST1_RCD_TYPE;
        public int DST1_ACK_BY;
        public DateTime? DST1_ACK_DATE;
        public string DST1_REMARKS;
        public DateTime? DST1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.DST1, keyed on DST1_T_KEY.</summary>
    public class Dst1_RW : LaTable<DST1>
    {
        public Dst1_RW() { }
        public Dst1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DST1";

        protected override string[] KeyColumns => new[] { "DST1_T_KEY" };

        protected override string IdentityColumn => "DST1_T_KEY";

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DST1 InitializeDst1(DST1 record) => Initialize(record);
    }
}
