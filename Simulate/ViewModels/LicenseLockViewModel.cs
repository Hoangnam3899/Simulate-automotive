using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public partial class LicenseLockViewModel : ObservableObject
    {
        private readonly ILicenseService _licenseService;

        [ObservableProperty]
        private string _machineId = string.Empty;

        [ObservableProperty]
        private string _licenseKey = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private bool _isActivated;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _licenseExpiryText = string.Empty;

        public Action<bool>? RequestClose { get; set; }

        public LicenseLockViewModel(ILicenseService? licenseService = null)
        {
            _licenseService = licenseService ?? new LicenseService();
            _machineId = _licenseService.GetMachineId();
            _isActivated = _licenseService.IsLicensed();

            if (_isActivated)
            {
                var info = _licenseService.GetCurrentLicenseInfo();
                if (info.IsValid)
                {
                    LicenseExpiryText = info.IsPermanent
                        ? "Bản quyền: Vĩnh viễn"
                        : $"Hạn dùng: Đến {info.ExpiryDate:dd/MM/yyyy} (Còn {info.RemainingDays} ngày)";
                }
            }
        }

        [RelayCommand]
        public void CopyMachineId()
        {
            try
            {
                if (!string.IsNullOrEmpty(MachineId))
                {
                    Clipboard.SetText(MachineId);
                    StatusMessage = "Đã sao chép Machine ID vào clipboard!";
                }
            }
            catch
            {
                // Bỏ qua lỗi clipboard
            }
        }

        [RelayCommand]
        public void PasteLicenseKey()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        LicenseKey = text.Trim();
                        HasError = false;
                        ErrorMessage = string.Empty;
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi clipboard
            }
        }

        [RelayCommand]
        public void Activate()
        {
            if (string.IsNullOrWhiteSpace(LicenseKey))
            {
                HasError = true;
                ErrorMessage = "Vui lòng nhập mã kích hoạt.";
                return;
            }

            var validation = _licenseService.ValidateLicense(MachineId, LicenseKey);
            if (validation.IsValid)
            {
                HasError = false;
                ErrorMessage = string.Empty;
                IsActivated = true;

                if (validation.IsPermanent)
                {
                    StatusMessage = $"Kích hoạt thành công ({validation.LicenseType} - Vĩnh viễn)!";
                    LicenseExpiryText = "Bản quyền: Vĩnh viễn";
                }
                else
                {
                    StatusMessage = $"Kích hoạt thành công ({validation.LicenseType} - Còn {validation.RemainingDays} ngày)!";
                    LicenseExpiryText = $"Hạn dùng: Đến {validation.ExpiryDate:dd/MM/yyyy} (Còn {validation.RemainingDays} ngày)";
                }

                _licenseService.SaveLicense(LicenseKey);
                RequestClose?.Invoke(true);
            }
            else
            {
                HasError = true;
                ErrorMessage = validation.ErrorMessage ?? "License key không hợp lệ. Vui lòng kiểm tra lại hoặc liên hệ bên cấp key.";
            }
        }

        [RelayCommand]
        public void OpenGuide()
        {
            try
            {
                MessageBox.Show(
                    $"HƯỚNG DẪN KÍCH HOẠT:\n\n" +
                    $"1. Nhấn nút 'Sao chép' để copy mã Machine ID của máy ({MachineId}).\n" +
                    $"2. Gửi mã này cho quản trị viên qua công cụ Key Manager để nhận License Key RSA-2048.\n" +
                    $"3. Nhận mã kích hoạt và nhấn 'Dán' vào ô License Key.\n" +
                    $"4. Nhấn 'Kích hoạt' để mở khóa toàn bộ tính năng ứng dụng.",
                    "Hướng Dẫn Kích Hoạt Bản Quyền",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                // Bỏ qua lỗi hiển thị dialog
            }
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke(false);
        }

        partial void OnLicenseKeyChanged(string value)
        {
            if (HasError && !string.IsNullOrWhiteSpace(value))
            {
                HasError = false;
                ErrorMessage = string.Empty;
            }
        }
    }
}
