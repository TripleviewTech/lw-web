// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_STK2.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// STK2 - the in-flight map record of a call being taken.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableStk2
{
    public class STK2
    {
        public long STK2_S_KEY;
        public short STK2_POSITION;
        public DateTime? STK2_RCV_DATE;
        public int STK2_RCV_ID;
        public short STK2_PRIORITY;
        public int STK2_CALL_CODE;
        public string STK2_LOCATION;
        public string STK2_LOC_JUR;
        public string STK2_LOC_ST_NAM;
        public decimal STK2_LOC_ST_NO;
        public string STK2_LOC_SUB_NO;
        public string STK2_LOC_UNIT;
        public string STK2_LOC_X_ST;
        public char STK2_FORCED;
        public string STK2_AREA;
        public string STK2_PREMISE;
        public decimal STK2_X_CORD;
        public decimal STK2_Y_CORD;
        public string STK2_X_Y_ELEVATION;
        public string STK2_X_Y_UNCERT;
        public string STK2_X_Y_CONFID;
        public string STK2_SERVICE_TYPE;
        public string STK2_SERVICE_NAME;
        public string STK2_PER_NAME_L;
        public string STK2_PER_NAME_F;
        public string STK2_PER_NAME_M;
        public short STK2_PER_PHONE_AC;
        public short STK2_PER_PHONE_1;
        public int STK2_PER_PHONE_2;
        public string STK2_PC_NAME;
        public DateTime? STK2_TRIGGER_DATE;
    }

    /// <summary>Reader for dbo.STK2, keyed on STK2_S_KEY.</summary>
    public class Stk2_RW : LaTable<STK2>
    {
        public Stk2_RW() { }
        public Stk2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "STK2";

        protected override string[] KeyColumns => new[] { "STK2_S_KEY" };

        protected override string IdentityColumn => "STK2_S_KEY";
    }
}
