## Task 1: Setup MVVM Framework
**Description:** Cài đặt CommunityToolkit.Mvvm và cấu trúc thư mục MVVM cơ bản.
**Acceptance criteria:**
- [ ] Cài đặt gói NuGet `CommunityToolkit.Mvvm`.
- [ ] Tạo thư mục `ViewModels/` và file `MainViewModel.cs`.
- [ ] Tạo thư mục `Services/` và `Models/`.
**Verification:**
- [ ] Build succeeds: `dotnet build`
**Dependencies:** None
**Files likely touched:** `Simulate.csproj`, `MainWindow.xaml.cs`
**Estimated scope:** Small: 1-2 files

## Task 2: Import Vector API Wrapper
**Description:** Add reference thư viện `vxlapi_NET.dll` vào dự án.
**Acceptance criteria:**
- [ ] Tạo thư mục `lib/` và add reference vào `.csproj`.
- [ ] Khai báo namespace `vxlapi_NET` thành công trong code.
**Verification:**
- [ ] Build succeeds: `dotnet build`
**Dependencies:** Task 1
**Files likely touched:** `Simulate.csproj`
**Estimated scope:** XS

## Task 3: Create Hardware Interfaces & Mock Service
**Description:** Tạo interface `ICanHardwareDriver` và bản `MockHardwareService` để test UI độc lập với phần cứng.
**Acceptance criteria:**
- [ ] `ICanHardwareDriver` chứa các hàm Connect, Disconnect, GetChannels.
- [ ] `MockHardwareService` trả về 2 kênh CAN ảo để test giao diện.
**Verification:**
- [ ] Code biên dịch không lỗi.
**Dependencies:** Task 1
**Files likely touched:** `Services/ICanHardwareDriver.cs`, `Services/MockHardwareService.cs`
**Estimated scope:** Small: 1-2 files

## Task 4: Implement VectorHardwareService
**Description:** Viết lớp giao tiếp thật với thiết bị Vector thông qua vxlapi.
**Acceptance criteria:**
- [ ] Khởi tạo `XLDriver` thành công.
- [ ] Lấy danh sách kênh phần cứng qua `XL_GetDriverConfig()`.
- [ ] Gọi `XL_OpenPort()` với Baudrate / CAN FD params truyền vào.
**Verification:**
- [ ] Build succeeds: `dotnet build`
**Dependencies:** Task 2, Task 3
**Files likely touched:** `Services/VectorHardwareService.cs`
**Estimated scope:** Medium: 3-5 files

## Task 5: Build ConnectionViewModel
**Description:** Viết ViewModel quản lý state cho bảng "CONNECTION / SETUP".
**Acceptance criteria:**
- [ ] Có properties: `AvailableInterfaces`, `SelectedTx`, `SelectedRx`, `IsCanFd`, `Baudrate`.
- [ ] Có `RelayCommand` cho nút Connect, Disconnect, Refresh.
- [ ] Dùng Interface `ICanHardwareDriver` để thực thi logic.
**Verification:**
- [ ] Unit test hoặc debug trả về kết quả đúng (tuỳ chọn).
**Dependencies:** Task 3
**Files likely touched:** `ViewModels/ConnectionViewModel.cs`, `ViewModels/MainViewModel.cs`
**Estimated scope:** Medium: 3-5 files

## Task 6: Bind MainWindow UI to ConnectionViewModel
**Description:** Gắn DataContext và Bind các Control trên UI vào ConnectionViewModel.
**Acceptance criteria:**
- [ ] Xóa `<ComboBoxItem>` giả.
- [ ] ComboBox hiển thị danh sách kênh (từ Mock hoặc Real).
- [ ] Đèn LED và nút Connect thay đổi trạng thái khi click.
**Verification:**
- [ ] Chạy tay `dotnet run` và kiểm tra thao tác bấm nút/chọn kênh.
**Dependencies:** Task 5
**Files likely touched:** `MainWindow.xaml`
**Estimated scope:** Medium: 1-2 files (nhưng sửa nhiều dòng XAML)
