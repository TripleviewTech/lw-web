// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_CCC1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableCcc1
{
    public class CCC1
    {
        public int CCC1_CODE;
        public string CCC1_DESC;
        public string CCC1_ABRV;
        public char CCC1_INACTIVE;
        public int CCC1_INACT_BY;
        public DateTime? CCC1_INACT_DATE;
        public char CCC1_HIDE;
        public short CCC1_PRTY_P;
        public short CCC1_PRTY_F;
        public short CCC1_PRTY_E;
        public short CCC1_PRTY_O;
        public short CCC1_PT_O_STCK;
        public short CCC1_PT_O_TRVL;
        public short CCC1_PT_O_ARRV;
        public short CCC1_FT_O_STCK;
        public short CCC1_FT_O_TRVL;
        public short CCC1_FT_O_ARRV;
        public short CCC1_ET_O_STCK;
        public short CCC1_ET_O_TRVL;
        public short CCC1_ET_O_ARRV;
        public short CCC1_OT_O_STCK;
        public short CCC1_OT_O_TRVL;
        public short CCC1_OT_O_ARRV;
        public char CCC1_RESTACK;
        public char CCC1_DISP_HIST;
        public char CCC1_BILLABLE;
        public char CCC1_BILL_TYPE;
        public char CCC1_TS_CODE;
        public char CCC1_ASSGN_ASST;
        public char CCC1_DELAY;
        public short CCC1_DELAY_HRS;
        public char CCC1_REACT;
        public short CCC1_REACT_HRS;
        public char CCC1_RECOM_1ST;
        public short CCC1_PRTY_1ST;
        public char CCC1_RECOM_2ND;
        public short CCC1_PRTY_2ND;
        public char CCC1_RECOM_3RD;
        public short CCC1_PRTY_3RD;
        public char CCC1_CASE_NO;
        public char CCC1_SEND_2_MDC;
        public char CCC1_AUTO_C_NO;
        public DateTime? CCC1_EFF_DATE;
        public char CCC1_ALERT;
        public char CCC1_VW_PUBLIC_SYS;
        public char CCC1_MILEAGE_PROMPT;
        public char CCC1_PROQA_MED;
        public int CCC1_ENT_BY;
        public DateTime? CCC1_ENT_DATE;
        public int CCC1_MOD_BY;
        public DateTime? CCC1_MOD_DATE;
        public char CCC1_PAGER;
        public char CCC1_PG_CHIEF;
        public char CCC1_PG_APP;
        public char CCC1_GSP;
    }

    /// <summary>Reader/writer for dbo.CCC1, keyed on CCC1_CODE.</summary>
    public class Ccc1_RW : LaTable<CCC1>
    {
        public Ccc1_RW() { }
        public Ccc1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "CCC1";

        protected override string[] KeyColumns => new[] { "CCC1_CODE" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public CCC1 InitializeCcc1(CCC1 record) => Initialize(record);
    }
}
