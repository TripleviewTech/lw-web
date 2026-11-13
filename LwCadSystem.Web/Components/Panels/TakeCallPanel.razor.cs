using DevExpress.Blazor;
using LwCadSystem.Web.Components.Cad;
using LwCadSystemUX.Models;
using LwCadSystemUX.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ConsoleKey = LwCadSystem.Web.Components.Cad.ConsoleKey;

namespace LwCadSystem.Web.Components.Panels;

/// <summary>
/// TakeCallPanel.xaml.cs: the legacy Take Call keys, Location validated on
/// leaving the field (but not on the way to Cancel), and SAME applied when the
/// address field is left.
/// </summary>
public partial class TakeCallPanel : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public TakeCallViewModel Vm { get; set; } = default!;
    [Parameter, EditorRequired] public ShellViewModel Shell { get; set; } = default!;
    [CascadingParameter] public CadShell Host { get; set; } = default!;

    private DxComboBox<CallCodeItem, CallCodeItem>? _callCodeBox;
    private DxComboBox<LookupItem, LookupItem>? _originBox;
    private DxComboBox<LookupItem, LookupItem>? _dispositionBox;
    private DxComboBox<string, string>? _stateBox;
    private DxComboBox<string, string>? _jurBox;
    private DxComboBox<string, string>? _forceBox;

    /// <summary>The field that has the caret (Keyboard.FocusedElement in WPF), by name.</summary>
    private string? _focused;

    /// <summary>A drop-down list is open - Enter picks from it rather than saving.</summary>
    private bool _dropOpen;

    /// <summary>The pointer went down on Cancel: leaving Location on the way there does not validate it.</summary>
    private bool _cancelPressing;

    private static readonly Dictionary<string, object> DispatchAttributes = new() { ["title"] = "F3 or Enter" };

    private string PrioClass => Vm.IsHigh ? "high" : Vm.IsMedium ? "medium" : "low";

    protected override void OnInitialized() => Host.RegisterPanelKeys("takecall", OnConsoleKey);

    public void Dispose() => Host.UnregisterPanelKeys("takecall");

    private void Focused(string field)
    {
        _focused = field;
        _cancelPressing = false;
    }

    private void Blurred(string field)
    {
        if (_focused == field) _focused = null;
    }

    /// <summary>
    /// Val_tbxAV_Location runs when the operator leaves the field - but not on the
    /// way to Cancel, and not when the form is closing or the field is blank.
    /// </summary>
    private void OnLocationBlur(FocusEventArgs e)
    {
        Blurred("location");
        if (_cancelPressing || !Vm.IsOpen || string.IsNullOrWhiteSpace(Vm.Location)) return;
        Vm.ValidateLocation(saveAfter: false);
    }

    private void OnAddressBlur(FocusEventArgs e)
    {
        Blurred("address");
        Vm.ApplySame();
    }

    private void OnCancelClick()
    {
        _cancelPressing = false;
        Vm.CancelCommand.Execute(null);
    }

    /// <summary>
    /// The legacy Take Call keys (TakeCall.cs:1180-1291), handed over by the shell
    /// while Take Call is the top panel: F3 saves, F5 cancels, F11 loads the E911
    /// call, F12 opens the focused field's list (or validates Location), and
    /// Shift+F1 - F4 open the call code, origin, disposition and state lists.
    /// </summary>
    private async Task<bool> OnConsoleKey(ConsoleKey key)
    {
        if (!Vm.IsOpen || Vm.IsCollapsed || key.Kind != "fkey" || key.Ctrl) return false;

        if (key.Shift)
        {
            switch (key.Number)
            {
                case 1: await OpenList(_callCodeBox); return true;
                case 2: await OpenList(_originBox); return true;
                case 3: await OpenList(_dispositionBox); return true;
                case 4: await OpenList(_stateBox); return true;
            }

            return false;
        }

        switch (key.Number)
        {
            case 3:
                Vm.DispatchCommand.Execute(null);
                return true;

            case 5:
                Vm.CancelCommand.Execute(null);
                return true;

            case 11:
                Vm.LoadE911Command.Execute(null);
                return true;

            case 12:
                switch (_focused)
                {
                    case "callcode": await OpenList(_callCodeBox); return true;
                    case "origin": await OpenList(_originBox); return true;
                    case "disposition": await OpenList(_dispositionBox); return true;
                    case "state": await OpenList(_stateBox); return true;
                    case "jur": await OpenList(_jurBox); return true;
                    case "force": await OpenList(_forceBox); return true;
                    case "location":
                        Vm.ValidateLocation(saveAfter: false);
                        return true;
                }

                return false;
        }

        return false;
    }

    private static async Task OpenList<TData, TValue>(DxComboBox<TData, TValue>? box)
    {
        if (box == null) return;
        await box.FocusAsync();
        box.ShowDropDown();
    }

    /// <summary>
    /// Enter saves, except where Enter means something else: a new line in the
    /// narrative, choosing from an open list, pressing a button.
    /// </summary>
    private void OnPanelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Enter" || e.ShiftKey || e.CtrlKey || e.AltKey) return;
        if (_focused == null || _focused == "narrative" || _dropOpen) return;

        // Values are bound on input, so the view model already holds what is typed.
        Vm.DispatchCommand.Execute(null);
    }

    // ----------------------------------------------------------------- fields

    private static string Upper(string? text, int max = 0)
    {
        var value = (text ?? string.Empty).ToUpperInvariant();
        return max > 0 && value.Length > max ? value.Substring(0, max) : value;
    }

    private static string Limit(string? text, int max)
    {
        var value = text ?? string.Empty;
        return value.Length > max ? value.Substring(0, max) : value;
    }

    /// <summary>A labelled text field, bound as the WPF box was (UpdateSourceTrigger=PropertyChanged).</summary>
    private RenderFragment TextField(string field, string label, string value, Action<string> set, string? nullText, bool upper = false) => __builder =>
    {
        __builder.OpenElement(0, "div");
        __builder.AddAttribute(1, "class", "lw-tc-field");
        __builder.AddAttribute(2, "onfocusin", EventCallback.Factory.Create<FocusEventArgs>(this, () => Focused(field)));
        __builder.AddAttribute(3, "onfocusout", EventCallback.Factory.Create<FocusEventArgs>(this, () => Blurred(field)));
        __builder.OpenElement(4, "label");
        __builder.AddAttribute(5, "class", "lw-field-label");
        __builder.AddContent(6, label);
        __builder.CloseElement();
        __builder.OpenComponent<DxTextBox>(7);
        __builder.AddComponentParameter(8, nameof(DxTextBox.Text), value);
        __builder.AddComponentParameter(9, nameof(DxTextBox.TextChanged), EventCallback.Factory.Create<string>(this, v => set(v ?? string.Empty)));
        __builder.AddComponentParameter(10, nameof(DxTextBox.BindValueMode), BindValueMode.OnInput);
        __builder.AddComponentParameter(11, nameof(DxTextBox.NullText), nullText ?? string.Empty);
        __builder.AddComponentParameter(12, nameof(DxTextBox.CssClass), upper ? "lw-tc-edit lw-tc-upper" : "lw-tc-edit");
        __builder.CloseComponent();
        __builder.CloseElement();
    };

    /// <summary>One of the P / F / E / O priority boxes - editable where CAD7_CAD_P/F/E/O allows.</summary>
    private RenderFragment PriorityBox(string tag, string tip, string value, Action<string> set, bool allowed) => __builder =>
    {
        __builder.OpenElement(0, "div");
        __builder.AddAttribute(1, "class", "lw-tc-prio-box");
        __builder.AddAttribute(2, "title", tip);
        __builder.AddAttribute(3, "onfocusin", EventCallback.Factory.Create<FocusEventArgs>(this, () => Focused("prio" + tag)));
        __builder.AddAttribute(4, "onfocusout", EventCallback.Factory.Create<FocusEventArgs>(this, () => Blurred("prio" + tag)));
        __builder.OpenComponent<DxTextBox>(5);
        __builder.AddComponentParameter(6, nameof(DxTextBox.Text), value);
        __builder.AddComponentParameter(7, nameof(DxTextBox.TextChanged), EventCallback.Factory.Create<string>(this, v => set(v ?? string.Empty)));
        __builder.AddComponentParameter(8, nameof(DxTextBox.BindValueMode), BindValueMode.OnInput);
        __builder.AddComponentParameter(9, nameof(DxTextBox.NullText), tag);
        __builder.AddComponentParameter(10, nameof(DxTextBox.ReadOnly), !allowed);
        __builder.AddComponentParameter(11, nameof(DxTextBox.CssClass), "lw-tc-edit lw-tc-prio-edit");
        __builder.CloseComponent();
        __builder.CloseElement();
    };
}
