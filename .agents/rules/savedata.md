---
trigger: always_on
---

# Mandatory Reporting Rule (Lưu trữ báo cáo thay đổi)

1. Sau mỗi lần thực hiện thay đổi, chỉnh sửa hoặc cập nhật nội dung dự án, bắt buộc phải lưu lại chi tiết các yêu cầu và nội dung thay đổi dưới dạng file `.txt` vào thư mục:
   - `D:\Analysis Log\report\` (báo cáo chi tiết từng mục thay đổi)
   - `D:\Analysis Log\newconvert\` (change log theo định dạng `change_log_YYYY-MM-DD.txt`)

2. **Nội dung báo cáo bắt buộc bao gồm**:
   - Danh sách tất cả các file đã tạo, sửa đổi hoặc xóa.
   - Mô tả ngắn gọn và chi tiết mục đích, logic của từng thay đổi.
   - Kết quả kiểm tra, build và xác minh logic (OK / PARTIAL / NEEDS_VERIFY).
   - Các điểm lưu ý kỹ thuật hoặc việc cần theo dõi tiếp theo.