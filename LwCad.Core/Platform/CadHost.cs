using System;

namespace LwCadSystemUX
{
    /// <summary>
    /// What the WPF App class supplied to the domain: start-up switches, and the
    /// desktop service a browser has to provide differently (launching
    /// legacy programs on the workstation). The clipboard is per user, on ShellViewModel. The web host sets these at start-up.
    /// </summary>
    public static class CadHost
    {
        /// <summary>True to skip the database and run on the demo board (the WPF --demo switch).</summary>
        public static bool ForceDemoBoard { get; set; }

        /// <summary>Puts text on the user's clipboard. In the browser this goes through JS interop.</summary>
        public static Action<string> CopyToClipboard { get; set; } = _ => { };

        /// <summary>Starts a legacy CAD program on the workstation: (program path, link arguments, out message) -> started.</summary>
        public delegate bool LegacyLaunch(string programPath, string arguments, out string message);

        /// <summary>When set, replaces the desktop Process.Start in LegacyLauncher. A web host must set it.</summary>
        public static LegacyLaunch LaunchLegacy { get; set; }
    }
}
