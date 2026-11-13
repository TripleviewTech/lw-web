using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Calls: taking, releasing and reshaping events on the stack.
    // Sources are under C:\Development\Git\Mass\LW\Apps unless noted.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> CallFunctions()
        {
            // ---------------------------------------------------------------- built
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD101" },
                Title = "Take Call",
                Area = "Calls",
                Handler = FunctionHandler.TakeCall,
                Legacy = "LwCad101 (run in-process by the console as LwCadSystem\\TakeCall.cs)",
                Source = "LwCadSystem\\TakeCall.cs; LwCad101\\Cad100.xaml.cs, TabA.cs",
                Purpose = "New call entry. Built in this console - see ViewModels/TakeCallViewModel.cs " +
                          "and CadWriteBack.Dispatch.",
                Invocation = "WinA.LaunchProgram matches \"lwcad101\" and opens the TakeCall window as a dialog; " +
                             "the check on ActiveCallInProgress is there because F1 can start it twice.",
                Reads = "CCC1/CCC2, PMM1/2/4/6, RUN1, JUR1/3, CLY7, EMG3",
                Writes = "DSC1 counters, DSE1 per tasked agency, DSN1, DSV1, STK1 (S), DST1; deletes STK2 and EMG2",
                Notes = new[]
                {
                    "Legacy offers \"Assign Unit(s)/Apparatus Immediately?\" (LwCad130) for operator types S, D and U.",
                    "Duplicate call check (LwCad190) is sized by CAD7_DUP_RANGE.",
                    "An auto-loaded E911 call cannot be cancelled."
                }
            };

            // ---------------------------------------------------------------- stubs
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD142", "CAD102" },
                Title = "Release Call",
                Area = "Calls",
                Needs = FunctionNeeds.StackedCall,
                Legacy = "LwCad142 -> LwCad102 Release Stacked Call",
                Source = "LwCad142; LwCad102\\Cad102.xaml.cs:302-540, TabA.cs:88-210",
                Purpose = "Closes a stacked call that will not be worked - no unit was ever assigned.",
                Invocation = "LwCad142 runs hidden and opens LwCad102 in-process. Needs a selected call and its " +
                             "SKey, else \"Select call to be released.\"",
                Reads = "STK1, DSE1, MLB1 05/01 (dispositions)",
                Writes = "DSE1 (CMP_DATE, disposition, RECLASS defaults to the call code); deletes STK1; deletes EMG2",
                Rules = new[]
                {
                    "Only STK1_STATUS 'S' - \"Only unassigned calls can be released\".",
                    "Locks STK1 (STK1_LOCK_ID) while the form is open.",
                    "A disposition is required.",
                    "TSK1 301 / 401 afterwards; FAT2 DSE1 update."
                },
                Notes = new[]
                {
                    "LwCad102 forces mode 'a' whatever it is passed, so 142's 'r' is ignored.",
                    "The \"overwritten by another user\" check is commented out.",
                    "Not the same as releasing a unit - that is LwCad141 / LwCad108 (Release Unit)."
                },
                ToBuild = new[]
                {
                    "A release panel: disposition picker (MLB1 05/01) and remarks.",
                    "CadWriteBack.ReleaseStackedCall: DSE1 update + STK1 delete + EMG2 delete in one transaction, " +
                    "then TSK1 301/401 and FAT2."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD105" },
                Title = "Delayed Call",
                Area = "Calls",
                Legacy = "LwCad105 Delayed Call",
                Source = "LwCad105\\Cad105.xaml.cs, TabA.cs:417",
                Purpose = "Enters a call to be worked later (the Delayed monitor), or changes one.",
                Invocation = "Exe. No event = add a new delayed call; with an event it becomes mode 'c', which " +
                             "needs STK1_STATUS 'D' and takes the lock.",
                Writes = "DSE1, DSN1, DSC1 counters, STK1 (status D); TSK1 301",
                Rules = new[]
                {
                    "Received date/time may not be further ahead than the call code's delay limit " +
                    "(\"Time cannot exceed n Hrs\")."
                },
                Notes = new[]
                {
                    "The \"Release Delayed Call\" title (mode r) can never show - the mode is overwritten first.",
                    "\"Call Code cannot be used for delayed calls\" is commented out."
                },
                ToBuild = new[] { "Reuse the Take Call form with a delayed date/time; write STK1 'D'." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD121" },
                Title = "Replicate Call",
                Area = "Calls",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad121 Replicate Call",
                Source = "LwCad121\\Cad121.xaml.cs",
                Purpose = "Makes a new call from the selected one, optionally copying caller and vehicle.",
                Invocation = "Exe. Needs a selected call and SKey (\"Select call to Replicate.\"). Not for delayed calls.",
                Writes = "DSE1 (new), DSC1, STK1, DSU1, DSN1, DSV1",
                Rules = new[]
                {
                    "Agencies offered follow CAD9_POLICE / FIRE / EMS / OTHER.",
                    "Offers \"assign a Case Number?\" (LwCad144) and \"replicate another?\"."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD139" },
                Title = "Cross Reference Call",
                Area = "Calls",
                Needs = FunctionNeeds.StackedCall,
                Legacy = "LwCad139 Cross Reference Call",
                Source = "LwCad139\\Cad139.xaml.cs:250-1080",
                Purpose = "Voids a stacked call as a duplicate of an earlier one.",
                Invocation = "Exe. Needs a stacked call - not Active or Delayed. Locks STK1.",
                Reads = "DSE1 / STK1 / DST1 / CCC1 joins, filtered by jurisdiction, type and date",
                Writes = "DSE1_X_REF_*, VOID_ID, CMP_DATE, disposition 05/01/997; deletes the STK1 row; TSK1 301/401"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD137" },
                Title = "Reset Stack Timeout",
                Area = "Calls",
                Needs = FunctionNeeds.StackedCall,
                Legacy = "LwCad137 Reset Stack Timeout",
                Source = "LwCad137\\Cad137.xaml.cs:186-625",
                Purpose = "Restarts the stacked-call timer (the overdue flag on the Stacked monitor).",
                Invocation = "Exe. Needs a stacked call and SKey; rejects Active and Delayed calls. Locks STK1.",
                Reads = "CCC1 ?T_O_STCK by agency (the default minutes)",
                Writes = "STK1_T_O_MN, T_O_F_HR / T_O_F_MN (now), T_O_FLAG = 0; TSK1 301 and 414",
                Notes = new[]
                {
                    "TSK1 414 is not in LwCadTsk001's code list.",
                    "Locks with gl_UserID, where LwCad174/175 use gl_EmpId - keep one identity here."
                },
                ToBuild = new[] { "A one-field panel (minutes, defaulted from CCC1) and a single STK1 update." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD107" },
                Title = "Self Initiate",
                Area = "Calls",
                Handler = FunctionHandler.SelfInitiate,
                Legacy = "LwCad107 (run in-process by the console as LwCadSystem\\SelfInit.cs)",
                Source = "LwCadSystem\\SelfInit.cs; LwCad107\\Cad107.xaml.cs, TabA.cs",
                Purpose = "An officer-initiated call: event and unit assignment created together, already Active. " +
                          "Built in this console - see ViewModels/SelfInitViewModel.cs and CadWriteBack.SelfInitiate.",
                Invocation = "WinA.LaunchProgram matches \"lwcad107\" and opens SelfInit as a dialog. No context; " +
                             "the unit is typed on the form.",
                Writes = "DSE1, DSU1, DSV1 (U), STK1 (A), DSC1 event / dispatch / global numbers, RST1 " +
                         "(deleted for a temporary unit); TSK1 202/301/401-404; FAT2 DSE1/DSU1",
                Rules = new[]
                {
                    "Event type comes from RST1_AGC_TYPE.",
                    "CAD7_SELF_E: E = stamp en route only, A = arrived, P = ask \"Has Unit/Apparatus arrived?\".",
                    "A unit on another call is released first (LwCad123), or the call is refused.",
                    "An inactive unit is offered activation (LwCad129)."
                },
                Notes = new[]
                {
                    "No DST1 row is written, unlike Take Call.",
                    "The \"Campus Must Be Selected\" check is dead code - its condition can never be true, so it is not carried over."
                },
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD112" },
                Title = "Traffic Stop",
                Area = "Calls",
                Handler = FunctionHandler.TrafficStop,
                Legacy = "LwCad112 (run in-process by the console as LwCadSystem\\TraffStop.cs)",
                Source = "LwCadSystem\\TraffStop.cs; LwCad112\\TabA.cs",
                Purpose = "Self-initiated traffic stop - the Self Init engine plus plate and vehicle. " +
                          "Built in this console as Self Initiate's Traffic Stop mode.",
                Invocation = "WinA.LaunchProgram matches \"lwcad112\" and opens TraffStop as a dialog.",
                Reads = "MLB2 50/01 (states), 50/22 (plate types), 5/60-66 (racial profile)",
                Writes = "As Self Init: DSE1, DSU1 (with DSU1_R_PROF_*), DSV1, STK1 (A), DSC1, RST1; TSK1 402/403/404; FAT2",
                Rules = new[]
                {
                    "Police units only (\"Traffic Stop is a function reserved for police\").",
                    "Offers to activate an inactive unit, and \"Assign Assist Unit(s)/Apparatus?\"."
                },
                Notes = new[]
                {
                    "No DST1 row is written, and DMV8 is never queried - the state is only checked against MLB2.",
                    "The campus rule is dead code here too."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADSTATIONCALL", "CAD120" },
                Title = "Station Call",
                Area = "Calls",
                Legacy = "LwCadStationCall (window Cad120)",
                Source = "LwCadStationCall\\TabA.cs, Cad120.xaml.cs",
                Purpose = "A walk-in or at-station call, created already complete - never reaches the stack.",
                Invocation = "Exe with the standard arguments; no context needed.",
                Writes = "DSE1, DSU1 (call taker's id in every id field), DSN1, DSV1; DSC1 event number and " +
                         "DSC1_GLOBAL_NO; TSK1 301/401/402",
                Rules = new[]
                {
                    "DSP / ENR / ARV / CMP dates all equal the received date. No STK1 row.",
                    "Event type comes from the unit's agency type; a unit on another call self-releases first (LwCad123).",
                    "CAD7 switches drive history, patrol sound / visual alerts and the duplicate check.",
                    "Optional case number prompt (LwCad106)."
                },
                Notes = new[] { "The RST1 update is commented out (\"COBOL code seems to be in error\")." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD788" },
                Title = "Incoming Calls",
                Area = "Calls",
                Legacy = "LwCad788 Incoming Call List -> LwCad101",
                Source = "LwCad788\\TabA.cs",
                Purpose = "Pending / processed list of E911 and transferred calls (EMG3) waiting to be taken.",
                Invocation = "Exe. Picking a row starts LwCAD101 with the EMG3 key in the Stack Key slot; " +
                             "LwCad101 then locks EMG3 (Cad100.xaml.cs:605).",
                Reads = "EMG3 where PROCESSED_BY = 0, with its lock owner",
                ToBuild = new[] { "Open Take Call pre-filled from an EMG3 row." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD190" },
                Title = "Duplicate Call List",
                Area = "Calls",
                IsMenuFunction = false,
                Legacy = "LwCad190 (a Take Call helper)",
                Source = "LwCad190\\ScreenA.cs, Cad190.xaml.cs:158",
                Purpose = "Stacked calls near a new call's location, offered while taking it.",
                Invocation = "Non-standard arguments: ^Jur|StName|StNo|XSt|FormattedLoc with '%' for spaces. " +
                             "Called from TakeCall.cs:4205.",
                Reads = "STK1 in the jurisdiction: same street within CAD7_DUP_RANGE, or the street / cross-street pair"
            };
        }
    }
}
