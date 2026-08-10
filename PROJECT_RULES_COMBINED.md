# BỘ LUẬT DỰ ÁN — SIMULATE WPF & AUTOMOTIVE HARDWARE WORKFLOW

> **BẮT BUỘC ĐỌC ĐẦU MỖI SESSION:** `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`.
>
> Bộ luật này định nghĩa quy tắc làm việc, lifecycle phát triển, quality gate, kiến trúc MVVM, TDD, security, CI/CD và review đa chiều cho dự án **Simulate (C# WPF .NET 8 / Vector XL CAN Interface)**.
>
> Kết hợp tinh hoa từ `addyosmani/agent-skills` và `mattpocock/skills` phù hợp với lập trình Desktop WPF Automotive.

---

## 0. Cấu hình dự án & Quy tắc tối cao (User Rules)

### 0.1. Bảng thông số bối cảnh dự án

| Khóa | Mô tả | Giá trị |
|---|---|---|
| `<NGÔN_NGỮ_GIAO_TIẾP>` | Ngôn ngữ giao tiếp và tài liệu | Tiếng Việt |
| `<NGÔN_NGỮ_CODE>` | Ngôn ngữ lập trình chính | C# (.NET 8.0 WPF) |
| `<FRAMEWORK_UI>` | UI framework & styling | WPF XAML (Theme tối tại `App.xaml`, MVVM với `CommunityToolkit.Mvvm`) |
| `<SOLUTION_PATH>` | Solution dự án | `Simulate.sln` |
| `<PROJECT_PATH>` | Project ứng dụng | `Simulate/Simulate.csproj` |
| `<HARDWARE_API>` | API kết nối phần cứng | Vector XL API (`vxlapi_NET.dll` 64-bit tại `Simulate/lib/` & `Doc/`) |
| `<THƯ_MỤC_SKILL>` | Nguồn skill chuẩn duy nhất | `.agents/skills/` |
| `<THƯ_MỤC_NHIỆM_VỤ>` | Quản lý kế hoạch & Todo | `tasks/plan.md`, `tasks/todo.md` |
| `<FILE_TỪ_VỰNG>` | Ubiquitous Language | `CONTEXT.md` |
| `<TÀI_LIỆU_XƯƠNG_SỐNG>` | Nguồn tài liệu kỹ thuật phần cứng | `Doc/` (`XL_Driver_Library_Manual_EN.pdf`, `XLClass.txt`, `XLDefine.txt`, `XLDriver.txt`) |
| `<NHÁNH_CHÍNH>` | Nhánh được bảo vệ | `main` |

### 0.2. Quy tắc tối cao từ người dùng (`RULE[user_global]` & Project Boundaries)

1. **BẮT BUỘC ĐỌC LUẬT VÀ SKILL DÙ Ở BẤT CỨ HOÀN CẢNH NÀO:** Tất cả các phiên làm việc của AI Agent (Antigravity) **BẮT BUỘC** phải đọc đầy đủ các tệp quy tắc (`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `PROJECT_RULES_COMBINED.md`) và tra cứu/áp dụng các skill liên quan trong `.agents/skills/` trước khi thực hiện bất kỳ phản hồi hay thay đổi code nào.
2. **Bảo vệ UI tuyệt đối:** KHÔNG ĐƯỢC THAY ĐỔI BẤT KỲ CÁI GÌ Ở TRÊN UI (XAML / Controls / Layout / Design) KHI CHƯA ĐƯỢC NGƯỜI DÙNG CHO PHÉP EXPLICITLY.
3. **Giới hạn phạm vi (Boundaries):**
   - Không commit secrets, API keys, credentials.
   - Không sửa file nằm ngoài solution mà không được phép.
   - **Hỏi trước** khi thêm NuGet package dependencies (ví dụ: `CommunityToolkit.Mvvm` đã được cài đặt).
   - **Hỏi trước** khi thay đổi cấu trúc dự án hoặc solution configuration.
   - **Luôn kiểm tra biên dịch:** Chạy `dotnet build` sau mỗi lần thay đổi code để đảm bảo ứng dụng biên dịch thành công 0 warning / 0 error.

---

## 1. Thứ tự áp dụng Rule & Nguồn sự thật

Khi có mâu thuẫn, áp dụng thứ tự ưu tiên giảm dần:

1. **Yêu cầu trực tiếp mới nhất của người dùng.**
2. **Quy tắc tối cao người dùng (`user_global`, `AGENTS.md`, `GEMINI.md`, `CLAUDE.md`).**
3. **Bản tả tài liệu kỹ thuật Vector chính thức (`Doc/XL_Driver_Library_Manual_EN.pdf`, `vxlapi_NET.dll`).**
4. **`PROJECT_RULES_COMBINED.md`.**
5. **Contract giao diện lõi (`ICanHardwareDriver`, `VectorHardwareService`, `MockHardwareService`).**
6. **Skill điều phối chính của task trong `.agents/skills/`.**
7. **Convention MVVM & C# (.NET 8).**

---

## 2. Session Bootstrap & Phân loại Task (T0, T1, T2)

### 2.1. Bootstrap bắt buộc đầu phiên
1. Đọc `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`.
2. Đọc `PROJECT_RULES_COMBINED.md`.
3. Đọc `.agents/skills/using-agent-skills/SKILL.md`.
4. Đọc `tasks/plan.md` và `tasks/todo.md` để nắm tiến độ công việc hiện tại.
5. Kiểm tra kết quả biên dịch bằng `dotnet build`.

### 2.2. Phân loại Cấp độ Task
- **Cấp T0 — Trivial:** Sửa typo, comment, format code. (Workflow: Đọc file -> Sửa nhỏ -> `dotnet build` -> Đóng).
- **Cấp T1 — Standard Engineering:** Thêm logic service, ViewModel, bug fix có phạm vi rõ. (Workflow: Plan ngắn -> Implementation -> `dotnet build` & `dotnet test` -> Code review).
- **Cấp T2 — Complex / Hardware / Architecture:** Thay đổi driver Vector, refactor MVVM, concurrency, fault injection, CAN FD bit timing, session lifecycle. (Workflow: Spec -> Test Spec -> Vertical Slice TDD -> Runtime Verification -> Two-Axis Code Review).

---

## 3. Kiến trúc MVVM & Hardware Abstraction Layer (Vector XL)

### 3.1. Mô hình MVVM chuẩn C# .NET 8
- **Model:** `HardwareInterface.cs`, `HardwareChannel.cs` đại diện cho dữ liệu thuần túy (POCO).
- **Service Layer:**
  - `ICanHardwareDriver.cs`: Interface trừu tượng hóa mọi thao tác CAN hardware (`GetAvailableInterfaces`, `Connect`, `Disconnect`).
  - `VectorHardwareService.cs`: Lớp giao tiếp thật với thiết bị Vector thông qua `vxlapi_NET.dll` (`XLDriver`, `XL_GetDriverConfig`, `XL_OpenPort`, `XL_CanFdSetConfiguration`, `XL_CanSetChannelBitrate`, `XL_ActivateChannel`).
  - `MockHardwareService.cs`: Lớp giả lập phần cứng phục vụ test độc lập và phát triển UI khi không cắm thiết bị thật.
- **ViewModel Layer:**
  - `MainViewModel.cs`: ViewModel chính của ứng dụng.
  - `ConnectionViewModel.cs`: Quản lý trạng thái bảng "CONNECTION / SETUP" (Interfaces, Tx/Rx Channel, Baudrate, CAN FD, Connect/Disconnect commands). Sử dụng `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- **View Layer:**
  - `MainWindow.xaml`: Giao diện người dùng binding dữ liệu từ `MainViewModel`.
  - **Cấm:** Không viết logic phần cứng, P/Invoke hay gọi `vxlapi_NET` trực tiếp trong XAML code-behind (`MainWindow.xaml.cs`). Code-behind chỉ chứa logic UI thuần túy (nếu có).

### 3.2. Quy tắc Hardware Concurrency & Resource Cleanup
- Mọi thao tác mở port, đóng port, gửi/nhận frame dài phải xử lý bất đồng bộ (`async`/`await`, `Task.Run`) tránh đóng băng UI thread (Main Looper).
- Quản lý tài nguyên `XLDriver` phải tuân thủ Idempotent Cleanup: khi ngắt kết nối hoặc đóng ứng dụng, gọi `XL_DeactivateChannel`, `XL_ClosePort` và `XL_CloseDriver` an toàn, không rò rỉ native handle.

---

## 4. Quy trình Phát triển TDD & Test Seam

1. **Test-Driven Development (RED -> GREEN -> REFACTOR):**
   - Viết test quan sát qua public seam (`ICanHardwareDriver`, ViewModels, Services).
   - Cấm test private members hoặc mock dư thừa internal collaborators.
2. **Xác minh Biên dịch & Test:**
   - Chạy lệnh `dotnet build` sau mỗi lần chỉnh sửa code.
   - Chạy lệnh `dotnet test` để kiểm tra toàn bộ suite kiểm thử unit test.

---

## 5. Quy tắc UI và Bảo vệ Assets Hiện Hữu

1. **Tuyệt đối tuân thủ `user_global`:** Cấm sửa bất kỳ thành phần XAML nào trong `MainWindow.xaml` hay `App.xaml` ngoại trừ khi được người dùng cho phép rõ ràng.
2. **Không phá vỡ DataBinding:** Khi cập nhật ViewModel, giữ nguyên các Binding path đã có trên View.
3. **Không ghi đè placeholder:** Không thay thế UI thật bằng dữ liệu giả lập (hardcoded strings) trực tiếp trên XAML.

---

## 6. End-to-End Traceability & Quy trình Work Log sau mỗi phiên làm việc

### 6.1. Chuỗi truy vết bắt buộc
Mọi thay đổi code phải truy xuất được theo chuỗi:
`Nguồn Vector SDK (Doc/) -> Yêu cầu (tasks/plan.md) -> Code C# (Services/ViewModels) -> Build (dotnet build) -> Task Checkpoint (tasks/todo.md)`

### 6.2. Quy tắc Work Log bắt buộc sau mỗi phiên (End-of-Session Work Log)
Sau mỗi phiên làm việc hoặc khi hoàn thành một task, AI Agent **BẮT BUỘC** phải:
1. **Cập nhật tiến độ nhiệm vụ:** Đánh dấu hoàn thành/cập nhật trạng thái công việc trong [tasks/todo.md](file:///c:/Users/Hnam/Desktop/Simulate/tasks/todo.md) và [tasks/plan.md](file:///c:/Users/Hnam/Desktop/Simulate/tasks/plan.md).
2. **Tạo/Báo cáo Work Log chi tiết:** Báo cáo tóm tắt phiên làm việc bao gồm các thông tin:
   - **Nhiệm vụ đã thực hiện:** Mục tiêu và các bước đã hoàn thành.
   - **Danh sách file đã sửa/tạo mới:** Đính kèm đường dẫn dạng clickable link `[filename](file:///...)`.
   - **Kết quả kiểm tra (Verification):** Kết quả biên dịch `dotnet build` và kiểm thử `dotnet test`.
   - **Các vấn đề tồn đọng / `NEEDS_VERIFY`:** Những điều cần lưu ý hoặc xác minh thêm trong phiên tiếp theo.

---

## 7. Quy tắc Git, Commit & Security

1. **Conventional Commits:** `feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`.
2. **Bảo mật:** Không commit API keys, credentials, binary logs không cần thiết.
3. **Phê duyệt:** Chỉ commit/push khi có chỉ thị từ người dùng.

---

## 8. Definition of Done (DoD)

Task được coi là HOÀN THÀNH khi:
1. `dotnet build` thành công 0 Error, 0 Warning.
2. `dotnet test` vượt qua 100% tests (nếu có).
3. Đúng kiến trúc MVVM (`CommunityToolkit.Mvvm`), tách biệt View và Hardware Logic.
4. Cập nhật tiến độ vào [tasks/todo.md](file:///c:/Users/Hnam/Desktop/Simulate/tasks/todo.md) và [tasks/plan.md](file:///c:/Users/Hnam/Desktop/Simulate/tasks/plan.md).
5. Ghi nhận Work Log tóm tắt công việc đã làm và kết quả verification.
6. Không vi phạm bất kỳ quy tắc UI hay User Boundary nào.
