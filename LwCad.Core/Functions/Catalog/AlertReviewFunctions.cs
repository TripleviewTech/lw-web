using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Alerts, bulletins, review and search - mostly read-only screens.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> AlertReviewFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD301" },
                Title = "CAD Alert",
                Area = "Alerts & Review",
                Handler = FunctionHandler.CadAlerts,
                Legacy = "LwCad301 Call Alerts (+ LwCad302 add, LwCad303 per call)",
                Source = "LwCad301\\TabA.cs, Cad301.xaml.cs:236-356",
                Purpose = "Call alerts waiting to be acknowledged. Built in this console - see " +
                          "ViewModels/CadAlertsViewModel.cs and Services/CadWriteBack.Alerts.cs.",
                Invocation = "Exe. With an event: that call only. Without: every STK1 call (S / A) with a DST1 " +
                             "ALERT_SW 'P', by priority. The console's img_CadAlert blanks the event first to get " +
                             "the list; btn_Alert opens it for the selected call.",
                Writes = "Acknowledge: DST1 ALERT_SW 'X', ACK_DATE, ACK_BY; TSK1 301",
                Rules = new[] { "Delayed calls are rejected." },
                Notes = new[]
                {
                    "A remark containing \"Call received from CADfusion\" also sends a FatPot acknowledge.",
                    "Rows are yellow when DSE1_FATPOT_ID is set.",
                    "The legacy call list joins DST1 without DISTINCT, so a call with two pending alerts is listed twice; " +
                    "this console lists it once with a count.",
                    "The legacy acknowledged each ticked alert on its own connection and swallowed failures; here they " +
                    "commit together."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD302" },
                Title = "Add Call Alert",
                Area = "Alerts & Review",
                Handler = FunctionHandler.CadAlerts,
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad302 Add Call Alert",
                Source = "LwCad302\\TabA.cs:28-90",
                Purpose = "Adds an alert to the selected call.",
                Writes = "One DST1 per DSE1 on the call's global number (RCD_TYPE 'E', ALERT_SW 'P', 5/7/999); TSK1 301"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD303" },
                Title = "Call Alerts (single call)",
                Area = "Alerts & Review",
                Handler = FunctionHandler.CadAlerts,
                IsMenuFunction = false,
                Legacy = "LwCad303 (from LwCad132 / LwCad133)",
                Source = "LwCad303\\TabA.cs",
                Purpose = "LwCad301's acknowledge for one call, opened from the fire and EMS assign screens."
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD699" },
                Title = "View Active Call",
                Area = "Alerts & Review",
                Legacy = "LwCad699 -> LwCadTransUnits (event selected) or LwCadCallReview (none)",
                Source = "LwCad699\\Cad699.xaml.cs",
                Purpose = "A router with no window of its own; passes mode v.",
                ToBuild = new[] { "A call detail panel over the board (event, units, transactions) - LwCadTransUnits' tabs." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADTRANSUNITS" },
                Title = "Call Detail",
                Area = "Alerts & Review",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCadTransUnits (console btn_View)",
                Source = "LwCadTransUnits\\TranUnit.xaml.cs, TabA/B/C.cs",
                Purpose = "The per-call viewer used everywhere: tab A event transactions (-> 119), B units (-> 110), " +
                          "C unit transactions (-> 115); links to View Event (104), Abstract (CadRpt520), 610.",
                Writes = "Reactivate within _reactHrs when CAD9_REACT = 'Y': inserts STK1 'S', nulls DSE1 completion " +
                         "and disposition; FatPot + TSK1 301",
                Notes = new[] { "NFIRS / NERIS transfer flags from CAD7." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD104" },
                Title = "View Event",
                Area = "Alerts & Review",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad104 View CAD Event Record",
                Source = "LwCad104\\Cad104.xaml.cs",
                Purpose = "Read-only view of the event record.",
                Reads = "DSE1, DSN1, DSV1, CAD7, CAD9",
                Notes = new[] { "Opens CAD030 history, PMM001, CCC910 and LaPtlHitList." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADCALLREVIEW" },
                Title = "Call Review",
                Area = "Alerts & Review",
                Legacy = "LwCadCallReview",
                Source = "LwCadCallReview\\TabA.cs:1187-1440",
                Purpose = "Lists calls by received date, jurisdiction and agency; opens LwCadTransUnits.",
                Reads = "DSE1 LEFT JOIN STK1; security from CAD9 / PER1 / AGC1",
                Notes = new[]
                {
                    "Mode p is Lowell's Public Log: the end time snaps to the last shift boundary (07:30 / 15:30 / 23:30), " +
                    "and call codes < 8001, 400/401/403/405/406/610/653/1440/5, 1300-1332, 1500-1508 and 7001-7031 are " +
                    "excluded - hard-coded, agency specific."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD030", "CAD031" },
                Title = "Previous Call History",
                Area = "Alerts & Review",
                Legacy = "LwCad030 (LwCad031 is an older near-copy)",
                Source = "LwCad030\\Cad8801.xaml.cs, TabA.cs; LwCad031\\TabA.cs",
                Purpose = "TOP 500 prior calls at a location.",
                Invocation = "Standard 13 arguments plus [13] Jur, [14] StName, [15] StNo, [16] SubNo, [17] Unit, " +
                             "[18] XSt; underscores in street names are spaces. Called from the call screens, not the console.",
                Reads = "DSE1 joined to CCC1 where CCC1_DISP_HIST = 'Y'; street + number, or street / cross street either way; " +
                        "* / % wildcards, + suffix = greater or equal",
                Notes = new[]
                {
                    "Call code 800 is limited to the last 30 days.",
                    "gl_SrchType is never set, so every event type always shows.",
                    "LwCad031: TOP 50, exact street only, no cross street, no 800 filter - retire in favour of 030."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADSEARCH", "CAD900", "CAD910", "CAD920", "CAD930", "CAD940" },
                Title = "CAD Search",
                Area = "Alerts & Review",
                Legacy = "LwCadSearch -> LwCad900 events / 910 units / 920 transactions / 930 names / 940 vehicles",
                Source = "LwCadSearch\\CadSearch.xaml.cs:171-292; LwCad9x0\\TabA.cs",
                Purpose = "Read-only searches, each opening LwCadTransUnits.",
                Reads = "DSE1 (900), DSU1 (910), DST1 join DSE1 (920), DSN1 (930), DSV1 (940); security from PER1 / AGC1 / CAD9",
                Notes = new[] { "900 can show results on LwMapDisplay." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADLOCATIONSEARCH", "MULTILOCSRCH" },
                Title = "Location Search",
                Area = "Alerts & Review",
                Legacy = "LwCadLocationSearch; the console's btn_SrchLoc runs LwMultiLocSrch.exe instead",
                Source = "LwCadLocationSearch\\TabA.cs (LwMultiLocSrch has no source in the LW tree)",
                Purpose = "Validates an address (LaGeo040) and shows the PMM1 premise, GEO1 geocode, history and patrol hits.",
                Invocation = "LwMultiLocSrch takes non-standard arguments: UserID|Top|Left|l|^Jur|StNo|StName|XSt|" +
                             "Police1|Fire1|PmmNo|0| with spaces as '!'. Needs a selected call.",
                Reads = "JUR1 (JUR1_GEO_JUR = 'Y'), PMM1, GEO1"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADBULLETINS" },
                Title = "CAD Bulletins",
                Area = "Alerts & Review",
                Legacy = "LwCadBulletins (console img_Bulletin)",
                Source = "LwCadBulletins\\WindowA.cs, Attachment.cs",
                Purpose = "Bulletins by type, jurisdiction and agency, with attachments.",
                Reads = "MSG2, MSF2 -> ATT1",
                Writes = "MSG2 / MSF2",
                Rules = new[] { "New / edit / delete gated by CAD9 NEW_BULL, EDIT_BULL, DEL_BULL, EDIT_B_TYP, DEL_B_TYP." },
                Notes = new[] { "The console polls the MSG2 latest entry date for its indicator (Timers.cs:1876)." }
            };
        }
    }
}
