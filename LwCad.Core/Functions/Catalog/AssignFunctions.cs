using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Putting units on calls.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> AssignFunctions()
        {
            // ---------------------------------------------------------------- built
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD130", "CAD131", "CAD132", "CAD133", "CAD134" },
                Title = "Assign Call",
                Area = "Assign",
                Handler = FunctionHandler.Assign,
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad130 -> LwCad131 Police / 132 Fire / 133 EMS / 134 Support",
                Source = "LwCad130\\Cad130.xaml.cs:160-700; LwCad131-134\\TabA.cs",
                Purpose = "Built in this console - see ViewModels/AssignUnitsViewModel.cs, " +
                          "Services/CadRecommendation.cs and CadWriteBack.AssignUnits. Renamed Assign / Assist " +
                          "on an Active call.",
                Invocation = "LwCad130 routes by EventType to 131-134 in-process, looping while the child " +
                             "returns Gl_LinkAssign so the operator can move between agencies on one global number.",
                Writes = "Per unit: DSU1 (new DispNo from DSC1), RST1, STK1 (A / units list), DSE1 dispatch time; " +
                         "TSK1 202/404 and 301/401; FAT2",
                Notes = new[]
                {
                    "WinA.cs labels its in-process lwcad110 branch \"Assign Call process error\", but LwCad110 is " +
                    "View Unit Record. The assign program is LwCad130.",
                    "LwCad130 does not pass UnitNo or DispNo on to 131-134.",
                    "laConfig.AgencyId == \"HU\" skips the jurisdiction check in 131/133/134 (Harvard)."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD140" },
                Title = "Assign Assist Units",
                Area = "Assign",
                Handler = FunctionHandler.Assist,
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad140 -> LwCad179 picker -> LwCad130 (mode a)",
                Source = "LwCad140\\Cad140.xaml.cs",
                Purpose = "Adds units to a call already working. Here it opens the assign panel in Assist mode.",
                Invocation = "Exe, runs hidden: takes TOP 1 RST1 on the call or opens LwCad179, then LwCad130 in-process.",
                Notes = new[]
                {
                    "Because LwCad130 drops the unit, 140's unit lookup only acts as a gate - cancelling the picker aborts."
                }
            };

            // ---------------------------------------------------------------- stubs
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD138" },
                Title = "Send Other",
                Area = "Assign",
                Needs = FunctionNeeds.StackedCall,
                Legacy = "LwCad138 -> LwCad130 \"Send To Agency\"",
                Source = "LwCad138\\Cad138.xaml.cs (121 lines); LwCad130\\Cad130.xaml.cs:168-260, 990, 1259",
                Purpose = "Sends the call to another agency: creates a sibling event for that agency on the same " +
                          "global number, then optionally assigns it.",
                Invocation = "LwCad138 only relaunches \"LwCad130.exe\" (hard-coded, no agency prefix) with event type " +
                             "'x' and 0/0, which makes 130 show its agency chooser, then closes. No tables of its own.",
                Reads = "CAD7_CAD_P/F/E/O (agencies in use), DSE1 on the global number",
                Writes = "Sibling DSE1, DSC1 numbers, STK1 (S), DSV1 copy; TSK1 301/401/403; FAT2 DSE1 insert",
                Rules = new[]
                {
                    "Only agencies enabled in CAD7 and not already on the call are offered.",
                    "A completed agency's event offers \"Reactivate?\"."
                },
                ToBuild = new[]
                {
                    "An agency chooser in the assign panel; CadWriteBack: sibling DSE1 + STK1 under the call's global number."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD179" },
                Title = "Unit / Apparatus Select",
                Area = "Assign",
                IsMenuFunction = false,
                Legacy = "LwCad179 (a picker used by 140 / 141 / 144)",
                Source = "LwCad179\\TabA.cs",
                Purpose = "Picks a unit; returns gl_UnitNo and the event / dispatch keys from RST1.",
                Rules = new[] { "Mode a = assigned + available, u/r/f = assigned only, c = available only (clears the event)." },
                Notes = new[]
                {
                    "Bug: INNER JOINs STK1, so a unit not on a call can never appear.",
                    "Bug: the agency filter is an else-if chain - only the first ticked agency applies."
                }
            };
        }
    }
}
