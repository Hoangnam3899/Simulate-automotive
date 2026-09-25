using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Simulate.Converters
{
    /// <summary>
    /// Bộ chuyển đổi trạng thái nội bộ sang chuỗi bản địa hóa theo ngôn ngữ hiện tại.
    /// Giúp tách biệt 100% giữa Code Logic (so sánh chuỗi literal bất biến) và Presentation (hiển thị đa ngôn ngữ).
    /// </summary>
    public sealed class StatusToLocalizedConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string rawStatus || string.IsNullOrWhiteSpace(rawStatus))
            {
                return value ?? string.Empty;
            }

            string clean = rawStatus.Trim();
            string prefix = string.Empty;

            // Xử lý các tiền tố ký tự biểu tượng thông dụng (●, ○, ♡, ⏸, ■)
            if (clean.StartsWith("● ", StringComparison.Ordinal) ||
                clean.StartsWith("○ ", StringComparison.Ordinal) ||
                clean.StartsWith("♡ ", StringComparison.Ordinal) ||
                clean.StartsWith("⏸ ", StringComparison.Ordinal) ||
                clean.StartsWith("■ ", StringComparison.Ordinal))
            {
                prefix = clean[..2];
                clean = clean[2..].Trim();
            }

            string? resourceKey = clean.ToUpperInvariant() switch
            {
                "CONNECTED" => "Loc_Status_Connected",
                "DISCONNECTED" => "Loc_Status_Disconnected",
                "CONNECTING" => "Loc_Status_Connecting",
                "CONNECTING..." => "Loc_Status_Connecting",
                "RUNNING" => "Loc_Status_Running",
                "PAUSED" => "Loc_Status_Paused",
                "STOPPED" => "Loc_Status_Stopped",
                "IDLE" => "Loc_Status_Idle",
                "STANDBY" => "Loc_Status_Standby",
                "OPTIMAL" => "Loc_Status_Optimal",
                "WARNING" => "Loc_Status_Warning",
                "CRITICAL" => "Loc_Status_Critical",
                "FAULTED" => "Loc_Status_Critical",
                "OFFLINE" => "Loc_Status_Offline",
                "ACTIVE" => "Loc_Status_Active",
                "NO DATA" => "Loc_Status_NoData",
                "INJECTED" => "Loc_Status_Injected",
                "PASSTHROUGH" => "Loc_Status_PassThrough",
                "BLOCK" => "Loc_Status_Block",
                "INJECT" => "Loc_Status_Inject",
                "NONE SELECTED" => "Loc_Overview_Driver",
                "— NONE LOADED —" => "Loc_Overview_Dbc",
                "DISABLED (CLASSIC)" => "Loc_Overview_CanFd",
                _ => null
            };

            if (resourceKey != null && Application.Current != null)
            {
                object? localized = Application.Current.TryFindResource(resourceKey);
                if (localized is string locStr && !string.IsNullOrWhiteSpace(locStr))
                {
                    // Nếu chuỗi dịch đã có sẵn biểu tượng ở đầu, trả về trực tiếp
                    if (locStr.StartsWith("● ", StringComparison.Ordinal) ||
                        locStr.StartsWith("○ ", StringComparison.Ordinal) ||
                        locStr.StartsWith("⏸ ", StringComparison.Ordinal) ||
                        locStr.StartsWith("■ ", StringComparison.Ordinal))
                    {
                        return prefix == "♡ " ? "♡ " + locStr[2..] : locStr;
                    }

                    return string.IsNullOrEmpty(prefix) ? locStr : prefix + locStr;
                }
            }

            return value;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("StatusToLocalizedConverter does not support ConvertBack.");
        }
    }
}
