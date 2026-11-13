using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Units: status, release, switching and the roster.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> UnitFunctions()
        {
            // ---------------------------------------------------------------- built
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD135" },
                Title = "Unit Status",
                Area = "Units",
                Handler = FunctionHandler.UnitStatus,
                Needs = FunctionNeeds.SelectedUnit,
                Legacy = "LwCad135 Unit Status Change (in-process via Gl_*)",
                Source = "LwCad135\\Cad135.xaml.cs, TabA.cs",
                Purpose = "Built in this console - see ViewModels/UnitFunctionViewModel.cs and CadWriteBack.ApplyUnitChange.",
                Invocation = "WinA.LaunchProgram matches \"lwcad135\" and opens Cad135 with the Gl_* globals.",
                Writes = "DSU1 times, RST1, DSE1, DST1 log, DSN1, DSC1; TSK1 202/404/301/401; FAT2",
                Notes = new[]
                {
                    "Legacy rejects stacked and delayed calls - the call must be Active.",
                    "Group change runs LwCad136 (Multi Status Change), Complete runs LwCad108 - neither is built here yet."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD141", "CAD108", "CAD123" },
                Title = "Release Unit",
                Area = "Units",
                Handler = FunctionHandler.ReleaseUnit,
                Needs = FunctionNeeds.SelectedUnit,
                Legacy = "LwCad141 -> LwCad108 Complete / Release (mode r); LwCad123 quick release",
                Source = "LwCad141; LwCad108\\Cad108.xaml.cs, TabA.cs:332-520, 2990-3070; LwCad123\\Cad123.xaml.cs",
                Purpose = "Built in this console as the Release mode of the unit panel.",
                Invocation = "LwCad141 runs hidden: one RST1 unit on the call is used directly, otherwise LwCad179 " +
                             "picks one; then LwCad108 runs in-process with CadMode r.",
                Writes = "DSU1 CMP_DATE, RST1 back to status 1, DST1; the last unit off a call completes DSE1 and " +
                         "deletes STK1 / EMG2, or restacks it (STK1 'S') when CCC1_RESTACK allows",
                Notes = new[]
                {
                    "Docs\\10-Discovery\\02-Program-Inventory.md says 141 duplicates 123 - it wraps 108.",
                    "108 in mode k (Complete) auto-assigns a case number when CCC1_CASE_NO and AGC1_CASE_NO are Y, " +
                    "and exports NFIRS / NERIS / Firehouse - none of that is built here yet."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADLOCATIONCHANGE" },
                Title = "Location Change",
                Area = "Units",
                Handler = FunctionHandler.LocationChange,
                Needs = FunctionNeeds.SelectedUnit,
                Legacy = "LwCad135 Location screen",
                Source = "LwCad135\\TabA.cs",
                Purpose = "Built in this console as the Location mode of the unit panel. Legacy has no program id of " +
                          "its own for it; this entry exists so the row menu resolves the same way as a key."
            };

            // ---------------------------------------------------------------- stubs
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD175" },
                Title = "Switch Unit",
                Area = "Units",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad175 Switch Unit / Apparatus -> LwCad108 (mode r)",
                Source = "LwCad175\\WinA.cs",
                Purpose = "Replaces one unit on a call with another.",
                Invocation = "Exe with an event, or with a unit (its RST1 row supplies the event). Locks STK1 when an SKey is given.",
                Writes = "DSC1 dispatch number (optimistic update, retried, DSU1 checked unique), DSU1 copied from DSE1, " +
                         "RST1 status 3 + keys + TEMP_JUR + timeout from CCC1 ?T_O_TRVL, STK1 'A', DSE1 DSP/ENR if empty; " +
                         "then the old unit is released through LwCad108; TSK1 202/404/301; FatPot",
                Rules = new[]
                {
                    "Replacement must have the same agency type and not already be on this call.",
                    "Confirms when the unit is on another call, is locked, or is from another jurisdiction."
                },
                Notes = new[] { "Possible bug: launched by unit only, the DSE1 key may not be set before GetRow." },
                ToBuild = new[] { "Assign (one unit) + Release Unit in a single CadWriteBack transaction." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD136" },
                Title = "Multi Status Change",
                Area = "Units",
                Needs = FunctionNeeds.ActiveCall,
                Legacy = "LwCad136 (from LwCad135)",
                Source = "LwCad136\\Cad136.xaml.cs, WinA.cs",
                Purpose = "Enroute / Arrived / Cmp-Scene / Complete for every unit on the call at once.",
                Writes = "DSU1, RST1, DSE1, DST1; deletes STK1 / EMG2 on completion; Firehouse, NFIRS, MDC600",
                Rules = new[] { "Does not assign case numbers (warns first)." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD129" },
                Title = "Activate Unit",
                Area = "Units",
                Handler = FunctionHandler.ActivateUnit,
                Legacy = "LwCad129 Activate Unit/Apparatus (in-process via Gl_*)",
                Source = "LwCad129\\Cad129.xaml.cs:480-600",
                Purpose = "Puts a unit on the roster (RST1). Built in this console - see ViewModels/UnitActivateViewModel.cs.",
                Reads = "JUR1, AGC1 (AGC1_LIC_AGC), MLB1 05/04 unit type and 05/10 station, UNT1",
                Writes = "RST1 STATUS 1, TEMP_UNIT 'A'; RST2 for up to 5 response areas",
                Rules = new[] { "Fire / EMS need a station, police an employee id; jurisdiction is required." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD110" },
                Title = "View Unit Record",
                Area = "Units",
                Needs = FunctionNeeds.UnitAssignment,
                Legacy = "LwCad110 View CAD Unit Record (in-process via Gl_*)",
                Source = "LwCad110\\Cad110.xaml.cs",
                Purpose = "Read-only view of one unit's assignment on a call.",
                Invocation = "Needs DispNo1 / DispNo2, not just a call - otherwise \"Unable to access Unit record.\"",
                Reads = "DSU1, DSE1, DSV1, CAD7",
                Notes = new[]
                {
                    "WinA.cs:1214 runs it in-process with the error text \"Assign Call process error\" and a variable " +
                    "named assignCall - the label is wrong. The console references a prebuilt " +
                    "..\\..\\Development\\LwCad110.exe; check which binary is deployed."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADUNITMONITOR" },
                Title = "Unit Monitor",
                Area = "Units",
                Legacy = "LwCadUnitMonitor (console btn_MonitorOpen)",
                Source = "LwCadUnitMonitor\\WinA.cs, Timers.cs",
                Purpose = "A separate window of three RST1 + UST1 panels with PER8 officer photos.",
                Invocation = "Exe; the console warns when it is already running.",
                Writes = "DAT1_TS_MON; consumes TSK1 'M' messages for its employee / session every 10 s",
                Notes = new[]
                {
                    "Double-click to LwCAD135 is commented out.",
                    "Bug: the Photo2 subquery uses OFCR_1.",
                    "This console's own Unit Monitor panel already covers the board view."
                }
            };
        }
    }
}
