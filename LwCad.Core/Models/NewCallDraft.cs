using System;
using LwCadSystemUX.Services.Location;
using LwCadSystemUX.Services.TakeCall;

namespace LwCadSystemUX.Models
{
    /// <summary>Values captured by the Take Call panel and handed to the data service.</summary>
    public class NewCallDraft
    {
        public string EventNo { get; set; } = string.Empty;
        public CallCodeItem CallCode { get; set; }
        public CallPriority Priority { get; set; } = CallPriority.High;

        /// <summary>
        /// The four priority boxes (tbxNV_PriorityP/F/E/O). One event is created
        /// for each agency with a priority above 0 (TakeCall_SaveDSE1).
        /// </summary>
        public short PriorityPolice { get; set; }
        public short PriorityFire { get; set; }
        public short PriorityEms { get; set; }
        public short PriorityOther { get; set; }

        public bool HasPriorities => PriorityPolice > 0 || PriorityFire > 0 || PriorityEms > 0 || PriorityOther > 0;

        public string Jurisdiction { get; set; } = string.Empty;
        public string PoliceCode { get; set; } = string.Empty;
        public string FireCode { get; set; } = string.Empty;
        public string EmsCode { get; set; } = string.Empty;
        public string SupportCode { get; set; } = string.Empty;
        public string TowCode { get; set; } = string.Empty;
        public string Force { get; set; } = "N";

        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// The validated location - geocode, parts, areas by side, premise, routing
        /// id, coordinates. Null only when nothing validated it (a board with no
        /// geobase), and the text is then split as typed.
        /// </summary>
        public LocationResult Place { get; set; }

        public string Premise { get; set; } = string.Empty;
        public LookupItem Origin { get; set; }
        public string OtherComment { get; set; } = string.Empty;

        /// <summary>MLB1 05/01. Set, the call is completed as it is entered - no stack row.</summary>
        public short Disposition { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        public VehicleInfo Vehicle { get; set; }
        public string Narrative { get; set; } = string.Empty;

        /// <summary>The E911 call loaded onto the form - DSE1_E911_DATE and its coordinates.</summary>
        public E911Call E911 { get; set; }

        /// <summary>The STK2 in-flight map record, removed when the call is saved.</summary>
        public long MapRecordKey { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.Now;

        public string ReportedBy =>
            string.Join(" ", new[] { FirstName, MiddleName, LastName })
                  .Replace("  ", " ")
                  .Trim();
    }
}
