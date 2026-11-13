// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/GEO2.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// GEO2 - the cross streets at each end of a GEO1 segment (LaGeo043).

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableGeo2
{
    public class GEO2
    {
        public int GEO2_P_KEY;
        public string GEO2_NAME;
        public char GEO2_TYPE;
        public string GEO2_NAME_ZIP;
        public int GEO2_ENT_BY;
        public DateTime? GEO2_ENT_DATE;
        public int GEO2_MOD_BY;
        public DateTime? GEO2_MOD_DATE;
    }

    /// <summary>Reader for dbo.GEO2, keyed on GEO2_P_KEY + GEO2_NAME + GEO2_TYPE.</summary>
    public class Geo2_RW : LaTable<GEO2>
    {
        public Geo2_RW() { }
        public Geo2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "GEO2";

        protected override string[] KeyColumns => new[] { "GEO2_P_KEY", "GEO2_NAME", "GEO2_TYPE" };
    }
}
