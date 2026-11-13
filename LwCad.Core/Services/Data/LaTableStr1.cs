// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/STR1.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// STR1 - street names and their aliases (LaGeo041/043).

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableStr1
{
    public class STR1
    {
        public int STR1_REC_NO;
        public string STR1_STR_JUR;
        public string STR1_STR_NM;
        public string STR1_SSORT_NM;
        public string STR1_ALIAS_NM;
        public string STR1_ASORT_NM;
        public string STR1_POLICE_1;
        public string STR1_POLICE_2;
        public string STR1_FIRE_1;
        public string STR1_FIRE_2;
        public string STR1_EMS_1;
        public string STR1_EMS_2;
        public string STR1_OTHER_1;
        public string STR1_OTHER_2;
        public string STR1_TOW;
        public string STR1_REMARKS;
        public string STR1_X_STR_PRF;
        public string STR1_X_STR_NM;
        public string STR1_X_STR_TYPE;
        public string STR1_X_STR_SUF;
        public int STR1_CAMPUS_ID;
        public int STR1_ENT_BY;
        public DateTime? STR1_ENT_DATE;
        public int STR1_MOD_BY;
        public DateTime? STR1_MOD_DATE;
        public char STR1_SEND_2_MDC;
    }

    /// <summary>Reader for dbo.STR1, keyed on STR1_REC_NO.</summary>
    public class Str1_RW : LaTable<STR1>
    {
        public Str1_RW() { }
        public Str1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "STR1";

        protected override string[] KeyColumns => new[] { "STR1_REC_NO" };

        protected override string IdentityColumn => "STR1_REC_NO";
    }
}
