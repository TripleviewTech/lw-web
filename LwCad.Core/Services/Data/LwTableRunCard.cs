// Record shapes generated from the CAD table definitions in
// TableDefs/LwTablesCAD, so the columns cannot drift.
//
// Fire and EMS do not recommend off response areas the way police and support
// do. They read a RUN CARD - a row keyed on the call's response area, its call
// code and a coverage band - whose R1..R15 columns are fixed-width strings of
// six-character unit numbers, one string per apparatus column of the board.
// That is why LwCad132 and LwCad133 lay their apparatus out in columns.
//
// Fire reads RUN1, EMS reads RUN5. The two are the same shape.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableRun1
{
    /// <summary>The fire run card, as LwCad132 reads it.</summary>
    public class RUN1
    {
        public string RUN1_RUN_NO;
        public int RUN1_CALL_CODE;
        public char RUN1_COVERAGE;
        public string RUN1_OVERVIEW;
        public string RUN1_R1;
        public string RUN1_R2;
        public string RUN1_R3;
        public string RUN1_R4;
        public string RUN1_R5;
        public string RUN1_R6;
        public string RUN1_R7;
        public string RUN1_R8;
        public string RUN1_R9;
        public string RUN1_R10;
        public string RUN1_R11;
        public string RUN1_R12;
        public string RUN1_R13;
        public string RUN1_R14;
        public string RUN1_R15;
        public string RUN1_REMARKS;
        public char RUN1_BOX_TYPE;

        // The box location - Take Call's B/ lookup and DSE1_ROUTING_ID override.
        public string RUN1_LOC_JUR;
        public string RUN1_LOC_ST_NAM;
        public decimal RUN1_LOC_ST_NO;
        public string RUN1_LOC_SUB_NO;
        public string RUN1_LOC_UNIT;
        public string RUN1_LOC_X_ST;

        /// <summary>The R columns in order, which is the order LwCad132 reads them.</summary>
        public string[] Runs => new[]
        {
            RUN1_R1, RUN1_R2, RUN1_R3, RUN1_R4, RUN1_R5, RUN1_R6, RUN1_R7, RUN1_R8,
            RUN1_R9, RUN1_R10, RUN1_R11, RUN1_R12, RUN1_R13, RUN1_R14, RUN1_R15
        };
    }

    /// <summary>Reader for dbo.RUN1, keyed on run number, call code and coverage.</summary>
    public class Run1_RW : LaTable<RUN1>
    {
        public Run1_RW() { }
        public Run1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "RUN1";

        protected override string[] KeyColumns =>
            new[] { "RUN1_RUN_NO", "RUN1_CALL_CODE", "RUN1_COVERAGE" };
    }
}

namespace LwTableRun5
{
    /// <summary>The EMS run card, as LwCad133 reads it.</summary>
    public class RUN5
    {
        public string RUN5_RUN_NO;
        public int RUN5_CALL_CODE;
        public char RUN5_COVERAGE;
        public string RUN5_OVERVIEW;
        public string RUN5_R1;
        public string RUN5_R2;
        public string RUN5_R3;
        public string RUN5_R4;
        public string RUN5_R5;
        public string RUN5_R6;
        public string RUN5_R7;
        public string RUN5_R8;
        public string RUN5_R9;
        public string RUN5_R10;
        public string RUN5_R11;
        public string RUN5_R12;
        public string RUN5_R13;
        public string RUN5_R14;
        public string RUN5_R15;
        public string RUN5_REMARKS;

        /// <summary>The R columns in order, which is the order LwCad133 reads them.</summary>
        public string[] Runs => new[]
        {
            RUN5_R1, RUN5_R2, RUN5_R3, RUN5_R4, RUN5_R5, RUN5_R6, RUN5_R7, RUN5_R8,
            RUN5_R9, RUN5_R10, RUN5_R11, RUN5_R12, RUN5_R13, RUN5_R14, RUN5_R15
        };
    }

    /// <summary>Reader for dbo.RUN5, keyed on run number, call code and coverage.</summary>
    public class Run5_RW : LaTable<RUN5>
    {
        public Run5_RW() { }
        public Run5_RW(string connectString) : base(connectString) { }

        protected override string TableName => "RUN5";

        protected override string[] KeyColumns =>
            new[] { "RUN5_RUN_NO", "RUN5_CALL_CODE", "RUN5_COVERAGE" };
    }
}
