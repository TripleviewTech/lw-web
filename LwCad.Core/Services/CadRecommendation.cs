using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwTableBmp1;
using LwTableBmp2;
using LwTableRst2;
using LwTableRun1;
using LwTableRun5;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// The unit recommendation the legacy assign screens make, ported from
    /// <c>LwCad131/132/133/134 TabA.cs</c>.
    ///
    /// The two disciplines work completely differently, which is why they are two
    /// methods rather than one rule with a switch:
    ///
    /// <list type="bullet">
    /// <item><b>Police and Support</b> (LwCad131, LwCad134 - identical code) draw
    /// off <b>deployment plans</b>. CAD7_PLAN_TYPE picks between a plan
    /// (BMP2 / BMP1, which name up to eight report areas in order) and the call's
    /// own report area. Either way the areas resolve to units through RST2.</item>
    /// <item><b>Fire and EMS</b> (LwCad132, LwCad133) do not look at areas at all.
    /// They read a <b>run card</b> - RUN1 for fire, RUN5 for EMS - whose R columns
    /// are fixed-width strings of six-character unit numbers, one string per
    /// apparatus column of the board.</item>
    /// </list>
    ///
    /// This is the membership and the order only. Laying the result out - the eight
    /// numbered slots, or the rows per apparatus type - stays in CadAssignment.
    /// </summary>
    internal static class CadRecommendation
    {
        /// <summary>A unit the plan or the run card named, and where it came in.</summary>
        internal sealed class Recommended
        {
            public string UnitNo;

            /// <summary>Which apparatus column named it. Empty for police and support.</summary>
            public string Group = string.Empty;

            /// <summary>Order across the whole recommendation.</summary>
            public int Rank;
        }

        // ------------------------------------------------------- police / support

        /// <summary>
        /// LwCad131 / LwCad134 <c>TabA_LoadRecommendations_D</c> and <c>_G</c>.
        ///
        /// 'D' walks the active deployment plan: BMP2 gives the plan in force for
        /// the jurisdiction, BMP1 gives that plan's row for the call's police
        /// report area, and its BMP1_RPT_AREA_1..8 are the areas to draw from, in
        /// order. 'G' skips the plan and uses the call's own area as the only one.
        ///
        /// Units come out in (plan area position, RST2_AREA_POS) order, which is
        /// the sort the legacy DataView applies before filling F1..F8.
        /// </summary>
        public static List<Recommended> ForDeployment(
            char planType, string jurisdiction, string policeArea,
            Bmp2_RW bmp2, Bmp1_RW bmp1, Rst2_RW rst2, Action<object> note)
        {
            var found = new List<Recommended>();

            var jur = (jurisdiction ?? string.Empty).Trim();
            var area = (policeArea ?? string.Empty).Trim();
            if (jur.Length == 0) return found;

            var areas = new List<string>();

            if (char.ToUpperInvariant(planType) == 'D')
            {
                var plan = ActivePlan(jur, bmp2, note);
                if (plan.Length == 0) return found;

                var row = bmp1.GetRow(new BMP1
                {
                    BMP1_JUR = jur,
                    BMP1_PLAN_NO = plan,
                    BMP1_POLICE = area
                });

                note(bmp1);
                if (bmp1.RecordStatus != 0) return found;

                foreach (var candidate in new[]
                         {
                             row.BMP1_RPT_AREA_1, row.BMP1_RPT_AREA_2, row.BMP1_RPT_AREA_3,
                             row.BMP1_RPT_AREA_4, row.BMP1_RPT_AREA_5, row.BMP1_RPT_AREA_6,
                             row.BMP1_RPT_AREA_7, row.BMP1_RPT_AREA_8
                         })
                {
                    var trimmed = (candidate ?? string.Empty).Trim();
                    if (trimmed.Length > 0) areas.Add(trimmed);
                }
            }
            else
            {
                // 'G' - no plan, the call's own report area and nothing else.
                if (area.Length == 0) return found;
                areas.Add(area);
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int position = 0; position < areas.Count; position++)
            {
                // RST2 rows for this area, in the order the area itself prefers.
                var rows = rst2.GetList(
                    where: "RST2_JUR = '" + Escape(jur) + "' AND RST2_R_AREA = '" + Escape(areas[position]) + "'",
                    orderBy: "RST2_AREA_POS");

                note(rst2);

                foreach (var row in rows)
                {
                    var unitNo = (row.RST2_UNIT_NO ?? string.Empty).Trim();
                    if (unitNo.Length == 0 || !seen.Add(unitNo)) continue;

                    // The plan's area order is the outer sort, the area's own
                    // position the inner one - RecommendSort "RecBmpPos, RecPos".
                    found.Add(new Recommended
                    {
                        UnitNo = unitNo,
                        Rank = (position * 1000) + row.RST2_AREA_POS
                    });
                }
            }

            return found.OrderBy(r => r.Rank).ToList();
        }

        /// <summary>The plan in force for a jurisdiction - BMP2_ACTIVE = 'X'.</summary>
        private static string ActivePlan(string jur, Bmp2_RW bmp2, Action<object> note)
        {
            var plans = bmp2.GetList(
                where: "BMP2_ACTIVE = 'X' AND BMP2_JUR = '" + Escape(jur) + "'");

            note(bmp2);

            var plan = plans.FirstOrDefault();
            return plan == null ? string.Empty : (plan.BMP2_PLAN_NO ?? string.Empty).Trim();
        }

        // ------------------------------------------------------------- run cards

        /// <summary>
        /// The group each R string fills, and the heading it is shown under.
        ///
        /// Lowell's fire card is laid out by ALARM, not by apparatus type:
        /// LwCad132 TabA_LoadRecommendations reads R1..R6 as the 1st to 6th alarm
        /// (ten units each), R7..R9 as the 7th to 9th (four each), then EMS, Haz and
        /// Still (four each), and nothing past R12. Easthampton's R1..R13 are
        /// apparatus columns - Chief, Pumper, Ladder - which is a different card.
        /// </summary>
        private static readonly string[] FireColumns =
        {
            "1st Alarm", "2nd Alarm", "3rd Alarm", "4th Alarm", "5th Alarm", "6th Alarm",
            "7th Alarm", "8th Alarm", "9th Alarm", "EMS", "Haz", "Still"
        };

        /// <summary>Units each fire column holds on the LwCad132 board.</summary>
        private static readonly int[] FireSlots = { 10, 10, 10, 10, 10, 10, 4, 4, 4, 4, 4, 4 };

        /// <summary>
        /// LwCad133, R1..R9, ten units each. Its two AEMT and two EMT rows are one
        /// group apiece here, the way the Figma board treats duplicated columns.
        /// </summary>
        private static readonly string[] EmsColumns =
        {
            "Chief", "Ambulance", "Capt / Lt", "Rescue", "AEMT", "AEMT", "EMT", "EMT", "Other"
        };

        private static readonly int[] EmsSlots = { 10, 10, 10, 10, 10, 10, 10, 10, 10 };

        /// <summary>
        /// LwCad132 <c>TabA_GetBestRunCard</c> then <c>TabA_LoadRecommendations</c>.
        /// <paramref name="fireBox"/> is DSE1_FIRE_1 in Lowell, not DSE1_RESP_AREA.
        /// </summary>
        public static List<Recommended> ForFire(
            string fireBox, int callCode, Run1_RW run1, Action<object> note)
        {
            var card = BestFireCard(fireBox, callCode, run1, note);
            return card == null ? new List<Recommended>() : Unpack(card.Runs, FireColumns, FireSlots);
        }

        /// <summary>LwCad133, the same against RUN5, keyed on DSE1_EMS_1.</summary>
        public static List<Recommended> ForEms(
            string emsArea, int callCode, Run5_RW run5, Action<object> note)
        {
            var card = BestEmsCard(emsArea, callCode, run5, note);
            return card == null ? new List<Recommended>() : Unpack(card.Runs, EmsColumns, EmsSlots);
        }

        /// <summary>
        /// The coverage ladder both screens walk: the card for this call code at 24
        /// hour coverage, then at the coverage band the clock is in, then the area's
        /// default card (call code 0) through the same two steps.
        /// </summary>
        private static IEnumerable<(int code, char coverage)> CardKeys(int callCode)
        {
            var band = DateTime.Now.Hour > 17 || DateTime.Now.Hour < 6 ? 'P' : 'A';

            yield return (callCode, '4');
            yield return (callCode, band);
            yield return (0, '4');
            yield return (0, band);
        }

        private static RUN1 BestFireCard(string responseArea, int callCode,
                                         Run1_RW run1, Action<object> note)
        {
            var area = (responseArea ?? string.Empty).Trim();
            if (area.Length == 0) return null;

            foreach (var (code, coverage) in CardKeys(callCode))
            {
                var card = run1.GetRow(new RUN1
                {
                    RUN1_RUN_NO = area,
                    RUN1_CALL_CODE = code,
                    RUN1_COVERAGE = coverage
                });

                if (run1.RecordStatus == 0) return card;
            }

            note(run1);
            return null;
        }

        private static RUN5 BestEmsCard(string responseArea, int callCode,
                                        Run5_RW run5, Action<object> note)
        {
            var area = (responseArea ?? string.Empty).Trim();
            if (area.Length == 0) return null;

            foreach (var (code, coverage) in CardKeys(callCode))
            {
                var card = run5.GetRow(new RUN5
                {
                    RUN5_RUN_NO = area,
                    RUN5_CALL_CODE = code,
                    RUN5_COVERAGE = coverage
                });

                if (run5.RecordStatus == 0) return card;
            }

            note(run5);
            return null;
        }

        /// <summary>
        /// Splits the run card's R strings into the six-character unit numbers they
        /// pack, in column order, skipping the blanks a part-filled card leaves. A
        /// column is read no further than the slots the legacy board gives it.
        /// </summary>
        private static List<Recommended> Unpack(string[] runs, string[] columns, int[] slots)
        {
            const int UnitWidth = 6;

            var found = new List<Recommended>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int rank = 0;

            for (int column = 0; column < runs.Length && column < columns.Length; column++)
            {
                var run = runs[column] ?? string.Empty;
                var limit = Math.Min(run.Length, slots[column] * UnitWidth);

                for (int at = 0; at + UnitWidth <= limit; at += UnitWidth)
                {
                    var unitNo = run.Substring(at, UnitWidth).Trim();
                    if (unitNo.Length == 0 || !seen.Add(unitNo)) continue;

                    found.Add(new Recommended
                    {
                        UnitNo = unitNo,
                        Group = columns[column],
                        Rank = rank++
                    });
                }
            }

            return found;
        }

        /// <summary>
        /// The legacy builds these clauses by concatenation, and the values are
        /// short codes off the same database rather than anything typed. Doubling
        /// the quote keeps a stray apostrophe from ending the literal all the same.
        /// </summary>
        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("'", "''");
    }
}
