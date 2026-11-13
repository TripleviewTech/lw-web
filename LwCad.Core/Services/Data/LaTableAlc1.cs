// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/ALC1.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// ALC1 - alarm companies (the reporting person for an A/ or B/ location).

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableAlc1
{
    public class ALC1
    {
        public short ALC1_CO_NO;
        public string ALC1_CO_NM;
        public string ALC1_ADDR_1;
        public string ALC1_ADDR_2;
        public string ALC1_CITY;
        public string ALC1_STATE;
        public string ALC1_ZIP_1;
        public string ALC1_ZIP_2;
        public string ALC1_PHONE_1;
        public string ALC1_PHONE_2;
        public string ALC1_CON_NM_1;
        public string ALC1_CON_PH_AREA_1;
        public string ALC1_CON_PH_NO_1;
        public string ALC1_CON_EXT_1;
        public string ALC1_CON_POS_1;
        public string ALC1_CON_COMTS_1;
        public string ALC1_CON_NM_2;
        public string ALC1_CON_PH_AREA_2;
        public string ALC1_CON_PH_NO_2;
        public string ALC1_CON_EXT_2;
        public string ALC1_CON_POS_2;
        public string ALC1_CON_COMTS_2;
        public string ALC1_CON_NM_3;
        public string ALC1_CON_PH_AREA_3;
        public string ALC1_CON_PH_NO_3;
        public string ALC1_CON_EXT_3;
        public string ALC1_CON_POS_3;
        public string ALC1_CON_COMTS_3;
        public string ALC1_CON_NM_4;
        public string ALC1_CON_PH_AREA_4;
        public string ALC1_CON_PH_NO_4;
        public string ALC1_CON_EXT_4;
        public string ALC1_CON_POS_4;
        public string ALC1_CON_COMTS_4;
        public string ALC1_CON_NM_5;
        public string ALC1_CON_PH_AREA_5;
        public string ALC1_CON_PH_NO_5;
        public string ALC1_CON_EXT_5;
        public string ALC1_CON_POS_5;
        public string ALC1_CON_COMTS_5;
        public string ALC1_CON_NM_6;
        public string ALC1_CON_PH_AREA_6;
        public string ALC1_CON_PH_NO_6;
        public string ALC1_CON_EXT_6;
        public string ALC1_CON_POS_6;
        public string ALC1_CON_COMTS_6;
        public string ALC1_CALL_B_1;
        public string ALC1_CALL_B_2;
        public string ALC1_SP_DIAL_1;
        public string ALC1_SP_DIAL_2;
        public string ALC1_LIC_NO;
        public DateTime? ALC1_EXP_DATE;
        public string ALC1_REMARKS;
        public int ALC1_ENT_BY;
        public DateTime? ALC1_ENT_DATE;
        public int ALC1_MOD_BY;
        public DateTime? ALC1_MOD_DATE;
    }

    /// <summary>Reader for dbo.ALC1, keyed on ALC1_CO_NO.</summary>
    public class Alc1_RW : LaTable<ALC1>
    {
        public Alc1_RW() { }
        public Alc1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "ALC1";

        protected override string[] KeyColumns => new[] { "ALC1_CO_NO" };
    }
}
