using System;
using System.Windows;
using Simulate.Services;
using Simulate.ViewModels;
using Simulate.Views;

namespace Simulate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Khoi tao he thong giam sat va bao cao su co (Crash Sentinel & Reporter)
            // ngay tu thoi diem dau tien de bat tat ca cac ngoai le truoc ca OnStartup
            CrashReporter.Initialize(this);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Phat clip mo man (chay het clip hoac nguoi dung nhan phim ESC/Skip)
            try
            {
                var introWindow = new IntroVideoWindow();
                introWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // Bo qua an toan neu moi truong khong khoi tao duoc media va ghi log
                CrashReporter.WriteCrashReport(ex, "IntroVideoWindow Startup Warning", isTerminating: false);
            }

            var licenseService = new LicenseService();
            if (!licenseService.IsLicensed())
            {
                var lockWindow = new LicenseLockWindow(new LicenseLockViewModel(licenseService));
                bool? result = lockWindow.ShowDialog();
                if (result != true || !licenseService.IsLicensed())
                {
                    Shutdown();
                    return;
                }
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}
