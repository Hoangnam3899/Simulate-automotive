---
trigger: always_on
---

# Vector Hardware & MITM Engine Rules

## 1. Vector XL Driver API
- **CAN Classic**: Dùng `VectorCANManager` với API V3 (`XL_INTERFACE_VERSION_V3`).
- **CAN FD**: Dùng `VectorCANFdManager` với API V4 (`XL_INTERFACE_VERSION_V4`), cấu hình dual bitrate (`XLcanFdConf`).
- **CH1 Luôn Listen-Only**: Khi thiết lập MITM, cổng nhận CH1 bắt buộc cấu hình `XL_OUTPUT_MODE_SILENT` để tránh làm nhiễu mạng CAN trên xe thật.
- **Thứ tự dừng kết nối**: Phải dừng background worker thread $\rightarrow$ Flush phần cứng $\rightarrow$ Đóng TX port $\rightarrow$ Đóng RX port.

## 2. Real-time Gateway & Timer Resolution
- Khi khởi động `MitmEngine`, bắt buộc gọi `timeBeginPeriod(1)` từ `winmm.dll` và gọi `timeEndPeriod(1)` khi kết thúc để timer đạt độ phân giải ~1ms.
- Phải duy trì độ trễ Pass-through $< 1\text{ms}$ và Inject $< 3\text{ms}$.
- Lọc Echo/Loopback trong khoảng thời gian `ECHO_TIMEOUT_MS = 10ms` để bảo vệ độ chính xác của Live Monitor.

## 3. E2E Protection & Signal Bit-Packing
- Khi can thiệp sửa đổi tín hiệu (Inject), phải tuân thủ chuẩn bit-packing:
  - Intel: LSB-first.
  - Motorola: Chuẩn Vector DBC (MSB-first với thuật toán bước nhảy byte `MotorolaNextBit`).
- Nếu message có E2E: Sau khi ghi đè tín hiệu, bắt buộc gọi `E2EHelper.ApplyE2E()` để tính lại CRC8 SAE J1850 và tăng Alive Counter trước khi phát ra bus.
