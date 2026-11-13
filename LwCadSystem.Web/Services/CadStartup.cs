using LwCadSystemUX;
using LwCadSystemUX.Services;

namespace LwCadSystem.Web.Services;

/// <summary>
/// Start-up: turns the WPF command-line switches into configuration, and applies
/// the <c>CadSystem</c> section to the domain's process-wide settings (CadHost,
/// LaGetConfig, CadSession, LegacyLauncher) exactly as App.OnStartup did.
/// </summary>
public static class CadStartup
{
    /// <summary>The WPF switches. Each takes a value except --demo.</summary>
    private static readonly Dictionary<string, string> Switches = new(StringComparer.OrdinalIgnoreCase)
    {
        ["--cfg"] = nameof(CadSystemOptions.LaCfgPath),
        ["--emp"] = nameof(CadSystemOptions.EmpId),
        ["--sess"] = nameof(CadSystemOptions.SessId),
        ["--legacy"] = nameof(CadSystemOptions.LegacyFolder),
        ["--kbrd"] = nameof(CadSystemOptions.Keyboard),
        ["--optype"] = nameof(CadSystemOptions.OpType),
        ["--sec"] = nameof(CadSystemOptions.SecurityLevel)
    };

    /// <summary>
    /// Takes the WPF switches out of the argument list (ASP.NET's own command-line
    /// provider would read "--demo --emp 5" as demo = "--emp"), and returns them as
    /// CadSystem:* configuration values. The remaining arguments go to the host.
    /// </summary>
    public static string[] ExtractSwitches(string[] args, out Dictionary<string, string?> values)
    {
        values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var rest = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var name = arg;
            string? inline = null;
            var eq = arg.IndexOf('=');
            if (arg.StartsWith("--", StringComparison.Ordinal) && eq > 0)
            {
                name = arg.Substring(0, eq);
                inline = arg.Substring(eq + 1);
            }

            if (string.Equals(name, "--demo", StringComparison.OrdinalIgnoreCase))
            {
                values[CadSystemOptions.Section + ":" + nameof(CadSystemOptions.Demo)] = inline ?? "true";
                continue;
            }

            if (Switches.TryGetValue(name, out var key))
            {
                var value = inline ?? (i + 1 < args.Length ? args[++i] : null);
                if (value != null) values[CadSystemOptions.Section + ":" + key] = value.Trim('"');
                continue;
            }

            rest.Add(arg);
        }

        return rest.ToArray();
    }

    /// <summary>
    /// Applies the options to the domain. Called once, before the first circuit:
    /// these are process-wide in the domain, as they were per process in WPF.
    /// </summary>
    public static void Apply(CadSystemOptions options, ILogger logger)
    {
        CadHost.ForceDemoBoard = options.Demo;
        LaConfig.LaGetConfig.OverridePath = string.IsNullOrWhiteSpace(options.LaCfgPath) ? null : options.LaCfgPath.Trim();

        // CadSession.FromArgs is the WPF parser (it also falls back to the LWCAD_*
        // environment variables); feeding it the configured values keeps one rule.
        var sessionArgs = new List<string>();
        if (options.EmpId.HasValue) sessionArgs.AddRange(new[] { "--emp", options.EmpId.Value.ToString() });
        if (options.SessId.HasValue) sessionArgs.AddRange(new[] { "--sess", options.SessId.Value.ToString() });
        if (!string.IsNullOrWhiteSpace(options.Keyboard)) sessionArgs.AddRange(new[] { "--kbrd", options.Keyboard });
        if (!string.IsNullOrWhiteSpace(options.OpType)) sessionArgs.AddRange(new[] { "--optype", options.OpType });
        if (options.SecurityLevel.HasValue) sessionArgs.AddRange(new[] { "--sec", options.SecurityLevel.Value.ToString() });
        CadSession.Current = CadSession.FromArgs(sessionArgs.ToArray());

        if (!string.IsNullOrWhiteSpace(options.LegacyFolder)) LegacyLauncher.Folder = options.LegacyFolder.Trim();

        // A web server never starts a desktop program. A key whose program is
        // installed answers with where to run it; one that is not opens its notes.
        CadHost.LaunchLegacy = (string programPath, string arguments, out string message) =>
        {
            var program = Path.GetFileNameWithoutExtension(programPath);
            message = program + " runs on the desktop; open it from the Larimore desktop menu.";
            return false;
        };

        // Names the mode and the config path only - never the connection string.
        if (options.Demo)
        {
            logger.LogInformation("CAD System: demo mode is on - the demo board is used and nothing is written.");
        }
        else
        {
            logger.LogWarning("CAD System: live mode. La.cfg is looked for in {Paths}. With a La.cfg the console reads AND WRITES the CAD tables.",
                string.Join("; ", LaConfig.LaGetConfig.SearchedPaths()));
        }

        logger.LogInformation("CAD System: writes stamped as employee {Emp}, session {Sess}.", CadSession.Current.EmpId, CadSession.Current.SessId);
    }
}
