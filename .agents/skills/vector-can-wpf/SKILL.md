---
name: vector-can-wpf
description: Chuyên sâu cho dự án CAN Bus MITM Simulator / CANForge WPF (.NET Framework 4.8, C# 7.3). Hướng dẫn quy chuẩn Vector XL Driver API (V3/V4), ICANBusManager, MitmEngine (Pass-through/Inject/Block + E2E CRC8 SAE J1850 + Alive Counter), UDS Diagnostics (ISO 14229/15765-2), DBC Parser, Signal Encoder (Intel/Motorola), và bảo toàn UI WPF.
---

# Vector CAN / CAN FD WPF Simulator & UDS Diagnostic Tool

## ⚠️ QUY TẮC BẤT DI BẤT DỊCH (NON-NEGOTIABLE)

1. **BẢO TOÀN GIAO DIỆN (UI PRESERVATION)**:
   - **KHÔNG ĐƯỢC THAY ĐỔI BẤT KỲ THỨ GÌ TRÊN UI (XAML/Layout/Style/Controls)** khi chưa được người dùng cho phép rõ ràng.
   - Khi sửa backend/logic: chỉ hook tối thiểu vào sự kiện hoặc binding hiện có, tuyệt đối không tự ý sắp xếp lại layout.

2. **LƯU BÁO CÁO THAY ĐỔI (MANDATORY REPORTING)**:
   - Sau mỗi lần sửa đổi hoặc hoàn thành task, **bắt buộc** phải ghi lại chi tiết các thay đổi vào file `.txt` trong thư mục `D:\Analysis Log\report\` (và file change log `change_log_YYYY-MM-DD.txt` trong `D:\Analysis Log\newconvert\`).
   - Báo cáo phải nêu rõ: danh sách file đã tạo/sửa, mục đích, chi tiết logic đã đổi, trạng thái build và verify.

3. **CHUẨN NGÔN NGỮ C# 7.3 (.NET Framework 4.8)**:
   - Không dùng syntax của C# 8+: KHÔNG switch expression (`x switch { ... }`), KHÔNG target-typed new (`new()`), KHÔNG range operator (`..`), KHÔNG nullable reference types (`string?`).
   - Sử dụng switch statement truyền thống, explicit new `new List<string>()`, explicit array/list indexing.

---

## 🛠 KIẾN TRÚC HỆ THỐNG (ARCHITECTURE MAP)

```
TreeViews and Value Converters/
├── [Entry & Quản trị]
│   ├── App.xaml / App.xaml.cs                 # Khởi động, kiểm tra License RSA (LicenseHelper)
│   ├── MainWindow.xaml / MainWindow.xaml.cs   # Quản lý phần cứng Vector (CAN/CAN FD), điều phối Sub-form
│   ├── ICANBusManager.cs                      # Abstraction layer chung cho CAN Classic & CAN FD
│   └── LocalizationManager.cs / LocExtension  # Đa ngôn ngữ (Lang/*.json: vi, en, zh, ko, ar)
│
├── [Hardware Managers]
│   ├── VectorCANManager.cs                    # CAN cổ điển (V3 API, 8-byte, ISO-TP, UDS, MITM)
│   └── VectorCANFdManager.cs                  # CAN FD (V4 API, 64-byte payload, dual bitrate, ISO-TP FD)
│
├── [Mô phỏng MITM & Tín hiệu]
│   ├── SIMULATE.xaml / SIMULATE.xaml.cs       # Form Simulate chính (DBC Explorer, TX List, Live Monitor, Value Setup)
│   ├── MitmEngine.cs                          # Real-time Gateway (Pass-through / Inject / Block)
│   ├── DbcParser.cs / DbcModels.cs            # Parser DBC (BU_, BO_, SG_, VAL_)
│   ├── SignalEncoder.cs                       # Bit-packing Intel (LSB) & Motorola (Vector convention)
│   ├── E2EHelper.cs                           # E2E CRC8 SAE J1850 + Alive Counter (CAPL-equivalent)
│   └── SimulatorModels.cs                     # Model dữ liệu TxMessageModel, SignalItemModel, GatewayMode
│
└── [Chẩn đoán & Quản lý ECU]
    ├── DIAG.xaml / DIAG.xaml.cs               # UDS Sequence, Tester Present (0x3E), Security Access
    ├── DID.xaml / DID.xaml.cs                 # Đọc DID, đối soát BOM, so khớp VIN
    ├── DTC.xaml / DTC.xaml.cs                 # Đọc/Xóa DTC đa ECU, chuẩn J2012, Freeze Frame
    ├── DATA_ECU.xaml / DATA_ECU.xaml.cs       # Quản trị cấu hình ECU dự án (JSON)
    └── SeedKeyHelper.cs / SystemDidHelper.cs  # Nạp DLL tính Seed-Key & tra cứu DID
```

---

## ⚡ CÁC QUY CHUẨN KỸ THUẬT QUAN TRỌNG

### 1. Vector XL Driver API (V3 vs V4)
- **CAN Classic**: Dùng `VectorCANManager` với `XL_INTERFACE_VERSION_V3`, sự kiện `xl_event`.
- **CAN FD**: Bắt buộc dùng `VectorCANFdManager` với `XL_INTERFACE_VERSION_V4`, sự kiện `XLcanRxEvent`, struct `XLcanFdConf` với dual bitrate (Nominal BPS & Data BPS).
- **CH1 Luôn Listen-Only trong MITM**: Kênh nhận CH1 phải cấu hình `XL_OUTPUT_MODE_SILENT` để tránh phát ACK làm nhiễu bus của xe thật.
- **Thứ tự đóng kênh an toàn**:
  `Dừng background thread` $\rightarrow$ `Flush hardware queue` $\rightarrow$ `Đóng TX Channel` $\rightarrow$ `Đóng RX Channel`.

### 2. MitmEngine Real-time Gateway
- **Độ phân giải Timer 1ms**: Bắt buộc dùng `timeBeginPeriod(1)` từ `winmm.dll` khi khởi động Engine và `timeEndPeriod(1)` khi dừng để đảm bảo `Thread.Sleep(1)` đạt chu kỳ ~1ms thay vì 15.6ms của Windows.
- **Xử lý 3 Mode cho từng Frame**:
  - `PassThrough`: Forward nguyên vẹn sang CH2 với độ trễ $< 1\text{ms}$.
  - `Inject`: Giải mã signal bằng `SignalEncoder`, ghi đè giá trị override, tính lại E2E CRC8 + Alive Counter, sau đó phát ra CH2.
  - `Block`: Chặn frame, tăng counter `DroppedFrames`.
- **Lọc Echo/Loopback**: Caching hash và timestamp trong 10ms (`ECHO_TIMEOUT_MS`) để Live Monitor không nhận nhầm frame do chính tool phát ra.

### 3. Bit-Packing Signal (SignalEncoder)
- **Intel (Little-Endian, ByteOrder = 1/0 theo DBC)**: `LSB-first`
- **Motorola (Big-Endian, ByteOrder = 0/1 theo DBC)**: Phải tuân thủ chuẩn Vector DBC bit-numbering (`MotorolaNextBit`: bit giảm dần trong byte, khi hết byte nhảy sang MSB byte kế tiếp `currentBit + 15`).
- Xử lý mở rộng dấu âm (Sign extension) cho tín hiệu có dấu (`ValueType == '-'`).

### 4. E2E Protection (E2EHelper)
- Thuật toán: **CRC8 SAE J1850** (Polynomial `0x1D`, Init `0xFF`, Final XOR `0xFF`).
- Khớp 100% với CAPL script `Checksum_AliveCounter_XGW.can`.
- Alive Counter được tăng trước, ghi vào `CounterByte` với `CounterMask`, sau đó toàn dải `[CrcStart..CrcEnd]` được tính CRC và ghi vào `ChecksumByte`.

### 5. Giao thức Chẩn đoán UDS & ISO-TP
- ISO-TP tự động xử lý Single Frame (SF), First Frame (FF), Consecutive Frame (CF), Flow Control (FC).
- Với CAN FD: Single Frame $> 7$ bytes dùng PCI 2 bytes `[0x00][Length]`.
- Giải mã DTC chuẩn J2012: Phân tích 3 byte sang tiền tố P/C/B/U và mã HEX 4 ký tự + 2 ký tự subtype (VD: `P0123-45`).

---

## 📋 WORKFLOW TRƯỚC VÀ SAU KHI CODE

### Trước khi bắt đầu:
1. Đọc lại quy tắc dự án và các tài liệu kỹ thuật liên quan trong thư mục `Docs/`.
2. Kiểm tra `ICANBusManager` và xác định phạm vi ảnh hưởng (CAN Classic hay CAN FD hay cả hai).
3. Tuyệt đối không thay đổi layout XAML nếu không có chỉ định từ người dùng.

### Sau khi hoàn thành:
1. Build kiểm tra mã nguồn (không có lỗi cú pháp / warning nghiêm trọng).
2. Tạo file báo cáo chi tiết vào `D:\Analysis Log\report\` và `D:\Analysis Log\newconvert\`.
3. Báo cáo trạng thái rõ ràng: `OK` / `PARTIAL` / `NEEDS_VERIFY`.
