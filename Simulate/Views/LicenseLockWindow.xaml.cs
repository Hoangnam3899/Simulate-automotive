using System.Windows;
using System.Windows.Input;
using Simulate.ViewModels;

namespace Simulate.Views
{
    public partial class LicenseLockWindow : Window
    {
        public LicenseLockViewModel ViewModel { get; }

        public LicenseLockWindow(LicenseLockViewModel? viewModel = null)
        {
            InitializeComponent();
            ViewModel = viewModel ?? new LicenseLockViewModel();
            DataContext = ViewModel;

            ViewModel.RequestClose = (isSuccess) =>
            {
                DialogResult = isSuccess;
                Close();
            };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
