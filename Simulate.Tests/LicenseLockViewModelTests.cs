using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class LicenseLockViewModelTests
    {
        private const string RsaPrivateKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC6jeavGl2/27ug
1F3v9CE+UCjYnVWclbVNePNcoMA+jpLCy9a7Fi+Xfkt0jW5kNEhRvDBFY8cL3E/Y
ux3A7eenyP3hEpbZTT6EcVMMzd9cw5fzIe26FturZ0bJq/pTNTjx94EjfmVMRHEA
Wm06sJnRgH6HqdxPs1h2WMYvytMgQiGYmuX9dLbmaJphr+R97UvZvzx6V+djJpMx
3gHGKVsew6nURZOkYIKsj5VZcL9S7fhtBmJD9NGQfhzss9s/WAro6udrY0RPiFnC
PNTOyBV3ku3JvLF02KlvLpJ3/svWrRsy5a+XKUPqY/ZgSAv0Tg447ZwHF8+wfzHb
Z9pzz/iBAgMBAAECggEAWtOGih8QoL3W+mUavR5DZya41Em5qkxbInZuKd1b67kX
a/65M4oILr4+92Z6Zg/sW9r/N3FuMfCX8WGciVdqz040iPW3euG4O9w+vn+nLW9P
FgEhUFYbqjZUiRCRhhxl9qx1c7XwlLLHv4/HGWls4kqoj91b1XPwcGIlT5R0uQGS
RhcyCSSY3Z5vDPHD46I/8/NpR+39wR0tuvEx7Ywy+cqLY+PjQSZk5IIVG8pQtIzr
mwSzfvwLLnyQfHclZXA5xHnBag70U9s5zierXtqJKpRriA6/CA06M7bnU1wmxE59
S7obIeJ1uY2/cJr8TGwL7TgIUJRF7B+2GfHLtqWFZwKBgQD+K5lw0MFygU4vGPsa
nYMk0aguCMgerEQMmuZLqNu9/IXskUb+UK5HplJzM0USjHBvVUoOLBNe8rvRGf0S
Wxz//pGlF96cHNRbnEziq8QFGPqheaKw8IruKz7xWozZvj8TCj469dCLCEuGetsF
1uhDlwZKLm0su1/iOZVWQyyckwKBgQC75bHe4q5loyXtTw/invbf4mPieEJDMhLQ
h83scxgaWg7wRg+kuiaz8VOY7vi+ZC9plgzinqp7x0sGDJaWudCnSSurDfEvA2Xe
oUpBM34ELF+AQ2P+xPXhKMz/NoF3LSv1xqO75G+OKeERsGWt7sOZk5YnHzjHiDTW
6O/MvHTXGwKBgQCnmHwvBZfpNxYkvCYnYKFvD8gDwsqiXxjn7uPYE9oBppdwbEMR
woWIvUU0rSPonS4uW2Dfg3SqcQgjUy2qguXWbzf+UoT5D5F/bsrL1FMGwXfSLfB3
F8WkUmPIpdqmYWoZ1fE+04PViXyziiMN8K1qlBUTqRVYH52UAEBWhyjNfQKBgBTb
k5olWayopq4oJ6BFeywxKltadCiXZ2VEngRQZm2Ob7gWXugvTdqNwHTqmiwwXN8A
rB9/83bYEajzPsguik33nmRXoN1SKD3Fc8O3HpcCAfvv/yqp3I2JBPCTsV10Yzve
OBDEc+m5FmXrSe474fSrYHCpU5k6snrk0rHMfb/fAoGAIwndjry54xJshTa2igB7
lk7m63oyw8VfsXb1mezuzym98biheCSBawdGQ5TehiVNGe0NyU6JXLXqVsSiLDen
W5O7Aq4SXxQ1E0dEF6HcChtyS2/B86lrlpsGLAMkvflPSi5el9lBndVKyncZPMFA
qCP3d48rdGPNnbkRDVM0mnk=
-----END PRIVATE KEY-----";

        private static string CreateSignedKey(string machineId, string expiry, string type)
        {
            string payload = $"MID={machineId};EXP={expiry};TYP={type};ISS={DateTime.UtcNow:yyyy-MM-dd}";
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(RsaPrivateKeyPem);
            byte[] signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"UTK-SM25-{Convert.ToBase64String(payloadBytes)}.{Convert.ToBase64String(signatureBytes)}";
        }

        private sealed class MockLicenseService : ILicenseService
        {
            public string MachineId { get; set; } = "4C4C4540-0058-3410-8050-B7C04F563233";
            public bool IsLicensedResult { get; set; }
            public string? SavedLicenseKey { get; private set; }

            public string GetMachineId() => MachineId;

            public LicenseValidationResult ValidateLicense(string machineId, string licenseKey)
            {
                if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(licenseKey))
                {
                    return LicenseValidationResult.Failure("Mã kích hoạt không được để trống.");
                }

                if (licenseKey.Trim() == "VALID-KEY-1234" || licenseKey.Trim() == "SIMULATE-PRO-AUTOMOTIVE-2026")
                {
                    return LicenseValidationResult.Success(machineId, null, true, null, "Full License");
                }

                return LicenseValidationResult.Failure("Mã kích hoạt không hợp lệ.");
            }

            public bool IsLicensed() => IsLicensedResult;

            public LicenseValidationResult GetCurrentLicenseInfo()
            {
                return IsLicensedResult
                    ? LicenseValidationResult.Success(MachineId, null, true, null, "Full License")
                    : LicenseValidationResult.Failure("Chưa kích hoạt.");
            }

            public void SaveLicense(string licenseKey)
            {
                SavedLicenseKey = licenseKey;
                IsLicensedResult = true;
            }

            public void ClearLicense()
            {
                SavedLicenseKey = null;
                IsLicensedResult = false;
            }
        }

        [TestMethod]
        public void LicenseService_Rsa2048_ValidPermanentKey_PassesValidation()
        {
            var service = new LicenseService();
            string machineId = service.GetMachineId();

            string validKey = CreateSignedKey(machineId, "PERMANENT", "Full License");
            var result = service.ValidateLicense(machineId, validKey);

            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.IsPermanent);
            Assert.IsNull(result.RemainingDays);
        }

        [TestMethod]
        public void LicenseService_Rsa2048_ValidTimeLimitedKey_CalculatesRemainingDaysCorrectly()
        {
            var service = new LicenseService();
            string machineId = service.GetMachineId();

            string futureDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");
            string validKey = CreateSignedKey(machineId, futureDate, "Standard 30 Days");
            var result = service.ValidateLicense(machineId, validKey);

            Assert.IsTrue(result.IsValid);
            Assert.IsFalse(result.IsPermanent);
            Assert.IsNotNull(result.RemainingDays);
            Assert.IsTrue(result.RemainingDays >= 29 && result.RemainingDays <= 30);
        }

        [TestMethod]
        public void LicenseService_Rsa2048_ExpiredKey_FailsValidationWithSpecificErrorMessage()
        {
            var service = new LicenseService();
            string machineId = service.GetMachineId();

            string pastDate = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd");
            string expiredKey = CreateSignedKey(machineId, pastDate, "Expired Trial");
            var result = service.ValidateLicense(machineId, expiredKey);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage!.Contains("hết hạn", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void LicenseService_Rsa2048_WrongMachineId_FailsValidation()
        {
            var service = new LicenseService();
            string otherMachineId = "OTHER-MACHINE-9999-XXXX";

            string keyForOtherMachine = CreateSignedKey(otherMachineId, "PERMANENT", "Full License");
            var result = service.ValidateLicense(service.GetMachineId(), keyForOtherMachine);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage!.Contains("không thuộc về thiết bị", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void LicenseService_MasterKey_AlwaysValid()
        {
            var service = new LicenseService();
            string machineId = service.GetMachineId();

            var result = service.ValidateLicense(machineId, "SIMULATE-PRO-AUTOMOTIVE-2026");
            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.IsPermanent);
        }

        [TestMethod]
        public void ViewModel_InitialState_LoadsMachineIdAndNoError()
        {
            var mock = new MockLicenseService();
            var vm = new LicenseLockViewModel(mock);

            Assert.AreEqual("4C4C4540-0058-3410-8050-B7C04F563233", vm.MachineId);
            Assert.AreEqual(string.Empty, vm.LicenseKey);
            Assert.IsFalse(vm.HasError);
            Assert.AreEqual(string.Empty, vm.ErrorMessage);
            Assert.IsFalse(vm.IsActivated);
        }

        [TestMethod]
        public void ViewModel_Activate_WithEmptyKey_SetsError()
        {
            var mock = new MockLicenseService();
            var vm = new LicenseLockViewModel(mock);

            vm.Activate();

            Assert.IsTrue(vm.HasError);
            Assert.AreEqual("Vui lòng nhập mã kích hoạt.", vm.ErrorMessage);
            Assert.IsFalse(vm.IsActivated);
        }

        [TestMethod]
        public void ViewModel_Activate_WithInvalidKey_SetsInvalidKeyError()
        {
            var mock = new MockLicenseService();
            var vm = new LicenseLockViewModel(mock)
            {
                LicenseKey = "WRONG-KEY"
            };

            vm.Activate();

            Assert.IsTrue(vm.HasError);
            Assert.IsTrue(vm.ErrorMessage.Contains("không hợp lệ", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(vm.IsActivated);
        }

        [TestMethod]
        public void ViewModel_Activate_WithValidKey_SucceedsAndInvokesClose()
        {
            var mock = new MockLicenseService();
            var vm = new LicenseLockViewModel(mock)
            {
                LicenseKey = "VALID-KEY-1234"
            };

            bool? closeResult = null;
            vm.RequestClose = (result) => closeResult = result;

            vm.Activate();

            Assert.IsFalse(vm.HasError);
            Assert.IsTrue(vm.IsActivated);
            Assert.AreEqual("VALID-KEY-1234", mock.SavedLicenseKey);
            Assert.AreEqual(true, closeResult);
        }
    }
}
