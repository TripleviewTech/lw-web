// Record shapes generated from the CAD table definitions in
// TableDefs/LwTablesCAD, so the columns cannot drift. Reader behaviour lives in
// LaTable<T>; see Services/Data/LaTable.cs.
//
// These five carry the unit recommendation the assign screens make:
//
//   CAD7  one configuration row; CAD7_PLAN_TYPE picks how police and support
//         units are recommended - 'D' off a deployment plan, 'G' off the call's
//         own report area.
//   BMP2  the deployment plans per jurisdiction; one carries BMP2_ACTIVE = 'X'.
//   BMP1  a plan's rows: for a police report area, the up-to-eight report areas
//         to draw units from, in recommendation order.
//   RST2  which response areas a unit answers in, and its position within each.
//
// Fire and EMS do not use any of these - they read run cards; see
// LwTableRunCard.cs.

using System;
using LwCadSystemUX.Services.Data;

namespace LwTableCad7
{
    /// <summary>
    /// The CAD configuration row. Only the columns this console reads are
    /// declared; the table carries a great many more.
    /// </summary>
    public class CAD7
    {
        public char CAD7_P_KEY;

        /// <summary>'D' deployment plan, 'G' the call's own report area.</summary>
        public char CAD7_PLAN_TYPE;

        public char CAD7_CAD_P;
        public char CAD7_CAD_F;
        public char CAD7_CAD_E;
        public char CAD7_CAD_O;
        public char CAD7_TMOUT_SW;
        public short CAD7_TMOUT_MINS;

        // Take Call / Self Init switches (TakeCall.xaml.cs:330-356).
        public char CAD7_PLAY_911;
        public char CAD7_PLAY_HIS;
        public char CAD7_PLAY_PAT;
        public char CAD7_VISUAL_911;
        public char CAD7_VISUAL_HIS;
        public char CAD7_VISUAL_PAT;
        public char CAD7_E911_XFER;
        public char CAD7_SELF_E;
        public char CAD7_TRAF_E;
        public char CAD7_PAT_SW;
        public char CAD7_DUP_SW;
        public short CAD7_DUP_RANGE;
        public char CAD7_PREM_SW;
    }

    /// <summary>Reader for dbo.CAD7, keyed on CAD7_P_KEY.</summary>
    public class Cad7_RW : LaTable<CAD7>
    {
        public Cad7_RW() { }
        public Cad7_RW(string connectString) : base(connectString) { }

        protected override string TableName => "CAD7";
        protected override string[] KeyColumns => new[] { "CAD7_P_KEY" };
    }
}

namespace LwTableBmp1
{
    /// <summary>
    /// One row of a deployment plan: for a jurisdiction, plan and police report
    /// area, the report areas to recommend units from, in order.
    /// </summary>
    public class BMP1
    {
        public string BMP1_JUR;
        public string BMP1_PLAN_NO;
        public string BMP1_POLICE;
        public string BMP1_RPT_AREA_1;
        public string BMP1_RPT_AREA_2;
        public string BMP1_RPT_AREA_3;
        public string BMP1_RPT_AREA_4;
        public string BMP1_RPT_AREA_5;
        public string BMP1_RPT_AREA_6;
        public string BMP1_RPT_AREA_7;
        public string BMP1_RPT_AREA_8;
    }

    /// <summary>Reader for dbo.BMP1, keyed on jurisdiction, plan and police area.</summary>
    public class Bmp1_RW : LaTable<BMP1>
    {
        public Bmp1_RW() { }
        public Bmp1_RW(string connectString) : base(connectString) { }

        protected override string TableName => "BMP1";

        protected override string[] KeyColumns =>
            new[] { "BMP1_JUR", "BMP1_PLAN_NO", "BMP1_POLICE" };
    }
}

namespace LwTableBmp2
{
    /// <summary>A deployment plan. The one carrying BMP2_ACTIVE = 'X' is in force.</summary>
    public class BMP2
    {
        public string BMP2_JUR;
        public string BMP2_PLAN_NO;
        public char BMP2_ACTIVE;
        public string BMP2_OVERVIEW;
        public string BMP2_REMARKS;
    }

    /// <summary>Reader for dbo.BMP2, keyed on jurisdiction and plan.</summary>
    public class Bmp2_RW : LaTable<BMP2>
    {
        public Bmp2_RW() { }
        public Bmp2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "BMP2";
        protected override string[] KeyColumns => new[] { "BMP2_JUR", "BMP2_PLAN_NO" };
    }
}

namespace LwTableRst2
{
    /// <summary>
    /// A unit's response areas. RST2_AREA_POS orders the units within one area,
    /// which is what decides who is recommended first out of that area.
    /// </summary>
    public class RST2
    {
        public string RST2_UNIT_NO;
        public string RST2_JUR;
        public string RST2_R_AREA;
        public short RST2_AREA_POS;
        public DateTime? RST2_TRIGGER_DATE;
    }

    /// <summary>Reader for dbo.RST2, keyed on unit, jurisdiction and area.</summary>
    public class Rst2_RW : LaTable<RST2>
    {
        public Rst2_RW() { }
        public Rst2_RW(string connectString) : base(connectString) { }

        protected override string TableName => "RST2";

        protected override string[] KeyColumns =>
            new[] { "RST2_UNIT_NO", "RST2_JUR", "RST2_R_AREA" };
    }
}
