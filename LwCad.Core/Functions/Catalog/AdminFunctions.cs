using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Supervisor corrections, backloads and system screens.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> AdminFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD103" },
                Title = "Correct Event",
                Area = "Admin",
                Legacy = "LwCad103 Backload / Edit CAD Event Record",
                Source = "LwCad103\\Cad103.xaml.cs, TabA.cs",
                Purpose = "Backload a call (mode a) or correct / view one (c / v).",
                Invocation = "Mode a needs a reserved event number (\"has not been reserved\" - see LwCad701).",
                Writes = "DSE1, DSN1, DSV1, DSU1, RST1, STK1; FAT2; TSK1 301",
                Rules = new[] { "Mode c on an active call becomes s when CAD9_COR_CMP = Y; locks STK1 if still stacked." },
                Notes = new[] { "\"Campus Must Be Selected\" rule." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD109" },
                Title = "Correct Unit Record",
                Area = "Admin",
                Legacy = "LwCad109 Backload / Edit CAD Unit Record",
                Source = "LwCad109\\Cad109.xaml.cs",
                Purpose = "Backload or correct a unit assignment.",
                Writes = "DSU1, DSE1, DSV1, DSC1 (dispatch number)",
                Rules = new[] { "Case-number editing gated by CAD9_COR_C_NO and reservations." },
                Notes = new[] { "Bug: CAD9_COR_C_NO is read from the wrong column (reader[1] used twice)." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD700", "CAD798" },
                Title = "Change Completed Call",
                Area = "Admin",
                Legacy = "LwCad798 -> LwCad700 list (no event) or LwCad705 (event)",
                Source = "LwCad798\\Cad798.xaml.cs:195-270; LwCad700\\TabA.cs",
                Purpose = "Supervisor list of DSE1 by date, number range, jurisdiction and agency, to correct or backload.",
                Reads = "DSE1; security from PER1 / AGC1 / CAD9",
                Notes = new[] { "Opens LwCad705 in-process with a hash key, LwCad701 to reserve numbers." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD799", "CAD705" },
                Title = "Change Active Call",
                Area = "Admin",
                Needs = FunctionNeeds.StackedCall,
                Legacy = "LwCad799 -> LwCad705 Change / Backload Units & Transactions",
                Source = "LwCad799\\Cad799.xaml.cs:192-240; LwCad705\\TabA/B/C.cs",
                Purpose = "Edits the event, units and transactions of a call; launches 103 / 106 / 109 / 114 / 118.",
                Invocation = "LwCad799 requires an SKey (otherwise its own \"Process Error\" window).",
                Rules = new[]
                {
                    "Reactivate gated by CAD9_REACT: deletes and re-inserts STK1, clears DSE1 completion and disposition.",
                    "\"Xfer to NFIRS\" gated by CAD7_XFER_2_NFIRS."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD701" },
                Title = "Reserve Backload Numbers",
                Area = "Admin",
                Legacy = "LwCad701",
                Source = "LwCad701\\WinA.cs",
                Purpose = "Reserves a block of event or case numbers for backloading.",
                Writes = "DSC1 EVENT_NO_P/F/E/O or DSC2 case counters + N; DBC1 BKLOAD_A/B",
                Notes = new[] { "Plain read-then-write on DSC1 - not the optimistic update used elsewhere." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADSESSIONVIEWER" },
                Title = "Session Viewer",
                Area = "Admin",
                Legacy = "LwCadSessionViewer",
                Source = "LwCadSessionViewer\\TabA.cs",
                Purpose = "Lists DAT1 sessions (owner, operator type, agencies, PC); deleting one kills the session.",
                Notes = new[] { "Does not clean up DAT2 or the STK1 / RST1 locks the session held." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CADMAPPING", "CADMAPPINGGDB" },
                Title = "CAD Map",
                Area = "Admin",
                Legacy = "LwCadMapping (console btn_Map)",
                Source = "LwCadMapping\\CadMap.xaml.cs (three forks exist)",
                Purpose = "ArcGIS Runtime map of calls and units.",
                Reads = "STK1, DSE1, RST1, PMM, MPO2; consumes TSK1 4xx; updates DAT1",
                Notes = new[]
                {
                    "Initial extent hard-coded to Lowell (CadMap.xaml.cs:468).",
                    "The top bar's Map button is the natural home for this."
                }
            };
        }
    }
}
