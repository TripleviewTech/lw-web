namespace LwCadSystemUX.Models
{
    /// <summary>
    /// Which monitor a call currently belongs to. Mirrors the 'S'/'A'/'D' monitor
    /// codes used by the legacy CadMain selection tracking.
    /// </summary>
    public enum CallQueue
    {
        Stacked,
        Active,
        Delayed,
        Closed
    }

    /// <summary>Responding discipline. Mirrors the legacy P/F/E/O event types.</summary>
    public enum Agency
    {
        Police,
        Fire,
        Ems,
        Other
    }

    /// <summary>
    /// Dispatch priority. The legacy screens store 1..9 per agency; the redesign
    /// presents the three operational bands shown on the Take Call board.
    /// </summary>
    public enum CallPriority
    {
        High = 1,
        Medium = 2,
        Low = 3
    }

    /// <summary>Unit availability, drives the Unit Monitor filter chips.</summary>
    public enum UnitAvailability
    {
        Available,
        Assigned,
        OutOfService
    }

    /// <summary>
    /// Which assignment the panel is running. Both call the same program - the
    /// legacy LwCad131/132/133/134 assign screens - but a call with no units yet
    /// is an Assign Call, and one already on the Active monitor is an Assign
    /// Assist (legacy LwCad140), so the panel says which.
    /// </summary>
    public enum AssignMode
    {
        Call,
        Assist
    }

    /// <summary>
    /// The state a unit is put in as it is assigned. The legacy assign screens
    /// offer these as the Dispatch / En Route / Arrive radio group.
    /// </summary>
    public enum DispatchAction
    {
        Dispatch,
        EnRoute,
        Arrived
    }

    public enum AppTheme
    {
        Light,
        Dark
    }
}
