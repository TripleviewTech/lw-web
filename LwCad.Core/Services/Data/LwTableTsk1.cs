// Record shape generated from the Lowell CAD table definition
// (Git/Mass/LW/TableDefs/LwTablesCAD/LW_TSK1.cs) so the columns cannot drift.
// Reader behaviour lives in LaTable<T>; see Services/Data/LaTable.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableTsk1
{
    public class TSK1
    {
        public long TSK1_P_KEY;
        public string TSK1_EMP_ID;
        public string TSK1_SESS_ID;
        public char TSK1_TASK_TYPE;
        public short TSK1_PRIORITY;
        public short TSK1_ACTION;
        public string TSK1_UNIT_NO;
        public long TSK1_DATE_TIME_KEY;
        public DateTime? TSK1_DATE_TIME;
        public DateTime? TSK1_TRIGGER_DATE;
    }

    /// <summary>Reader/writer for dbo.TSK1, keyed on TSK1_P_KEY.</summary>
    public class Tsk1_RW : LaTable<TSK1>
    {
        public Tsk1_RW() { }
        public Tsk1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "TSK1";

        protected override string[] KeyColumns => new[] { "TSK1_P_KEY" };

        protected override string IdentityColumn => "TSK1_P_KEY";

        /// <summary>Named as the generated reader named it, so ported code still compiles.</summary>
        public TSK1 InitializeTsk1(TSK1 record) => Initialize(record);
    }
}
