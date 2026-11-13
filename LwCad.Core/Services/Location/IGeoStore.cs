using System.Collections.Generic;
using LaTableAlc1;
using LaTableGeo1;
using LaTableGeo2;
using LaTableStr1;
using LwTablePmm1;
using LwTablePmm6;
using LwTableRun1;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// Where the location engine reads the geobase and the premise tables from.
    /// Every call is a coarse fetch - by a literal prefix or a key - and the engine
    /// applies the legacy rules on top, so the live tables and the demo board's
    /// in-memory geobase produce the same answers from the same code.
    /// </summary>
    public interface IGeoStore
    {
        /// <summary>STR1 rows whose STR1_ASORT_NM starts with (or, exact, equals) the text; jurisdiction when given.</summary>
        IReadOnlyList<STR1> StreetNames(string asortPrefix, bool exact, string jurisdiction);

        /// <summary>GEO1 rows whose GEO1_LOC_STR_ZIP starts with the prefix.</summary>
        IReadOnlyList<GEO1> SegmentsByStreet(string zipPrefix);

        /// <summary>GEO1 rows with GEO1_LOC_STR_ZIP on or after the text, in street order - the second-attempt list.</summary>
        IReadOnlyList<GEO1> SegmentsFromStreet(string zipFrom, int top);

        /// <summary>True when any GEO1_LOC_STR_ZIP starts with the prefix - GetClosestStreetMatch.</summary>
        bool AnySegmentStreet(string zipPrefix);

        /// <summary>GEO1 rows whose GEO1_C_NAME_ZIP starts with the prefix - LaGeo045.</summary>
        IReadOnlyList<GEO1> SegmentsByCommonName(string zipPrefix);

        GEO1 Segment(int key);

        IReadOnlyList<GEO1> Segments(IEnumerable<int> keys);

        /// <summary>GEO2 rows (the cross streets at a segment's ends) for the given segments.</summary>
        IReadOnlyList<GEO2> CrossStreets(IEnumerable<int> segmentKeys);

        /// <summary>GEO2 rows whose GEO2_NAME_ZIP starts with the prefix.</summary>
        IReadOnlyList<GEO2> CrossStreetsByName(string zipPrefix);

        /// <summary>PMM1 rows whose PMM1_SPACELESS starts with the prefix.</summary>
        IReadOnlyList<PMM1> PremisesByName(string spacelessPrefix);

        /// <summary>PMM1 rows with PMM1_SPACELESS on or after the text - the second-attempt list.</summary>
        IReadOnlyList<PMM1> PremisesFromName(string spacelessFrom, int top);

        PMM1 Premise(int fop);

        /// <summary>Active PMM1 rows at a jurisdiction / street / number - SearchForPremise.</summary>
        IReadOnlyList<PMM1> PremisesAt(string jurisdiction, string street, decimal streetNo);

        /// <summary>Active PMM1 rows at a jurisdiction / street / cross street.</summary>
        IReadOnlyList<PMM1> PremisesAtCorner(string jurisdiction, string street, string cross);

        /// <summary>PMM6 rows whose PMM6_ROUTING_ID starts with (or, exact, equals) the text.</summary>
        IReadOnlyList<PMM6> Alarms(string routingPrefix, bool exact);

        /// <summary>PMM6 rows with PMM6_ROUTING_ID on or after the text.</summary>
        IReadOnlyList<PMM6> AlarmsFrom(string routingFrom, int top);

        /// <summary>RUN1 by run number, any coverage - TakeCall_FindBoxByRunNo.</summary>
        RUN1 RunCard(string runNo);

        /// <summary>RUN1 by run number and coverage - the DSE1_ROUTING_ID override uses coverage '4'.</summary>
        RUN1 RunCard(string runNo, char coverage);

        ALC1 AlarmCompany(short companyNo);

        /// <summary>PMM4 latitude / longitude, or null.</summary>
        (decimal Lat, decimal Long)? PremiseCoordinates(int fop);

        /// <summary>PMM2 rows for the premise.</summary>
        int HazardCount(int fop);

        /// <summary>dbo.la_fn_GetPMM1Name - the premise's display name.</summary>
        string PremiseName(int fop);
    }
}
