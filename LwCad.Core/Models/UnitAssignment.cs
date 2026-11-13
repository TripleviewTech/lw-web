using System.Collections.Generic;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// What an assign screen is asking for. Mirrors the fields LwCad131 / 132 /
    /// 133 / 134 collect before their Assign button: the event, the units picked,
    /// the state to put them in, the status timeout and whether the assignment is
    /// transmitted to the mobiles.
    /// </summary>
    public class UnitAssignment
    {
        public UnitAssignment(CadCall call, Agency agency, AssignMode mode)
        {
            Call = call;
            Agency = agency;
            Mode = mode;
        }

        public CadCall Call { get; }

        /// <summary>Which of the four assign programs is running.</summary>
        public Agency Agency { get; }

        /// <summary>Assign Call on a stacked event, Assign / Assist on an active one.</summary>
        public AssignMode Mode { get; }

        public List<CadUnit> Units { get; } = new List<CadUnit>();

        /// <summary>Dispatch / En Route / Arrive, the legacy screens' radio group.</summary>
        public DispatchAction Action { get; set; } = DispatchAction.Dispatch;

        /// <summary>
        /// Minutes before the monitor flags the unit; 0 leaves the call code's own
        /// timeout in force. Only LwCad131 and LwCad134 offer this field.
        /// </summary>
        public int TimeoutMinutes { get; set; }

        /// <summary>cbx_XmitToMdc - send the assignment on to the unit's mobile.</summary>
        public bool XmitToMdc { get; set; }
    }
}
