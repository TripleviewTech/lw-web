using System;
using System.Collections.Generic;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// What a location entry is, as LaGeo040 GetLocationType classifies it. The
    /// legacy codes are kept in the comments so the port can be read against it.
    /// </summary>
    public enum LocationKind
    {
        /// <summary>ADR1 - "12-A MAIN ST (4)". Picker: LaGeo041 Address.</summary>
        Address,

        /// <summary>BUSI - "P/BAKER". Picker: LaGeo042 Premise.</summary>
        Premise,

        /// <summary>CROS - "MAIN ST / ELM ST". Picker: LaGeo043 Intersection.</summary>
        Intersection,

        /// <summary>ALRM - "A/1234". Picker: LaGeo044 Alarm Number.</summary>
        Alarm,

        /// <summary>COMM - "C/CITY HALL". Picker: LaGeo045 Common Name.</summary>
        CommonName,

        /// <summary>"B/21" - a box, TakeCall_FindBox: PMM6 routing id, then RUN1.</summary>
        Box,

        /// <summary>XXXX - Force X: the text is the location, nothing is looked up.</summary>
        AsTyped,

        /// <summary>MLP1 / ADR2 - a mile post or an unvalidated jurisdiction; accepted as parsed.</summary>
        MilePost,

        /// <summary>The premise list for a validated address - LwPmm905 "Multiple Premises".</summary>
        PremiseAtLocation
    }

    /// <summary>What the operator typed, with the jurisdiction and Force flag beside it.</summary>
    public sealed class LocationRequest
    {
        public string Text { get; init; } = string.Empty;
        public string Jurisdiction { get; init; } = string.Empty;

        /// <summary>DSE1_FORCED: 'N' look it up, 'Y' accept as parsed, 'X' accept as typed.</summary>
        public char Forced { get; init; } = 'N';
    }

    /// <summary>
    /// The entry once parsed - LaGeo040's Loc* properties - carried to the picker so
    /// its list and its Force button work on the same values the search used.
    /// </summary>
    public sealed class LocationQuery
    {
        public LocationKind Kind { get; set; }
        public string Original { get; set; } = string.Empty;
        public string Jurisdiction { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public decimal StreetNo { get; set; }
        public string Sub { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Cross { get; set; } = string.Empty;
        public char Forced { get; set; } = 'N';

        /// <summary>The text after the P/ A/ C/ prefix - what the premise, alarm and common name searches take.</summary>
        public string Search { get; set; } = string.Empty;

        public bool AddressIsEven => ((int)StreetNo / 2) * 2 == StreetNo;

        /// <summary>A run-card box's call code and notice, kept while its location is picked.</summary>
        public int BoxCallCode { get; set; }
        public string BoxNotice { get; set; } = string.Empty;
    }

    /// <summary>
    /// A validated location - every DSE1 location column Take Call fills from
    /// LaGeo040 and its own TakeCall_ValidateLocation afterwards.
    /// </summary>
    public sealed class LocationResult
    {
        public LocationKind Kind { get; set; }

        /// <summary>The Location box text - LaGeo040 FormatLocation.</summary>
        public string Formatted { get; set; } = string.Empty;

        public string Jurisdiction { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public decimal StreetNo { get; set; }
        public string Sub { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Cross { get; set; } = string.Empty;

        /// <summary>DSE1_GEOCODE_NO - the GEO1 segment, 0 when not geocoded.</summary>
        public int GeoKey { get; set; }

        /// <summary>'L' or 'R' - which side of the segment the address is on.</summary>
        public char Side { get; set; } = ' ';

        /// <summary>DSE1_FORCED - 'Y' when accepted without (or outside) a geobase match.</summary>
        public char Forced { get; set; } = 'N';

        /// <summary>DSE1_ROUTING_ID - the alarm or box routing id.</summary>
        public string RoutingId { get; set; } = string.Empty;

        /// <summary>DSE1_PMM_NO.</summary>
        public int PremiseNo { get; set; }

        // DSE1 areas, from GEO1 by side and then the premise's overrides.
        public string Police1 { get; set; } = string.Empty;
        public string Police2 { get; set; } = string.Empty;
        public string Fire1 { get; set; } = string.Empty;
        public string Fire2 { get; set; } = string.Empty;
        public string Ems1 { get; set; } = string.Empty;
        public string Ems2 { get; set; } = string.Empty;
        public string Other1 { get; set; } = string.Empty;
        public string Other2 { get; set; } = string.Empty;
        public string Tow { get; set; } = string.Empty;
        public int CampusId { get; set; }

        /// <summary>PMM4 latitude / longitude - DSE1_XLOW/XHIGH and YLOW/YHIGH.</summary>
        public decimal X { get; set; }
        public decimal Y { get; set; }

        /// <summary>What the Premise box shows: PMM1 name, else GEO1 common name, else subdivision.</summary>
        public string PremiseName { get; set; } = string.Empty;

        /// <summary>GEO1_C_NAME of the segment - what STK1_LOCATION brackets when there is no premise.</summary>
        public string CommonName { get; set; } = string.Empty;

        /// <summary>PMM2 has hazards for the premise - the legacy Premise button turns red.</summary>
        public bool PremiseHazard { get; set; }

        /// <summary>More than one active premise sits at the address - "Multiple Premises".</summary>
        public bool MultiplePremises { get; set; }

        /// <summary>One premise sits at the address but CAD7_PREM_SW did not attach it.</summary>
        public bool PremiseAvailable { get; set; }

        /// <summary>PMM6_CALL_CODE / RUN1_CALL_CODE for an alarm or box, 0 otherwise.</summary>
        public int CallCode { get; set; }

        /// <summary>The alarm company (ALC1) as the reporting person, for A/ and B/.</summary>
        public AlarmContact Contact { get; set; }

        /// <summary>A notice to show with the result, e.g. that a box came from the run card.</summary>
        public string Notice { get; set; } = string.Empty;
    }

    public sealed class AlarmContact
    {
        public string Name { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
    }

    /// <summary>One row of a picker list. <see cref="Cells"/> follow <see cref="LocationPickList.Columns"/>.</summary>
    public sealed class LocationCandidate
    {
        public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();

        public int GeoKey { get; init; }
        public string CrossName { get; init; } = string.Empty;
        public char CrossType { get; init; } = ' ';
        public int PremiseNo { get; init; }
        public string RoutingId { get; init; } = string.Empty;
    }

    /// <summary>The picker a location needs - which screen, its columns and its rows.</summary>
    public sealed class LocationPickList
    {
        public LocationKind Kind { get; init; }
        public string Title { get; init; } = string.Empty;
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<LocationCandidate> Rows { get; init; } = Array.Empty<LocationCandidate>();
        public LocationQuery Query { get; init; }

        /// <summary>The picker offers Force - every LaGeo04x screen does; the premise list does not.</summary>
        public bool CanForce => Kind != LocationKind.PremiseAtLocation;
    }

    public enum LocationStatus
    {
        /// <summary>A single location was found or chosen.</summary>
        Resolved,

        /// <summary>No single match - the picker has to be shown.</summary>
        Choose,

        /// <summary>An intersection on a split segment - LaGeo043 DefaultSide asks L or R.</summary>
        ChooseSide,

        /// <summary>The entry cannot be a location, or the lookup was abandoned.</summary>
        Invalid
    }

    public sealed class LocationCheck
    {
        public LocationStatus Status { get; init; }
        public LocationResult Result { get; init; }
        public LocationPickList Pick { get; init; }
        public string Message { get; init; } = string.Empty;

        /// <summary>For ChooseSide: the candidate waiting on its side.</summary>
        public LocationCandidate Pending { get; init; }

        public static LocationCheck Invalid(string message) =>
            new LocationCheck { Status = LocationStatus.Invalid, Message = message };

        public static LocationCheck Resolved(LocationResult result) =>
            new LocationCheck { Status = LocationStatus.Resolved, Result = result };
    }

    /// <summary>The CAD7 / JUR1 settings the validation depends on.</summary>
    public sealed class LocationSettings
    {
        /// <summary>CAD7_PREM_SW - attach a single premise found at an address automatically.</summary>
        public bool AttachSinglePremise { get; init; }

        /// <summary>JUR1 where JUR1_CAD_DFLT = 'Y' - used when the jurisdiction is blank.</summary>
        public string DefaultJurisdiction { get; init; } = string.Empty;

        /// <summary>Rows a picker list is capped at - the legacy lists had no cap.</summary>
        public int ListLimit { get; init; } = 300;
    }
}
