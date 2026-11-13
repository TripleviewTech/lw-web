using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ConsoleKey = LwCadSystem.Web.Components.Cad.ConsoleKey;

namespace LwCadSystem.Web.Components.Panels;

/// <summary>
/// Activate Unit behaviour - UnitActivatePanel.xaml.cs: a typed unit pre-fills
/// from UNT1 as the operator leaves it; F3 or Enter (from a text field, not an
/// open list) activates.
/// </summary>
public partial class UnitActivatePanel : ComponentBase, IDisposable
{
    protected override void OnInitialized() => Host.RegisterPanelKeys("unitactivate", OnPanelKey);

    public void Dispose() => Host.UnregisterPanelKeys("unitactivate");

    private void UnitLeft(FocusEventArgs e)
    {
        if (Vm.IsOpen && Vm.IsUnitEditable) Vm.Prefill();
    }

    private Task<bool> OnPanelKey(ConsoleKey key)
    {
        if (!Vm.IsOpen || !key.IsF(3)) return Task.FromResult(false);
        Vm.SaveCommand.Execute(null);
        return Task.FromResult(true);
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Enter" || e.ShiftKey || e.CtrlKey || e.AltKey || _focus != "text") return;
        Vm.SaveCommand.Execute(null);
    }
}
