# Workspace Agent Rules — Strict Enforcement

> **BẮT BUỘC VỚI MỌI PHIÊN LÀM VIỆC DÙ Ở BẤT CỨ HOÀN CẢNH NÀO:**
> Antigravity và tất cả AI agents khi hoạt động trong workspace này **BẮT BUỘC** phải tuân thủ nghiêm ngặt các điều khoản sau:

1. **ĐỌC LUẬT ĐẦU MỖI PHIÊN:**
   Trước khi thực hiện bất kỳ phản hồi, phân tích hay thay đổi code nào, Agent PHẢI đọc các tệp quy tắc:
   - `AGENTS.md`
   - `CLAUDE.md`
   - `GEMINI.md`
   - `PROJECT_RULES_COMBINED.md`

2. **ĐỌC & ÁP DỤNG SKILL ĐẦU MỖI PHIÊN:**
   Agent PHẢI tra cứu và đọc skill tương ứng tại `.agents/skills/` (bắt đầu bằng `.agents/skills/using-agent-skills/SKILL.md` hoặc `.agents/SKILL-CATALOG.md`) phù hợp với loại công việc trước khi triển khai.

3. **BẢO VỆ UI TỰ NHIÊN (`user_global`):**
   KHÔNG ĐƯỢC THAY ĐỔI BẤT KỲ CÁI GÌ Ở TRÊN UI (XAML / Controls / Layout / Design) khi chưa có sự cho phép trực tiếp từ người dùng.

4. **VERIFICATION & WORK LOG:**
   - Sau mỗi phiên/task làm việc, bắt buộc cập nhật tiến độ vào `tasks/todo.md` và `tasks/plan.md`.
   - Chạy `dotnet build` để đảm bảo ứng dụng biên dịch thành công 0 warning / 0 error.
