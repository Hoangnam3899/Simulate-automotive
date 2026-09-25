using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Simulate.Views
{
    /// <summary>
    /// Cửa sổ hiển thị video mở màn (Cinematic Intro / Splash Video) trước khi vào ứng dụng chính.
    /// Hỗ trợ tự động đóng khi phát hết, phím tắt bỏ qua (ESC/Space) và cơ chế phòng ngừa treo (Fail-Safe Timer).
    /// </summary>
    public partial class IntroVideoWindow : Window
    {
        private readonly DispatcherTimer? _safetyTimer;
        private int _isClosed;

        public IntroVideoWindow()
        {
            InitializeComponent();

            string assetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "clipStart.mp4");
            if (File.Exists(assetPath))
            {
                IntroMedia.Source = new Uri(assetPath, UriKind.Absolute);
            }

            // Thiết lập bộ đếm an toàn tối đa 11 giây (video gốc dài 10 giây)
            _safetyTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(11)
            };
            _safetyTimer.Tick += SafetyTimer_Tick;

            Loaded += IntroVideoWindow_Loaded;
        }

        private void IntroVideoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (IntroMedia.Source is not null)
            {
                try
                {
                    IntroMedia.Play();
                    _safetyTimer?.Start();
                }
                catch (Exception)
                {
                    CloseIntro();
                }
            }
            else
            {
                CloseIntro();
            }
        }

        private void SafetyTimer_Tick(object? sender, EventArgs e)
        {
            CloseIntro();
        }

        private void IntroMedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            CloseIntro();
        }

        private void IntroMedia_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            CloseIntro();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            CloseIntro();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Escape or Key.Space or Key.Enter)
            {
                CloseIntro();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Đóng cửa sổ Intro an toàn, giải phóng tài nguyên phát media và dừng timer.
        /// </summary>
        public void CloseIntro()
        {
            if (Interlocked.Exchange(ref _isClosed, 1) != 0)
            {
                return;
            }

            try
            {
                _safetyTimer?.Stop();
                IntroMedia.Stop();
                IntroMedia.Close();
            }
            catch (Exception)
            {
                // Bỏ qua lỗi dọn dẹp media nếu có
            }

            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
                // Xử lý an toàn khi Window không được mở dạng ShowDialog (ví dụ trong unit test hoặc Show thông thường)
            }

            Close();
        }
    }
}
