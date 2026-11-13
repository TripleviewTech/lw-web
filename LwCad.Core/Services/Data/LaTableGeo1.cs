// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/GEO1.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// GEO1 - the geobase street segments (LaGeo041/043/045).

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableGeo1
{
    public class GEO1
    {
        public int GEO1_P_KEY;
        public string GEO1_LOC_STR;
        public string GEO1_LOC_JUR_L;
        public decimal GEO1_LOC_ADD_HI_L;
        public decimal GEO1_LOC_ADD_LO_L;
        public string GEO1_LOC_STR_ZIP;
        public string GEO1_LOC_JUR_R;
        public decimal GEO1_LOC_ADD_HI_R;
        public decimal GEO1_LOC_ADD_LO_R;
        public string GEO1_C_NAME;
        public string GEO1_C_NAME_ZIP;
        public string GEO1_SUB_NM;
        public char GEO1_INT_PT_HI;
        public char GEO1_INT_PT_LO;
        public short GEO1_TYPE_CODE;
        public char GEO1_ADDR_TYPE;
        public string GEO1_POLICE_1L;
        public string GEO1_POLICE_2L;
        public string GEO1_FIRE_1L;
        public string GEO1_FIRE_2L;
        public string GEO1_EMS_1L;
        public string GEO1_EMS_2L;
        public string GEO1_OTHER_1L;
        public string GEO1_OTHER_2L;
        public string GEO1_TOW_L;
        public string GEO1_ZIP_CODE_1L;
        public string GEO1_ZIP_CODE_2L;
        public string GEO1_POLICE_1R;
        public string GEO1_POLICE_2R;
        public string GEO1_FIRE_1R;
        public string GEO1_FIRE_2R;
        public string GEO1_EMS_1R;
        public string GEO1_EMS_2R;
        public string GEO1_OTHER_1R;
        public string GEO1_OTHER_2R;
        public string GEO1_TOW_R;
        public string GEO1_ZIP_CODE_1R;
        public string GEO1_ZIP_CODE_2R;
        public char GEO1_DFLT_SIDE;
        public int GEO1_CAMPUS_ID;
        public int GEO1_MAP_ID;
        public string GEO1_REMARKS;
        public int GEO1_ENT_BY;
        public DateTime? GEO1_ENT_DATE;
        public int GEO1_MOD_BY;
        public DateTime? GEO1_MOD_DATE;
    }

    /// <summary>Reader for dbo.GEO1, keyed on GEO1_P_KEY.</summary>
    public class Geo1_RW : LaTable<GEO1>
    {
        public Geo1_RW() { }
        public Geo1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "GEO1";

        protected override string[] KeyColumns => new[] { "GEO1_P_KEY" };

        protected override string IdentityColumn => "GEO1_P_KEY";
    }
}
