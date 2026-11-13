// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_DSN1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableDsn1
{
    public class DSN1
    {
        public char DSN1_TYPE;
        public char DSN1_EVENT_TYPE;
        public short DSN1_EVENT_NO_1;
        public int DSN1_EVENT_NO_2;
        public short DSN1_DISP_NO_1;
        public int DSN1_DISP_NO_2;
        public long DSN1_T_KEY;
        public int DSN1_CALL_CODE;
        public int DSN1_RECLASS;
        public string DSN1_LOC_JUR;
        public DateTime? DSN1_RCV_DATE;
        public long DSN1_RCV_DATE_TIME;
        public short DSN1_RP_TYPE_S;
        public short DSN1_RP_TYPE_L;
        public short DSN1_RP_TYPE_C;
        public string DSN1_SDX_NAME_L;
        public string DSN1_SDX_NAME_F;
        public string DSN1_SDX_NAME_M;
        public string DSN1_ZIP_NAME_L;
        public string DSN1_ZIP_NAME_F;
        public string DSN1_ZIP_NAME_M;
        public string DSN1_PER_NAME_L;
        public string DSN1_PER_NAME_F;
        public string DSN1_PER_NAME_M;
        public short DSN1_PER_PHONE_AC;
        public short DSN1_PER_PHONE_1;
        public int DSN1_PER_PHONE_2;
        public string DSN1_PER_ADDR;
        public string DSN1_PER_CITY;
        public string DSN1_PER_STATE;
        public DateTime? DSN1_PER_DOB_DATE;
        public char DSN1_PER_RACE;
        public char DSN1_PER_SEX;
        public string DSN1_PER_OLN;
        public string DSN1_PER_OLN_ST;
        public short DSN1_PER_SSN_1;
        public short DSN1_PER_SSN_2;
        public short DSN1_PER_SSN_3;
        public string DSN1_PATIENT_NO;
        public DateTime? DSN1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.DSN1, keyed on DSN1_DISP_NO_1, DSN1_DISP_NO_2, DSN1_EVENT_NO_1, DSN1_EVENT_NO_2, DSN1_EVENT_TYPE, DSN1_T_KEY, DSN1_TYPE.</summary>
    public class Dsn1_RW : LaTable<DSN1>
    {
        public Dsn1_RW() { }
        public Dsn1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "DSN1";

        protected override string[] KeyColumns => new[] { "DSN1_DISP_NO_1", "DSN1_DISP_NO_2", "DSN1_EVENT_NO_1", "DSN1_EVENT_NO_2", "DSN1_EVENT_TYPE", "DSN1_T_KEY", "DSN1_TYPE" };

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public DSN1 InitializeDsn1(DSN1 record) => Initialize(record);
    }
}
