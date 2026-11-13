using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LwCadSystemUX.Services.Data;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Runs a legacy CAD program the way LwCadSystem's WinA.LaunchProgram does,
    /// for the functions this console has not rebuilt: an .exe found beside the
    /// console is started with the pipe-delimited link parameters, and a
    /// MicroFocus .int is started through LaRunw.exe.
    ///
    /// The programs are looked for in --legacy &lt;folder&gt; / LWCAD_LEGACY_DIR when
    /// given, then beside this console (where the LW apps are deployed together).
    /// </summary>
    public static class LegacyLauncher
    {
        public static string Folder { get; set; } = FromArgs();

        /// <summary>The .exe or .int for a program id, or null when it is not installed.</summary>
        public static string Find(string programId)
        {
            var id = KeymapLoader.NormalizeProgramId(programId);
            if (id.Length == 0) return null;

            foreach (var folder in new[] { Folder, AppContext.BaseDirectory }.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())
            {
                foreach (var ext in new[] { ".exe", ".int" })
                {
                    var path = Path.Combine(folder, id + ext);
                    if (File.Exists(path)) return path;

                    // Program ids are stored upper case; the files are "LwCad102.exe".
                    var match = SafeFiles(folder, "*" + ext).FirstOrDefault(f =>
                        string.Equals(Path.GetFileNameWithoutExtension(f), id, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
            }

            return null;
        }

        public static bool IsInstalled(string programId) => Find(programId) != null;

        /// <summary>True when the program was found and started; the message says what happened.</summary>
        public static bool Launch(string programId, LegacyContext context, out string message)
        {
            var path = Find(programId);
            if (path == null)
            {
                message = KeymapLoader.NormalizeProgramId(programId) + " is not installed beside this console.";
                return false;
            }

            context ??= new LegacyContext();

            // Web port: this process never starts a program itself. The host decides
            // what a launch means (CadHost.LaunchLegacy); a web server answers that the
            // program runs on the operator's desktop. With no host hook, nothing runs.
            if (CadHost.LaunchLegacy == null)
            {
                message = Path.GetFileName(path) + " cannot be started from this host.";
                return false;
            }

            try
            {
                var start = path.EndsWith(".int", StringComparison.OrdinalIgnoreCase)
                    ? MicroFocus(path, context)
                    : Executable(path, context);

                return CadHost.LaunchLegacy(start.FileName, start.Arguments, out message);
            }
            catch (Exception ex)
            {
                LaLog.Error("Launch", "Program load error - " + path, ex);
                message = "Program Load Error - " + Path.GetFileName(path) + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// LoadExecutableVersion: UserID|Top|Left|v|^EventType|EventNo1|EventNo2|
        /// DispNo1|DispNo2|TKey|SKey|CadMode|EmpId|SessId|UnitNo|RtnSw|Mon| - a blank
        /// event type goes as 'X' "to prevent unstring problems on the other end".
        /// </summary>
        public static string ExecutableArguments(LegacyContext c)
        {
            var session = CadSession.Current;
            var type = c.EventType == ' ' || c.EventType == '\0' ? 'X' : c.EventType;

            return session.EmpId + "|" + c.Top + "|" + c.Left + "|" + "v" + "|" + "^" +
                   type + "|" + c.EventNo1 + "|" + c.EventNo2 + "|" +
                   c.DispNo1 + "|" + c.DispNo2 + "|" +
                   c.TKey + "|" + c.SKey + "|" +
                   c.ProgramMode + "|" + session.EmpId + "|" + session.SessId + "|" +
                   (c.UnitNo ?? string.Empty) + "|" + c.RtnSw + "|" + c.Mon + "|";
        }

        private static ProcessStartInfo Executable(string path, LegacyContext c) =>
            new ProcessStartInfo(path, ExecutableArguments(c));

        /// <summary>
        /// RunMicroFocusVersion: LaRunw.exe with the menu link string - no call or
        /// unit context reaches a MicroFocus program, as in the legacy.
        /// </summary>
        private static ProcessStartInfo MicroFocus(string path, LegacyContext c)
        {
            var runner = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "LaRunw.exe");
            if (!File.Exists(runner)) runner = "LaRunw.exe";

            var args = CadSession.Current.EmpId + "|868|623|" + c.Top + "|" + c.Left + "|1|true|l|true|10|10|^X|X|" +
                       Path.GetFileName(path) + "|" + new string(' ', 200);

            return new ProcessStartInfo(runner, args.Replace(" ", "!"));
        }

        private static string[] SafeFiles(string folder, string pattern)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetFiles(folder, pattern) : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string FromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--legacy=", StringComparison.OrdinalIgnoreCase)) return args[i].Substring(9).Trim('"');
                if (string.Equals(args[i], "--legacy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) return args[i + 1].Trim('"');
            }

            return Environment.GetEnvironmentVariable("LWCAD_LEGACY_DIR") ?? string.Empty;
        }
    }

    /// <summary>What a legacy program is handed about the call and unit it opens on (WinA Globals).</summary>
    public sealed class LegacyContext
    {
        public char EventType { get; init; } = ' ';
        public short EventNo1 { get; init; }
        public int EventNo2 { get; init; }
        public short DispNo1 { get; init; }
        public int DispNo2 { get; init; }
        public long TKey { get; init; }
        public long SKey { get; init; }

        /// <summary>Gl_ProgramMode - "u" once CadMain has read the session.</summary>
        public string ProgramMode { get; init; } = "u";

        public string UnitNo { get; init; } = string.Empty;
        public char RtnSw { get; init; } = ' ';
        public char Mon { get; init; } = ' ';
        public int Top { get; init; }
        public int Left { get; init; }
    }
}
