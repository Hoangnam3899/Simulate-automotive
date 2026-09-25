using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class LanguageServiceTests
    {
        [TestMethod]
        public void GetResourceUriForLanguage_ReturnsValidUris_ForEveryLanguage()
        {
            var mapping = new Dictionary<AppLanguage, string>
            {
                [AppLanguage.Vietnamese] = "/Simulate;component/Resources/Languages/Strings.vi-VN.xaml",
                [AppLanguage.English] = "/Simulate;component/Resources/Languages/Strings.en-US.xaml",
                [AppLanguage.Korean] = "/Simulate;component/Resources/Languages/Strings.ko-KR.xaml",
                [AppLanguage.Japanese] = "/Simulate;component/Resources/Languages/Strings.ja-JP.xaml",
                [AppLanguage.Chinese] = "/Simulate;component/Resources/Languages/Strings.zh-CN.xaml"
            };

            foreach (var kvp in mapping)
            {
                string uri = LanguageService.GetResourceUriForLanguage(kvp.Key);
                Assert.AreEqual(kvp.Value, uri, $"URI không khớp cho ngôn ngữ {kvp.Key}");
            }
        }

        [TestMethod]
        public void ChangeLanguage_UpdatesCurrentLanguage_AndFiresEvent()
        {
            var service = new LanguageService();
            AppLanguage? eventPayload = null;
            service.LanguageChanged += (_, lang) => eventPayload = lang;

            service.ChangeLanguage(AppLanguage.English);
            Assert.AreEqual(AppLanguage.English, service.CurrentLanguage);
            Assert.AreEqual(AppLanguage.English, eventPayload);

            service.ChangeLanguage(AppLanguage.Korean);
            Assert.AreEqual(AppLanguage.Korean, service.CurrentLanguage);
            Assert.AreEqual(AppLanguage.Korean, eventPayload);
        }

        [TestMethod]
        public void GetString_WhenKeyMissing_ReturnsFallbackOrKey()
        {
            var service = new LanguageService();

            string resWithFallback = service.GetString("Loc_NonExistent_Key_XYZ", "DefaultFallback");
            Assert.AreEqual("DefaultFallback", resWithFallback);

            string resWithoutFallback = service.GetString("Loc_NonExistent_Key_XYZ");
            Assert.AreEqual("Loc_NonExistent_Key_XYZ", resWithoutFallback);
        }

        [TestMethod]
        public void CycleThroughAllLanguages_SucceedsWithoutExceptions()
        {
            var service = new LanguageService();
            var allLanguages = new[]
            {
                AppLanguage.Vietnamese,
                AppLanguage.English,
                AppLanguage.Korean,
                AppLanguage.Japanese,
                AppLanguage.Chinese,
                AppLanguage.Vietnamese
            };

            foreach (var lang in allLanguages)
            {
                service.ChangeLanguage(lang);
                Assert.AreEqual(lang, service.CurrentLanguage);
            }
        }

        [TestMethod]
        public void AllFiveResourceDictionaries_FilesExistOnDisk()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Tìm thư mục Simulate/Resources/Languages từ output directory
            string[] relativePaths = new[]
            {
                @"..\..\..\..\Simulate\Resources\Languages\Strings.vi-VN.xaml",
                @"..\..\..\..\Simulate\Resources\Languages\Strings.en-US.xaml",
                @"..\..\..\..\Simulate\Resources\Languages\Strings.ko-KR.xaml",
                @"..\..\..\..\Simulate\Resources\Languages\Strings.ja-JP.xaml",
                @"..\..\..\..\Simulate\Resources\Languages\Strings.zh-CN.xaml"
            };

            foreach (string relPath in relativePaths)
            {
                string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, relPath));
                Assert.IsTrue(System.IO.File.Exists(fullPath), $"Tệp từ điển không tồn tại: {fullPath}");

                string content = System.IO.File.ReadAllText(fullPath);
                Assert.IsTrue(content.Contains("Loc_App_Title"), $"Tệp {fullPath} thiếu key cơ bản Loc_App_Title");
                Assert.IsTrue(content.Contains("Loc_Sec_Connection"), $"Tệp {fullPath} thiếu key cơ bản Loc_Sec_Connection");
                Assert.IsTrue(content.Contains("Loc_Sec_FaultConfig"), $"Tệp {fullPath} thiếu key cơ bản Loc_Sec_FaultConfig");
                Assert.IsTrue(content.Contains("Loc_Sec_ExecControl"), $"Tệp {fullPath} thiếu key cơ bản Loc_Sec_ExecControl");
            }
        }
    }
}
