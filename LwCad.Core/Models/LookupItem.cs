using System.Collections.Generic;

namespace LwCadSystemUX.Models
{
    /// <summary>A code/description pair as served by the legacy KYB reference tables.</summary>
    public class LookupItem
    {
        public LookupItem() { }

        public LookupItem(string code, string description)
        {
            Code = code;
            Description = description;
        }

        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string Display =>
            string.IsNullOrWhiteSpace(Code) ? Description : Code + ", " + Description;

        public override string ToString() => Display;
    }

    /// <summary>
    /// Call code reference entry. Selecting one on Take Call seeds the four
    /// priority boxes from CCC1_PRTY_P / F / E / O, as the legacy call-code
    /// validation does. (It never touched the Police / Fire / EMS areas - those
    /// come from the location.)
    /// </summary>
    public class CallCodeItem : LookupItem
    {
        public CallCodeItem() { }

        public CallCodeItem(string code, string description, string callType,
                            Agency agency, CallPriority band, int priorityNumber)
        {
            Code = code;
            Description = description;
            CallType = callType;
            Agency = agency;
            DefaultBand = band;
            DefaultPriorityNumber = priorityNumber;
        }

        public string CallType { get; set; } = string.Empty;
        public Agency Agency { get; set; } = Agency.Police;
        public CallPriority DefaultBand { get; set; } = CallPriority.Medium;
        public int DefaultPriorityNumber { get; set; } = 5;

        /// <summary>CCC1_ALERT - after the save Take Call asks "Enter Call ALERT?".</summary>
        public bool PromptsAlert { get; set; }

        /// <summary>CCC1_ASSGN_ASST - Self Init / Traffic Stop offer to assign assist units.</summary>
        public bool OffersAssist { get; set; }

        /// <summary>
        /// CCC1_PRTY_P / _F / _E / _O - the dispatch priority this code carries for
        /// each agency, 0 where it does not task that agency at all. The assign
        /// screens show all four across the top of the call.
        /// </summary>
        public int PolicePriority { get; set; }
        public int FirePriority { get; set; }
        public int EmsPriority { get; set; }
        public int OtherPriority { get; set; }

        /// <summary>
        /// CCC1_RESTACK - the call is significant enough that releasing the last
        /// unit off it puts the event back on the stack instead of closing it.
        /// </summary>
        public bool RestacksOnRelease { get; set; }
    }
}
