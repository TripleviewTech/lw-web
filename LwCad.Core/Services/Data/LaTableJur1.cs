// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/JUR1.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// JUR1 - jurisdictions; GEO_JUR / CAD_DFLT drive Take Call.

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableJur1
{
    public class JUR1
    {
        public string JUR1_JUR_CODE;
        public string JUR1_MUN_CODE;
        public string JUR1_ESN;
        public string JUR1_P_NAME;
        public string JUR1_ALIAS;
        public string JUR1_COUNTY;
        public string JUR1_STATE;
        public string JUR1_P_NAME_CMP;
        public string JUR1_ALIAS_CMP;
        public string JUR1_AGENCY_1;
        public string JUR1_PH_AREA_1;
        public string JUR1_PH_NO_1;
        public string JUR1_AGENCY_2;
        public string JUR1_PH_AREA_2;
        public string JUR1_PH_NO_2;
        public string JUR1_AGENCY_3;
        public string JUR1_PH_AREA_3;
        public string JUR1_PH_NO_3;
        public string JUR1_AGENCY_4;
        public string JUR1_PH_AREA_4;
        public string JUR1_PH_NO_4;
        public string JUR1_AGENCY_5;
        public string JUR1_PH_AREA_5;
        public string JUR1_PH_NO_5;
        public string JUR1_AGENCY_6;
        public string JUR1_PH_AREA_6;
        public string JUR1_PH_NO_6;
        public string JUR1_NOTES;
        public char JUR1_CAD_DFLT;
        public char JUR1_GEO_JUR;
        public char JUR1_E911_JUR;
        public string JUR1_D_JUR_CODE;
        public int JUR1_ENT_BY;
        public DateTime? JUR1_ENT_DATE;
        public int JUR1_MOD_BY;
        public DateTime? JUR1_MOD_DATE;
        public char JUR1_UNITS;
        public char JUR1_SEND_2_MDC;
        public string JUR1_ACC_ZONE;
    }

    /// <summary>Reader for dbo.JUR1, keyed on JUR1_JUR_CODE.</summary>
    public class Jur1_RW : LaTable<JUR1>
    {
        public Jur1_RW() { }
        public Jur1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "JUR1";

        protected override string[] KeyColumns => new[] { "JUR1_JUR_CODE" };
    }
}
