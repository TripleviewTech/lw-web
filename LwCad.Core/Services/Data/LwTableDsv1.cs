// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DSV1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDsv1
{
    public class DSV1
    {
        public char DSV1_TYPE;
        public char DSV1_EVENT_TYPE;
        public short DSV1_EVENT_NO_1;
        public int DSV1_EVENT_NO_2;
        public short DSV1_DISP_NO_1;
        public int DSV1_DISP_NO_2;
        public long DSV1_T_KEY;
        public int DSV1_CALL_CODE;
        public int DSV1_RECLASS;
        public DateTime? DSV1_RCV_DATE;
        public long DSV1_RCV_DATE_TIME;
        public string DSV1_JUR;
        public string DSV1_VEH_PLATE;
        public string DSV1_ZIP_PLATE;
        public string DSV1_VEH_STATE;
        public string DSV1_VEH_VIN;
        public string DSV1_VEH_MAKE;
        public string DSV1_VEH_MODEL;
        public string DSV1_VEH_STYLE;
        public string DSV1_VEH_COLOR;
        public string DSV1_VEH_P_TYPE;
        public short DSV1_VEH_P_YR;
        public char DSV1_VEH_P_COLOR;
        public DateTime? DSV1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.DSV1, keyed on DSV1_DISP_NO_1, DSV1_DISP_NO_2, DSV1_EVENT_NO_1, DSV1_EVENT_NO_2, DSV1_EVENT_TYPE, DSV1_T_KEY, DSV1_TYPE.</summary>
    public class Dsv1_RW : LaTable<DSV1>
    {
        public Dsv1_RW() { }
        public Dsv1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DSV1";

        protected override string[] KeyColumns => new[] { "DSV1_DISP_NO_1", "DSV1_DISP_NO_2", "DSV1_EVENT_NO_1", "DSV1_EVENT_NO_2", "DSV1_EVENT_TYPE", "DSV1_T_KEY", "DSV1_TYPE" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DSV1 InitializeDsv1(DSV1 record) => Initialize(record);
    }
}
