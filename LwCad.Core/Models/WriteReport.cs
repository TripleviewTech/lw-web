using System;
using System.Collections.Generic;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// What a data service did with an assignment or a unit function: whether it
    /// reached the CAD tables, which units it refused, and the line the status bar
    /// should show. The demo board reports that nothing was saved; the live service
    /// reports the database outcome, refusals included.
    /// </summary>
    public class WriteReport
    {
        /// <summary>True when the change was committed to the CAD tables.</summary>
        public bool Saved { get; set; }

        /// <summary>True when nothing was applied - the panel should stay open on the message.</summary>
        public bool Failed { get; set; }

        public string Message { get; set; } = string.Empty;

        /// <summary>Units an assignment could not put on the call, and so did not process.</summary>
        public HashSet<string> RefusedUnits { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static WriteReport BoardOnly(string message) => new WriteReport
        {
            Message = message + " Demo board - not written to the CAD database."
        };
    }
}
