using System;

namespace Simulate.Services
{
    public sealed class LicenseValidationResult
    {
        public bool IsValid { get; }
        public string? ErrorMessage { get; }
        public string? MachineId { get; }
        public DateTime? ExpiryDate { get; }
        public bool IsPermanent { get; }
        public int? RemainingDays { get; }
        public string? LicenseType { get; }

        private LicenseValidationResult(
            bool isValid,
            string? errorMessage,
            string? machineId,
            DateTime? expiryDate,
            bool isPermanent,
            int? remainingDays,
            string? licenseType)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            MachineId = machineId;
            ExpiryDate = expiryDate;
            IsPermanent = isPermanent;
            RemainingDays = remainingDays;
            LicenseType = licenseType;
        }

        public static LicenseValidationResult Success(
            string machineId,
            DateTime? expiryDate,
            bool isPermanent,
            int? remainingDays,
            string licenseType)
        {
            return new LicenseValidationResult(
                isValid: true,
                errorMessage: null,
                machineId: machineId,
                expiryDate: expiryDate,
                isPermanent: isPermanent,
                remainingDays: remainingDays,
                licenseType: licenseType);
        }

        public static LicenseValidationResult Failure(string errorMessage)
        {
            return new LicenseValidationResult(
                isValid: false,
                errorMessage: errorMessage,
                machineId: null,
                expiryDate: null,
                isPermanent: false,
                remainingDays: null,
                licenseType: null);
        }
    }

    public interface ILicenseService
    {
        string GetMachineId();

        LicenseValidationResult ValidateLicense(string machineId, string licenseKey);

        bool IsLicensed();

        LicenseValidationResult GetCurrentLicenseInfo();

        void SaveLicense(string licenseKey);

        void ClearLicense();
    }
}
