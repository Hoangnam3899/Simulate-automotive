using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Simulate.Services
{
    /// <summary>
    /// Triển khai dịch vụ đa ngôn ngữ chuẩn WPF (.NET 8 MVVM).
    /// Hỗ trợ nạp và hoán đổi MergedDictionaries động (Runtime Zero-Restart Live Switching)
    /// và ghi nhớ ngôn ngữ đã chọn tại %LocalAppData%\Simulate\settings.json.
    /// </summary>
    public class LanguageService : ILanguageService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Simulate");
        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
        private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

        private AppLanguage _currentLanguage = AppLanguage.Vietnamese;
        private ResourceDictionary? _activeDictionary;

        public AppLanguage CurrentLanguage => _currentLanguage;

        public event EventHandler<AppLanguage>? LanguageChanged;

        public LanguageService()
        {
            AppLanguage initialLanguage = LoadSavedLanguage();
            ApplyLanguage(initialLanguage, saveSetting: false);
        }

        public void ChangeLanguage(AppLanguage language)
        {
            if (_currentLanguage == language && _activeDictionary != null)
            {
                return;
            }

            ApplyLanguage(language, saveSetting: true);
        }

        public string GetString(string key, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return fallback ?? string.Empty;
            }

            try
            {
                if (Application.Current != null)
                {
                    object? res = Application.Current.TryFindResource(key);
                    if (res is string s)
                    {
                        return s;
                    }
                }

                if (_activeDictionary != null && _activeDictionary.Contains(key))
                {
                    object? res = _activeDictionary[key];
                    if (res is string s)
                    {
                        return s;
                    }
                }
            }
            catch
            {
                // Fallback nếu có ngoại lệ tra cứu
            }

            return fallback ?? key;
        }

        private void ApplyLanguage(AppLanguage language, bool saveSetting)
        {
            _currentLanguage = language;
            string uriString = GetResourceUriForLanguage(language);

            try
            {
                var newDict = new ResourceDictionary
                {
                    Source = new Uri(uriString, UriKind.RelativeOrAbsolute)
                };

                if (Application.Current != null)
                {
                    // Hoán đổi MergedDictionaries trên Dispatcher UI nếu cần
                    void SwapDictionary()
                    {
                        var merged = Application.Current.Resources.MergedDictionaries;
                        ResourceDictionary? oldDict = null;

                        foreach (var d in merged)
                        {
                            if (d.Source != null && d.Source.OriginalString.Contains("/Resources/Languages/Strings.", StringComparison.OrdinalIgnoreCase))
                            {
                                oldDict = d;
                                break;
                            }
                        }

                        if (oldDict != null)
                        {
                            merged.Remove(oldDict);
                        }

                        merged.Add(newDict);
                    }

                    if (Application.Current.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(SwapDictionary);
                    }
                    else
                    {
                        SwapDictionary();
                    }
                }

                _activeDictionary = newDict;
            }
            catch (Exception)
            {
                // Trong môi trường Unit Test không có BAML Pack URI, vẫn giữ trạng thái ngôn ngữ
            }

            if (saveSetting)
            {
                SaveLanguageSetting(language);
            }

            LanguageChanged?.Invoke(this, language);
        }

        public static string GetResourceUriForLanguage(AppLanguage language)
        {
            string culture = language switch
            {
                AppLanguage.Vietnamese => "vi-VN",
                AppLanguage.English => "en-US",
                AppLanguage.Korean => "ko-KR",
                AppLanguage.Japanese => "ja-JP",
                AppLanguage.Chinese => "zh-CN",
                _ => "vi-VN"
            };

            return $"/Simulate;component/Resources/Languages/Strings.{culture}.xaml";
        }

        private static AppLanguage LoadSavedLanguage()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var prop))
                    {
                        string? langStr = prop.GetString();
                        if (Enum.TryParse<AppLanguage>(langStr, true, out var lang))
                        {
                            return lang;
                        }
                    }
                }
            }
            catch
            {
                // Fallback nếu file cấu hình bị hỏng hoặc chưa có
            }

            return AppLanguage.Vietnamese;
        }

        private static void SaveLanguageSetting(AppLanguage language)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var obj = new { Language = language.ToString() };
                string json = JsonSerializer.Serialize(obj, IndentedJsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Bỏ qua lỗi ghi file nếu quyền truy cập bị hạn chế
            }
        }
    }
}
