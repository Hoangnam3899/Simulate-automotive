---
trigger: always_on
---

# UI Protection Rule (Bảo toàn giao diện)

1. **Tuyệt đối không thay đổi bất kỳ thành phần nào trên UI (XAML, Layout, Styling, Controls, Colors, Margins)** khi chưa được người dùng cho phép rõ ràng.
2. Khi thực hiện các yêu cầu liên quan đến logic, thuật toán, CAN/UDS backend, chỉ gắn kết (wire/hook) tối thiểu vào ViewModel / Event Handler hiện có.
3. Không tự ý tái cấu trúc giao diện (refactor/redesign) trong bất kỳ tình huống nào.
4. Nếu người dùng phản ánh hoặc phát hiện UI bị lệch/lỗi: dừng ngay lập tức mọi chỉnh sửa tiếp theo và khôi phục về trạng thái ban đầu.
