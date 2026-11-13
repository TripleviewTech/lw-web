// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DSU1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDsu1
{
    public class DSU1
    {
        public short DSU1_DISP_NO_1;
        public int DSU1_DISP_NO_2;
        public char DSU1_EVENT_TYPE;
        public short DSU1_EVENT_NO_1;
        public int DSU1_EVENT_NO_2;
        public string DSU1_LOC_JUR;
        public string DSU1_LOC_ST_NAM;
        public decimal DSU1_LOC_ST_NO;
        public string DSU1_LOC_SUB_NO;
        public string DSU1_LOC_UNIT;
        public string DSU1_LOC_X_ST;
        public string DSU1_DOW;
        public int DSU1_CALL_CODE;
        public int DSU1_RECLASS;
        public short DSU1_PRIORITY;
        public int DSU1_RCV_ID;
        public DateTime? DSU1_RCV_DATE;
        public int DSU1_DSP_ID;
        public DateTime? DSU1_DSP_DATE;
        public int DSU1_ENR_ID;
        public DateTime? DSU1_ENR_DATE;
        public int DSU1_ARV_ID;
        public DateTime? DSU1_ARV_DATE;
        public int DSU1_CS_ID;
        public DateTime? DSU1_CS_DATE;
        public int DSU1_CMP_ID;
        public DateTime? DSU1_CMP_DATE;
        public string DSU1_POLICE_1;
        public string DSU1_POLICE_2;
        public string DSU1_FIRE_1;
        public string DSU1_FIRE_2;
        public string DSU1_EMS_1;
        public string DSU1_EMS_2;
        public string DSU1_OTHER_1;
        public string DSU1_OTHER_2;
        public string DSU1_TOW_ZONE;
        public short DSU1_ORIGIN_S;
        public short DSU1_ORIGIN_L;
        public short DSU1_ORIGIN_C;
        public short DSU1_DISPO_S;
        public short DSU1_DISPO_L;
        public short DSU1_DISPO_C;
        public string DSU1_UNIT_NO;
        public string DSU1_AGENCY;
        public char DSU1_U_TYPE;
        public int DSU1_OFFICER_1;
        public int DSU1_OFFICER_2;
        public char DSU1_FORCED;
        public int DSU1_GEOCODE_NO;
        public int DSU1_PMM_NO;
        public short DSU1_CASE_NO_1;
        public int DSU1_CASE_NO_2;
        public DateTime? DSU1_RVW_DATE;
        public int DSU1_RVW_ID;
        public DateTime? DSU1_APV_DATE;
        public int DSU1_APV_ID;
        public char DSU1_XMIT_MDC;
        public string DSU1_REMARKS;
        public int DSU1_R_PROF_OFCR;
        public string DSU1_R_PROF_RACE;
        public string DSU1_R_PROF_SEX;
        public string DSU1_R_PROF_SECTOR;
        public string DSU1_R_PROF_DISC;
        public string DSU1_R_PROF_RESIDENT;
        public string DSU1_R_PROF_CIT_WARN;
        public string DSU1_R_PROF_SEARCH;
        public DateTime? DSU1_TRIGGER_DATE;
        public short DSU1_911_POS;
        public string DSU1_PC_NAME;
    }

    /// <summary>Reader/writer for dbo.DSU1, keyed on DSU1_DISP_NO_1, DSU1_DISP_NO_2.</summary>
    public class Dsu1_RW : LaTable<DSU1>
    {
        public Dsu1_RW() { }
        public Dsu1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DSU1";

        protected override string[] KeyColumns => new[] { "DSU1_DISP_NO_1", "DSU1_DISP_NO_2" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DSU1 InitializeDsu1(DSU1 record) => Initialize(record);
    }
}
