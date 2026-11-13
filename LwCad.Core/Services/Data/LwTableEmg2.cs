// Record shape generated from the legacy table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_EMG2.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// EMG2 - E911 calls waiting at a 911 position.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableEmg2
{
    public class EMG2
    {
        public short EMG2_CAD_POS;
        public char EMG2_EVENT_TYPE;
        public short EMG2_EVENT_NO_1;
        public int EMG2_EVENT_NO_2;
        public DateTime? EMG2_RCV_DATE;
        public short EMG2_POS;
        public short EMG2_PHONE_1;
        public short EMG2_PHONE_2;
        public short EMG2_PHONE_3;
        public DateTime? EMG2_MSG_DATE;
        public string EMG2_NAME;
        public int EMG2_ST_NO;
        public string EMG2_SUB_NO;
        public string EMG2_ST_NAM;
        public string EMG2_CITY;
        public string EMG2_STATE;
        public string EMG2_UNIT;
        public string EMG2_P_ESN_NAME;
        public string EMG2_F_ESN_NAME;
        public string EMG2_E_ESN_NAME;
        public char EMG2_NEW_MSG_SW;
        public decimal EMG2_XLOW;
        public decimal EMG2_YLOW;
        public decimal EMG2_XHIGH;
        public decimal EMG2_YHIGH;
        public string EMG2_RAW_MSG;
        public short EMG2_ESN;
    }

    /// <summary>Reader for dbo.EMG2, keyed on EMG2_CAD_POS.</summary>
    public class Emg2_RW : LaTable<EMG2>
    {
        public Emg2_RW() { }
        public Emg2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "EMG2";

        protected override string[] KeyColumns => new[] { "EMG2_CAD_POS" };
    }
}
