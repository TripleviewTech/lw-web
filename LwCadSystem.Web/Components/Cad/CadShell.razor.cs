using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using DevExpress.Blazor;
using LwCadSystem.Web.Services;
using LwCadSystemUX;
using LwCadSystemUX.Models;
using LwCadSystemUX.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace LwCadSystem.Web.Components.Cad;

/// <summary>
/// The shell's behaviour - ShellWindow.xaml.cs. Owns the circuit's one-second
/// clock (ShellViewModel.Tick on the render context), re-renders when the view
/// models change, routes the console keys the browser hands over, and opens the
/// row menus. Panels cascade this component to register their own keys.
/// </summary>
public partial class CadShell : ComponentBase, IAsyncDisposable
{
    [Inject] private CadConsole Console { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDialogService Dialogs { get; set; } = default!;
    [Inject] private IThemeChangeService ThemeChange { get; set; } = default!;

    public ShellViewModel Shell => Console.Shell;

    private IJSObjectReference? _module;
    private DotNetObjectReference<CadShell>? _self;
    private Timer? _clock;
    private bool _disposed;
    private bool _renderQueued;

    private DxContextMenu? _callMenu;
    private DxContextMenu? _unitMenu;

    // The column split between the calls and the Unit Monitor (WPF 1.15* / *).
    private double _callsWeight = 1.15;
    private double _unitsWeight = 1.0;

    // Each monitor's DxGrid column layout (order, widths, visibility), saved per
    // browser in localStorage so a dispatcher's layout comes back next time.
    private MonitorLayout[] _layouts = default!;

    // Each monitor keeps its own cursor row, as each legacy grid kept its current row.
    private readonly Dictionary<CallListViewModel, CadCall> _callCursor = new();
    private CadUnit? _unitCursor;

    // Work for after the next render: a row to scroll to, a list or field to focus.
    private string? _revealId;
    private string? _focusSelector;
    private string? _lastTopPanel;
    private bool _lastModal;

    // ------------------------------------------------------------- lifecycle

    protected override void OnInitialized()
    {
        // Confirmations: the legacy MessageBox, as a DevExpress dialog the view
        // models await (ShellViewModel.ConfirmAsync). Yes is the default, as it was.
        _layouts = new[]
        {
            new MonitorLayout("stacked", Shell.Stacked.Columns),
            new MonitorLayout("active", Shell.Active.Columns),
            new MonitorLayout("delayed", Shell.Delayed.Columns),
            new MonitorLayout("units", Shell.Units.Columns)
        };
        foreach (var layout in _layouts)
        {
            var l = layout;
            l.Changed += () => _ = SaveLayoutAsync(l);
        }

        Shell.ConfirmAsync = ConfirmAsync;
        Shell.CopyToClipboard = text => _ = CopyAsync(text);
        Shell.ThemeChanged += OnThemeChanged;

        Observe(Shell);
        Observe(Shell.Stacked); Observe(Shell.Active); Observe(Shell.Delayed); Observe(Shell.Units);
        Observe(Shell.Stacked.Items); Observe(Shell.Active.Items); Observe(Shell.Delayed.Items); Observe(Shell.Units.Items);
        Observe(Shell.TakeCall); Observe(Shell.UnitFunctions); Observe(Shell.AssignUnits); Observe(Shell.FunctionStub);
        Observe(Shell.CadAlerts); Observe(Shell.LocationPicker); Observe(Shell.SelfInit); Observe(Shell.UnitActivate);
        Observe(Shell.QuickMenuItems);
        foreach (var item in Shell.QuickMenuItems) Observe(item);
        foreach (var item in Shell.CallRowActions) Observe(item);
        foreach (var column in Shell.Stacked.Columns.Concat(Shell.Active.Columns).Concat(Shell.Delayed.Columns).Concat(Shell.Units.Columns))
        {
            Observe(column);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;

        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/cadsystem.js");
            _self = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", _self);
            await _module.InvokeVoidAsync("setTheme", Shell.IsDark);

            // The dispatcher's saved column layouts (nice-to-have; absent is fine).
            foreach (var layout in _layouts)
            {
                try
                {
                    layout.Apply(await _module.InvokeAsync<string?>("loadLayout", "lwcad.layout." + layout.Key));
                }
                catch (JSException)
                {
                }
            }
            StateHasChanged();

            // The board clock - the WPF DispatcherTimer - runs on the circuit's
            // render context, so the board never changes under a render.
            _clock = new Timer(_ => _ = InvokeAsync(OnClockTick), null, 1000, 1000);
        }

        if (_module == null) return;

        // Tell the browser whether a panel holds the console (Ctrl+K and the F12
        // pass-through depend on it).
        if (Shell.IsModalOpen != _lastModal)
        {
            _lastModal = Shell.IsModalOpen;
            await SafeJs("setState", new { modal = _lastModal });
        }

        // A panel that has just come up takes the cursor (ShellWindow's FocusLocation,
        // FocusUnit, FocusList ...); one closing hands it back to the panel beneath.
        var top = TopPanel();
        if (top != _lastTopPanel)
        {
            var returning = _lastTopPanel != null && top != null;
            _lastTopPanel = top;
            if (top != null) await SafeJs("focusFirst", "#lw-panel-" + top, returning ? "data-lw-refocus" : "data-lw-autofocus");
        }

        if (_focusSelector != null)
        {
            var selector = _focusSelector;
            _focusSelector = null;
            await SafeJs("focusElement", selector);
        }

        if (_revealId != null)
        {
            var id = _revealId;
            _revealId = null;
            await SafeJs("reveal", id);
        }
    }

    private void OnClockTick()
    {
        if (_disposed) return;
        Shell.Tick();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _clock?.Dispose();
        Shell.ThemeChanged -= OnThemeChanged;
        foreach (var unsubscribe in _subscriptions) unsubscribe();
        _subscriptions.Clear();

        if (_module != null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone; nothing to tidy in the browser.
            }
        }

        _self?.Dispose();
    }

    // ------------------------------------------------------- change tracking

    private readonly List<Action> _subscriptions = new();

    /// <summary>Re-renders the board when a view model or one of its lists changes.</summary>
    private void Observe(object source)
    {
        if (source is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnModelChanged;
            _subscriptions.Add(() => npc.PropertyChanged -= OnModelChanged);
        }

        if (source is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += OnCollectionChanged;
            _subscriptions.Add(() => ncc.CollectionChanged -= OnCollectionChanged);
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => QueueRender();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRender();

    /// <summary>
    /// The view models raise on the render context (every change comes from an
    /// event handler or the clock), so this only coalesces a burst of
    /// notifications into one render.
    /// </summary>
    private void QueueRender()
    {
        if (_renderQueued || _disposed) return;
        _renderQueued = true;
        _ = InvokeAsync(() =>
        {
            _renderQueued = false;
            if (!_disposed) StateHasChanged();
        });
    }

    /// <summary>Panels call this after changing state the shell draws (status line, scrim).</summary>
    public void Refresh() => QueueRender();

    // ----------------------------------------------------------------- theme

    private void OnThemeChanged(object? sender, EventArgs e) => _ = ApplyThemeAsync();

    private async Task ApplyThemeAsync()
    {
        await SafeJs("setTheme", Shell.IsDark);
        try
        {
            await ThemeChange.SetTheme(Themes.Fluent.Clone(p => p.Mode = Shell.IsDark ? ThemeMode.Dark : ThemeMode.Light));
        }
        catch (Exception)
        {
            // The CAD tokens still re-colour the board; DevExpress keeps its last mode.
        }
    }

    // ---------------------------------------------------------- confirmation

    private bool _confirming;

    private async Task<bool> ConfirmAsync(string message)
    {
        // One question at a time; a second one while the first is up is a no.
        if (_confirming) return false;
        _confirming = true;
        try
        {
            await SafeJs("setState", new { confirm = true });
            return await Dialogs.ConfirmAsync(new MessageBoxOptions
            {
                Title = "Confirmation",
                Text = message,
                OkButtonText = "Yes",
                CancelButtonText = "No",
                CloseOnEscape = true,
                CloseOnOutsideClick = false,
                ShowCloseButton = false,
                CssClass = "lw-confirm",
                Width = "420px"
            });
        }
        finally
        {
            _confirming = false;
            await SafeJs("setState", new { confirm = false });
            QueueRender();
        }
    }

    private Task SaveLayoutAsync(MonitorLayout layout) =>
        SafeJs("saveLayout", "lwcad.layout." + layout.Key, layout.Serialize());

    private async Task CopyAsync(string text)
    {
        if (_module == null) return;
        try
        {
            if (!await _module.InvokeAsync<bool>("copyText", text))
            {
                Shell.SetStatus("The browser did not allow the clipboard - select the text and copy it instead.");
            }
        }
        catch (JSException)
        {
        }
    }

    // ------------------------------------------------------------ board sizing

    private static string Fr(double weight) => weight.ToString("0.###", CultureInfo.InvariantCulture) + "fr";

    /// <summary>The CSS grid track for a monitor: its RowHeight, floored at its minimum.</summary>
    private static string Track(CallListViewModel monitor)
    {
        if (monitor.RowHeight.IsAuto) return "auto";
        var min = monitor.RowMinHeight.ToString("0", CultureInfo.InvariantCulture) + "px";
        return "minmax(" + min + ", " + monitor.RowHeight.ToCss() + ")";
    }

    /// <summary>
    /// A grab bar was dropped (or nudged with the arrow keys). As WPF's
    /// GridSplitter: two star tracks keep their combined weight and share it in
    /// proportion to the dragged sizes; a track that is not star (a fitted or
    /// dragged monitor) takes the dragged pixels, which drops the fit.
    /// </summary>
    [JSInvokable]
    public Task OnSplitterMoved(string bar, double before, double after) => OnSplitterMovedCore(bar, before, after);

    private Task OnSplitterMovedCore(string bar, double before, double after)
    {
        return InvokeAsync(() =>
        {
            switch (bar)
            {
                case "stacked-active": Split(Shell.Stacked, before, Shell.Active, after); break;
                case "active-delayed": Split(Shell.Active, before, Shell.Delayed, after); break;
                case "calls-units":
                    var total = _callsWeight + _unitsWeight;
                    if (before + after > 0)
                    {
                        _callsWeight = Math.Round(total * before / (before + after), 3);
                        _unitsWeight = Math.Round(total - _callsWeight, 3);
                    }
                    break;
            }

            StateHasChanged();
        });
    }

    private static void Split(CallListViewModel a, double aPx, CallListViewModel b, double bPx)
    {
        if (aPx + bPx <= 0) return;

        if (a.RowHeight.IsStar && b.RowHeight.IsStar)
        {
            var total = a.RowHeight.Value + b.RowHeight.Value;
            var share = Math.Round(total * aPx / (aPx + bPx), 4);
            a.RowHeight = new GridLength(share, GridUnitType.Star);
            b.RowHeight = new GridLength(Math.Round(total - share, 4), GridUnitType.Star);
            return;
        }

        if (!a.RowHeight.IsStar) a.RowHeight = new GridLength(Math.Round(aPx), GridUnitType.Pixel);
        if (!b.RowHeight.IsStar) b.RowHeight = new GridLength(Math.Round(bPx), GridUnitType.Pixel);
    }

    // ----------------------------------------------------------- the cursor

    private CadCall? CursorFor(CallListViewModel monitor) =>
        _callCursor.TryGetValue(monitor, out var call) && monitor.Items.Contains(call) ? call : null;

    /// <summary>A row was clicked: it is selected, and becomes its list's cursor row.</summary>
    public void SelectCallRow(CallListViewModel monitor, CadCall call)
    {
        _callCursor[monitor] = call;
        Shell.SelectCallCommand.Execute(call);
    }

    public void SelectUnitRow(CadUnit unit)
    {
        _unitCursor = unit;
        Shell.Units.SelectUnitCommand.Execute(unit);
    }

    public static string RowId(string list, CadCall call) =>
        "lw-" + list + "-" + Sanitize(call.EventKey > 0 ? call.EventKey.ToString(CultureInfo.InvariantCulture) : call.EventNo);

    public static string RowId(CadUnit unit) => "lw-unit-" + Sanitize(unit.UnitNo);

    private static string Sanitize(string? text) =>
        new string((text ?? string.Empty).Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    // ------------------------------------------------------------- row menus

    private bool _menuOpen;

    /// <summary>
    /// The ellipsis on a call row: selects the row first - the menu is named for
    /// the monitor the call is in (Assign Call / Assign / Assist) - then opens it.
    /// </summary>
    public async Task ShowCallMenu(CallListViewModel monitor, CadCall call, MouseEventArgs e)
    {
        SelectCallRow(monitor, call);
        Shell.CallRowMenuCommand.Execute(call);
        StateHasChanged();
        await Task.Yield();
        if (_callMenu != null)
        {
            _menuOpen = true;
            await _callMenu.ShowAsync(e);
        }
    }

    public async Task ShowUnitMenu(CadUnit unit, MouseEventArgs e)
    {
        SelectUnitRow(unit);
        Shell.UnitRowMenuCommand.Execute(unit);
        StateHasChanged();
        await Task.Yield();
        if (_unitMenu != null)
        {
            _menuOpen = true;
            await _unitMenu.ShowAsync(e);
        }
    }

    private void OnMenuHidden() => _menuOpen = false;

    private void RunAction(MenuAction action)
    {
        _menuOpen = false;
        if (action.Command?.CanExecute(action.CommandParameter) ?? false) action.Command.Execute(action.CommandParameter);
        QueueRender();
    }

    // ------------------------------------------------------------- keyboard

    /// <summary>The panel key handlers, by panel name (see TopPanel).</summary>
    private readonly Dictionary<string, Func<ConsoleKey, Task<bool>>> _panelKeys = new();

    /// <summary>
    /// A panel registers its own keys (Take Call's F3 / F5 / F11 / F12 / Shift+F1-F4 ...).
    /// They reach it while it is the topmost open panel. Esc is not passed: it
    /// closes the topmost panel through the view model, as in WPF.
    /// </summary>
    public void RegisterPanelKeys(string panel, Func<ConsoleKey, Task<bool>> handler) => _panelKeys[panel] = handler;

    public void UnregisterPanelKeys(string panel) => _panelKeys.Remove(panel);

    /// <summary>The panel holding the keyboard, in the order Esc closes them (ShellWindow).</summary>
    public string? TopPanel()
    {
        if (Shell.UnitActivate.IsOpen) return "unitactivate";
        if (Shell.LocationPicker.IsOpen) return "locationpicker";
        if (Shell.FunctionStub.IsOpen) return "functionstub";
        if (Shell.CadAlerts.IsOpen) return "cadalerts";
        if (Shell.AssignUnits.IsOpen) return "assign";
        if (Shell.UnitFunctions.IsOpen) return "unitfunction";
        if (Shell.SelfInit.IsOpen) return "selfinit";
        if (Shell.TakeCall.IsOpen && Shell.TakeCall.IsExpanded) return "takecall";
        return null;
    }

    [JSInvokable]
    public Task OnConsoleKey(ConsoleKey key) => InvokeAsync(() => HandleKey(key));

    private async Task HandleKey(ConsoleKey key)
    {
        if (_disposed || _confirming) return;

        switch (key.Kind)
        {
            case "chord":
                // Ctrl+K then the key's letter (the browser tracked the chord).
                Shell.InvokeShortcut(key.Key);
                break;

            case "ctrlK":
                // The chords reach the Quick Menu whatever has focus - which makes
                // them the one way past the scrim, so they are refused here.
                Shell.SetStatus(Shell.IsModalOpen
                    ? "The Quick Menu is not available while a panel is open. Finish or cancel it first - Esc closes it."
                    : "Ctrl+K - waiting for a function key...");
                break;

            case "fkey":
                await HandleFunctionKey(key);
                break;

            case "ctrl":
                await HandleCadCtrlFunction(key.Key);
                break;

            case "esc":
                await HandleEscape();
                break;

            case "nav":
                HandleNavigation(key);
                break;
        }

        StateHasChanged();
    }

    /// <summary>
    /// F1-F9 alone, with Shift or with Ctrl run the Standard Functions key
    /// (CadMain Window_KeyDown). While a panel is open the keys are the panel's -
    /// Take Call has its own Quickeys on F1-F6.
    /// </summary>
    private async Task HandleFunctionKey(ConsoleKey key)
    {
        var top = TopPanel();
        if (top != null)
        {
            if (_panelKeys.TryGetValue(top, out var handler)) await handler(key);
            return;
        }

        if (key.Number > 9) return;

        KeyBank bank;
        if (!key.Shift && !key.Ctrl) bank = KeyBank.Function;
        else if (key.Shift && !key.Ctrl) bank = KeyBank.Shift;
        else if (key.Ctrl && !key.Shift) bank = KeyBank.Ctrl;
        else return;

        Shell.InvokeFunctionKey(bank, key.Number);
    }

    /// <summary>Esc closes the topmost panel; with nothing open it locks every list (Lowell).</summary>
    private async Task HandleEscape()
    {
        if (_menuOpen)
        {
            _menuOpen = false;
            if (_callMenu != null) await _callMenu.HideAsync();
            if (_unitMenu != null) await _unitMenu.HideAsync();
            return;
        }

        if (Shell.UnitActivate.IsOpen) Shell.UnitActivate.Cancel();
        else if (Shell.LocationPicker.IsOpen) Shell.LocationPicker.Cancel();
        else if (Shell.FunctionStub.IsOpen) Shell.FunctionStub.Cancel();
        else if (Shell.CadAlerts.IsOpen) Shell.CadAlerts.Cancel();
        else if (Shell.AssignUnits.IsOpen) Shell.AssignUnits.Cancel();
        else if (Shell.UnitFunctions.IsOpen) Shell.UnitFunctions.Cancel();
        else if (Shell.SelfInit.IsOpen) await Shell.SelfInit.CancelAsync();
        else if (Shell.TakeCall.IsOpen) await Shell.TakeCall.CancelAsync();
        else
        {
            // Lowell: Esc with nothing open locks every list, so a dispatcher
            // can freeze the board mid-refresh (CadMain SetLockStacked/Active/Delayed/Units).
            Shell.LockAllLists();
        }
    }

    /// <summary>
    /// The Ctrl functions of the legacy CadMain: Ctrl+S / A / D / U put the
    /// cursor on a monitor, Ctrl+T (Alt+T) on the toolbar, Ctrl+W (Alt+W) opens
    /// the working call's functions. Refused while a panel holds the console. The
    /// browser has already left them alone while the caret is in a text field.
    /// </summary>
    private async Task HandleCadCtrlFunction(string letter)
    {
        if (Shell.IsModalOpen)
        {
            Shell.SetStatus("The board is not available while a panel is open. Finish or cancel it first - Esc closes it.");
            return;
        }

        switch (letter)
        {
            case "S": FocusMonitor(Shell.Stacked, "stacked"); break;
            case "A": FocusMonitor(Shell.Active, "active"); break;
            case "D": FocusMonitor(Shell.Delayed, "delayed"); break;

            case "U":
                if (Shell.Units.Items.Count == 0)
                {
                    Shell.SetStatus("Unit Monitor is empty.");
                    break;
                }

                var unit = Shell.Units.SelectedUnit != null && Shell.Units.Items.Contains(Shell.Units.SelectedUnit)
                    ? Shell.Units.SelectedUnit
                    : Shell.Units.Items[0];
                _unitCursor = unit;
                _focusSelector = "[data-lw-list=units]";
                _revealId = RowId(unit);
                Shell.SetStatus("Cursor on the Unit Monitor" + RowSuffix(Shell.Units.SelectedUnit?.UnitNo) + ".");
                break;

            case "T":
                _focusSelector = "#lw-topbar-first";
                Shell.SetStatus("Cursor on the toolbar.");
                break;

            case "W":
                await OpenWorkingCallMenu();
                break;
        }
    }

    /// <summary>
    /// Puts the cursor on one call monitor, on its selected row when it has one
    /// and its first row otherwise. A collapsed monitor is opened first: a Ctrl+D
    /// that appeared to do nothing would be worse than no Ctrl+D at all.
    /// </summary>
    private void FocusMonitor(CallListViewModel monitor, string list)
    {
        if (!monitor.ShowRows) monitor.Restore();

        if (monitor.Items.Count == 0)
        {
            Shell.SetStatus(monitor.Title + " is empty.");
            return;
        }

        var selected = monitor.Items.FirstOrDefault(c => c.IsSelected);
        var row = selected ?? monitor.Items[0];
        _callCursor[monitor] = row;
        _focusSelector = "[data-lw-list=" + list + "]";
        _revealId = RowId(list, row);
        Shell.SetStatus("Cursor on " + monitor.Title + RowSuffix(selected?.EventNo) + ".");
    }

    private static string RowSuffix(string? row) => string.IsNullOrWhiteSpace(row) ? ", first row" : ", " + row;

    /// <summary>
    /// The legacy Ctrl+W put the cursor on the working call's button bank. This
    /// board has none; the same functions live behind the selected call's row
    /// menu, so that is what this opens (a re-map, as in the WPF build).
    /// </summary>
    private async Task OpenWorkingCallMenu()
    {
        var call = Shell.SelectedCall;
        if (call == null)
        {
            Shell.SetStatus("No call is selected. Ctrl+S, Ctrl+A or Ctrl+D picks one first.");
            return;
        }

        var (monitor, list) = call.Queue switch
        {
            CallQueue.Active => (Shell.Active, "active"),
            CallQueue.Delayed => (Shell.Delayed, "delayed"),
            _ => (Shell.Stacked, "stacked")
        };

        if (!monitor.Items.Contains(call))
        {
            Shell.SetStatus("The selected call is no longer on the board.");
            return;
        }

        if (!monitor.ShowRows) monitor.Restore();
        _callCursor[monitor] = call;
        Shell.CallRowMenuCommand.Execute(call);
        StateHasChanged();
        await Task.Yield();

        var id = RowId(list, call);
        if (_module != null) await SafeJs("reveal", id);
        var box = _module == null ? null : await _module.InvokeAsync<BoxRect?>("rect", "#" + id + " .lw-rowmenu-btn");
        if (box == null || _callMenu == null)
        {
            Shell.SetStatus("The selected call is no longer on the board.");
            return;
        }

        _menuOpen = true;
        await _callMenu.ShowAsync(box.Left, box.Bottom + 4);
        Shell.SetStatus("Functions for " + call.EventNo + ".");
    }

    private sealed class BoxRect
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Right { get; set; }
        public double Bottom { get; set; }
    }

    /// <summary>
    /// Cursoring a monitor with the arrow keys, Home / End and PageUp / PageDown
    /// selects the row the cursor lands on (the ListBox did the same); Space
    /// toggles the preview on the selected row.
    /// </summary>
    private void HandleNavigation(ConsoleKey key)
    {
        if (Shell.IsModalOpen) return;

        if (key.List == "units")
        {
            NavigateUnits(key);
            return;
        }

        var monitor = key.List switch
        {
            "stacked" => Shell.Stacked,
            "active" => Shell.Active,
            "delayed" => Shell.Delayed,
            _ => null
        };
        if (monitor == null || monitor.Items.Count == 0) return;

        var items = monitor.Items;
        var selected = items.FirstOrDefault(c => c.IsSelected);

        if (key.Key == "Space")
        {
            if (selected != null) monitor.ToggleExpandCommand.Execute(selected);
            return;
        }

        var current = selected ?? CursorFor(monitor);
        var index = Move(current == null ? -1 : items.IndexOf(current), items.Count, key);
        var target = items[index];

        _callCursor[monitor] = target;
        Shell.SelectCall(target);
        _revealId = RowId(key.List, target);
    }

    private void NavigateUnits(ConsoleKey key)
    {
        var items = Shell.Units.Items;
        if (items.Count == 0 || key.Key == "Space") return;

        var current = Shell.Units.SelectedUnit != null && items.Contains(Shell.Units.SelectedUnit)
            ? Shell.Units.SelectedUnit
            : _unitCursor != null && items.Contains(_unitCursor) ? _unitCursor : null;
        var index = Move(current == null ? -1 : items.IndexOf(current), items.Count, key);

        _unitCursor = items[index];
        Shell.Units.SelectedUnit = items[index];
        _revealId = RowId(items[index]);
    }

    private static int Move(int index, int count, ConsoleKey key)
    {
        var page = Math.Max(1, key.Page);
        var next = key.Key switch
        {
            "ArrowDown" => index < 0 ? 0 : index + 1,
            "ArrowUp" => index < 0 ? 0 : index - 1,
            "Home" => 0,
            "End" => count - 1,
            "PageDown" => index < 0 ? Math.Min(page, count - 1) : index + page,
            "PageUp" => index < 0 ? 0 : index - page,
            _ => index < 0 ? 0 : index
        };
        return Math.Clamp(next, 0, count - 1);
    }

    // ------------------------------------------------------------------ util

    private async Task SafeJs(string identifier, params object?[] args)
    {
        if (_module == null || _disposed) return;
        try
        {
            await _module.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    /// <summary>Lets a panel run a browser helper (focus a field, copy text).</summary>
    public Task RunJs(string identifier, params object?[] args) => SafeJs(identifier, args);

}
