---
trigger: always_on
---

# Mandatory Reporting Rule (Lưu trữ báo cáo thay đổi)

1. Sau mỗi lần thực hiện thay đổi, chỉnh sửa hoặc cập nhật nội dung dự án, bắt buộc phải lưu lại chi tiết các yêu cầu và nội dung thay đổi dưới dạng file `.txt` vào đồng thời cả 3 thư mục:
   - `D:\Analysis Log\report\` (báo cáo chi tiết từng mục thay đổi)
   - `D:\Analysis Log\newconvert\` (change log theo định dạng `change_log_YYYY-MM-DD.txt`)
   - `d:\Automotive dev\Simulate\report\` (thư mục report trực tiếp trong workspace dự án)

2. **Nội dung và định dạng báo cáo bắt buộc tuân thủ `.agents/rules/report-standards.md`**:
   - Header tiêu chuẩn (metadata, người thực hiện, thời gian, tiêu chuẩn đối chiếu ISO/Vector).
   - Danh sách tất cả các file đã tạo, sửa đổi hoặc xóa.
   - Mô tả ngắn gọn và chi tiết mục đích, logic của từng thay đổi.
   - Kết quả kiểm tra, build (0 warning, 0 error) và xác minh logic / test cases (100% PASS).
   - Xác nhận bảo vệ giao diện UI (0% thay đổi XAML / controls).
   - Các điểm lưu ý kỹ thuật hoặc việc cần theo dõi tiếp theo.

3. **Lưu trữ Session & Conversation Transcripts**:
   - Sao lưu toàn bộ conversation transcript (`.jsonl`) và hình ảnh đính kèm vào `conversation_archives/` tại cả `D:\Analysis Log\conversation_archives\` và `d:\Automotive dev\Simulate\report\conversation_archives\`.