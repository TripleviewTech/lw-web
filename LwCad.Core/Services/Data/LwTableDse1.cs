// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DSE1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDse1
{
    public class DSE1
    {
        public char DSE1_EVENT_TYPE;
        public short DSE1_EVENT_NO_1;
        public int DSE1_EVENT_NO_2;
        public short DSE1_G_EVENT_1;
        public int DSE1_G_EVENT_2;
        public string DSE1_LOC_JUR;
        public string DSE1_LOC_ST_NAM;
        public decimal DSE1_LOC_ST_NO;
        public string DSE1_LOC_SUB_NO;
        public string DSE1_LOC_UNIT;
        public string DSE1_LOC_X_ST;
        public int DSE1_CALL_CODE;
        public int DSE1_RECLASS;
        public short DSE1_PRIORITY;
        public string DSE1_DOW;
        public DateTime? DSE1_RCV_DATE;
        public DateTime? DSE1_XMIT_DATE;
        public DateTime? DSE1_DSP_DATE;
        public DateTime? DSE1_ENR_DATE;
        public DateTime? DSE1_ARV_DATE;
        public DateTime? DSE1_CS_DATE;
        public DateTime? DSE1_CMP_DATE;
        public DateTime? DSE1_E911_DATE;
        public short DSE1_ORIGIN_S;
        public short DSE1_ORIGIN_L;
        public short DSE1_ORIGIN_C;
        public short DSE1_DISPO_S;
        public short DSE1_DISPO_L;
        public short DSE1_DISPO_C;
        public string DSE1_POLICE_1;
        public string DSE1_POLICE_2;
        public string DSE1_FIRE_1;
        public string DSE1_FIRE_2;
        public string DSE1_EMS_1;
        public string DSE1_EMS_2;
        public string DSE1_OTHER_1;
        public string DSE1_OTHER_2;
        public string DSE1_TOW_ZONE;
        public string DSE1_RESP_AREA;
        public int DSE1_RCV_ID;
        public char DSE1_FORCED;
        public int DSE1_GEOCODE_NO;
        public int DSE1_PMM_NO;
        public int DSE1_CAMPUS_ID;
        public int DSE1_ALARM_NO;
        public char DSE1_ALM_AUDIT;
        public short DSE1_ALM_LEVEL;
        public char DSE1_X_REF_TYPE;
        public short DSE1_X_REF_NO_1;
        public int DSE1_X_REF_NO_2;
        public string DSE1_AGENCY_01;
        public string DSE1_AGENCY_02;
        public string DSE1_AGENCY_03;
        public string DSE1_AGENCY_04;
        public string DSE1_AGENCY_05;
        public string DSE1_AGENCY_06;
        public string DSE1_AGENCY_07;
        public string DSE1_AGENCY_08;
        public string DSE1_AGENCY_09;
        public string DSE1_AGENCY_10;
        public short DSE1_CASE_NO_1_01;
        public int DSE1_CASE_NO_2_01;
        public short DSE1_CASE_NO_1_02;
        public int DSE1_CASE_NO_2_02;
        public short DSE1_CASE_NO_1_03;
        public int DSE1_CASE_NO_2_03;
        public short DSE1_CASE_NO_1_04;
        public int DSE1_CASE_NO_2_04;
        public short DSE1_CASE_NO_1_05;
        public int DSE1_CASE_NO_2_05;
        public short DSE1_CASE_NO_1_06;
        public int DSE1_CASE_NO_2_06;
        public short DSE1_CASE_NO_1_07;
        public int DSE1_CASE_NO_2_07;
        public short DSE1_CASE_NO_1_08;
        public int DSE1_CASE_NO_2_08;
        public short DSE1_CASE_NO_1_09;
        public int DSE1_CASE_NO_2_09;
        public short DSE1_CASE_NO_1_10;
        public int DSE1_CASE_NO_2_10;
        public int DSE1_VOID_ID;
        public DateTime? DSE1_DLY_E_DATE;
        public int DSE1_DLY_E_ID;
        public DateTime? DSE1_DLY_R_DATE;
        public short DSE1_FIRE_LEVEL;
        public short DSE1_TONE_LEVEL;
        public string DSE1_ROUTING_ID;
        public string DSE1_OTHER_NAME;
        public char DSE1_CHF_PAGED;
        public decimal DSE1_XLOW;
        public decimal DSE1_YLOW;
        public decimal DSE1_XHIGH;
        public decimal DSE1_YHIGH;
        public string DSE1_REMARKS;
        public DateTime? DSE1_TRIGGER_DATE;
        public string DSE1_FATPOT_ID;
        public short DSE1_911_POS;
        public string DSE1_PC_NAME;
    }

    /// <summary>Reader/writer for dbo.DSE1, keyed on DSE1_EVENT_NO_1, DSE1_EVENT_NO_2, DSE1_EVENT_TYPE.</summary>
    public class Dse1_RW : LaTable<DSE1>
    {
        public Dse1_RW() { }
        public Dse1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DSE1";

        protected override string[] KeyColumns => new[] { "DSE1_EVENT_NO_1", "DSE1_EVENT_NO_2", "DSE1_EVENT_TYPE" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DSE1 InitializeDse1(DSE1 record) => Initialize(record);
    }
}
