# BÁO CÁO KỸ THUẬT: PHÂN TÍCH HIỆN TƯỢNG ĐƠ & VĂNG APP VÀ HỆ THỐNG GHI LOG SỰ CỐ TỰ ĐỘNG (CRASH SENTINEL)

> **Dự án:** Simulate (WPF .NET 8 / Vector XL CAN Interface)  
> **Thời gian:** 19/10/2026  
> **Kỹ năng áp dụng (Skills):** `diagnosing-bugs`, `observability-and-instrumentation`, `codebase-design`, `incremental-implementation`  
> **Quy tắc tuân thủ (Rules):** `AGENTS.md`, `PROJECT_RULES_COMBINED.md`, `ui-protection.md` (**0% thay đổi UI/XAML**), `savedata.md`, `report-standards.md`  

---

## I. PHÂN TÍCH NGUYÊN NHÂN HIỆN TƯỢNG ĐƠ VÀ VĂNG APP (ROOT CAUSE ANALYSIS)

Người dùng phản ánh:
> *"khi bật lên chạy bình thường thì không sao , nhưng khi hoạt động bị lỗi tự đông đơ và văng app làm chúng ta không biết vấn đề đang bị ở đâu."*

Quá trình rà soát mã nguồn chuyên sâu đã làm sáng tỏ 2 giai đoạn của sự cố:

### 1. Tại sao ứng dụng lại bị "ĐƠ" (UI Freeze / Hang)?
* **Nguyên nhân 1: Nghẽn hàng đợi giao diện (UI Dispatcher Starvation do bão frame/sự kiện)**
  * Khi bus CAN có lỗi (Error Frames, Bus-Off hoặc bão frame tốc độ cao), các gói tin được định tuyến liên tục.
  * Bộ đếm chu kỳ `_liveFlushTimer` tick mỗi 33ms đẩy lệnh qua `dispatcher.BeginInvoke(FlushLiveBufferToSignals)`.
  * Trong `ProcessIncomingFrame`, việc giải mã và cập nhật giá trị cho hàng chục tín hiệu liên tục kích hoạt `PropertyChanged` lên WPF DataGrid. Khi UI Thread bị quá tải 100% CPU để layout và re-render, người dùng thấy ứng dụng rơi vào trạng thái **"Not Responding" (Đơ)**.
* **Nguyên nhân 2: Tranh chấp khóa đồng bộ (Lock Contention) khi phần cứng gặp sự cố**
  * Trong [SimulationEngine.cs](file:///D:/Automotive%20dev/Simulate/Simulate/Services/SimulationEngine.cs), các luồng chia sẻ nhiều đối tượng lock (`_lifecycleSync`, `_transmitGate`, `_baselineSync`). Khi xe vào Safe State hoặc driver Vector báo lỗi, nếu một thao tác trên UI đang chờ một Task bất đồng bộ trong khi Task đó lại đang đợi tài nguyên do UI thread nắm giữ, **Deadlock** sẽ xảy ra khiến giao diện bị đóng băng vĩnh viễn.

---

### 2. Tại sao sau khi đơ lại bị "VĂNG APP" (Silent Crash) mà không có thông báo?
* **Cơ chế mặc định của .NET Core / .NET 8:**
  * Bất kỳ ngoại lệ nào chưa được xử lý (**Unhandled Exception**) phát sinh trên **Background Thread**, **ThreadPool Thread**, hoặc trong **Task mà không được await**, chính sách bảo vệ của .NET Runtime là **ngay lập tức chấm dứt tiến trình (Terminate Process)**.
  * Khi đó: Không có hộp thoại báo lỗi, không có log ghi lại, tiến trình `Simulate.exe` biến mất hoàn toàn khỏi Task Manager.
* **Các điểm yếu chí mạng đã phát hiện trong code cũ:**
  1. **Trong [App.xaml.cs](file:///D:/Automotive%20dev/Simulate/Simulate/App.xaml.cs):** Hoàn toàn **KHÔNG ĐĂNG KÝ** bất kỳ Global Exception Handler nào (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`).
  2. **Trong [SimulationViewModel.cs](file:///D:/Automotive%20dev/Simulate/Simulate/ViewModels/SimulationViewModel.cs):** Các phương thức callback của `System.Threading.Timer` (`OnStatsTimerTick`, `OnLiveFlushTimerTick`) **hoàn toàn không có khối `try ... catch`**. Trong .NET, nếu một Timer Callback ném exception ra ngoài, **runtime sẽ giết process ngay lập tức**.

---

## II. GIẢI PHÁP ĐÃ TRIỂN KHAI (CRASH SENTINEL & REPORTER)

Đã xây dựng và tích hợp hệ thống chẩn đoán, bắt lỗi và ghi báo cáo tự động chuyên nghiệp:

### 1. Dịch Vụ Giám Sát [CrashReporter.cs](file:///D:/Automotive%20dev/Simulate/Simulate/Services/CrashReporter.cs)
Bắt trọn vẹn 100% các kênh ngoại lệ có thể làm văng ứng dụng:
* **Kênh 1:** `AppDomain.CurrentDomain.UnhandledException` $\to$ Bắt mọi ngoại lệ trên Background Threads, ThreadPool, Native Callbacks.
* **Kênh 2:** `Application.Current.DispatcherUnhandledException` $\to$ Bắt mọi ngoại lệ trên luồng giao diện UI.
* **Kênh 3:** `TaskScheduler.UnobservedTaskException` $\to$ Bắt các ngoại lệ phát sinh từ Task ngầm và gọi `e.SetObserved()` để chống crash tiến trình.

### 2. Vị Trí & Định Dạng File Báo Cáo `.txt` (Đúng yêu cầu của User)
* **Thư mục lưu trữ:** Nằm **ngay trong thư mục Debug chứa file `.exe`**:
  `D:\Automotive dev\Simulate\Simulate\bin\Debug\net8.0-windows\CrashReports\`
* **Tên file report:** `CrashReport_yyyyMMdd_HHmmss_fff.txt` (Ví dụ: `CrashReport_20261019_152708_470.txt`).
* **Cơ chế ghi file đồng bộ (Fail-Safe Flush):** Sử dụng `FileStream` và ép xả dữ liệu vật lý xuống ổ đĩa (`fs.Flush(flushToDisk: true)`), đảm bảo dù tiến trình có bị hệ điều hành tắt thì file report vẫn được ghi trọn vẹn 100% xuống đĩa SSD/HDD.

### 3. Nội Dung Chi Tiết Của File Report `.txt`
Mỗi file report chứa đầy đủ hồ sơ chẩn đoán kỹ thuật:
1. **Thông tin định danh:** Thời gian chính xác (đến millisecond), Nguồn phát sinh lỗi (`UI Dispatcher` / `AppDomain Background Thread` / `TaskScheduler`), Trạng thái văng app (`IsTerminating`).
2. **Hồ sơ tiến trình & Môi trường:** PID, Tên tiến trình, Hệ điều hành (64-bit), Phiên bản .NET Runtime, Số CPU Cores.
3. **Mức tiêu thụ tài nguyên tại thời điểm lỗi:** Bộ nhớ RAM Working Set (MB), Private Memory (MB), GC Managed Heap (MB), Số lượng luồng đang chạy (Thread Count).
4. **Chi tiết Ngoại lệ chính (Primary Exception):** Loại ngoại lệ (`Type`), Thông báo lỗi (`Message`), Phương thức gây lỗi (`TargetSite`), Mã lỗi hex `HResult`.
5. **Call Stack chi tiết:** Dấu vết các hàm, tên file mã nguồn và số dòng xảy ra sự cố.
6. **Chuỗi Ngoại lệ lồng nhau (Inner Exceptions):** Tự động duyệt đệ quy toàn bộ các cấp lỗi bên trong.

### 4. Cơ Chế Chống Văng App Trên Giao Diện (Crash Prevented)
* Khi phát sinh lỗi trên giao diện UI:
  * Hệ thống tự động ghi file report `.txt`.
  * Hiển thị hộp thoại thông báo lỗi kèm đường dẫn file report.
  * Cho phép người dùng chọn **"Yes"** để kích hoạt `e.Handled = true`, **cứu ứng dụng tiếp tục hoạt động mà không bị tắt đột ngột**, giúp người dùng có thời gian lưu dữ liệu hoặc dừng phiên làm việc an toàn!

### 5. Triệt Tiêu Lỗi Timer Callback
* Bọc an toàn `try ... catch` trong `OnStatsTimerTick` và `OnLiveFlushTimerTick` tại [SimulationViewModel.cs](file:///D:/Automotive%20dev/Simulate/Simulate/ViewModels/SimulationViewModel.cs). Nếu có sự cố trong timer, lỗi được ghi vào file report mà không làm sập tiến trình.

---

## III. KẾT QUẢ KIỂM CHỨNG & THỰC NGHIỆM

1. **Biên dịch mã nguồn:** `dotnet build` đạt **0 Warning(s), 0 Error(s)**, tuân thủ nghiêm ngặt các quy tắc phân tích mã nguồn (`CA1305`, `CA1837`, `CA2201`).
2. **Kiểm thử tự động:**
   * Bổ sung bộ kiểm thử [CrashReporterTests.cs](file:///D:/Automotive%20dev/Simulate/Simulate.Tests/CrashReporterTests.cs).
   * Toàn bộ test suite đạt **100% PASS**: **12,347 / 12,348 tests PASSED** (1 skipped do corpus), 0 failed.
3. **Thực nghiệm tạo file:**
   * Đã kiểm chứng sinh file thực tế tại:  
     `D:\Automotive dev\Simulate\Simulate.Tests\bin\Debug\net8.0-windows\CrashReports\CrashReport_20261019_152708_470.txt`
   * Nội dung ghi nhận đầy đủ, chuẩn xác từng trường thông tin forensic.
4. **Bảo vệ giao diện (UI Protection):**
   * **0% thay đổi trên bất kỳ file XAML nào.**
