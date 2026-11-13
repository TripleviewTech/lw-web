namespace LwCadMapping.Web.Geo;

/// <summary>A premise record with a hazard note - what the Premise_Locations layer shows.</summary>
public sealed class Premise
{
    public int No { get; init; }
    public string Name { get; init; } = "";
    public string Address { get; init; } = "";
    public string Hazards { get; init; } = "";
    public string Contact { get; init; } = "";
    public GeoPoint Point { get; init; }

    /// <summary>True for the mock-up's fictional premises (demo geography only).</summary>
    public bool IsDemo { get; init; }
}

/// <summary>
/// The mock-up's six fictional Lowell premises (Services\DemoCadData.BuildPremises),
/// placed with the demo geocoder. Used only on the demo board / without an ArcGIS key.
/// </summary>
public static class DemoPremises
{
    public static IReadOnlyList<Premise> Build()
    {
        var search = new DemoLocationSearch(() => Array.Empty<Premise>());
        var list = new List<Premise>();

        void Add(int no, string name, string address, string hazards, string contact) =>
            list.Add(new Premise
            {
                No = no, Name = name, Address = address, Hazards = hazards, Contact = contact,
                Point = search.Find(address)?.Point ?? DemoGeography.Home, IsDemo = true,
            });

        Add(101, "MILL CITY OFFICE BLDG", "350 MERRIMACK ST",
            "Knox box at the Merrimack St door. Alarm panel in the lobby, left of the elevators.",
            "Building manager 978-555-0142");
        Add(102, "BELVIDERE SENIOR HOUSING", "85 ANDOVER ST",
            "Elderly residents. Elevator 2 out of service until 10/20. Stair chair on floor 3.",
            "Front desk 978-555-0170");
        Add(103, "MILL CITY APARTMENTS", "250 WESTFORD ST",
            "48 units. Gas meters in the basement, rear entry off the lot.",
            "Superintendent 978-555-0118");
        Add(104, "RIVERSIDE TEXTILE (VACANT)", "1100 PAWTUCKET BLVD",
            "Vacant mill. Structurally unsound - no interior operations. Squatters reported.",
            "Owner's agent 617-555-0103");
        Add(105, "BRIDGE ST PHARMACY", "118 BRIDGE ST",
            "Controlled substances on site. Alarm company 800-555-0199.",
            "Store manager 978-555-0155");
        Add(106, "LOWELL POLICE HQ", "50 ARCAND DR",
            "Sally port on Arcand Dr. Visitor parking only in the front lot.",
            "Desk officer 978-555-0100");

        return list;
    }
}
