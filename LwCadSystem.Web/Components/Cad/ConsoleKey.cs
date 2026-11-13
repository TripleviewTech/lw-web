namespace LwCadSystem.Web.Components.Cad;

/// <summary>
/// A key the browser module (wwwroot/js/cadsystem.js) decided belongs to the
/// console and handed over - the web stand-in for ShellWindow.OnPreviewKeyDown's
/// KeyEventArgs. Kind is one of: fkey, esc, ctrlK, chord, ctrl, nav.
/// </summary>
public sealed class ConsoleKey
{
    public string Kind { get; set; } = string.Empty;

    /// <summary>The letter for chord / ctrl, or the navigation key for nav (ArrowUp, Space, ...).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>F1-F12 for fkey.</summary>
    public int Number { get; set; }

    public bool Shift { get; set; }
    public bool Ctrl { get; set; }

    /// <summary>The caret was in a text field when the key was pressed.</summary>
    public bool InText { get; set; }

    /// <summary>For nav: which monitor list had the focus (stacked, active, delayed, units).</summary>
    public string List { get; set; } = string.Empty;

    /// <summary>For nav: rows a PageUp / PageDown moves, from the list's visible height.</summary>
    public int Page { get; set; } = 10;

    public bool IsF(int number, bool shift = false, bool ctrl = false) =>
        Kind == "fkey" && Number == number && Shift == shift && Ctrl == ctrl;
}
