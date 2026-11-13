using System;
using LwCadSystemUX.Services.Data;
using LaConfig;

namespace LwCadSystemUX.Services
{
    /// <summary>
    /// Chooses the back end at start-up: the live CAD tables when La.cfg resolves
    /// and the database answers, otherwise the in-memory demo board so the shell
    /// still runs on a machine with no database.
    /// </summary>
    public static class CadDataServiceFactory
    {
        /// <summary>Seconds to wait for the probe before falling back.</summary>
        public const int ProbeTimeoutSeconds = 5;

        public static ICadDataService Create(out string status)
        {
            if (CadHost.ForceDemoBoard)
            {
                status = "Demo mode is on (CadHost:Demo) - running on the demo board.";
                return new DemoCadDataService();
            }

            var config = new LaGetConfig();
            var connectString = config.GetConnectString();

            if (string.IsNullOrWhiteSpace(config.ServerName) || string.IsNullOrWhiteSpace(config.DatabaseName))
            {
                status = "No La.cfg found (looked in " +
                         string.Join("; ", LaGetConfig.SearchedPaths()) +
                         ") - running on the demo board.";
                return new DemoCadDataService();
            }

            // Keep the probe short so a missing server does not hold up the shell.
            var probeString = connectString + ";Connect Timeout=" + ProbeTimeoutSeconds;

            try
            {
                var service = new SqlCadDataService(probeString);
                service.Refresh();

                if (!string.IsNullOrEmpty(service.LastError))
                {
                    status = "CAD database unavailable (" + service.LastError + ") - running on the demo board.";
                    return new DemoCadDataService();
                }

                // Say plainly that this console writes: Take Call, Assign and the unit
                // functions all commit to the CAD tables on the live back end.
                status = "Connected to " + config.DatabaseName + " on " + config.ServerName +
                         ". Changes are written to the CAD tables as employee " + CadSession.Current.EmpId +
                         ", session " + CadSession.Current.SessId + ".";
                return service;
            }
            catch (Exception ex)
            {
                LaLog.Error("startup", "Could not open the CAD database.", ex);
                status = "CAD database unavailable (" + ex.Message + ") - running on the demo board.";
                return new DemoCadDataService();
            }
        }
    }
}
