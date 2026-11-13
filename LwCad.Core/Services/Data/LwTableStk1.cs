// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_STK1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableStk1
{
    public class STK1
    {
        public long STK1_S_KEY;
        public string STK1_JUR;
        public char STK1_STATUS;
        public short STK1_PRIORITY;
        public DateTime? STK1_RCV_DATE;
        public int STK1_RCV_ID;
        public int STK1_CALL_CODE;
        public short STK1_G_EVENT_1;
        public int STK1_G_EVENT_2;
        public char STK1_EVENT_TYPE;
        public short STK1_EVENT_NO_1;
        public int STK1_EVENT_NO_2;
        public string STK1_LOCATION;
        public string STK1_LOC_JUR;
        public string STK1_LOC_ST_NAM;
        public decimal STK1_LOC_ST_NO;
        public string STK1_LOC_SUB_NO;
        public string STK1_LOC_UNIT;
        public string STK1_LOC_X_ST;
        public char STK1_FORCED;
        public short STK1_DSP_HR;
        public short STK1_DSP_MN;
        public short STK1_T_O_F_HR;
        public short STK1_T_O_F_MN;
        public short STK1_T_O_MN;
        public short STK1_T_O_FLAG;
        public DateTime? STK1_ACT_DATE;
        public int STK1_LOCK_ID;
        public int STK1_CT_ID;
        public string STK1_AREA;
        public decimal STK1_X_CORD;
        public decimal STK1_Y_CORD;
        public short STK1_T_T_F_HR;
        public short STK1_T_T_F_MN;
        public short STK1_T_T_MN;
        public short STK1_T_T_FLAG;
        public string STK1_UNITS;
        public DateTime? STK1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.STK1, keyed on STK1_S_KEY.</summary>
    public class Stk1_RW : LaTable<STK1>
    {
        public Stk1_RW() { }
        public Stk1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "STK1";

        protected override string[] KeyColumns => new[] { "STK1_S_KEY" };

        protected override string IdentityColumn => "STK1_S_KEY";

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public STK1 InitializeStk1(STK1 record) => Initialize(record);
    }
}
