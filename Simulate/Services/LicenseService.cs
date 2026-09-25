using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Simulate.Services
{
    public class LicenseService : ILicenseService
    {
        public const string MasterKey = "SIMULATE-PRO-AUTOMOTIVE-2026";
        private const string SecretSalt = "SIMULATE_AUTOMOTIVE_E2E_SECRET_SALT_2026";

        public const string RsaPublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuo3mrxpdv9u7oNRd7/Qh
PlAo2J1VnJW1TXjzXKDAPo6SwsvWuxYvl35LdI1uZDRIUbwwRWPHC9xP2LsdwO3n
p8j94RKW2U0+hHFTDM3fXMOX8yHtuhbbq2dGyav6UzU48feBI35lTERxAFptOrCZ
0YB+h6ncT7NYdljGL8rTIEIhmJrl/XS25miaYa/kfe1L2b88elfnYyaTMd4Bxilb
HsOp1EWTpGCCrI+VWXC/Uu34bQZiQ/TRkH4c7LPbP1gK6Orna2NET4hZwjzUzsgV
d5LtybyxdNipby6Sd/7L1q0bMuWvlylD6mP2YEgL9E4OOO2cBxfPsH8x22fac8/4
gQIDAQAB
-----END PUBLIC KEY-----";

        private static readonly string LicenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simulate");
        private static readonly string LicenseFilePath = Path.Combine(LicenseDirectory, "license.lic");
        private static readonly string MachineIdFilePath = Path.Combine(LicenseDirectory, "machine_id.id");

        private string? _cachedMachineId;

        public virtual string GetMachineId()
        {
            if (!string.IsNullOrEmpty(_cachedMachineId))
            {
                return _cachedMachineId;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    object? guidValue = key?.GetValue("MachineGuid");
                    if (guidValue is string guidStr && !string.IsNullOrWhiteSpace(guidStr))
                    {
                        _cachedMachineId = guidStr.Trim().ToUpperInvariant();
                        return _cachedMachineId;
                    }
                }
            }
            catch
            {
                // Fallback nếu không có quyền đọc Registry
            }

            try
            {
                if (File.Exists(MachineIdFilePath))
                {
                    string savedId = File.ReadAllText(MachineIdFilePath).Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(savedId))
                    {
                        _cachedMachineId = savedId;
                        return _cachedMachineId;
                    }
                }

                Directory.CreateDirectory(LicenseDirectory);
                string newId = Guid.NewGuid().ToString().ToUpperInvariant();
                File.WriteAllText(MachineIdFilePath, newId);
                _cachedMachineId = newId;
                return _cachedMachineId;
            }
            catch
            {
                _cachedMachineId = "4C4C4540-0058-3410-8050-B7C04F563233";
                return _cachedMachineId;
            }
        }

        public virtual LicenseValidationResult ValidateLicense(string machineId, string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(licenseKey))
            {
                return LicenseValidationResult.Failure("Mã kích hoạt hoặc Machine ID không được để trống.");
            }

            string cleanKey = licenseKey.Trim();

            // 1. Kiểm tra Master Key (Dành cho nội bộ / Developer)
            if (string.Equals(cleanKey, MasterKey, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.Success(
                    machineId: machineId,
                    expiryDate: null,
                    isPermanent: true,
                    remainingDays: null,
                    licenseType: "Master License (Developer)");
            }

            // 2. Kiểm tra RSA-2048 Asymmetric Digital Signature
            var rsaResult = ValidateRsaSignature(cleanKey, machineId);
            if (rsaResult.IsValid)
            {
                return rsaResult;
            }

            // 3. Fallback: Kiểm tra mã băm SHA256 cơ bản
            string legacyKey = GenerateLegacyKeyForMachine(machineId);
            if (string.Equals(cleanKey, legacyKey, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.Success(
                    machineId: machineId,
                    expiryDate: null,
                    isPermanent: true,
                    remainingDays: null,
                    licenseType: "Standard License");
            }

            // Trả về lỗi chi tiết từ bước xác thực RSA nếu có
            return !string.IsNullOrEmpty(rsaResult.ErrorMessage)
                ? rsaResult
                : LicenseValidationResult.Failure("License key không hợp lệ hoặc đã bị thay đổi.");
        }

        public virtual bool IsLicensed()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    return false;
                }

                string savedKey = File.ReadAllText(LicenseFilePath).Trim();
                var result = ValidateLicense(GetMachineId(), savedKey);
                return result.IsValid;
            }
            catch
            {
                return false;
            }
        }

        public virtual LicenseValidationResult GetCurrentLicenseInfo()
        {
            try
            {
                if (!File.Exists(LicenseFilePath))
                {
                    return LicenseValidationResult.Failure("Ứng dụng chưa được kích hoạt.");
                }

                string savedKey = File.ReadAllText(LicenseFilePath).Trim();
                return ValidateLicense(GetMachineId(), savedKey);
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Failure($"Lỗi kiểm tra bản quyền: {ex.Message}");
            }
        }

        public virtual void SaveLicense(string licenseKey)
        {
            try
            {
                Directory.CreateDirectory(LicenseDirectory);
                File.WriteAllText(LicenseFilePath, licenseKey.Trim());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Không thể lưu thông tin bản quyền.", ex);
            }
        }

        public virtual void ClearLicense()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                }
            }
            catch
            {
                // Ignore cleanup error
            }
        }

        /// <summary>
        /// Xác thực chữ ký số RSA-2048 và kiểm tra ngày hết hạn bản quyền.
        /// </summary>
        private static LicenseValidationResult ValidateRsaSignature(string licenseKey, string currentMachineId)
        {
            try
            {
                string raw = licenseKey;
                if (raw.StartsWith("UTK-SM25-", StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw.Substring("UTK-SM25-".Length);
                }
                else if (raw.StartsWith("SIMULATE-", StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw.Substring("SIMULATE-".Length);
                }

                string[] parts = raw.Split('.');
                if (parts.Length != 2)
                {
                    return LicenseValidationResult.Failure("Định dạng cấu trúc License Key không hợp lệ.");
                }

                string payloadBase64 = parts[0];
                string signatureBase64 = parts[1];

                byte[] payloadBytes = Convert.FromBase64String(FromUrlSafeBase64(payloadBase64));
                byte[] signatureBytes = Convert.FromBase64String(FromUrlSafeBase64(signatureBase64));
                string payloadText = Encoding.UTF8.GetString(payloadBytes);

                // Xác thực chữ ký số bằng Public Key RSA-2048
                using var rsa = RSA.Create();
                rsa.ImportFromPem(RsaPublicKeyPem);

                bool isVerified = rsa.VerifyData(
                    payloadBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                if (!isVerified)
                {
                    return LicenseValidationResult.Failure("Chữ ký số RSA-2048 không hợp lệ. Bản quyền có thể đã bị chỉnh sửa.");
                }

                // Trích xuất các trường từ Payload: MID=...;EXP=...;TYP=...;ISS=...
                string? mid = null;
                string? exp = null;
                string licenseType = "Full License";

                foreach (string segment in payloadText.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    int eqIndex = segment.IndexOf('=');
                    if (eqIndex <= 0)
                    {
                        continue;
                    }

                    string key = segment.Substring(0, eqIndex).Trim().ToUpperInvariant();
                    string val = segment.Substring(eqIndex + 1).Trim();

                    if (key == "MID") mid = val;
                    else if (key == "EXP") exp = val;
                    else if (key == "TYP") licenseType = val;
                }

                if (string.IsNullOrEmpty(mid))
                {
                    return LicenseValidationResult.Failure("License không chứa thông tin định danh thiết bị.");
                }

                if (!string.Equals(mid, currentMachineId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return LicenseValidationResult.Failure("Mã kích hoạt này không thuộc về thiết bị của bạn.");
                }

                // Kiểm tra thời hạn bản quyền
                if (string.Equals(exp, "PERMANENT", StringComparison.OrdinalIgnoreCase))
                {
                    return LicenseValidationResult.Success(
                        machineId: mid,
                        expiryDate: null,
                        isPermanent: true,
                        remainingDays: null,
                        licenseType: licenseType);
                }

                if (DateTime.TryParseExact(exp, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expiryDate))
                {
                    int remainingDays = (int)(expiryDate.Date - DateTime.UtcNow.Date).TotalDays;
                    if (remainingDays < 0)
                    {
                        return LicenseValidationResult.Failure(
                            $"License key đã hết hạn sử dụng vào ngày {expiryDate:dd/MM/yyyy}. Vui lòng liên hệ bên cấp key để gia hạn.");
                    }

                    return LicenseValidationResult.Success(
                        machineId: mid,
                        expiryDate: expiryDate,
                        isPermanent: false,
                        remainingDays: remainingDays,
                        licenseType: licenseType);
                }

                return LicenseValidationResult.Failure("Thời hạn trong License Key không hợp lệ.");
            }
            catch (Exception ex)
            {
                return LicenseValidationResult.Failure($"Lỗi giải mã License: {ex.Message}");
            }
        }

        private static string FromUrlSafeBase64(string base64Url)
        {
            string padded = base64Url.Replace('-', '+').Replace('_', '/');
            int mod4 = padded.Length % 4;
            if (mod4 > 0)
            {
                padded += new string('=', 4 - mod4);
            }
            return padded;
        }

        public static string GenerateLegacyKeyForMachine(string machineId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

            byte[] inputBytes = Encoding.UTF8.GetBytes(machineId.Trim().ToUpperInvariant() + SecretSalt);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            string hex = Convert.ToHexString(hashBytes);

            return $"{hex[..4]}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}".ToUpperInvariant();
        }
    }
}
