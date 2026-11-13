using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LaConfig
{
    /// <summary>
    /// .NET 8 port of the La.Cfg reader from LaSubroutines (Missouri/LA/Apps/LaSubroutines/LaConfig.cs).
    ///
    /// Same file format and the same two value-parsing rules as the original:
    /// most keys read forward from the first '=', while the DATABASE "co" key
    /// reads backward from the end of the line.
    ///
    /// Deliberately different from the original in one respect: it does not query
    /// ENV1 while building the connection string. Resolving a connection string
    /// should not require a working connection; callers that need the ENV1
    /// settings read them through a table reader like any other row.
    /// </summary>
    public class LaGetConfig
    {
        public const string DefaultFileName = "La.cfg";

        /// <summary>Environment variable holding a full path to La.cfg.</summary>
        public const string PathVariable = "LWCAD_LA_CFG";

        /// <summary>
        /// Set from the --cfg command line switch. Wins over every other location
        /// except a rooted path passed straight to the constructor.
        /// </summary>
        public static string OverridePath { get; set; }

        /// <summary>
        /// A full SQL Server connection string that replaces the [DATABASE] section of La.cfg
        /// (set from the ConnectionStrings:LwCad setting, e.g. an Azure app setting or user
        /// secret). La.cfg is then optional: when one is found its other sections (agency,
        /// print title, defaults) are still read. Never logged.
        /// </summary>
        public static string ConnectionStringOverride { get; set; }

        /// <summary>True when <see cref="ConnectionStringOverride"/> is set.</summary>
        public static bool HasConnectionStringOverride => !string.IsNullOrWhiteSpace(ConnectionStringOverride);

        public LaGetConfig() : this(DefaultFileName)
        {
        }

        public LaGetConfig(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; set; }

        public string ServerName { get; private set; } = string.Empty;
        public string DatabaseName { get; private set; } = string.Empty;
        public string UserIdName { get; private set; } = string.Empty;
        public string AgencyId { get; private set; } = string.Empty;
        public string PrintTitle { get; private set; } = string.Empty;
        public int DbLibrary { get; private set; }
        public int ListMaxRows { get; private set; } = 20000;
        public int CenturyCutOff { get; private set; } = 20;

        public string ErrMsg { get; private set; } = string.Empty;

        private string _password = string.Empty;
        private string _connectString = string.Empty;

        public string ConnectString
        {
            get => _connectString;
            set => _connectString = value;
        }

        /// <summary>Reads La.cfg and returns the connection string, or empty if it could not be built.</summary>
        public string GetConnectString()
        {
            try
            {
                Read();
            }
            catch (Exception ex)
            {
                ErrMsg = ex.Message;
                return string.Empty;
            }

            return _connectString;
        }

        /// <summary>
        /// Every place La.cfg is looked for, in order: the --cfg switch, the
        /// LWCAD_LA_CFG variable, beside the executable, then the working directory.
        /// Deployed the way the other LW apps are deployed, the second-to-last wins.
        /// </summary>
        public static string[] SearchedPaths(string fileName = DefaultFileName)
        {
            var paths = new List<string>();

            if (!string.IsNullOrWhiteSpace(OverridePath)) paths.Add(OverridePath);

            var fromEnvironment = Environment.GetEnvironmentVariable(PathVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment)) paths.Add(fromEnvironment);

            paths.Add(Path.Combine(AppContext.BaseDirectory, fileName));
            paths.Add(Path.GetFullPath(fileName));

            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>First location that exists, or the last one tried so the error names something.</summary>
        public static string ResolveConfigPath(string fileName = DefaultFileName)
        {
            // A rooted name passed straight in is taken at face value.
            if (Path.IsPathRooted(fileName)) return fileName;

            var candidates = SearchedPaths(fileName);
            return candidates.FirstOrDefault(File.Exists) ?? candidates[candidates.Length - 1];
        }

        private void Read()
        {
            var path = ResolveConfigPath(FileName);
            var found = File.Exists(path);

            if (HasConnectionStringOverride)
            {
                // The database comes from the setting; La.cfg, if any, still supplies the rest.
                if (found) ReadFile(path);
                UseConnectionStringOverride();
                return;
            }

            if (!found)
            {
                ErrMsg = "Could not find " + path;
                return;
            }

            ReadFile(path);

            _connectString =
                "Server=" + ServerName +
                ";Database=" + DatabaseName +
                ";User ID=" + UserIdName +
                ";Password=" + _password +
                ";MultiSubnetFailover=True;Encrypt=True;TrustServerCertificate=True";
        }

        private void UseConnectionStringOverride()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionStringOverride.Trim());
            ServerName = builder.DataSource;
            DatabaseName = builder.InitialCatalog;
            UserIdName = builder.UserID;
            _password = string.Empty;   // kept only inside the connection string
            _connectString = builder.ConnectionString;
            ErrMsg = string.Empty;
        }

        private void ReadFile(string path)
        {
            var section = string.Empty;

            foreach (var raw in File.ReadLines(path))
            {
                // Parsed untrimmed: the original never trims values, so a trailing
                // space in La.cfg is part of the value it produces. Trimming here
                // would silently change the password this resolves to.
                var line = raw;
                if (line.Trim().Length == 0) continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                    trimmed.EndsWith("]", StringComparison.Ordinal))
                {
                    section = trimmed;
                    continue;
                }

                switch (section)
                {
                    case "[AGENCY]": ReadAgency(line); break;
                    case "[DATABASE]": ReadDatabase(line); break;
                    case "[System_Defaults]": ReadSystemDefaults(line); break;
                    case "[Imaging]": ReadImaging(line); break;
                }
            }
        }

        private void ReadAgency(string line)
        {
            if (StartsWith(line, "PrintTitle")) PrintTitle = Forward(line).Trim();
            else if (StartsWith(line, "AgencyId")) AgencyId = Forward(line).Trim();
        }

        private void ReadDatabase(string line)
        {
            if (StartsWith(line, "Server")) ServerName = Forward(line).Trim();
            else if (StartsWith(line, "Db")) DatabaseName = Forward(line).Trim();
            else if (StartsWith(line, "UserName")) UserIdName = Forward(line).Trim();
            else if (StartsWith(line, "co")) _password = Reversed(line);
            else if (StartsWith(line, "DBLibrary")) DbLibrary = ParseInt(Forward(line), DbLibrary);
            else if (StartsWith(line, "ListMaxRows")) ListMaxRows = ParseInt(Forward(line), ListMaxRows);
        }

        private void ReadSystemDefaults(string line)
        {
            if (StartsWith(line, "ListMaxRows")) ListMaxRows = ParseInt(Forward(line), ListMaxRows);
            else if (StartsWith(line, "CenturyCutOff")) CenturyCutOff = ParseInt(Forward(line), CenturyCutOff);
            else if (StartsWith(line, "AgencyId")) AgencyId = Forward(line).Trim();
        }

        private void ReadImaging(string line)
        {
            if (StartsWith(line, "PrintTitle")) PrintTitle = Forward(line).Trim();
        }

        // "UserName" must not match before "UserNameSomethingElse"; the original
        // used StartsWith on the trimmed line, so this keeps that behaviour.
        private static bool StartsWith(string line, string key) =>
            line.TrimStart().StartsWith(key, StringComparison.Ordinal);

        /// <summary>Everything after the first '='.</summary>
        private static string Forward(string line)
        {
            int i = line.IndexOf('=');
            return i < 0 ? string.Empty : line.Substring(i + 1);
        }

        /// <summary>
        /// Everything after the last '=', reversed. The original walks the line
        /// backwards building the string as it goes, which yields the value spelled
        /// backwards - the light obfuscation used for the database password.
        /// </summary>
        private static string Reversed(string line)
        {
            int i = line.LastIndexOf('=');
            if (i < 0) return string.Empty;

            var value = line.Substring(i + 1);
            var chars = value.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text.Trim(), out var value) ? value : fallback;
    }
}
