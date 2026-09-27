---
trigger: always_on
---

# Project Core Rules (Quy chuẩn cốt lõi dự án Simulate)

## 1. Nền tảng & Kiến trúc (Platform & Architecture)
- **Nền tảng**: .NET 8.0 (WPF), C# 12 hiện đại.
- **Mô hình kiến trúc**: MVVM chuẩn mực với `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- **Phân tách tầng triệt để**:
  - Model: POCO data structs / entities.
  - Service: Driver phần cứng (`ICanHardwareDriver`, `VectorHardwareService`, `MockHardwareService`), License, Localization, DBC, E2E.
  - ViewModel: Quản lý trạng thái, command, không chứa logic P/Invoke trực tiếp.
  - View (XAML): DataBinding, không viết logic nghiệp vụ trong code-behind (`MainWindow.xaml.cs`).

## 2. Xác minh luồng thực thi End-to-End
- Không kết luận logic đúng nếu chỉ kiểm tra hàm tĩnh rời rạc. Phải truy vết đầy đủ chuỗi:
  `Input / Event -> Data Model -> Service / Engine -> Vector XL Driver -> ViewModel / UI`.
- Mọi trường dữ liệu sau khi giải mã từ DBC hoặc file cấu hình phải được liên kết và sử dụng thực tế trong runtime.
- Kiểm tra các lỗi thường gặp: `parse-but-unused`, `helper-but-not-called`, `UI-without-backend-hook`.

## 3. Bảo toàn Giao diện (UI Protection - user_global)
- Tuyệt đối KHÔNG thay đổi bất kỳ thành phần nào trên UI (XAML, Layout, Styling, Controls, Colors, Margins) khi chưa được người dùng cho phép tường minh.

## 4. Kiểm chuẩn & Verification
- Luôn chạy `dotnet build` sau mỗi lần sửa đổi code, bảo đảm **0 warning, 0 error**.
- Luôn chạy `dotnet test` bảo đảm toàn bộ unit test suite vượt qua 100%.

## 5. Quản lý Git & Bản quyền
- Luôn giữ an toàn cho mã nguồn và khóa RSA (`LicenseService`).
- Tuân thủ quy ước commit (Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `chore:`).
