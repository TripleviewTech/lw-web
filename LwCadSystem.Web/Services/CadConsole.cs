using LwCadSystemUX.Services;
using LwCadSystemUX.ViewModels;

namespace LwCadSystem.Web.Services;

/// <summary>
/// One CAD console per user circuit (a scoped service): the data service the
/// factory chose and the ShellViewModel over it - what ShellWindow's constructor
/// built in WPF. Everything on it is touched only on the circuit's render context.
/// </summary>
public sealed class CadConsole : IDisposable
{
    public CadConsole()
    {
        // Live CAD tables when La.cfg resolves and the database answers, otherwise
        // the demo board. The status line says which is in use.
        var data = CadDataServiceFactory.Create(out var status);
        DataStatus = status;
        Shell = new ShellViewModel(data);
        Shell.SetStatus(string.IsNullOrEmpty(Shell.KeymapStatus) ? status : status + " " + Shell.KeymapStatus);
    }

    public ShellViewModel Shell { get; }

    /// <summary>What the factory said at start-up (database and server names only, never credentials).</summary>
    public string DataStatus { get; }

    /// <summary>
    /// The circuit has ended: the tab was closed, or the connection dropped and was not
    /// resumed within the retention period (10 minutes, Program.cs). A call left open in
    /// Take Call has its STK2 map record and EMG2 rows removed, as closing the WPF
    /// console did. The scoped container calls this once.
    /// </summary>
    public void Dispose() => Shell.TakeCall.AbandonOnDisconnect();
}
