using System.Globalization;
using System.Text.Json;
using LwCadSystemUX.Models;

namespace LwCadSystem.Web.Components.Cad;

/// <summary>
/// The column layout of one monitor's DxGrid: which columns show and how wide
/// they are live on the view model's <see cref="ColumnSpec"/> (so the column
/// chooser and Reset to default keep working); the order the dispatcher dragged
/// the columns into, and the width of the flexible column (Location) once it has
/// been dragged, live here. The grid's Visible / Width / VisibleIndex parameters
/// are bound two-way to these, so the layout survives the grid being unmounted
/// (an empty or collapsed monitor renders no grid) and can be saved per user.
/// </summary>
public sealed class MonitorLayout
{
    private readonly IList<ColumnSpec> _columns;
    private readonly Dictionary<string, int> _order = new();
    private readonly Dictionary<string, string?> _flexWidth = new();

    public MonitorLayout(string key, IList<ColumnSpec> columns)
    {
        Key = key;
        _columns = columns;
        ResetOrder();
    }

    /// <summary>stacked, active, delayed or units - also the localStorage key suffix.</summary>
    public string Key { get; }

    /// <summary>Raised when the dispatcher changes the layout (to save it).</summary>
    public event Action? Changed;

    /// <summary>Column order for DxGrid VisibleIndex. 0 is the leading tool column.</summary>
    public int OrderOf(ColumnSpec column) => _order.TryGetValue(column.Key, out var i) ? i : 1;

    public void SetOrder(ColumnSpec column, int index)
    {
        if (_order.TryGetValue(column.Key, out var current) && current == index) return;
        _order[column.Key] = index;
        Changed?.Invoke();
    }

    /// <summary>The DxGrid Width for a column: fixed columns from ColumnSpec.Width, the flexible one auto until dragged.</summary>
    public string? WidthOf(ColumnSpec column) =>
        column.IsFlexible
            ? _flexWidth.GetValueOrDefault(column.Key)
            : column.Width.ToString("0", CultureInfo.InvariantCulture) + "px";

    public void SetWidth(ColumnSpec column, string? width)
    {
        if (column.IsFlexible)
        {
            if (_flexWidth.GetValueOrDefault(column.Key) == width) return;
            _flexWidth[column.Key] = width;
            Changed?.Invoke();
            return;
        }

        var px = ParsePx(width);
        if (px == null || Math.Abs(px.Value - column.Width) < 0.5) return;
        column.Width = Math.Max(ColumnSpec.MinWidth, Math.Round(px.Value));
        Changed?.Invoke();
    }

    public void SetVisible(ColumnSpec column, bool visible)
    {
        if (column.IsVisible == visible || (!visible && !column.CanHide)) return;
        column.IsVisible = visible;
        Changed?.Invoke();
    }

    /// <summary>Reset to default: the view model resets visibility and widths; this resets order and the flexible width.</summary>
    public void ResetOrder()
    {
        _order.Clear();
        _flexWidth.Clear();
        for (int i = 0; i < _columns.Count; i++) _order[_columns[i].Key] = i + 1;
    }

    // ------------------------------------------------------------ persistence

    private sealed record Saved(string Key, bool Visible, double Width, int Order, string? FlexWidth);

    public string Serialize() => JsonSerializer.Serialize(_columns.Select(c =>
        new Saved(c.Key, c.IsVisible, c.Width, OrderOf(c), _flexWidth.GetValueOrDefault(c.Key))));

    /// <summary>Applies a saved layout; unknown or malformed content is ignored.</summary>
    public void Apply(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<List<Saved>>(json);
            if (saved == null) return;
            foreach (var s in saved)
            {
                var column = _columns.FirstOrDefault(c => c.Key == s.Key);
                if (column == null) continue;
                if (column.CanHide) column.IsVisible = s.Visible;
                if (!column.IsFlexible && s.Width >= ColumnSpec.MinWidth) column.Width = s.Width;
                _order[column.Key] = s.Order;
                if (column.IsFlexible) _flexWidth[column.Key] = s.FlexWidth;
            }
        }
        catch (JsonException)
        {
        }
    }

    private static double? ParsePx(string? width)
    {
        if (string.IsNullOrWhiteSpace(width)) return null;
        var text = width.Trim();
        if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase)) text = text[..^2];
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
