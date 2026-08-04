# Implementation Plan: Hardware Connection & Setup (Vector XL)

## Overview
Xây dựng chức năng Connection / Setup cho giao diện hiện tại, cho phép kết nối phần cứng Vector XL, chọn kênh, cấu hình Baudrate, và bật tắt CAN FD thông qua thư viện `vxlapi_NET.dll`. Đồng thời, dự án sẽ được thiết lập nền tảng MVVM (Model-View-ViewModel) để tách biệt UI (`MainWindow.xaml`) và logic xử lý.

## Architecture Decisions
- **MVVM Framework**: Sử dụng `CommunityToolkit.Mvvm` (via NuGet) vì đây là chuẩn hiện đại của Microsoft cho WPF, giúp giảm boilerplate code (Source Generators).
- **Service Injection**: Tách logic phần cứng Vector ra một class riêng (`VectorHardwareService`) implement interface `ICanHardwareDriver`. Điều này giúp ta có thể test UI thông qua `MockHardwareService` khi không cắm thiết bị thật.
- **Dependency Management**: Thư viện `vxlapi_NET.dll` (64-bit) cần được reference trực tiếp vào `.csproj` và copy vào thư mục output (`Copy Local = True`).

## Task List

### Phase 1: Foundation (MVVM & DLL Reference)
- [ ] Task 1: Setup MVVM Framework
- [ ] Task 2: Import Vector API Wrapper

### Checkpoint: Foundation
- [ ] Dự án build thành công với `CommunityToolkit.Mvvm` và reference được `vxlapi_NET.dll`.
- [ ] ViewModels cơ bản được gắn vào MainWindow.

### Phase 2: Hardware Service Layer
- [ ] Task 3: Create Hardware Interfaces & Mock Service
- [ ] Task 4: Implement VectorHardwareService

### Checkpoint: Hardware Service Layer
- [ ] `VectorHardwareService` có thể gọi `XL_GetDriverConfig()` thành công mà không văng lỗi DLLNotFound.

### Phase 3: UI Integration (Connection Panel)
- [ ] Task 5: Build ConnectionViewModel
- [ ] Task 6: Bind MainWindow UI to ConnectionViewModel

### Checkpoint: Complete
- [ ] Giao diện (ComboBox, Buttons, LED) phản hồi đúng với trạng thái Connection (Mock hoặc Real).
- [ ] Có thể chọn kênh, chọn baudrate và nhấn Connect.
- [ ] All acceptance criteria met. Ready for code review.

## Risks and Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| `DllNotFoundException` (Do sai kiến trúc 32-bit/64-bit) | High | Đảm bảo `.csproj` build target `x64` hoặc `AnyCPU` khớp với bản build của DLL Vector. |
| Vector Hardware không kết nối được | Med | Luôn triển khai `MockHardwareService` song song để UI/UX dev không bị chặn. |
| Treo UI khi gọi API Vector | Low | Sử dụng `Task.Run` hoặc `async/await` cho các thao tác mở port/gửi nhận CAN lâu. |

## Open Questions
- Thư viện `vxlapi_NET.dll` hiện chưa có trong thư mục source code (trên git). Bạn đã để sẵn file này ở đâu chưa để tôi reference (ví dụ thư mục `lib/`), hay tôi nên tự tạo thư mục `lib/` và bạn sẽ copy nó vào sau?
