using System.Globalization;

namespace LwCadSystemUX.Services.Location
{
    /// <summary>
    /// LaGeo040 GeoFormatLocation: "12-A MAIN ST (4)" for an address,
    /// "MAIN ST / ELM ST" for an intersection, and the cross-street text alone when
    /// there is no street (the P/ A/ C/ literal, or a Force X entry).
    /// </summary>
    public static class LocationFormatter
    {
        public static string Format(decimal streetNo, string street, string sub, string unit, string cross)
        {
            street = (street ?? string.Empty).Trim();
            sub = (sub ?? string.Empty).Trim();
            unit = (unit ?? string.Empty).Trim();
            cross = (cross ?? string.Empty).Trim();

            if (street.Length == 0) return cross;

            string text;
            if (streetNo == 0 && cross.Length > 0)
            {
                text = street + " / " + cross;
            }
            else
            {
                text = streetNo == decimal.Truncate(streetNo)
                    ? ((long)streetNo).ToString(CultureInfo.InvariantCulture)
                    : streetNo.ToString(CultureInfo.InvariantCulture);

                if (sub.Length > 0) text += "-" + sub;
                text += " " + street;
                if (unit.Length > 0) text += " (" + unit + ")";
            }

            return text.Replace("((", "(").Replace("))", ")");
        }
    }
}
