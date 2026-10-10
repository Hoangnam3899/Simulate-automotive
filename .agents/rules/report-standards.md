---
trigger: always_on
---

# Quy Chuẩn Viết Báo Cáo & Lưu Trữ Dự Án Simulate (Report & Archiving Standards)

> **BẮT BUỘC ÁP DỤNG TRONG MỌI PHIÊN LÀM VIỆC SAU KHI HOÀN THÀNH BẤT KỲ THAY ĐỔI, SỬA LỖI, REVIEW HOẶC THÊM TÍNH NĂNG/TEST CASES:**

## 1. Nguyên Tắc Đồng Bộ Ba Vị Trí (Triple-Destination Synchronization)
Mọi báo cáo và nhật ký thay đổi bắt buộc phải được ghi nhận và đồng bộ đồng thời vào cả 3 thư mục sau:
1. `D:\Analysis Log\report\` — Chứa báo cáo chi tiết từng tính năng, lỗi, review, benchmark.
2. `D:\Analysis Log\newconvert\` — Chứa change log theo ngày (`change_log_YYYY-MM-DD.txt`).
3. `d:\Automotive dev\Simulate\report\` — Thư mục `report/` trong chính repository mã nguồn dự án.

## 2. Quy Chuẩn Đặt Tên Tệp Báo Cáo (Naming Conventions)
- **Báo cáo chi tiết:** `report_<chu_de>_<YYYY-MM-DD>.txt` (hoặc `review_<chu_de>_<YYYY-MM-DD>.txt`).
  * Ví dụ: `report_fix_freeze_crash_echo_storm_3000tcs_2026-10-10.txt`, `report_vector_hardware_buffer_overflow_resolution_2026-10-09.txt`.
- **Nhật ký thay đổi theo ngày:** `change_log_<YYYY-MM-DD>.txt`.
  * Ghi nhận tích lũy tất cả các mốc thay đổi diễn ra trong ngày đó.

## 3. Cấu Trúc Nội Dung Báo Cáo Bắt Buộc (Mandatory Report Structure)
Mỗi tệp báo cáo chi tiết bắt buộc phải có đủ 5 phần chuẩn hóa:

### Phần I: Header Tiêu Chuẩn (Metadata Header)
- Tiêu đề báo cáo viết hoa rõ ràng, súc tích.
- Thời gian thực hiện (YYYY-MM-DD HH:mm).
- Người / Agent thực hiện & Skill/Rule kích hoạt.
- Hệ thống / Nền tảng: Simulate (.NET 8.0 WPF, C# 12, Vector XL Driver).
- Tiêu chuẩn đối chiếu liên quan (ISO 11898, ISO 14229, Vector XL Manual, AUTOSAR E2E, Clean Architecture...).

### Phần II: Tổng Quan Vấn Đề / Yêu Cầu (Overview & Context)
- Mô tả chính xác thực trạng, hiện tượng thực tế trên phần cứng/phần mềm hoặc yêu cầu của người dùng.
- Nêu rõ các ranh giới kỹ thuật và lý do tại sao một số giải pháp không khả thi (ví dụ: lý do không dùng Listen-Only).

### Phần III: Phân Tích Nguyên Nhân Gốc Rễ & Logic (Root Cause & Technical Analysis)
- Truy vết luồng thực thi End-to-End: `Event/Input -> Data Model -> Service/Engine -> Vector XL Driver -> ViewModel/UI`.
- Nêu rõ nguyên nhân cốt lõi gây ra lỗi (tràn hàng đợi, bão echo, nghẽn UI Dispatcher, unhandled task exception...).

### Phần IV: Triển Khai & Sửa Đổi Mã Nguồn (Implementation Details)
- Danh sách tất cả các file đã tạo, sửa đổi hoặc xóa.
- Giải trình chi tiết logic mới, thuật toán áp dụng (ví dụ: bộ tra cứu O(1), cơ chế so khớp EchoWindow, safe teardown).

### Phần V: Kiểm Chuẩn Chất Lượng (Quality Gate Verification)
- Biên dịch: Lệnh `dotnet build Simulate.sln` -> Bắt buộc **0 Warning, 0 Error**.
- Kiểm thử: Lệnh `dotnet test Simulate.sln` -> Báo cáo tổng số test cases, số Passed (bắt buộc 100%), Failed (0), Skipped.
- Bảo vệ giao diện: Báo cáo rõ **0% thay đổi trên bất kỳ file UI/XAML nào** (tuân thủ `user_global` và `ui-protection.md`).
- Các điểm lưu ý kỹ thuật hoặc kế hoạch theo dõi tiếp theo.

## 4. Lưu Trữ Nhật Ký Hội Thoại & Hình Ảnh (Session & Media Archiving)
- Sau mỗi phiên làm việc lớn hoặc khi có yêu cầu đồng bộ:
  Toàn bộ Conversation Transcripts (`transcript_full.jsonl`, `transcript.jsonl`) và các ảnh người dùng tải lên (`.png`) phải được sao lưu vào:
  * `D:\Analysis Log\conversation_archives\session_<conversation_id>\`
  * `d:\Automotive dev\Simulate\report\conversation_archives\session_<conversation_id>\`
