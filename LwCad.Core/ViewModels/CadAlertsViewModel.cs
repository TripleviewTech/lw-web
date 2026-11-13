using System;
using System.Collections.ObjectModel;
using System.Linq;
using LwCadSystemUX.Models;
using LwCadSystemUX.Services;

namespace LwCadSystemUX.ViewModels
{
    /// <summary>
    /// CAD Alerts - LwCad301 Call Alerts, with LwCad302 Add Call Alert folded in.
    ///
    /// The left list is every stacked or active call with an alert waiting
    /// (TabA_LoadCallList), ordered by priority and call type as the legacy orders
    /// it. The right side is the picked call and its alerts, pending first: tick
    /// the ones read and Acknowledge (TabA_Save), or type a message and Send it to
    /// every agency on the call (LwCad302 TabA_Save).
    ///
    /// Opened for one call - a key or the row menu with a call selected - that call
    /// is picked even when it has no alerts yet, so one can be added. A delayed call
    /// is refused, as LwCad301 refuses it.
    /// </summary>
    public class CadAlertsViewModel : ObservableObject
    {
        private readonly ShellViewModel _shell;
        private readonly ICadDataService _data;

        private bool _isOpen;
        private AlertCallRow _selectedRow;
        private string _newAlertText = string.Empty;
        private string _message = string.Empty;
        private bool _messageIsError;
        private bool _isBusy;

        public CadAlertsViewModel(ShellViewModel shell, ICadDataService data)
        {
            _shell = shell;
            _data = data;

            CloseCommand = new RelayCommand(Cancel);
            RefreshCommand = new RelayCommand(Reload);
            AcknowledgeCommand = new RelayCommand(Acknowledge);
            TickAllCommand = new RelayCommand(TickAll);
            SendCommand = new RelayCommand(Send);
        }

        public RelayCommand CloseCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AcknowledgeCommand { get; }
        public RelayCommand TickAllCommand { get; }
        public RelayCommand SendCommand { get; }

        public string Title => "CAD Alerts";

        public bool IsOpen
        {
            get => _isOpen;
            private set => Set(ref _isOpen, value);
        }

        /// <summary>Calls with alerts waiting, plus the call it was opened for.</summary>
        public ObservableCollection<AlertCallRow> Calls { get; } = new ObservableCollection<AlertCallRow>();

        /// <summary>The picked call's alerts, pending first.</summary>
        public ObservableCollection<CallAlert> Alerts { get; } = new ObservableCollection<CallAlert>();

        public AlertCallRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!Set(ref _selectedRow, value)) return;

                Raise(nameof(SelectedCall));
                Raise(nameof(HasSelectedCall));
                LoadAlerts();
            }
        }

        public CadCall SelectedCall => SelectedRow?.Call;

        public bool HasSelectedCall => SelectedCall != null;

        public bool HasCalls => Calls.Count > 0;

        public bool HasAlerts => Alerts.Count > 0;

        public bool HasPending => Alerts.Any(a => a.IsPending);

        public int TickedCount => Alerts.Count(a => a.IsChecked);

        public string AcknowledgeLabel => TickedCount > 0 ? "Acknowledge (" + TickedCount + ")" : "Acknowledge";

        public string EmptyListText => "No calls have alerts waiting.";

        public string NewAlertText
        {
            get => _newAlertText;
            set => Set(ref _newAlertText, value);
        }

        public string Message
        {
            get => _message;
            private set
            {
                if (Set(ref _message, value)) Raise(nameof(HasMessage));
            }
        }

        public bool HasMessage => !string.IsNullOrEmpty(Message);

        public bool MessageIsError
        {
            get => _messageIsError;
            private set => Set(ref _messageIsError, value);
        }

        /// <summary>
        /// Opens the screen. With a call, that call is picked - the legacy's single
        /// call mode, reached from a key or btn_Alert; without one it is the list,
        /// as the top bar's img_CadAlert opens it.
        /// </summary>
        public void Open(CadCall forCall) => Open(forCall, null);

        /// <summary>Opens, and runs <paramref name="onClosed"/> once the screen is closed.</summary>
        public void Open(CadCall forCall, Action onClosed)
        {
            _onClosed = onClosed;

            if (forCall != null && forCall.Queue == CallQueue.Delayed)
            {
                _shell.SetStatus("Alert function not supported by Delayed calls.");
                return;
            }

            NewAlertText = string.Empty;
            SetMessage(string.Empty, false);
            IsOpen = true;

            LoadCalls(forCall);
        }

        public void Cancel()
        {
            IsOpen = false;
            _shell.RefreshAlertBadge();

            var next = _onClosed;
            _onClosed = null;
            next?.Invoke();
        }

        private Action _onClosed;

        private void Reload()
        {
            SetMessage(string.Empty, false);
            LoadCalls(SelectedCall);
        }

        private void LoadCalls(CadCall keep)
        {
            System.Collections.Generic.IReadOnlyList<PendingAlerts> pending;

            try
            {
                pending = _data.GetPendingAlerts();
            }
            catch (Exception ex)
            {
                SetMessage("Alerts could not be read: " + ex.Message, true);
                pending = Array.Empty<PendingAlerts>();
            }

            var rows = pending
                .Select(p => new { p, call = _data.Calls.FirstOrDefault(p.Matches) })
                .Where(x => x.call != null)
                .Select(x => new AlertCallRow(x.call, x.p.Count, x.p.IsCadFusion))
                .OrderBy(r => r.Call.PriorityNumber)
                .ThenBy(r => r.Call.CallType, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // The call the screen was opened for stays on the list with nothing
            // pending, so an alert can be added to it.
            if (keep != null && keep.Queue != CallQueue.Delayed && rows.All(r => !ReferenceEquals(r.Call, keep)))
            {
                rows.Insert(0, new AlertCallRow(keep, 0, false));
            }

            Calls.Clear();
            foreach (var row in rows) Calls.Add(row);
            Raise(nameof(HasCalls));

            var pick = rows.FirstOrDefault(r => ReferenceEquals(r.Call, keep)) ?? rows.FirstOrDefault();

            // Re-picking the same row leaves the property unchanged, so load directly.
            if (ReferenceEquals(pick, _selectedRow)) LoadAlerts();
            else SelectedRow = pick;

            _shell.RefreshAlertBadge();
        }

        private void LoadAlerts()
        {
            foreach (var alert in Alerts) alert.PropertyChanged -= OnAlertChanged;
            Alerts.Clear();

            if (SelectedCall != null)
            {
                try
                {
                    foreach (var alert in _data.GetAlerts(SelectedCall))
                    {
                        alert.PropertyChanged += OnAlertChanged;
                        Alerts.Add(alert);
                    }
                }
                catch (Exception ex)
                {
                    SetMessage("Alerts for " + SelectedCall.EventNo + " could not be read: " + ex.Message, true);
                }
            }

            RaiseTicks();
            Raise(nameof(HasAlerts));
            Raise(nameof(HasPending));
        }

        private void OnAlertChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CallAlert.IsChecked)) RaiseTicks();
        }

        private void RaiseTicks()
        {
            Raise(nameof(TickedCount));
            Raise(nameof(AcknowledgeLabel));
        }

        private void TickAll()
        {
            var tick = Alerts.Any(a => a.IsPending && !a.IsChecked);
            foreach (var alert in Alerts.Where(a => a.IsPending)) alert.IsChecked = tick;
        }

        private void Acknowledge()
        {
            if (_isBusy || SelectedCall == null) return;

            var keys = Alerts.Where(a => a.IsChecked).Select(a => a.Key).ToList();
            if (keys.Count == 0)
            {
                SetMessage("Tick the alerts to acknowledge first.", true);
                return;
            }

            Write(() => _data.AcknowledgeAlerts(SelectedCall, keys), clearText: false);
        }

        private void Send()
        {
            if (_isBusy || SelectedCall == null) return;

            if (string.IsNullOrWhiteSpace(NewAlertText))
            {
                SetMessage("Enter alert message to send.", true);
                return;
            }

            Write(() => _data.AddAlert(SelectedCall, NewAlertText), clearText: true);
        }

        private void Write(Func<WriteReport> write, bool clearText)
        {
            _isBusy = true;
            try
            {
                var call = SelectedCall;
                WriteReport report;

                try
                {
                    report = write();
                }
                catch (Exception ex)
                {
                    report = new WriteReport { Failed = true, Message = ex.Message };
                }

                SetMessage(report.Message, report.Failed);
                _shell.SetStatus(report.Message);

                if (report.Failed) return;

                if (clearText) NewAlertText = string.Empty;

                var message = Message;
                LoadCalls(call);
                SetMessage(message, false);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void SetMessage(string message, bool isError)
        {
            MessageIsError = isError;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>One call on the CAD Alerts list.</summary>
    public sealed class AlertCallRow
    {
        public AlertCallRow(CadCall call, int pending, bool isCadFusion)
        {
            Call = call;
            Pending = pending;
            IsCadFusion = isCadFusion;
        }

        public CadCall Call { get; }

        /// <summary>Alerts waiting on the call.</summary>
        public int Pending { get; }

        public bool HasPending => Pending > 0;

        /// <summary>DSE1_FATPOT_ID set - the legacy list paints the row yellow.</summary>
        public bool IsCadFusion { get; }

        public string QueueText => Call.Queue.ToString();
    }
}
