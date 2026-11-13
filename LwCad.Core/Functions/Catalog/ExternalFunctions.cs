using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Programs outside the LwCad family - roster, shared LA systems, and binaries
    // with no source in the LW tree.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> ExternalFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "RSTPREPLAN" },
                Title = "Pre-Roster",
                Area = "External",
                Legacy = "LwRstPrePlan CAD Preplan Roster",
                Source = "LwRstPrePlan\\ActivatePrePlan.xaml.cs, ClearPrePlan.xaml.cs, WindowA.cs",
                Purpose = "Maintains the staged roster (RST5) and activates it by agency, shift (TOUR_C) and unit type.",
                Writes = "Activate: optionally deletes the shift's RST1 units, copies RST5 into RST1 at status 1, " +
                         "skipping units on a call. Also Clear and Change Unit No.",
                Notes = new[]
                {
                    "Sister program LwRstCodes \"CAD Duty Roster\" edits RST1 live (log on / off, change unit number), " +
                    "writing DST1 log rows, DSC1 and RST2."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "RSTCODES" },
                Title = "Duty Roster",
                Area = "External",
                Legacy = "LwRstCodes CAD Duty Roster",
                Source = "LwRstCodes",
                Purpose = "Live roster maintenance: log units on and off, multi log-off, change unit number.",
                Writes = "RST1, RST2, DST1 log transactions, DSC1"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "PTLSYSTEM", "PTL001", "PTLHITLIST" },
                Title = "Patrol File",
                Area = "External",
                Legacy = "LaPtlSystem / LaPtl001 / LaPtlHitList (shared LA code)",
                Source = "C:\\Development\\Git\\Missouri\\LA\\Apps\\LaPtlSystem, LaPtl001, LaPtlHitList",
                Purpose = "Patrol / premise watch records (PTL1): location, person, vehicle, type, review date, notify flag.",
                Reads = "PTL1, linked REG1 / ROR2 / WAR1; hit list uses la_fn_ValidatePTL1HitLevel",
                Notes = new[]
                {
                    "Hit list filter: REVIEW_DATE >= now OR NOTIFY = 'Y' OR REVIEW_DATE IS NULL.",
                    "Shared with the other agencies - not in the LW Apps tree; LwTrp001 / LwRor001 also update PTL1."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "RDXSYSTEM", "RDX001" },
                Title = "Rolodex",
                Area = "External",
                Legacy = "LaRdxSystem / LaRdx001 (shared LA code)",
                Source = "C:\\Development\\Git\\Missouri\\LA\\Apps\\LaRdxSystem, LaRdx001",
                Purpose = "Contact cards (RDX1): names, addresses, numbers, premise code, directions.",
                Reads = "RDX1; security from RDX9"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "MDCEMAIL" },
                Title = "Mailbox",
                Area = "External",
                Legacy = "LwMDCEMAIL (console img_AccessEmail)",
                Source = "No source in any repository.",
                Purpose = "MDC e-mail between the console and the vehicles.",
                Reads = "The console's unread check reads MDCEmailMessages (UserId, MailBox = 'Inbox', ReadFlag = 0), Timers.cs:2089",
                Notes = new[] { "The top bar's inbox button is the natural home for this." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "DMV222" },
                Title = "CJIS / State Query",
                Area = "External",
                Legacy = "LwDMV222 (console btn_CJIS)",
                Source = "No source found - probably a MicroFocus .int or an external program.",
                Purpose = "State / CJIS query."
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "RSTQUICKVIEW" },
                Title = "Unit Quick View",
                Area = "External",
                Needs = FunctionNeeds.SelectedUnit,
                Legacy = "LwRstQuickView (console unit emergency click)",
                Source = "Only an MB copy exists: C:\\Development\\Git\\Mass\\MB\\Apps\\MbRstQuickView",
                Purpose = "Quick view of a unit's roster row."
            };
        }
    }
}
