// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DAT1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDat1
{
    public class DAT1
    {
        public int DAT1_EMP_ID;
        public short DAT1_SESS_ID;
        public short DAT1_ACC_LEVEL;
        public char DAT1_OP_TYPE;
        public char DAT1_AGC_P;
        public char DAT1_AGC_F;
        public char DAT1_AGC_E;
        public char DAT1_AGC_O;
        public string DAT1_KBRD_ID;
        public char DAT1_MSG_SW;
        public char DAT1_BULL_SW;
        public char DAT1_STATE_SW;
        public short DAT1_MSG_PEND;
        public short DAT1_MSG_VW;
        public short DAT1_BULLETIN;
        public long DAT1_TS_STK;
        public long DAT1_TS_MON;
        public DateTime? DAT1_TS_MAP;
        public short DAT1_NO_STK_1;
        public short DAT1_NO_STK_2;
        public short DAT1_NO_STK_3;
        public short DAT1_NO_ACT_1;
        public short DAT1_NO_ACT_2;
        public short DAT1_NO_ACT_3;
        public short DAT1_NO_DLY_1;
        public short DAT1_NO_DLY_2;
        public short DAT1_NO_DLY_3;
        public int DAT1_EXT;
        public short DAT1_911_POS;
        public DateTime? DAT1_LOGON_DATE;
        public short DAT1_CYCLE_STK;
        public short DAT1_CYCLE_MON;
        public short DAT1_MAX_SESS;
        public string DAT1_FUNC_ID_01;
        public string DAT1_FUNC_ID_02;
        public string DAT1_FUNC_ID_03;
        public string DAT1_FUNC_ID_04;
        public string DAT1_FUNC_ID_05;
        public string DAT1_FUNC_ID_06;
        public string DAT1_FUNC_ID_07;
        public string DAT1_FUNC_ID_08;
        public string DAT1_FUNC_ID_09;
        public string DAT1_FUNC_ID_10;
        public string DAT1_FUNC_ID_11;
        public string DAT1_FUNC_ID_12;
        public char DAT1_FUNC_SW_01;
        public char DAT1_FUNC_SW_02;
        public char DAT1_FUNC_SW_03;
        public char DAT1_FUNC_SW_04;
        public char DAT1_FUNC_SW_05;
        public char DAT1_FUNC_SW_06;
        public char DAT1_FUNC_SW_07;
        public char DAT1_FUNC_SW_08;
        public char DAT1_FUNC_SW_09;
        public char DAT1_FUNC_SW_10;
        public char DAT1_SIZE;
        public char DAT1_FORMAT;
        public char DAT1_INCLUDE;
        public char DAT1_GROUPING;
        public string DAT1_MORE;
        public char DAT1_SECURE_SW;
        public char DAT1_CMD_TYPE;
        public long DAT1_TS_EXT;
        public long DAT1_TS_MSG1;
        public char DAT1_INCLUDE_P;
        public char DAT1_INCLUDE_F;
        public char DAT1_INCLUDE_E;
        public char DAT1_INCLUDE_O;
        public string DAT1_ORI;
        public string DAT1_PC_NAME;
        public char DAT1_PROQA_FLAG;
        public string DAT1_PROQA_NO;
        public DateTime? DAT1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.DAT1, keyed on DAT1_EMP_ID, DAT1_SESS_ID.</summary>
    public class Dat1_RW : LaTable<DAT1>
    {
        public Dat1_RW() { }
        public Dat1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DAT1";

        protected override string[] KeyColumns => new[] { "DAT1_EMP_ID", "DAT1_SESS_ID" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DAT1 InitializeDat1(DAT1 record) => Initialize(record);
    }
}
