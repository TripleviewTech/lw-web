using System;
using System.Collections.Generic;

namespace LwCadSystemUX.Functions
{
    /// <summary>
    /// What this build does when a key resolves to the function. Everything but
    /// Stub is a panel that already exists; Stub opens the function's notes.
    /// </summary>
    public enum FunctionHandler
    {
        Stub,
        TakeCall,
        Assign,
        Assist,
        UnitStatus,
        ReleaseUnit,
        LocationChange,
        CadAlerts,
        SelfInitiate,
        TrafficStop,
        ActivateUnit
    }

    /// <summary>The board context the legacy program expects to be handed.</summary>
    public enum FunctionNeeds
    {
        Nothing,

        /// <summary>A selected call on any monitor (EventType / EventNo1 / EventNo2).</summary>
        SelectedCall,

        /// <summary>A call still on the Stacked monitor (STK1_STATUS 'S') and its SKey.</summary>
        StackedCall,

        /// <summary>A call on the Active monitor (STK1_STATUS 'A').</summary>
        ActiveCall,

        /// <summary>A unit from the Unit Monitor (Gl_UnitNo).</summary>
        SelectedUnit,

        /// <summary>A unit's assignment on a call (DispNo1 / DispNo2 - the DSU1 row).</summary>
        UnitAssignment
    }

    /// <summary>
    /// One legacy CAD program as this console knows it: which program ids name it,
    /// what the key does here, and - for the ones not built yet - the notes an
    /// implementation will need, taken from reading the legacy source.
    ///
    /// A key's program id is data (KYB1_PRG_ID, or the FUNC of a type P key), and
    /// it differs by agency and by site, so an entry answers to the id without its
    /// agency prefix: LwCad137, BkCad137 and XxCad137 all resolve to CAD137.
    /// </summary>
    public sealed class CadFunctionInfo
    {
        /// <summary>Program ids without the agency prefix, upper case - "CAD137".</summary>
        public IReadOnlyList<string> Programs { get; init; } = Array.Empty<string>();

        /// <summary>The function's name, used when a key has no caption and in the stub header.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>The group the catalogue files it under, e.g. "Calls".</summary>
        public string Area { get; init; } = string.Empty;

        public FunctionHandler Handler { get; init; } = FunctionHandler.Stub;

        public FunctionNeeds Needs { get; init; } = FunctionNeeds.Nothing;

        /// <summary>The legacy program chain, e.g. "LwCad142 -> LwCad102".</summary>
        public string Legacy { get; init; } = string.Empty;

        /// <summary>Folder(s) under Git\Mass\LW\Apps (or elsewhere) holding the source.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>What the program is for, in a sentence or two.</summary>
        public string Purpose { get; init; } = string.Empty;

        /// <summary>How the legacy starts it and what it must be handed.</summary>
        public string Invocation { get; init; } = string.Empty;

        public string Reads { get; init; } = string.Empty;

        public string Writes { get; init; } = string.Empty;

        /// <summary>Validations and behaviour an implementation has to keep.</summary>
        public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();

        /// <summary>Quirks, bugs and agency-specific code found in the legacy source.</summary>
        public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

        /// <summary>What building it here involves - the stub's to-do list.</summary>
        public IReadOnlyList<string> ToBuild { get; init; } = Array.Empty<string>();

        /// <summary>False for helpers the legacy never puts on a key by themselves.</summary>
        public bool IsMenuFunction { get; init; } = true;

        public bool IsImplemented => Handler != FunctionHandler.Stub;

        /// <summary>True for the entry made up for a program id the catalogue does not know.</summary>
        public bool IsUncatalogued { get; init; }
    }
}
