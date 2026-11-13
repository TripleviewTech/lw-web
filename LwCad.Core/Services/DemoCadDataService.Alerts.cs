using System;
using System.Collections.Generic;
using System.Linq;
using LwCadSystemUX.Models;

namespace LwCadSystemUX.Services
{
    // CAD Alerts on the demo board - kept in memory, written nowhere.
    public partial class DemoCadDataService
    {
        private readonly Dictionary<long, List<CallAlert>> _alerts = new Dictionary<long, List<CallAlert>>();
        private long _alertSequence = 1;

        public int PendingAlertCallCount =>
            _alerts.Count(a => a.Value.Any(x => x.IsPending) && IsLive(a.Key));

        /// <summary>Two stacked calls and the active one carry alerts, one of them from CADfusion.</summary>
        private void SeedAlerts()
        {
            var now = DateTime.Now;

            Seed(Calls.FirstOrDefault(c => c.CallType == "Burglary"),
                 (now.AddMinutes(-20), "Caller advises suspect is armed with a knife - last seen rear yard.", true),
                 (now.AddMinutes(-18), "Second caller: white van parked on Cedar Ln. with engine running.", true));

            Seed(Calls.FirstOrDefault(c => c.CallType == "Fire Alarm" && c.Queue == CallQueue.Stacked),
                 (now.AddMinutes(-15), "Call received from CADfusion - Mutual aid request from neighbouring district.", true));

            Seed(Calls.FirstOrDefault(c => c.Queue == CallQueue.Active),
                 (now.AddMinutes(-6), "Patient history: cardiac, on blood thinners.", false),
                 (now.AddMinutes(-4), "Dog on premises - caller will secure it.", true));
        }

        private void Seed(CadCall call, params (DateTime At, string Remarks, bool Pending)[] alerts)
        {
            if (call == null) return;

            _alerts[call.EventKey] = alerts.Select(a => new CallAlert
            {
                Key = _alertSequence++,
                IsPending = a.Pending,
                EnteredAt = a.At,
                EnteredBy = 443221,
                Remarks = a.Remarks,
                AcknowledgedAt = a.Pending ? (DateTime?)null : a.At.AddMinutes(1),
                AcknowledgedBy = a.Pending ? 0 : 443221
            }).ToList();
        }

        public IReadOnlyList<PendingAlerts> GetPendingAlerts() =>
            _alerts
                .Where(a => a.Value.Any(x => x.IsPending) && IsLive(a.Key))
                .Select(a =>
                {
                    var call = Calls.First(c => c.EventKey == a.Key);
                    return new PendingAlerts
                    {
                        StackKey = a.Key,
                        EventType = call.EventType,
                        EventNo1 = call.EventNo1,
                        EventNo2 = call.EventNo2,
                        Count = a.Value.Count(x => x.IsPending),
                        IsCadFusion = a.Value.Any(x => x.IsFromCadFusion)
                    };
                })
                .ToList();

        public IReadOnlyList<CallAlert> GetAlerts(CadCall call)
        {
            if (call == null || !_alerts.TryGetValue(call.EventKey, out var list)) return Array.Empty<CallAlert>();

            // Fresh instances, so the panel's ticks never leak back into the store.
            return list.OrderBy(a => a.IsPending ? 0 : 1).ThenBy(a => a.Key).Select(Copy).ToList();
        }

        public WriteReport AcknowledgeAlerts(CadCall call, IReadOnlyCollection<long> alertKeys)
        {
            if (call == null || alertKeys == null || alertKeys.Count == 0)
            {
                return new WriteReport { Failed = true, Message = "Tick the alerts to acknowledge first." };
            }

            if (!_alerts.TryGetValue(call.EventKey, out var list)) list = new List<CallAlert>();

            var count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!alertKeys.Contains(list[i].Key) || !list[i].IsPending) continue;

                list[i] = Copy(list[i], acknowledged: true);
                count++;
            }

            return WriteReport.BoardOnly(count + " alert" + (count == 1 ? string.Empty : "s") +
                                         " acknowledged on " + call.EventNo + ".");
        }

        public WriteReport AddAlert(CadCall call, string remarks)
        {
            remarks = (remarks ?? string.Empty).Trim();
            if (call == null) return new WriteReport { Failed = true, Message = "No call was given." };
            if (remarks.Length == 0) return new WriteReport { Failed = true, Message = "Enter alert message to send." };
            if (call.Queue == CallQueue.Delayed)
            {
                return new WriteReport { Failed = true, Message = "Alert function not supported by Delayed calls." };
            }

            if (!_alerts.TryGetValue(call.EventKey, out var list)) _alerts[call.EventKey] = list = new List<CallAlert>();

            list.Add(new CallAlert
            {
                Key = _alertSequence++,
                IsPending = true,
                EnteredAt = DateTime.Now,
                EnteredBy = int.TryParse(OperatorId, out var id) ? id : 0,
                Remarks = remarks
            });

            return WriteReport.BoardOnly("Alert sent to " + call.EventNo + ".");
        }

        private bool IsLive(long stackKey) =>
            Calls.Any(c => c.EventKey == stackKey && c.Queue != CallQueue.Delayed);

        private CallAlert Copy(CallAlert a) => Copy(a, acknowledged: false);

        private CallAlert Copy(CallAlert a, bool acknowledged) => new CallAlert
        {
            Key = a.Key,
            IsPending = a.IsPending && !acknowledged,
            EnteredAt = a.EnteredAt,
            EnteredBy = a.EnteredBy,
            Remarks = a.Remarks,
            AcknowledgedAt = acknowledged ? DateTime.Now : a.AcknowledgedAt,
            AcknowledgedBy = acknowledged ? (int.TryParse(OperatorId, out var id) ? id : 0) : a.AcknowledgedBy
        };
    }
}
