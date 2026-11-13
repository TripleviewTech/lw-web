// Record shape generated from the premise table definition
// (TableDefs/LwTablesPMM/LW_PMM1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTablePmm1
{
    public class PMM1
    {
        public int PMM1_FOP;
        public string PMM1_NAME_L;
        public string PMM1_NAME_F_M;
        public short PMM1_PREMISE_S;
        public short PMM1_PREMISE_L;
        public short PMM1_PREMISE_C;
        public string PMM1_LOC_JUR;
        public string PMM1_LOC_STR;
        public decimal PMM1_LOC_ADDR;
        public string PMM1_LOC_SUB_NO;
        public string PMM1_LOC_UNI_NO;
        public string PMM1_LOC_X_ST;
        public string PMM1_LOC_FORMAT;
        public char PMM1_LOC_TYPE;
        public string PMM1_POLICE;
        public string PMM1_FIRE;
        public string PMM1_EMS;
        public string PMM1_OTHER;
        public string PMM1_TOW_ZONE;
        public int PMM1_GEO_NO;
        public string PMM1_CON_NAME_1;
        public string PMM1_CON_ADDR_1;
        public string PMM1_CON_REL_1;
        public string PMM1_CON_NAME_2;
        public string PMM1_CON_ADDR_2;
        public string PMM1_CON_REL_2;
        public string PMM1_CON_NAME_3;
        public string PMM1_CON_ADDR_3;
        public string PMM1_CON_REL_3;
        public string PMM1_CON_NAME_4;
        public string PMM1_CON_ADDR_4;
        public string PMM1_CON_REL_4;
        public string PMM1_CON_NAME_5;
        public string PMM1_CON_ADDR_5;
        public string PMM1_CON_REL_5;
        public string PMM1_CON_NAME_6;
        public string PMM1_CON_ADDR_6;
        public string PMM1_CON_REL_6;
        public string PMM1_CON_PH_AREA_1;
        public string PMM1_CON_PH_NO_1;
        public string PMM1_CON_PH_AREA_2;
        public string PMM1_CON_PH_NO_2;
        public string PMM1_CON_PH_AREA_3;
        public string PMM1_CON_PH_NO_3;
        public string PMM1_CON_PH_AREA_4;
        public string PMM1_CON_PH_NO_4;
        public string PMM1_CON_PH_AREA_5;
        public string PMM1_CON_PH_NO_5;
        public string PMM1_CON_PH_AREA_6;
        public string PMM1_CON_PH_NO_6;
        public string PMM1_REMARKS;
        public string PMM1_ML1_ADDR_1;
        public string PMM1_ML1_ADDR_2;
        public string PMM1_ML1_CITY;
        public string PMM1_ML1_STATE;
        public string PMM1_ML1_ZIP_1;
        public string PMM1_ML1_ZIP_2;
        public string PMM1_ML1_PH_1;
        public string PMM1_ML1_PH_2;
        public string PMM1_SP_DIAL_1;
        public string PMM1_SP_DIAL_2;
        public string PMM1_ML2_ADDR_1;
        public string PMM1_ML2_ADDR_2;
        public string PMM1_ML2_CITY;
        public string PMM1_ML2_STATE;
        public string PMM1_ML2_ZIP_1;
        public string PMM1_ML2_ZIP_2;
        public string PMM1_ML2_PH_1;
        public string PMM1_ML2_PH_2;
        public string PMM1_ML3_ADDR_1;
        public string PMM1_ML3_ADDR_2;
        public string PMM1_ML3_CITY;
        public string PMM1_ML3_STATE;
        public string PMM1_ML3_ZIP_1;
        public string PMM1_ML3_ZIP_2;
        public string PMM1_ML3_PH_1;
        public string PMM1_ML3_PH_2;
        public short PMM1_SEQ_NO;
        public char PMM1_PERM_STAT;
        public char PMM1_EXEMPT;
        public char PMM1_NON_RES;
        public char PMM1_RESPONSE;
        public char PMM1_ALRM_BURG;
        public char PMM1_ALRM_HOLD;
        public char PMM1_ALRM_PANC;
        public char PMM1_ALRM_FIRE;
        public char PMM1_ALRM_MEDI;
        public int PMM1_TRANS_NO;
        public short PMM1_ALARMS_P;
        public short PMM1_ALARMS_F;
        public short PMM1_ALARMS_M;
        public string PMM1_PERM_NO;
        public DateTime? PMM1_ISSUED_DATE;
        public DateTime? PMM1_RENEW_DATE;
        public short PMM1_P_ALARMS;
        public short PMM1_F_ALARMS;
        public string PMM1_ATTENTION;
        public string PMM1_BILL_NAME;
        public string PMM1_OWNER_NM;
        public short PMM1_SEND_BILL;
        public DateTime? PMM1_DEACT_DATE;
        public string PMM1_SPACELESS;
        public string PMM1_ADDR_JUR;
        public string PMM1_ADDR_STR;
        public decimal PMM1_ADDR_NO;
        public DateTime? PMM1_EST_DATE;
        public short PMM1_STORIES;
        public short PMM1_LENGTH;
        public short PMM1_WIDTH;
        public char PMM1_BASEMENT;
        public short PMM1_CONS_TYP_S;
        public short PMM1_CONS_TYP_L;
        public short PMM1_CONS_TYP_C;
        public short PMM1_FUEL_TYP_S;
        public short PMM1_FUEL_TYP_L;
        public short PMM1_FUEL_TYP_C;
        public string PMM1_KNOX_BOX;
        public string PMM1_ACCESS_CON;
        public string PMM1_FIRE_PNL;
        public string PMM1_FIRE_CON;
        public char PMM1_XB_STAND;
        public string PMM1_STAND_LOC;
        public char PMM1_XB_SPRK;
        public string PMM1_SPRK_LOC;
        public char PMM1_XB_VENT;
        public string PMM1_VENT_LOC;
        public DateTime? PMM1_PLAN_DATE;
        public string PMM1_FIRE_RMKS;
        public int PMM1_ENT_BY;
        public DateTime? PMM1_ENT_DATE;
        public int PMM1_MOD_BY;
        public DateTime? PMM1_MOD_DATE;
        public char PMM1_INACTIVE;
        public int PMM1_INACT_BY;
        public DateTime? PMM1_INACT_DATE;
        public char PMM1_SEND_2_MDC;
    }

    /// <summary>Reader for dbo.PMM1, keyed on PMM1_FOP.</summary>
    public class Pmm1_RW : LaTable<PMM1>
    {
        public Pmm1_RW() { }
        public Pmm1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "PMM1";

        protected override string[] KeyColumns => new[] { "PMM1_FOP" };

        public PMM1 InitializePmm1(PMM1 record) => Initialize(record);
    }
}
