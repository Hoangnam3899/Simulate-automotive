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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Phát clip mở màn (chạy hết clip hoặc người dùng nhấn phím ESC/Skip)
            try
            {
                var introWindow = new IntroVideoWindow();
                introWindow.ShowDialog();
            }
            catch (Exception)
            {
                // Bỏ qua an toàn nếu môi trường không khởi tạo được media
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
