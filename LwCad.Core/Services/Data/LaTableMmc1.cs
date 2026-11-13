// Record shape generated from the legacy table definition
// (Git/Missouri/LA/TableDefs/LaTables/MMC1.designer.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.
// MMC1 - vehicle make / model / style / colour codes.

using System;
using LwCadSystemUX.Services.Data;

namespace LaTableMmc1
{
    public class MMC1
    {
        public int MMC1_P_KEY;
        public string MMC1_COLOR;
        public string MMC1_STYLE;
        public short MMC1_R_TYPE;
        public string MMC1_MAKE;
        public string MMC1_MODEL;
        public string MMC1_COLOR_DESC;
        public string MMC1_STYLE_DESC;
        public string MMC1_MAKE_DESC;
        public string MMC1_MODEL_DESC;
        public int MMC1_ENT_BY;
        public DateTime? MMC1_ENT_DT;
        public int MMC1_MOD_BY;
        public DateTime? MMC1_MOD_DT;
    }

    /// <summary>Reader for dbo.MMC1, keyed on MMC1_P_KEY.</summary>
    public class Mmc1_RW : LaTable<MMC1>
    {
        public Mmc1_RW() { }
        public Mmc1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "MMC1";

        protected override string[] KeyColumns => new[] { "MMC1_P_KEY" };

        protected override string IdentityColumn => "MMC1_P_KEY";
    }
}
