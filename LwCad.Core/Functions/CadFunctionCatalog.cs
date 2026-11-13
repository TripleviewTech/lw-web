using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;

namespace LwCadSystemUX.Functions
{
    /// <summary>
    /// Every legacy CAD program a keyboard template can put on a key, and what this
    /// console does with it. The entries live in Functions/Catalog, one file per
    /// area; each is either wired to a panel this build has (Take Call, Assign,
    /// Unit Status, Release Unit, Location Change) or a stub that opens its notes.
    ///
    /// Which program sits on which key is not decided here - that is KYB1-3 data,
    /// and it differs by agency and site. A program id nobody catalogued still gets
    /// a stub (Uncatalogued), so a site's own programs show up rather than vanish.
    /// </summary>
    public static partial class CadFunctionCatalog
    {
        private static readonly Lazy<IReadOnlyList<CadFunctionInfo>> AllEntries =
            new Lazy<IReadOnlyList<CadFunctionInfo>>(() =>
                CallFunctions()
                    .Concat(AssignFunctions())
                    .Concat(UnitFunctions())
                    .Concat(CaseNumberFunctions())
                    .Concat(MoveUpFunctions())
                    .Concat(TransactionFunctions())
                    .Concat(AlertReviewFunctions())
                    .Concat(AdminFunctions())
                    .Concat(ExternalFunctions())
                    .ToList());

        private static readonly Lazy<Dictionary<string, CadFunctionInfo>> Index =
            new Lazy<Dictionary<string, CadFunctionInfo>>(() =>
            {
                var index = new Dictionary<string, CadFunctionInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in AllEntries.Value)
                {
                    foreach (var program in entry.Programs)
                    {
                        // First entry wins; a program id is only ever catalogued once.
                        if (!index.ContainsKey(program)) index.Add(program, entry);
                    }
                }

                return index;
            });

        public static IReadOnlyList<CadFunctionInfo> All => AllEntries.Value;

        /// <summary>
        /// The entry for a key's program id. Tried as it stands, then without a
        /// two-letter agency prefix (LWCAD137 -> CAD137, LAPTLSYSTEM -> PTLSYSTEM),
        /// so another agency's copy of a program resolves to the same notes.
        /// </summary>
        public static CadFunctionInfo Resolve(string programId)
        {
            var id = KeymapLoader.NormalizeProgramId(programId);
            if (id.Length == 0) return null;

            if (Index.Value.TryGetValue(id, out var entry)) return entry;
            if (id.Length > 2 && Index.Value.TryGetValue(id.Substring(2), out entry)) return entry;

            return Uncatalogued(id);
        }

        /// <summary>The entry for a known program, by its catalogued id - "CAD144".</summary>
        public static CadFunctionInfo Get(string cataloguedId) =>
            Index.Value.TryGetValue(cataloguedId, out var entry) ? entry : Uncatalogued(cataloguedId);

        public static CadFunctionInfo Resolve(KeySlot slot) => slot == null ? null : Resolve(slot.ProgramId);

        /// <summary>
        /// A program the catalogue has no notes for - a site's own program, a
        /// MicroFocus .int, or a program id mistyped in KYB1. What the legacy would
        /// have done with it is still known, so the stub says that.
        /// </summary>
        public static CadFunctionInfo Uncatalogued(string programId) => new CadFunctionInfo
        {
            Programs = new[] { programId },
            Title = programId,
            Area = "Uncatalogued",
            IsUncatalogued = true,
            Legacy = programId,
            Purpose = "A program this console has no notes for. It may be site specific, a MicroFocus " +
                      "program, or a KYB1_PRG_ID that no longer matches a program.",
            Invocation = "LwCadSystem WinA.LaunchProgram: if " + programId + ".exe exists beside the console it is " +
                         "started with UserID|Top|Left|v|^EventType|EventNo1|EventNo2|DispNo1|DispNo2|TKey|SKey|" +
                         "CadMode|EmpId|SessId|UnitNo|RtnSw|Mon| (a blank event type is sent as 'X'). If " +
                         programId + ".int exists it is run through LaRunw.exe with a 200-space link section - " +
                         "so a MicroFocus program gets no selected call or unit at all. The console does not " +
                         "wait for it, and clears its selected event afterwards.",
            ToBuild = new[]
            {
                "Find the program's source and add an entry to Functions/Catalog.",
                "Decide whether it belongs in this console or stays an external program.",
                "Check KYB1 (LwKybFunctions) - the function may simply point at the wrong program id."
            }
        };
    }
}
