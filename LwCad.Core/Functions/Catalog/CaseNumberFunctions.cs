using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Agency case numbers on unit assignments.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> CaseNumberFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD144", "CAD106" },
                Title = "Assign Case Number",
                Area = "Case Numbers",
                Needs = FunctionNeeds.UnitAssignment,
                Legacy = "LwCad144 -> LwCad179 picker -> LwCad106 Assign Case Number",
                Source = "LwCad144; LwCad106\\Cad106.xaml.cs:286-700",
                Purpose = "Gives a unit's assignment its agency case number.",
                Invocation = "LwCad144 runs hidden: with DispNo, EventNo and UnitNo all passed it goes straight " +
                             "through, else one RST1 unit on the call is used or LwCad179 picks one; then LwCad106 " +
                             "in-process. The console also reaches 144 from btn_CallCaseNo (the selected call) and " +
                             "btn_UnitCaseNo (the selected unit's RST1 keys, GetUnitInfo).",
                Reads = "AGC1_CASE_TYPE (Y by year, M by month, D by day), DSC2 counters",
                Writes = "DSU1_CASE_NO_1/2 and the next free DSE1_CASE_NO_*_nn / AGENCY_nn slot; TSK1 202/301",
                Rules = new[]
                {
                    "At most 10 agency case numbers per call (DSE1_AGENCY_01-10, \"limit has been reached\").",
                    "One case number per agency (\"already been assigned\").",
                    "A case number belongs to a unit, so the call row menu only offers it on a call with units."
                },
                ToBuild = new[]
                {
                    "Unit picker (units on the call) + CadWriteBack: DSC2 counter update (optimistic, as DSC1) " +
                    "and the DSU1 / DSE1 slot writes in one transaction."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD191" },
                Title = "Transfer Case Number",
                Area = "Case Numbers",
                Legacy = "LwCad191 Transfer Case Number",
                Source = "LwCad191\\WinA.cs",
                Purpose = "Moves a case number from a unit on event A to a unit on event B.",
                Writes = "Target DSU1 CASE_NO_1/2 and DSE1 CASE_NO_*_01 + AGENCY_01 (unless already in one of the " +
                         "four slots); clears the source DSU1 and its DSE1 slot; rolls the target back on failure; TSK1 301",
                Rules = new[]
                {
                    "Event A must have a unit holding a case number; event B and its target DSU1 must have none.",
                    "Locks both RST1 rows."
                },
                Notes = new[]
                {
                    "Event types are hard-coded to P and F only.",
                    "Bug: the \"different event\" check compares a field with itself, so it is always skipped.",
                    "Locks with gl_EmpId but other programs check gl_UserID."
                }
            };
        }
    }
}
