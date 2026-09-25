using System;

namespace Simulate.Services
{
    /// <summary>
    /// Các ngôn ngữ được hỗ trợ trong hệ thống đa ngôn ngữ của Simulate.
    /// </summary>
    public enum AppLanguage
    {
        Vietnamese, // vi-VN: Tiếng Việt (Mặc định)
        English,    // en-US: Tiếng Anh
        Korean,     // ko-KR: Tiếng Hàn (한국어)
        Japanese,   // ja-JP: Tiếng Nhật (日本語)
        Chinese     // zh-CN: Tiếng Trung (简体中文)
    }

    /// <summary>
    /// Giao diện dịch vụ quản lý ngôn ngữ và chuyển đổi từ điển động (Runtime Live-Switching).
    /// </summary>
    public interface ILanguageService
    {
        /// <summary>
        /// Ngôn ngữ đang hoạt động trong phiên làm việc.
        /// </summary>
        AppLanguage CurrentLanguage { get; }

        /// <summary>
        /// Chuyển đổi ngôn ngữ hiển thị động cho toàn bộ ứng dụng mà không cần khởi động lại.
        /// </summary>
        /// <param name="language">Ngôn ngữ đích cần chuyển đổi.</param>
        void ChangeLanguage(AppLanguage language);

        /// <summary>
        /// Lấy chuỗi bản địa hóa theo Resource Key từ ResourceDictionary hiện tại.
        /// </summary>
        /// <param name="key">Khóa tài nguyên chuỗi (bắt đầu bằng Loc_).</param>
        /// <param name="fallback">Giá trị mặc định nếu không tìm thấy key.</param>
        string GetString(string key, string? fallback = null);

        /// <summary>
        /// Sự kiện phát ra mỗi khi ngôn ngữ được chuyển đổi thành công.
        /// </summary>
        event EventHandler<AppLanguage>? LanguageChanged;
    }
}
