using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Simulate.Models;

namespace Simulate.ViewModels
{
    /// <summary>
    /// ViewModel quản lý viễn thám và chỉ báo trạng thái sức khỏe của bus CAN (Bảng 9: Bus Monitor Health).
    /// Hỗ trợ cơ chế đổi màu thông minh (Xám/Xanh/Vàng/Đỏ) và tính toán tải bus định kỳ mà không gây giật lag.
    /// </summary>
    public partial class BusHealthViewModel : ObservableObject, IDisposable
    {
        private readonly Func<bool> _isConnectedProvider;
        private readonly Func<bool> _isEngineRunningProvider;
        private readonly Func<uint> _baudrateProvider;
        private readonly Func<GatewayStatistics?> _statisticsProvider;
        private readonly Func<HardwareFailure?> _lastFailureProvider;
        private readonly Func<int> _warningCountProvider;

        private readonly Timer? _telemetryTimer;
        private readonly object _syncLock = new();

        private long _previousTotalFrames;
        private DateTime _previousSampleTime;
        private int _accumulatedErrors;
        private int _accumulatedWarnings;
        private bool _disposed;

        [ObservableProperty]
        private string _busLoadDisplay = "0.0%";

        [ObservableProperty]
        private string _busLoadColor = "#64748B";

        [ObservableProperty]
        private string _errorCountDisplay = "0";

        [ObservableProperty]
        private string _errorColor = "#64748B";

        [ObservableProperty]
        private string _lostCountDisplay = "0";

        [ObservableProperty]
        private string _lostColor = "#64748B";

        [ObservableProperty]
        private string _warningCountDisplay = "0";

        [ObservableProperty]
        private string _warningColor = "#64748B";

        [ObservableProperty]
        private string _healthStrokeColor = "#64748B";

        [ObservableProperty]
        private string _healthStatusText = "● Offline";

        [ObservableProperty]
        private string _busLoadToolTip = "Tải đường truyền CAN (% chiếm dụng băng thông).";

        [ObservableProperty]
        private string _errorToolTip = "Số lỗi truyền nhận hoặc sự cố phần cứng.";

        [ObservableProperty]
        private string _lostToolTip = "Số frame bị chặn (Block) hoặc rớt gói.";

        [ObservableProperty]
        private string _warningToolTip = "Số cảnh báo vận hành phát sinh trong phiên.";

        /// <summary>
        /// Khởi tạo mặc định cho UI binding hoặc mock testing.
        /// </summary>
        public BusHealthViewModel()
            : this(() => false, () => false, () => 500000, () => null, () => null, () => 0, startTimer: true)
        {
        }

        /// <summary>
        /// Khởi tạo với các provider dữ liệu thực tế từ hệ thống.
        /// </summary>
        public BusHealthViewModel(
            Func<bool> isConnectedProvider,
            Func<uint> baudrateProvider,
            Func<GatewayStatistics?> statisticsProvider,
            Func<HardwareFailure?> lastFailureProvider,
            Func<int> warningCountProvider,
            bool startTimer = true)
            : this(isConnectedProvider, () => isConnectedProvider(), baudrateProvider, statisticsProvider, lastFailureProvider, warningCountProvider, startTimer)
        {
        }

        /// <summary>
        /// Khởi tạo với các provider dữ liệu thực tế từ hệ thống, bao gồm trạng thái chạy của Engine.
        /// </summary>
        public BusHealthViewModel(
            Func<bool> isConnectedProvider,
            Func<bool> isEngineRunningProvider,
            Func<uint> baudrateProvider,
            Func<GatewayStatistics?> statisticsProvider,
            Func<HardwareFailure?> lastFailureProvider,
            Func<int> warningCountProvider,
            bool startTimer = true)
        {
            _isConnectedProvider = isConnectedProvider ?? throw new ArgumentNullException(nameof(isConnectedProvider));
            _isEngineRunningProvider = isEngineRunningProvider ?? throw new ArgumentNullException(nameof(isEngineRunningProvider));
            _baudrateProvider = baudrateProvider ?? throw new ArgumentNullException(nameof(baudrateProvider));
            _statisticsProvider = statisticsProvider ?? throw new ArgumentNullException(nameof(statisticsProvider));
            _lastFailureProvider = lastFailureProvider ?? throw new ArgumentNullException(nameof(lastFailureProvider));
            _warningCountProvider = warningCountProvider ?? throw new ArgumentNullException(nameof(warningCountProvider));

            _previousSampleTime = DateTime.UtcNow;

            if (startTimer)
            {
                _telemetryTimer = new Timer(OnTelemetryTick, null, 500, 500);
            }
        }

        /// <summary>
        /// Ghi nhận 1 lỗi phát sinh ngoại vi (ví dụ transmission error, bus-off).
        /// </summary>
        public void IncrementErrorCount()
        {
            Interlocked.Increment(ref _accumulatedErrors);
            UpdateTelemetry();
        }

        /// <summary>
        /// Xóa bộ đếm lỗi ngoại vi tích lũy.
        /// </summary>
        public void ClearErrors()
        {
            Interlocked.Exchange(ref _accumulatedErrors, 0);
            UpdateTelemetry();
        }

        /// <summary>
        /// Ghi nhận 1 cảnh báo phát sinh ngoại vi.
        /// </summary>
        public void IncrementWarningCount()
        {
            Interlocked.Increment(ref _accumulatedWarnings);
            UpdateTelemetry();
        }

        /// <summary>
        /// Xóa bộ đếm cảnh báo ngoại vi tích lũy.
        /// </summary>
        public void ClearWarnings()
        {
            Interlocked.Exchange(ref _accumulatedWarnings, 0);
            UpdateTelemetry();
        }

        private void OnTelemetryTick(object? state)
        {
            UpdateTelemetry();
        }

        /// <summary>
        /// Cập nhật các chỉ số viễn thám và màu sắc trạng thái dựa trên dữ liệu hiện tại.
        /// </summary>
        public void UpdateTelemetry()
        {
            bool isConnected = _isConnectedProvider();
            bool isEngineRunning = _isEngineRunningProvider();
            uint baudrate = _baudrateProvider();
            GatewayStatistics? stats = _statisticsProvider();
            HardwareFailure? lastFailure = _lastFailureProvider();
            int warnings = _warningCountProvider() + _accumulatedWarnings;

            DateTime now = DateTime.UtcNow;
            long currentTotalFrames = 0;
            long droppedFrames = 0;

            if (stats != null)
            {
                currentTotalFrames = stats.ReceivedFrames + stats.TransmittedFrames;
                droppedFrames = stats.DroppedFrames;
            }

            TimeSpan elapsed;
            long deltaFrames;

            lock (_syncLock)
            {
                elapsed = now - _previousSampleTime;
                if (elapsed < TimeSpan.FromMilliseconds(200))
                {
                    elapsed = TimeSpan.FromMilliseconds(500);
                }

                deltaFrames = currentTotalFrames - _previousTotalFrames;
                if (deltaFrames < 0) deltaFrames = 0;

                _previousTotalFrames = currentTotalFrames;
                _previousSampleTime = now;
            }

            int errors = _accumulatedErrors;
            if (lastFailure != null)
            {
                errors++;
            }

            var snapshot = ComputeTelemetrySnapshot(isConnected, isEngineRunning, baudrate, deltaFrames, elapsed, droppedFrames, errors, warnings);
            ApplySnapshot(snapshot);
        }

        /// <summary>
        /// Thuật toán thuần túy tính toán trạng thái sức khỏe và các chỉ số viễn thám (hỗ trợ tương thích ngược).
        /// </summary>
        public static TelemetrySnapshot ComputeTelemetrySnapshot(
            bool isConnected,
            uint baudrate,
            long deltaFrames,
            TimeSpan elapsed,
            long droppedFrames,
            int errors,
            int warnings)
            => ComputeTelemetrySnapshot(isConnected, isConnected, baudrate, deltaFrames, elapsed, droppedFrames, errors, warnings);

        /// <summary>
        /// Thuật toán thuần túy tính toán trạng thái sức khỏe và các chỉ số viễn thám đầy đủ.
        /// </summary>
        public static TelemetrySnapshot ComputeTelemetrySnapshot(
            bool isConnected,
            bool isEngineRunning,
            uint baudrate,
            long deltaFrames,
            TimeSpan elapsed,
            long droppedFrames,
            int errors,
            int warnings)
        {
            if (!isConnected)
            {
                return new TelemetrySnapshot(
                    BusLoadDisplay: "0.0%",
                    BusLoadColor: "#64748B",
                    ErrorCountDisplay: errors.ToString(CultureInfo.InvariantCulture),
                    ErrorColor: errors > 0 ? "#EF4444" : "#64748B",
                    LostCountDisplay: droppedFrames.ToString(CultureInfo.InvariantCulture),
                    LostColor: droppedFrames > 0 ? "#EF4444" : "#64748B",
                    WarningCountDisplay: warnings.ToString(CultureInfo.InvariantCulture),
                    WarningColor: warnings > 0 ? "#F59E0B" : "#64748B",
                    HealthStrokeColor: errors > 0 ? "#EF4444" : (warnings > 0 ? "#F59E0B" : "#64748B"),
                    HealthStatusText: errors > 0 ? "● Error" : "● Offline",
                    StatusLevel: errors > 0 ? HealthStatusLevel.Critical : HealthStatusLevel.Offline);
            }

            if (!isEngineRunning)
            {
                return new TelemetrySnapshot(
                    BusLoadDisplay: "0.0%",
                    BusLoadColor: "#64748B",
                    ErrorCountDisplay: errors.ToString(CultureInfo.InvariantCulture),
                    ErrorColor: errors > 0 ? "#EF4444" : "#64748B",
                    LostCountDisplay: droppedFrames.ToString(CultureInfo.InvariantCulture),
                    LostColor: droppedFrames > 0 ? "#EF4444" : "#64748B",
                    WarningCountDisplay: warnings.ToString(CultureInfo.InvariantCulture),
                    WarningColor: warnings > 0 ? "#F59E0B" : "#64748B",
                    HealthStrokeColor: errors > 0 ? "#EF4444" : (warnings > 0 ? "#F59E0B" : "#64748B"),
                    HealthStatusText: errors > 0 ? "● Faulted" : "● Standby",
                    StatusLevel: errors > 0 ? HealthStatusLevel.Critical : HealthStatusLevel.Offline);
            }

            // Tính toán Bus Load (%) với sàn thời gian lấy mẫu an toàn tối thiểu 0.2s
            double busLoad = 0.0;
            double seconds = Math.Max(elapsed.TotalSeconds, 0.2);
            if (baudrate > 0 && deltaFrames > 0)
            {
                // Trung bình 1 frame CAN gồm khoảng 120 bit (header, payload, CRC, ACK, bit-stuffing)
                const double bitsPerFrame = 120.0;
                double totalBitsInPeriod = deltaFrames * bitsPerFrame;
                double maxBitsPossible = baudrate * seconds;
                busLoad = (totalBitsInPeriod / maxBitsPossible) * 100.0;
                if (busLoad > 100.0) busLoad = 100.0;
                if (busLoad < 0.0) busLoad = 0.0;
            }

            // Xác định phân cấp trạng thái sức khỏe
            HealthStatusLevel level;
            if (errors > 0 || droppedFrames > 0 || busLoad > 80.0)
            {
                level = HealthStatusLevel.Critical;
            }
            else if (busLoad >= 60.0 || warnings > 0)
            {
                level = HealthStatusLevel.Warning;
            }
            else
            {
                level = HealthStatusLevel.Optimal;
            }

            // Ánh xạ màu sắc
            string strokeColor = level switch
            {
                HealthStatusLevel.Optimal => "#10B981",
                HealthStatusLevel.Warning => "#F59E0B",
                HealthStatusLevel.Critical => "#EF4444",
                _ => "#64748B"
            };

            string statusText = level switch
            {
                HealthStatusLevel.Optimal => "● Optimal",
                HealthStatusLevel.Warning => "● Warning",
                HealthStatusLevel.Critical => "● Critical",
                _ => "● Offline"
            };

            string loadColor = busLoad switch
            {
                > 80.0 => "#EF4444",
                >= 60.0 => "#F59E0B",
                _ => "#10B981"
            };

            string errorColor = errors > 0 ? "#EF4444" : "#F8FAFC";
            string lostColor = droppedFrames > 0 ? "#EF4444" : "#F8FAFC";
            string warningColor = warnings > 0 ? "#F59E0B" : "#F8FAFC";

            return new TelemetrySnapshot(
                BusLoadDisplay: $"{busLoad:F1}%",
                BusLoadColor: loadColor,
                ErrorCountDisplay: errors.ToString(CultureInfo.InvariantCulture),
                ErrorColor: errorColor,
                LostCountDisplay: droppedFrames.ToString(CultureInfo.InvariantCulture),
                LostColor: lostColor,
                WarningCountDisplay: warnings.ToString(CultureInfo.InvariantCulture),
                WarningColor: warningColor,
                HealthStrokeColor: strokeColor,
                HealthStatusText: statusText,
                StatusLevel: level);
        }

        private void ApplySnapshot(TelemetrySnapshot snapshot)
        {
            BusLoadDisplay = snapshot.BusLoadDisplay;
            BusLoadColor = snapshot.BusLoadColor;
            ErrorCountDisplay = snapshot.ErrorCountDisplay;
            ErrorColor = snapshot.ErrorColor;
            LostCountDisplay = snapshot.LostCountDisplay;
            LostColor = snapshot.LostColor;
            WarningCountDisplay = snapshot.WarningCountDisplay;
            WarningColor = snapshot.WarningColor;
            HealthStrokeColor = snapshot.HealthStrokeColor;
            HealthStatusText = snapshot.HealthStatusText;
        }

        /// <summary>
        /// Hoàn nguyên tất cả chỉ số về 0 và chuyển sang trạng thái Offline.
        /// </summary>
        public void Reset()
        {
            lock (_syncLock)
            {
                _previousTotalFrames = 0;
                _previousSampleTime = DateTime.UtcNow;
                _accumulatedErrors = 0;
                _accumulatedWarnings = 0;
            }

            var offlineSnapshot = ComputeTelemetrySnapshot(false, 500000, 0, TimeSpan.FromSeconds(1), 0, 0, 0);
            ApplySnapshot(offlineSnapshot);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _telemetryTimer?.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Mức độ sức khỏe của mạng CAN.
    /// </summary>
    public enum HealthStatusLevel
    {
        Offline,
        Optimal,
        Warning,
        Critical
    }

    /// <summary>
    /// Bản ghi bất biến lưu trữ kết quả tính toán viễn thám tại một thời điểm.
    /// </summary>
    public record TelemetrySnapshot(
        string BusLoadDisplay,
        string BusLoadColor,
        string ErrorCountDisplay,
        string ErrorColor,
        string LostCountDisplay,
        string LostColor,
        string WarningCountDisplay,
        string WarningColor,
        string HealthStrokeColor,
        string HealthStatusText,
        HealthStatusLevel StatusLevel);
}
