namespace LwCad.Web.Shared;

/// <summary>
/// The stroked 24x24 icon geometries of LwCadSystemUX Themes\Icons.xaml, as SVG
/// path data. The XAML StreamGeometry syntax is SVG-compatible, so the strings
/// are copied unchanged. Drawn by <see cref="LwIcon"/>.
/// </summary>
public static class LwIcons
{
    public const string Swap = "M 7,4 L 3,8 L 7,12 M 3,8 L 20,8 M 17,12 L 21,16 L 17,20 M 21,16 L 4,16";
    public const string Layers = "M 12,3 L 21,7.5 L 12,12 L 3,7.5 Z M 3,12 L 12,16.5 L 21,12 M 3,16.5 L 12,21 L 21,16.5";
    public const string Clock = "M 12,3 A 9,9 0 1 1 11.99,3 Z M 12,7 L 12,12.5 L 16,14.5";
    public const string Monitor = "M 4,4 L 20,4 A 1.5,1.5 0 0 1 21.5,5.5 L 21.5,15 A 1.5,1.5 0 0 1 20,16.5 L 4,16.5 A 1.5,1.5 0 0 1 2.5,15 L 2.5,5.5 A 1.5,1.5 0 0 1 4,4 Z M 8,20.5 L 16,20.5 M 12,16.5 L 12,20.5";
    public const string PopOut = "M 14,3.5 L 20.5,3.5 L 20.5,10 M 20.5,3.5 L 12.5,11.5 M 18,13.5 L 18,19 A 1.8,1.8 0 0 1 16.2,20.8 L 5,20.8 A 1.8,1.8 0 0 1 3.2,19 L 3.2,7.8 A 1.8,1.8 0 0 1 5,6 L 10.5,6";
    public const string Lock = "M 5.5,10.5 L 18.5,10.5 A 1.5,1.5 0 0 1 20,12 L 20,19 A 1.5,1.5 0 0 1 18.5,20.5 L 5.5,20.5 A 1.5,1.5 0 0 1 4,19 L 4,12 A 1.5,1.5 0 0 1 5.5,10.5 Z M 7.5,10.5 L 7.5,7.5 A 4.5,4.5 0 0 1 16.5,7.5 L 16.5,10.5";
    public const string Unlock = "M 5.5,10.5 L 18.5,10.5 A 1.5,1.5 0 0 1 20,12 L 20,19 A 1.5,1.5 0 0 1 18.5,20.5 L 5.5,20.5 A 1.5,1.5 0 0 1 4,19 L 4,12 A 1.5,1.5 0 0 1 5.5,10.5 Z M 7.5,10.5 L 7.5,7.5 A 4.5,4.5 0 0 1 16.2,6.2";
    public const string Ellipsis = "M 5.6,12 A 1.4,1.4 0 1 1 5.59,12 Z M 12,12 A 1.4,1.4 0 1 1 11.99,12 Z M 18.4,12 A 1.4,1.4 0 1 1 18.39,12 Z";
    public const string Columns = "M 4,4 L 20,4 L 20,20 L 4,20 Z M 10.5,4 L 10.5,20 M 16,4 L 16,20";
    public const string Funnel = "M 3.5,6.5 L 20.5,6.5 M 7,12 L 17,12 M 10,17.5 L 14,17.5";
    public const string ChevronDown = "M 6,9.5 L 12,15.5 L 18,9.5";
    public const string ChevronsLeft = "M 11,18 L 5,12 L 11,6 M 19,18 L 13,12 L 19,6";
    public const string ChevronsRight = "M 13,6 L 19,12 L 13,18 M 5,6 L 11,12 L 5,18";
    public const string Inbox = "M 22,12.5 L 16,12.5 L 14,15.5 L 10,15.5 L 8,12.5 L 2,12.5 M 5.6,4.6 L 2,12.5 L 2,18.5 A 1.8,1.8 0 0 0 3.8,20.3 L 20.2,20.3 A 1.8,1.8 0 0 0 22,18.5 L 22,12.5 L 18.4,4.6 A 1.8,1.8 0 0 0 16.8,3.6 L 7.2,3.6 A 1.8,1.8 0 0 0 5.6,4.6 Z";
    public const string Bell = "M 6,9.5 A 6,6 0 0 1 18,9.5 C 18,16 20.5,18 20.5,18 L 3.5,18 C 3.5,18 6,16 6,9.5 Z M 10.2,21 A 2,2 0 0 0 13.8,21";
    public const string Copy = "M 9,9 L 19,9 A 1.5,1.5 0 0 1 20.5,10.5 L 20.5,20.5 A 1.5,1.5 0 0 1 19,22 L 9,22 A 1.5,1.5 0 0 1 7.5,20.5 L 7.5,10.5 A 1.5,1.5 0 0 1 9,9 Z M 4.5,15 A 1.5,1.5 0 0 1 3,13.5 L 3,3.5 A 1.5,1.5 0 0 1 4.5,2 L 14.5,2 A 1.5,1.5 0 0 1 16,3.5 L 16,5";
    public const string PlusSquare = "M 5,3.5 L 19,3.5 A 1.5,1.5 0 0 1 20.5,5 L 20.5,19 A 1.5,1.5 0 0 1 19,20.5 L 5,20.5 A 1.5,1.5 0 0 1 3.5,19 L 3.5,5 A 1.5,1.5 0 0 1 5,3.5 Z M 12,8 L 12,16 M 8,12 L 16,12";
    public const string Close = "M 6,6 L 18,18 M 18,6 L 6,18";
    public const string Sun = "M 12,5 A 7,7 0 1 1 11.99,5 Z M 12,1.5 L 12,3 M 12,21 L 12,22.5 M 1.5,12 L 3,12 M 21,12 L 22.5,12 M 4.6,4.6 L 5.6,5.6 M 18.4,18.4 L 19.4,19.4 M 19.4,4.6 L 18.4,5.6 M 5.6,18.4 L 4.6,19.4";
    public const string Moon = "M 20,14.5 A 8.5,8.5 0 1 1 9.5,4 A 7,7 0 0 0 20,14.5 Z";
    public const string UserPlus = "M 3.5,20.5 C 3.5,16.6 6.6,14.5 10,14.5 C 13.4,14.5 16.5,16.6 16.5,20.5 M 10,4 A 3.6,3.6 0 1 1 9.99,4 Z M 19,8.5 L 19,15 M 15.8,11.7 L 22.2,11.7";
    public const string CheckSquare = "M 5.5,3.5 L 18.5,3.5 A 2,2 0 0 1 20.5,5.5 L 20.5,18.5 A 2,2 0 0 1 18.5,20.5 L 5.5,20.5 A 2,2 0 0 1 3.5,18.5 L 3.5,5.5 A 2,2 0 0 1 5.5,3.5 Z M 7.5,12 L 10.8,15.3 L 16.5,8.8";
    public const string Plus = "M 12,5 L 12,19 M 5,12 L 19,12";
    public const string Warning = "M 12,3.5 L 22,20.5 L 2,20.5 Z M 12,9.5 L 12,14 M 12,17.2 L 12,17.4";

    /// <summary>The XAML key names the view models carry (CallListViewModel.IconKey: "Icon.Swap").</summary>
    public static string FromKey(string? key) => key switch
    {
        "Icon.Swap" => Swap,
        "Icon.Layers" => Layers,
        "Icon.Clock" => Clock,
        "Icon.Monitor" => Monitor,
        _ => string.Empty
    };
}
