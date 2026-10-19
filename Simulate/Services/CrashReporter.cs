using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Simulate.Services
{
    /// <summary>
    /// Bo giam sat va bao cao su co toan dien (Crash Sentinel & Reporter).
    /// Tu dong bat moi ngoai le chua duoc xu ly (Unhandled Exceptions), ghi file bao cao chi tiet
    /// vao thu muc CrashReports ngay trong thu muc debug chua file .exe.
    /// </summary>
    public static class CrashReporter
    {
        private static readonly object SyncLock = new();
        private static bool _isInitialized;

        /// <summary>
        /// Lay thu muc chua cac file bao cao crash (nam cung thu muc voi file .exe).
        /// </summary>
        public static string CrashReportDirectory
        {
            get
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string crashDir = Path.Combine(baseDir, "CrashReports");
                if (!Directory.Exists(crashDir))
                {
                    Directory.CreateDirectory(crashDir);
                }
                return crashDir;
            }
        }

        /// <summary>
        /// Khoi tao he thong giam sat crash tren tat ca cac kenh cua ung dung.
        /// </summary>
        /// <param name="app">Doi tuong WPF Application (neu co).</param>
        public static void Initialize(Application? app = null)
        {
            lock (SyncLock)
            {
                if (_isInitialized)
                {
                    return;
                }

                // 1. Bat ngoai le tren moi Background Thread / ThreadPool / Native Callback
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

                // 2. Bat ngoai le tren cac Task khong duoc await (Unobserved Task Exceptions)
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // 3. Bat ngoai le tren UI Dispatcher Thread
                if (app != null)
                {
                    app.DispatcherUnhandledException += OnDispatcherUnhandledException;
                }
                else if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
                }

                _isInitialized = true;
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string reportPath = WriteCrashReport(e.Exception, "UI Dispatcher Unhandled Exception", isTerminating: false);

            try
            {
                string msg = string.Format(
                    CultureInfo.InvariantCulture,
                    "Ung dung gap su co bat ngo tren giao dien!\n\nLoai loi: {0}\nThong bao: {1}\n\nChi tiet loi da duoc tu dong ghi lai vao file report:\n{2}\n\nBan co muon tiep tuc chay ung dung khong?",
                    e.Exception.GetType().Name,
                    e.Exception.Message,
                    reportPath);

                MessageBoxResult result = MessageBox.Show(
                    msg,
                    "Simulate - Su Co He Thong (Crash Prevented)",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    // Ngan khong cho ung dung bi vang
                    e.Handled = true;
                    return;
                }
            }
            catch
            {
                // Neu khong the hien thi MessageBox (do UI bi hong), de mac dinh ung dung dong an toan
            }
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception ?? new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unhandled non-exception object: {0}", e.ExceptionObject));
            WriteCrashReport(ex, "AppDomain Unhandled Exception (Background Thread / Worker)", isTerminating: e.IsTerminating);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashReport(e.Exception, "TaskScheduler Unobserved Task Exception", isTerminating: false);
            // Danh dau da quan sat de runtime khong kill process
            e.SetObserved();
        }

        /// <summary>
        /// Ghi file bao cao su co chi tiet xuong o dia (dong bo hoan toan de chong mat du lieu khi crash).
        /// </summary>
        /// <param name="exception">Ngoai le xay ra.</param>
        /// <param name="crashSource">Nguon goc ngoai le.</param>
        /// <param name="isTerminating">Co lam dong ung dung hay khong.</param>
        /// <returns>Duong dan day du cua file bao cao da duoc ghi.</returns>
        public static string WriteCrashReport(Exception exception, string crashSource, bool isTerminating)
        {
            lock (SyncLock)
            {
                try
                {
                    string dir = CrashReportDirectory;
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                    string fileName = string.Format(CultureInfo.InvariantCulture, "CrashReport_{0}.txt", timestamp);
                    string filePath = Path.Combine(dir, fileName);

                    var sb = new StringBuilder();
                    sb.AppendLine("================================================================================");
                    sb.AppendLine("                 SIMULATE AUTOMOTIVE - HE THONG BAO CAO SU CO (CRASH REPORT)     ");
                    sb.AppendLine("================================================================================");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Thoi gian ghi nhan  : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Nguon phat sinh loi : {crashSource}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Trang thai vang app : {(isTerminating ? "CO (Process Is Terminating)" : "KHONG (Handled / Prevented)")}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Thu muc ung dung    : {AppDomain.CurrentDomain.BaseDirectory}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Ten tien trinh (PID): {Process.GetCurrentProcess().ProcessName} (PID: {Environment.ProcessId})");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"He dieu hanh        : {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS")})");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Runtime .NET        : {Environment.Version}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"So luong CPU Cores  : {Environment.ProcessorCount}");

                    try
                    {
                        Process proc = Process.GetCurrentProcess();
                        long workingSetMb = proc.WorkingSet64 / (1024 * 1024);
                        long privateMemoryMb = proc.PrivateMemorySize64 / (1024 * 1024);
                        long gcHeapMb = GC.GetTotalMemory(false) / (1024 * 1024);
                        int threadsCount = proc.Threads.Count;

                        sb.AppendLine(CultureInfo.InvariantCulture, $"Bo nho RAM (WorkingSet) : {workingSetMb:N0} MB");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Bo nho RAM (Private)    : {privateMemoryMb:N0} MB");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Bo nho GC Managed Heap  : {gcHeapMb:N0} MB");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"So luong Thread hien tai: {threadsCount}");
                    }
                    catch
                    {
                        // Bo qua neu khong doc duoc thong tin tien trinh
                    }

                    sb.AppendLine("--------------------------------------------------------------------------------");
                    sb.AppendLine("CHI TIET NGOAI LE CHINH (PRIMARY EXCEPTION):");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Loai ngoai le (Type)    : {exception?.GetType().FullName ?? "Unknown"}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Thong bao loi (Message) : {exception?.Message ?? "No message"}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Phuong thuc loi (Target): {exception?.TargetSite?.DeclaringType?.FullName}.{exception?.TargetSite?.Name}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Ma loi HResult          : 0x{exception?.HResult:X8}");
                    sb.AppendLine();
                    sb.AppendLine("CALL STACK CHI TIET:");
                    sb.AppendLine(exception?.StackTrace ?? "No StackTrace available.");

                    // Ghi nhan chuoi Inner Exceptions neu co
                    Exception? inner = exception?.InnerException;
                    int innerLevel = 1;
                    while (inner != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("--------------------------------------------------------------------------------");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"INNER EXCEPTION CAP {innerLevel}:");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Loai ngoai le (Type)    : {inner.GetType().FullName}");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Thong bao loi (Message) : {inner.Message}");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Phuong thuc loi (Target): {inner.TargetSite?.DeclaringType?.FullName}.{inner.TargetSite?.Name}");
                        sb.AppendLine(CultureInfo.InvariantCulture, $"Ma loi HResult          : 0x{inner.HResult:X8}");
                        sb.AppendLine("Inner Call Stack:");
                        sb.AppendLine(inner.StackTrace ?? "No inner StackTrace.");
                        inner = inner.InnerException;
                        innerLevel++;
                    }

                    sb.AppendLine("================================================================================");
                    sb.AppendLine("                       KET THUC BAO CAO SU CO (END OF REPORT)                   ");
                    sb.AppendLine("================================================================================");

                    // Ghi dong bo truc tiep va flush xuong dia cung vat ly ngay lap tuc
                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(fs, Encoding.UTF8))
                    {
                        writer.Write(sb.ToString());
                        writer.Flush();
                        fs.Flush(flushToDisk: true);
                    }

                    return filePath;
                }
                catch (Exception writeEx)
                {
                    // Fallback cuoi cung: neu thu muc bi khoa quyen, ghi ra temp
                    try
                    {
                        string fallbackTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                        string fallbackPath = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.InvariantCulture, "Simulate_Emergency_Crash_{0}.txt", fallbackTimestamp));
                        File.WriteAllText(fallbackPath, string.Format(CultureInfo.InvariantCulture, "Crash Logger Failed: {0}\nOriginal Exception:\n{1}", writeEx.Message, exception));
                        return fallbackPath;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }
        }
    }
}
