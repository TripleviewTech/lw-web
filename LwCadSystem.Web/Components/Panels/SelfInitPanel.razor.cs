using DevExpress.Blazor;
using LwCadSystem.Web.Components.Cad;
using ConsoleKey = LwCadSystem.Web.Components.Cad.ConsoleKey;
using LwCadSystemUX.Models;
using LwCadSystemUX.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LwCadSystem.Web.Components.Panels;

/// <summary>
/// Self Initiate / Traffic Stop behaviour - SelfInitPanel.xaml.cs: the unit and
/// the location are checked as the operator leaves them (not on the way to
/// Cancel), and the legacy SelectFunction keys - F3 / Enter save, F5 cancel, F12
/// opens the focused list, Shift+F1-F4 the call code, origin, disposition and
/// plate state lists.
/// </summary>
public partial class SelfInitPanel : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public SelfInitViewModel Vm { get; set; } = default!;
    [Parameter, EditorRequired] public ShellViewModel Shell { get; set; } = default!;
    [CascadingParameter] public CadShell Host { get; set; } = default!;

    private const string Check = "M 4,12 L 9,17 L 20,6";

    private static readonly Dictionary<string, object> CancelAttributes = new() { ["style"] = "width:130px" };
    private static readonly Dictionary<string, object> SaveAttributes = new() { ["style"] = "width:150px", ["title"] = "F3 or Enter" };

    private DxComboBox<CallCodeItem, CallCodeItem>? _callCode;
    private DxComboBox<LookupItem, LookupItem>? _origin;
    private DxComboBox<LookupItem, LookupItem>? _disposition;
    private DxComboBox<string, string>? _tsState;
    private DxComboBox<string, string>? _siState;

    /// <summary>What kind of field has the caret: text, location, memo or combo.</summary>
    private string _focus = string.Empty;

    /// <summary>The mouse went down on Cancel: the field losing focus is not checked.</summary>
    private bool _cancelling;

    protected override void OnInitialized()
    {
        Host.RegisterPanelKeys("selfinit", OnPanelKey);
    }

    public void Dispose() => Host.UnregisterPanelKeys("selfinit");

    private void Focus(string kind) => _focus = kind;

    private static string Upper(string? text) => (text ?? string.Empty).ToUpperInvariant();

    // ------------------------------------------------------- field checks

    private void UnitLeft(FocusEventArgs e)
    {
        if (_cancelling || !Vm.IsOpen || string.IsNullOrWhiteSpace(Vm.UnitNo)) return;
        Vm.ValidateUnit();
    }

    private void LocationLeft(FocusEventArgs e)
    {
        if (_cancelling || !Vm.IsOpen || string.IsNullOrWhiteSpace(Vm.Location)) return;
        Vm.ValidateLocation(saveAfter: false);
    }

    private void CancelClicked()
    {
        _cancelling = false;
        Vm.CancelCommand.Execute(null);
    }

    // --------------------------------------------------------------- keys

    /// <summary>The F-keys, handed over by CadShell while Self Initiate is the top panel.</summary>
    private async Task<bool> OnPanelKey(ConsoleKey key)
    {
        if (!Vm.IsOpen || key.Kind != "fkey") return false;

        if (key.Shift && !key.Ctrl)
        {
            switch (key.Number)
            {
                case 1: await Drop(_callCode); return true;
                case 2: await Drop(_origin); return true;
                case 3: await Drop(_disposition); return true;
                case 4: await Drop(Vm.IsTrafficStop ? _tsState : _siState); return true;
            }

            return false;
        }

        if (key.Shift || key.Ctrl) return false;

        switch (key.Number)
        {
            case 3:
                Vm.SaveCommand.Execute(null);
                return true;

            case 5:
                await Vm.CancelAsync();
                return true;

            case 12:
                // F12 opens the focused list; on Location it runs the lookup.
                if (_focus == "location")
                {
                    Vm.ValidateLocation(saveAfter: false);
                    return true;
                }

                return false;
        }

        return false;
    }

    private static async Task Drop<TData, TValue>(DxComboBox<TData, TValue>? combo)
    {
        if (combo == null) return;
        await combo.FocusAsync();
        combo.ShowDropDown();
    }

    /// <summary>
    /// Enter saves from a single-line field. It adds a line in the narrative, and
    /// is left to a list (where it picks the item) - the WPF code skipped only an
    /// open drop-down; here every list keeps its Enter.
    /// </summary>
    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Enter" || e.ShiftKey || e.CtrlKey || e.AltKey) return;
        if (_focus != "text" && _focus != "location") return;
        Vm.SaveCommand.Execute(null);
    }
}
