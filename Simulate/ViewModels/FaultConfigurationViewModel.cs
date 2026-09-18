using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Simulate.ViewModels
{
    public partial class FaultConfigurationViewModel : ObservableObject
    {
        private readonly ObservableCollection<FaultQueueModel> _faultQueue;

        [ObservableProperty]
        private SignalModel? _targetSignal;

        [ObservableProperty]
        private string _selectedFaultType = "Signal Override";

        [ObservableProperty]
        private string _selectedInjectionMode = "Cyclic";

        [ObservableProperty]
        private string _cycleTimeText = "100 ms";

        [ObservableProperty]
        private string _startDelayText = "0 ms";

        [ObservableProperty]
        private string _repeatCountText = "0";

        [ObservableProperty]
        private string _durationText = "10 s";

        [ObservableProperty]
        private string _stopTimeText = "0 s";

        [ObservableProperty]
        private string _faultValueText = "0x0000";

        [ObservableProperty]
        private bool _isOverrideExisting = true;

        [ObservableProperty]
        private bool _isRestoreAfterStop = true;

        public ObservableCollection<string> AvailableFaultTypes { get; } = new()
        {
            "Signal Override",
            "Stuck at Value",
            "Bit Flip",
            "Offset",
            "Noise",
            "Ramp"
        };

        public ObservableCollection<string> AvailableInjectionModes { get; } = new()
        {
            "Cyclic",
            "One-Shot",
            "Event",
            "Sequence"
        };

        public string SelectedSignalDisplay => TargetSignal is not null
            ? $"Selected: {TargetSignal.MessageName}.{TargetSignal.Name}"
            : "Selected: (No signal selected)";

        public bool HasSelectedSignal => TargetSignal is not null;

        public Visibility AddToQueueVisibility => SelectedInjectionMode == "Sequence"
            ? Visibility.Visible
            : Visibility.Collapsed;

        public bool IsCycleEnabled => SelectedInjectionMode is "Cyclic" or "Sequence";
        public bool IsRepeatEnabled => SelectedInjectionMode is "Cyclic" or "Sequence";
        public bool IsDurationEnabled => SelectedInjectionMode is "Cyclic" or "Sequence";
        public bool IsDelayEnabled => SelectedInjectionMode is "Cyclic" or "One-Shot" or "Sequence";

        public string FaultTypeGuideText => GetFaultTypeGuide(SelectedFaultType);

        public string ModeGuideText => GetModeGuide(SelectedInjectionMode);

        public string CombinedGuideText => $"{FaultTypeGuideText}\n{ModeGuideText}";

        public FaultConfigurationViewModel(ObservableCollection<FaultQueueModel> faultQueue)
        {
            _faultQueue = faultQueue ?? throw new ArgumentNullException(nameof(faultQueue));
        }

        public void SetTargetSignal(SignalModel? signal)
        {
            TargetSignal = signal;
            if (signal is not null)
            {
                FaultValueText = signal.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            OnPropertyChanged(nameof(SelectedSignalDisplay));
            OnPropertyChanged(nameof(HasSelectedSignal));
            AddToQueueCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedFaultTypeChanged(string value)
        {
            OnPropertyChanged(nameof(FaultTypeGuideText));
            OnPropertyChanged(nameof(CombinedGuideText));
        }

        partial void OnSelectedInjectionModeChanged(string value)
        {
            OnPropertyChanged(nameof(AddToQueueVisibility));
            OnPropertyChanged(nameof(ModeGuideText));
            OnPropertyChanged(nameof(CombinedGuideText));
            OnPropertyChanged(nameof(IsCycleEnabled));
            OnPropertyChanged(nameof(IsRepeatEnabled));
            OnPropertyChanged(nameof(IsDurationEnabled));
            OnPropertyChanged(nameof(IsDelayEnabled));
        }

        private static string GetFaultTypeGuide(string faultType) => faultType switch
        {
            "Signal Override" => "Signal Override: Replaces live signal data with the overridden value configured in Panel 6.",
            "Stuck at Value" => "Stuck at Value: Freezes signal at current/fixed state (simulates sensor freeze or ground/power short).",
            "Bit Flip" => "Bit Flip: Inverts payload bit states (simulates bus noise or ECU memory corruption).",
            "Offset" => "Offset: Applies a fixed delta addition/subtraction to live incoming values.",
            "Noise" => "Noise: Adds pseudo-random fluctuation to signal values.",
            "Ramp" => "Ramp: Gradually increments/decrements signal value over configured duration.",
            _ => "Configured fault injection mechanism."
        };

        private static string GetModeGuide(string mode) => mode switch
        {
            "Cyclic" => "Cyclic: Repeated transmission at configured Cycle interval. Continuous until stopped.",
            "One-Shot" => "One-Shot: Single transmission after configured Delay. Does not repeat.",
            "Event" => "Event: Transmitted immediately upon trigger or value change.",
            "Sequence" => "Sequence: Step added to execution queue. Transmitted sequentially according to queue order.",
            _ => "Injection is configured but does not transmit until started."
        };

        private bool CanAddToQueue() => HasSelectedSignal;

        [RelayCommand(CanExecute = nameof(CanAddToQueue))]
        public void AddToQueue()
        {
            if (TargetSignal is null)
            {
                return;
            }

            var item = new FaultQueueModel
            {
                Index = _faultQueue.Count + 1,
                MsgId = TargetSignal.MessageId,
                MsgName = TargetSignal.MessageName,
                Signal = TargetSignal.Name,
                FaultType = SelectedFaultType,
                FaultValue = FaultValueText,
                StartTime = StartDelayText,
                Duration = DurationText,
                Repeat = RepeatCountText,
                Mode = SelectedInjectionMode,
                Status = "Queued",
                StatusColor = "#38BDF8",
                StatusBg = "#0C4A6E"
            };

            _faultQueue.Add(item);
        }
    }
}
