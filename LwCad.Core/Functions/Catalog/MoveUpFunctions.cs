using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    // Fire coverage: move ups, move downs and tones.
    public static partial class CadFunctionCatalog
    {
        private static IEnumerable<CadFunctionInfo> MoveUpFunctions()
        {
            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD171" },
                Title = "Dynamic Move Up",
                Area = "Fire Coverage",
                Legacy = "LwCad171 Dynamic Moveup",
                Source = "LwCad171\\WinA.cs",
                Purpose = "Moves an available unit to cover another unit's station.",
                Invocation = "Exe, no context. Also run in-process from LwCad132 when the alarm level text starts with \"D\".",
                Reads = "RST1, MLB1 sys 5 lib 10 (stations)",
                Writes = "RST1_C_STATION and RST1_REPL_ID; TSK1 202/404",
                Rules = new[]
                {
                    "The moving unit must be status 1 (available) and not already replacing another unit.",
                    "The replaced unit must not be status 1."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD172" },
                Title = "Fire Move Down",
                Area = "Fire Coverage",
                Legacy = "LwCad172 Fire Move Downs",
                Source = "LwCad172\\WinA.cs",
                Purpose = "Returns moved-up units to their own stations.",
                Reads = "TOP 10 RST1 where REPL_ID <> ' '",
                Writes = "RST1_C_STATION = A_STATION, REPL_ID cleared; TSK1 202/404",
                Rules = new[] { "The unit must be status 1 and still replacing someone; a locked unit's slot is disabled." },
                Notes = new[] { "Bug: reloads the list before sending 202/404, so _rst1 may point at another unit by then." }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD170" },
                Title = "Fire Move Ups",
                Area = "Fire Coverage",
                IsMenuFunction = false,
                Legacy = "LwCad170 (in-process from LwCad132 TabA_MoveUpNum)",
                Source = "LwCad170\\WinA.cs",
                Purpose = "Move-up plan for a numbered alarm level.",
                Reads = "DSE1, RUN2 plan: fire district + call code, then district default; coverage 4 (24 h) then A/P by clock",
                Writes = "RST1_C_STATION, RST1_REPL_ID; TSK1 202/404",
                Notes = new[]
                {
                    "Bug: station 9 reads Substring(28,6); it should be 48.",
                    "Bug: the enable check wants REPL_ID set but processing rejects a set REPL_ID - slots are likely unusable."
                }
            };

            yield return new CadFunctionInfo
            {
                Programs = new[] { "CAD174" },
                Title = "Transmit Fire Tone",
                Area = "Fire Coverage",
                Needs = FunctionNeeds.SelectedCall,
                Legacy = "LwCad174 Transmit Fire Tone (from LwCad132)",
                Source = "LwCad174\\WinA.cs, Cad174.xaml.cs:299-347",
                Purpose = "Records a tone-out on a fire / EMS call and restarts the re-tone timer.",
                Invocation = "Needs the event and SKey; locks STK1.",
                Reads = "CAD7 F_TONE_SW / E_TONE_SW, 1ST_TO_* / SUB_TO_*",
                Writes = "DSE1_FIRE_LEVEL++, DSP_DATE if empty; DST1 (5/7/998 \"Transmitted - Nth Tone\"); STK1 tone timer; TSK1 301",
                Notes = new[] { "LwCad132 calls it in-process without Gl_SKey, so its STK1 lookup should fail." }
            };
        }
    }
}
