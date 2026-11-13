// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_RST1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableRst1
{
    public class RST1
    {
        public string RST1_UNIT_NO;
        public char RST1_AGC_TYPE;
        public string RST1_AGENCY;
        public string RST1_JUR;
        public string RST1_R_AREA_1;
        public string RST1_R_AREA_2;
        public string RST1_R_AREA_3;
        public string RST1_R_AREA_4;
        public string RST1_R_AREA_5;
        public string RST1_R_AREA_6;
        public string RST1_R_AREA_7;
        public string RST1_R_AREA_8;
        public string RST1_R_AREA_9;
        public string RST1_R_AREA_10;
        public int RST1_OFCR_1;
        public string RST1_PORT_1;
        public int RST1_OFCR_2;
        public string RST1_PORT_2;
        public short RST1_TOUR_S;
        public short RST1_TOUR_L;
        public short RST1_TOUR_C;
        public short RST1_STATUS;
        public char RST1_DISPLAY;
        public char RST1_RECOMMEND;
        public char RST1_EVENT_TYPE;
        public short RST1_EVENT_NO_1;
        public int RST1_EVENT_NO_2;
        public short RST1_DISP_NO_1;
        public int RST1_DISP_NO_2;
        public short RST1_TRANS_NO_1;
        public int RST1_TRANS_NO_2;
        public char RST1_ASGN_TYPE;
        public int RST1_LOCK_ID;
        public int RST1_CALL_CODE;
        public short RST1_LAST_HR;
        public short RST1_LAST_MN;
        public string RST1_LAST_LOC;
        public string RST1_MDT_NO;
        public string RST1_STATE_ID;
        public short RST1_EMG_FLAG;
        public short RST1_T_O_F_HR;
        public short RST1_T_O_F_MN;
        public short RST1_T_O_MN;
        public short RST1_T_O_FLAG;
        public string RST1_REPL_ID;
        public char RST1_TEMP_UNIT;
        public string RST1_TEMP_JUR;
        public string RST1_A_STATION;
        public string RST1_C_STATION;
        public string RST1_NOTES;
        public short RST1_U_TYPE_S;
        public short RST1_U_TYPE_L;
        public short RST1_U_TYPE_C;
        public short RST1_T_TYPE_S;
        public short RST1_T_TYPE_L;
        public short RST1_T_TYPE_C;
        public short RST1_DFLT_GROUP;
        public short RST1_DFLT_CODE;
        public string RST1_RADIO_NO;
        public string RST1_VEH_ID;
        public string RST1_PAGER_NO;
        public char RST1_TMOUT_SW;
        public short RST1_TMOUT_MINS;
        public short RST1_PRIORITY;
        public char RST1_DSPL_SW;
        public short RST1_HIT_FLAG;
        public char RST1_DETAIL_SW;
        public string RST1_DETAIL_LOC;
        public DateTime? RST1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.RST1, keyed on RST1_UNIT_NO.</summary>
    public class Rst1_RW : LaTable<RST1>
    {
        public Rst1_RW() { }
        public Rst1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "RST1";

        protected override string[] KeyColumns => new[] { "RST1_UNIT_NO" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public RST1 InitializeRst1(RST1 record) => Initialize(record);
    }
}
