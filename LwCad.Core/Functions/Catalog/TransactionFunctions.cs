using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // DST1 transactions - the running log of a call and its units.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> TransactionFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD610" },
                Title = "Transaction Viewer",
                Area = "Transactions",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad610 Unit Transaction Viewer -> LwCad115",
                Source = "LwCad610\\TabA.cs",
                Purpose = "Lists a call's unit transactions; picking one opens LwCad115.",
                Reads = "DSU1 (unit combo), DST1 rows with a unit number; sort by unit or date/time",
                Notes = new[] { "Bug: the unit filter DST1_UNIT_NO = <unit> is not quoted, so filtering by a unit throws." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD177" },
                Title = "Transactions",
                Area = "Transactions",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad177 Transaction Type Selector -> LwCad117 / LwCad113",
                Source = "LwCad177\\WinA.cs",
                Purpose = "Two buttons: Event transactions (LwCad117) or Unit transactions (LwCad113).",
                Rules = new[] { "Event disabled without an event; Unit disabled when no RST1 unit is on the call." },
                Notes = new[] { "Docs call it a \"Transaction Types\" maintenance screen - it is a selector." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD113" },
                Title = "Unit Transaction",
                Area = "Transactions",
                Legacy = "LwCad113 CAD Unit Transactions (entry)",
                Source = "LwCad113\\Cad113.xaml.cs, TabA.cs:205-500",
                Purpose = "Logs a unit transaction, with person / vehicle details.",
                Invocation = "Exe. UnitNo optional (finds DispNo from RST1); EventNo optional (locks the unit picker); TKey optional.",
                Writes = "DST1 RCD_TYPE 'U' ([VEHICLE] / [PERSON] folded into DST1_REMARKS), DSN1 / DSV1 type T; " +
                         "resets RST1_T_O_F_HR/MN, T_O_FLAG 0; TSK1 202",
                Notes = new[]
                {
                    "State DMV query when DMV8 is connected; crisis-intervention search (ENV3 'WRK').",
                    "Tow stubs XXTWR701 / 801 show \"incomplete\"."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD117" },
                Title = "Event Transaction",
                Area = "Transactions",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad117 CAD Event Transactions (entry)",
                Source = "LwCad117",
                Purpose = "Logs a transaction against the call itself.",
                Invocation = "Exe. Needs EventType / No (\"Event record must be selected.\").",
                Writes = "DST1 RCD_TYPE 'E', DSN1, DSV1",
                Notes = new[] { "Reads CAD9_STATE with gl_EmpId, not gl_UserID." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD115", "CAD119" },
                Title = "View Transaction",
                Area = "Transactions",
                IsMenuFunction = false,
                Legacy = "LwCad115 (unit) / LwCad119 (event) View Transaction",
                Source = "LwCad115; LwCad119",
                Purpose = "Read-only view of one DST1 row, opened from the viewers.",
                Invocation = "Needs a TKey (119 also the EventNo). A missing DSE1 is tolerated by 115.",
                Reads = "DST1, DSE1, DSN1, DSV1"
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD114", "CAD118" },
                Title = "Correct Transaction",
                Area = "Transactions",
                Legacy = "LwCad114 (unit) / LwCad118 (event) Backload / Correct Transaction",
                Source = "LwCad114; LwCad118",
                Purpose = "Supervisor backload or correction of a transaction.",
                Invocation = "Exe. Mode a = backload; otherwise loads by TKey.",
                Writes = "DST1, DSN1, DSV1 (114 also RST1)",
                Rules = new[] { "Gated by CAD9_COR_CMP (118 only on non-active calls)." }
            };
        }
    }
}
