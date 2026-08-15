using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Simulate.ViewModels;

namespace Simulate
{
    public partial class MainWindow : Window
    {
        private bool _isShutdownStarted;
        private bool _isShutdownComplete;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isShutdownComplete || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            e.Cancel = true;
            if (_isShutdownStarted)
            {
                return;
            }

            _isShutdownStarted = true;
            await Task.Yield();

            try
            {
                await viewModel.ShutdownAsync();
            }
            catch (Exception)
            {
                // Runtime failures remain observable on the view models; the window must still finish closing.
            }
            finally
            {
                _isShutdownComplete = true;
                Closing -= MainWindow_Closing;
                Close();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
