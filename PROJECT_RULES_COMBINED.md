# BỘ LUẬT DỰ ÁN — AUTOMOTIVE MULTI-VENDOR AGENT WORKFLOW

> Quy tắc bắt buộc cho AI coding agent làm việc trong dự án PCAN + Vector/CANoe.
>
> Bộ luật này kết hợp:
>
> - Lifecycle phát triển, quality gate, TDD, security, CI/CD và review đa chiều từ bộ `addyosmani/agent-skills`.
> - Requirement grilling, domain modeling, test seam, codebase design, chẩn đoán lỗi sâu và review tách `Spec`/`Standards` từ bộ `mattpocock/skills`.
>
> Mục tiêu không phải tạo thêm thủ tục, mà là giúp agent **hiểu đúng domain, thay đổi đúng phạm vi, chứng minh được runtime behavior và bảo vệ tài sản hiện hữu**.

---

## 0. Cấu hình dự án

| Khóa | Mô tả | Giá trị |
|---|---|---|
| `<NGÔN_NGỮ_GIAO_TIẾP>` | Ngôn ngữ giao tiếp và tài liệu | Tiếng Việt |
| `<NGÔN_NGỮ_CODE>` | Ngôn ngữ lập trình chính | Python, C# |
| `<FRAMEWORK_UI>` | UI framework chuẩn | C#: `MaterialDesignInXAML`; Python: `PyQt-Fluent-Widgets` |
| `<FRAMEWORK_UI_PYTHON_CHUẨN>` | Nguồn UI Python cục bộ | `UI_Frameworks/PyQt-Fluent-Widgets` |
| `<THƯ_MỤC_SKILL>` | Nguồn skill chuẩn duy nhất | `.agents/skills/` |
| `<THƯ_MỤC_SPEC>` | Design specification | `Docs/design-specs/` |
| `<THƯ_MỤC_TEST_SPEC>` | Test specification | `Docs/test-specs/` |
| `<FILE_TRẠNG_THÁI>` | Trạng thái và Work Log | `Docs/PROJECT_STATUS.md` |
| `<FILE_TỪ_VỰNG>` | Ubiquitous Language | `CONTEXT.md` |
| `<THƯ_MỤC_ADR>` | Architecture Decision Records | `Docs/adr/` |
| `<TÀI_LIỆU_XƯƠNG_SỐNG>` | Nguồn kỹ thuật chính thức | Header/tài liệu đúng phiên bản runtime của từng vendor |
| `<NGUỒN_PARITY>` | Nguồn đối chiếu hành vi | PEAK examples trong `Docs/`; Vector Python trong `Docs/Vendor_Vector/python/` |
| `<NHÁNH_CHÍNH>` | Nhánh được bảo vệ | `main` |
| `<DỰ_ÁN_CŨ_READ_ONLY>` | Nguồn tham khảo tuyệt đối không sửa | `D:\Analysis Log\Dec\newcode` |

### 0.1. Nguồn sự thật và thứ tự ưu tiên

Khi các nguồn mâu thuẫn, áp dụng thứ tự:

1. **Yêu cầu hiện tại đã được chủ dự án phê duyệt.**
2. **Header, ABI/API và tài liệu chính thức đúng phiên bản runtime của vendor.**
3. **Design-spec, test-spec và ADR đã được phê duyệt.**
4. **Tiêu chuẩn chính thức phù hợp với phạm vi thay đổi.**
5. **Runtime/hardware evidence có provenance rõ ràng.**
6. **Test tự động, log và artifact tái lập được.**
7. **`CONTEXT.md` và contract lõi của dự án.**
8. **Nguồn parity/read-only.**
9. **Code hiện tại, comment, tên hàm và trí nhớ của agent.**

Không dùng code parity, comment hoặc “hành vi có vẻ giống” để ghi đè tài liệu chính thức.

### 0.2. Tuyên ngôn dự án

- **Domain-First:** hiểu đúng thuật ngữ và ranh giới trước khi thiết kế.
- **Spec-First:** không phát triển logic đáng kể khi chưa biết chính xác phải xây gì.
- **Test-First:** thay đổi behavior phải có cách chứng minh trước khi được coi là hoàn thành.
- **Source-Driven:** quyết định vendor/API/standard phải dựa trên nguồn chính thức.
- **Evidence-Driven:** “code tồn tại” không đồng nghĩa “runtime hoạt động”.
- **Incremental:** triển khai theo vertical slice nhỏ, test được, rollback được.
- **Human-Controlled:** agent không tự quyết định thay đổi safety behavior, interface lõi, test acceptance hoặc hardware execution.

---

## 1. Thứ tự áp dụng rule

Khi có xung đột, ưu tiên:

1. Yêu cầu trực tiếp mới nhất của chủ dự án.
2. Quy tắc safety, bảo mật, dữ liệu và bảo vệ tài sản.
3. `PROJECT_RULES.md`.
4. `AGENTS.md` và adapter dành cho tool hiện tại.
5. Skill điều phối chính của task.
6. Skill hỗ trợ.
7. Convention cục bộ của module.
8. Thói quen hoặc mặc định của agent.

Skill là workflow bắt buộc, nhưng không được ghi đè rule dự án, nguồn vendor hoặc approval gate.

---

## 2. Session Bootstrap và phân loại task

Áp dụng cho mọi phiên mới và mọi agent mới.

### 2.1. Bootstrap tối thiểu bắt buộc

Trước khi sửa file hoặc chạy hành động có tác động:

1. Đọc `Docs/AGENT_BOOTSTRAP.md`.
2. Đọc `AGENTS.md`.
3. Đọc `PROJECT_RULES.md`.
4. Đọc `.agents/skills/using-agent-skills/SKILL.md`.
5. Đọc `<FILE_TRẠNG_THÁI>` đủ để biết trạng thái hiện tại và blocker.
6. Kiểm tra Git branch và working tree.
7. Phân loại task.
8. Chọn **một skill điều phối chính**.
9. Chỉ đọc skill hỗ trợ khi workflow chính thật sự cần.
10. Đọc source/spec tối thiểu liên quan đến phạm vi task.

Không dựa vào trí nhớ từ phiên trước.

### 2.2. Ba cấp task

#### Cấp T0 — Trivial

Ví dụ:

- Sửa typo.
- Chỉnh format tài liệu.
- Đổi một chuỗi hiển thị không ảnh hưởng logic.
- Cập nhật link hoặc comment trong phần đang sửa.

Workflow:

```text
Bootstrap tối thiểu
→ đọc file đích
→ sửa nhỏ
→ verify liên quan
→ kết thúc
```

Không chạy full spec, TDD, architecture review hoặc scan toàn dự án.

#### Cấp T1 — Standard Engineering

Ví dụ:

- Sửa bug đã rõ nguyên nhân.
- Thêm logic trong một module.
- Thêm test.
- Chỉnh UI có phạm vi rõ.
- Thay đổi adapter không ảnh hưởng public contract.

Workflow:

```text
Spec/acceptance hiện có
→ plan ngắn
→ TDD/incremental implementation
→ targeted verification
→ code review
```

#### Cấp T2 — Complex, Cross-cutting hoặc Safety-sensitive

Ví dụ:

- Requirement chưa rõ.
- Thay đổi interface/architecture.
- Thay đổi driver behavior.
- Timing, concurrency, reconnect, recovery.
- Fault injection.
- CAN signal/frame interpretation.
- Hardware/HIL/vehicle test.
- Thay đổi có thể ảnh hưởng bằng chứng hoặc tiêu chuẩn.

Workflow:

```text
grill-with-docs
→ domain-modeling
→ spec-driven-development
→ planning-and-task-breakdown
→ test seams + test-spec
→ incremental implementation
→ TDD
→ E2E/runtime verification
→ two-axis code review
→ human approval/release
```

### 2.3. Chọn skill không chồng chéo

- Mặc định chỉ có **một skill điều phối chính**.
- Tối đa hai skill hỗ trợ đồng thời, trừ khi plan ghi rõ lý do.
- Không cài hoặc kích hoạt hai skill có cùng trigger.
- Không chạy toàn bộ lifecycle cho task T0.
- Không tự động kích hoạt browser/web performance cho desktop/native tool.
- Không để skill auto-build vượt qua approval gate.

---

## 3. Skill Router chuẩn

| Tình huống | Skill điều phối chính | Skill hỗ trợ |
|---|---|---|
| Không rõ user thật sự cần gì | `interview-me` | `grill-with-docs` nếu domain phức tạp |
| Cần xây dựng thuật ngữ/domain | `grill-with-docs` | `domain-modeling` |
| Đã trao đổi đủ, cần tạo spec | `spec-driven-development` | `domain-modeling`, `source-driven-development` |
| Có spec, cần chia task | `planning-and-task-breakdown` | `wayfinder` nếu vượt một session |
| Bắt đầu code behavior | `incremental-implementation` | `test-driven-development` |
| Tra SDK/API/vendor | `source-driven-development` | skill vendor tương ứng |
| Thiết kế module/interface | `api-and-interface-design` | `codebase-design` |
| Build/test fail thông thường | `debugging-and-error-recovery` | Không dùng chẩn đoán sâu ngay |
| Bug khó, không ổn định | `diagnosing-bugs` | `source-driven-development` |
| Chỉnh UI | `frontend-ui-engineering` | `pcan-python-ui`, skill behavior liên quan |
| Chuẩn bị merge | `code-review` | `security-and-hardening` khi có external input |
| Công việc vượt một session | `wayfinder` | `handoff` |
| Chuyển agent hoặc context | `handoff` | Không tự tạo implementation mới |

### 3.1. Quy tắc hợp nhất TDD

Chỉ sử dụng skill chuẩn:

```text
test-driven-development
```

Workflow bắt buộc:

```text
RED → GREEN → REFACTOR
```

Bổ sung nguyên tắc từ Matt Pocock:

- Test qua **public seam**, không test private implementation.
- Xác định seam cần test trước khi viết test cho task T1/T2.
- Expected value phải đến từ requirement, worked example hoặc nguồn độc lập.
- Một vòng chỉ xử lý một vertical slice.
- Không viết hàng loạt test cho behavior còn tưởng tượng.
- Không dùng mock internal collaborator khi fake hoặc real implementation phù hợp hơn.

Không giữ một skill `tdd` thứ hai có trigger tương đương.

### 3.2. Quy tắc hợp nhất Specification

Skill chuẩn:

```text
spec-driven-development
```

Bắt buộc bổ sung:

- Thuật ngữ từ `CONTEXT.md`.
- ADR liên quan.
- Nguồn requirement.
- Assumption được ghi rõ.
- Public seam và evidence dự kiến.
- Out-of-scope.
- Approval gate.
- Standard/vendor reference.
- Mapping requirement → implementation → verification.

`to-spec` chỉ được dùng khi cuộc trao đổi đã đủ rõ; không dùng nó để lấp chỗ trống bằng giả định.

### 3.3. Quy tắc hợp nhất Code Review

Skill chuẩn:

```text
code-review
```

Review luôn tách thành hai trục:

1. **Specification Compliance**
2. **Engineering Standards**

Engineering Standards gồm:

- Correctness.
- Readability and Simplicity.
- Architecture.
- Security.
- Performance.

Không gộp hai trục thành một điểm tổng hợp khiến code đẹp che lấp implementation sai requirement.

---

## 4. Vòng đời phát triển theo cổng

### Cổng 0 — Intake và Domain Alignment

Áp dụng khi task T2 hoặc requirement chưa rõ.

Bắt buộc:

- Xác định actor, mục tiêu và điều kiện thành công.
- Tách requirement khỏi giải pháp mà user đang hình dung.
- Làm rõ thuật ngữ mơ hồ.
- Đối chiếu thuật ngữ với `CONTEXT.md`.
- Kiểm tra code hiện tại có mâu thuẫn với mô tả không.
- Ghi assumption và câu hỏi chưa giải quyết.
- Không viết code.

Output:

- Cập nhật `CONTEXT.md` nếu domain term được chốt.
- ADR chỉ khi quyết định khó đảo ngược, có trade-off và sẽ khó hiểu nếu thiếu bối cảnh.

### Cổng A — Spec-First

Tạo design-spec tại `<THƯ_MỤC_SPEC>`.

Design-spec tối thiểu phải có:

1. Problem statement.
2. Objective và non-objective.
3. Requirement source và provenance.
4. Domain vocabulary.
5. Existing behavior/baseline.
6. Proposed behavior.
7. Module/interface bị ảnh hưởng.
8. State, timing và concurrency.
9. Success path.
10. Error, timeout, reconnect, cancellation và cleanup.
11. Edge cases.
12. Vendor/standard reference.
13. Security/data concerns.
14. Compatibility và migration.
15. Out-of-scope.
16. Acceptance criteria.
17. Verification/evidence strategy.
18. Approval gates.
19. `NEEDS_VERIFY` items.

**Đóng băng implementation** cho đến khi spec đủ để tạo test-spec.

### Cổng B — Test-First

Tạo test-spec tại `<THƯ_MỤC_TEST_SPEC>`.

Bắt buộc xác định:

- Public seam.
- Test level: unit/component/integration/SIL/HIL/manual.
- Preconditions.
- Inputs và nguồn dữ liệu.
- Trigger.
- Expected state/output.
- Error/recovery behavior.
- Timing tolerance.
- Cleanup.
- Evidence phải lưu.
- Pass/fail criteria.
- Requirement trace ID.

Test mix phải phù hợp rủi ro; không áp dụng cứng tỷ lệ web `80/15/5`.

**Đóng băng implementation** cho đến khi test strategy đủ chứng minh acceptance criteria.

### Cổng C — Implementation

Chỉ bắt đầu khi:

- Spec/test-spec đã tồn tại hoặc task T0/T1 có acceptance criteria rõ.
- Branch hợp lệ.
- Approval gate không bị vi phạm.
- Agent đã xác định vertical slice đầu tiên.

Thực hiện:

```text
Một task nhỏ
→ RED
→ GREEN tối thiểu
→ REFACTOR có kiểm soát
→ targeted test
→ commit checkpoint khi được phép
→ task tiếp theo
```

Không triển khai toàn bộ feature rồi mới test.

### Cổng D — Runtime Verification

Không kết luận “done” chỉ vì unit test xanh.

Phải xác minh theo chuỗi:

```text
Input/Config/UI
→ Model/Contract
→ Service
→ Adapter/Worker
→ Vendor Runtime
→ Output/Event/Log
→ Test Evidence
```

### Cổng E — Two-Axis Review

#### Axis 1 — Specification Compliance

Kiểm tra:

- Missing requirement.
- Partial implementation.
- Wrong behavior.
- Scope creep.
- Acceptance criteria chưa được chứng minh.
- Test hoặc evidence thiếu.
- Timing/recovery sai.
- Spec/test/implementation mismatch.

#### Axis 2 — Engineering Standards

Kiểm tra:

- Correctness.
- Readability.
- Architecture.
- Security.
- Performance.
- Dependency discipline.
- Dead code.
- Resource lifecycle.
- Logging/observability.
- Vendor boundary.
- UI protection.

### Cổng F — Ship, Sync và Lock

Trước khi báo hoàn thành:

- Targeted test pass.
- Regression liên quan pass.
- Work Log cập nhật.
- Status cập nhật.
- Evidence path tồn tại.
- Deviation và `NEEDS_VERIFY` còn lại được ghi rõ.
- Không push/merge/release nếu chưa được user yêu cầu.

---

## 5. Domain Modeling và `CONTEXT.md`

`CONTEXT.md` là glossary/domain model, không phải spec.

Nó chỉ chứa:

- Thuật ngữ chuẩn.
- Ý nghĩa.
- Quan hệ domain.
- Ranh giới giữa các concept.
- Từ bị cấm hoặc dễ gây hiểu nhầm.
- Ví dụ ngắn cần thiết để phân biệt nghĩa.

Không chứa:

- File path implementation.
- Chi tiết class/function.
- Task tạm thời.
- Decision kỹ thuật.
- TODO.

Khi user dùng thuật ngữ xung đột:

- Agent phải chỉ ra ngay.
- Không âm thầm chọn một nghĩa.
- Sau khi chốt, cập nhật `CONTEXT.md` tại thời điểm đó.

Mọi tên module, API, biến, test và tài liệu phải dùng vocabulary đã thống nhất.

---

## 6. Source-Driven Development và vendor boundaries

### 6.1. PCAN

Nguồn ưu tiên:

1. Header/API chính thức đúng phiên bản PCAN runtime.
2. Tài liệu PEAK trong `Docs/Vendor_PCAN/` hoặc `Docs/`.
3. Ví dụ PEAK dùng để hiểu usage.
4. Code hiện tại.
5. Parity source.

### 6.2. Vector

Nguồn ưu tiên:

1. `vxlapi.h` khớp DLL/runtime.
2. ABI export và constant đúng runtime.
3. `Docs/Vendor_Vector/docs/XL_Driver_Library_Manual_EN.pdf` cho semantics.
4. Vector sample/reference.
5. `Docs/Vendor_Vector/python/` để đối chiếu hành vi, read-only.
6. Code hiện tại.

### 6.3. Không trộn vendor semantics

Cấm:

- Dùng PCAN behavior để suy diễn Vector.
- Dùng Vector masks/handles/status làm contract lõi.
- Đưa native struct lên UI hoặc Signal Monitor.
- Tạo một “universal adapter” bằng cách union mọi khái niệm vendor.
- Fallback âm thầm từ hardware vendor sang Mock.

### 6.4. Quy tắc đọc tài liệu

Khi quyết định phụ thuộc SDK/API:

- Tra nguồn chính thức trước.
- Ghi version/runtime đang áp dụng.
- Trích vị trí hoặc symbol liên quan trong spec/work log.
- Phân biệt `VERIFIED`, `INFERENCE`, `NEEDS_VERIFY`.
- Không tuyên bố đã verify tài liệu chưa đọc.

---

## 7. Kiến trúc PCAN + Vector

### 7.1. Một codebase chính

- PCAN và Vector cùng tồn tại trong repository/worktree chính.
- Vector bổ sung, không thay PCAN.
- Không tạo worktree vendor song song nếu chưa được yêu cầu.
- Mọi slice Vector phải chạy regression PCAN liên quan.

### 7.2. Ranh giới chuẩn hóa

Vendor-specific được phép tồn tại trong:

```text
Discovery
→ Open/Close
→ Native Configuration
→ Native RX/TX
→ Error translation
```

Trước khi đi vào phần dùng chung, phải chuẩn hóa thành contract lõi, ví dụ:

```text
HardwareChannelInfo
BusConfig
RawFrame
RuntimeStatus
DecodedSignalEvent
LogEvent
```

Các module dùng chung không được đọc trực tiếp:

- Native handle.
- Access mask.
- Vendor struct.
- DLL symbol.
- Vendor error code.
- Vendor-specific channel index.

### 7.3. Session lifecycle

Mọi session phải định nghĩa:

```text
Created
→ Configured
→ Opening
→ Active
→ Stopping
→ Closed
```

Và trạng thái lỗi/recovery nếu cần.

Bắt buộc có:

- Idempotent cleanup.
- Cancellation.
- Timeout.
- Thread/resource ownership.
- Reconnect policy.
- Typed error.
- Không nuốt exception.
- Không để UI sở hữu native resource.

### 7.4. Mixed-vendor session

- Mặc định có thể giới hạn một vendor cho mỗi session.
- Mixed PCAN + Vector đồng thời phải có spec riêng.
- Nếu chưa hỗ trợ, trả typed rejection.
- Không giả lập mixed session bằng Mock mà không thông báo.

---

## 8. Testing Rules

### 8.1. Test behavior, không test implementation

Test phải quan sát qua public seam.

Tốt:

```text
Open session
→ inject configuration
→ receive normalized frame
→ verify status/event
```

Không tốt:

```text
Assert private method X gọi private method Y đúng 3 lần
```

### 8.2. Xác nhận seam

Với task T1/T2, trước test đầu tiên phải ghi seam:

```markdown
## Test Seams
- DriverAdapter.open()
- SimulationSession.start()
- FaultInjectionService.activate()
- NormalizedFrameStream
```

Seam mới phải được biện minh. Ưu tiên seam hiện có.

### 8.3. RED phải thật sự đỏ

- Test mới phải fail đúng lý do.
- Bug fix phải có test tái hiện bug trước khi sửa khi điều kiện cho phép.
- Nếu không thể tạo automated reproduction, ghi rõ lý do và evidence thay thế.
- Test pass ngay không chứng minh bug/feature.

### 8.4. Expected result độc lập

Không tính expected bằng cùng logic với implementation.

Nguồn expected hợp lệ:

- Requirement.
- Vendor specification.
- Worked example.
- Known-good fixture.
- Captured hardware evidence có provenance.

### 8.5. Vertical slice

Không viết tất cả test trước rồi toàn bộ implementation.

Mỗi vòng:

```text
Một behavior
→ một test đỏ
→ implementation tối thiểu
→ refactor
→ verify
```

### 8.6. Mock discipline

Ưu tiên:

1. Real implementation phù hợp.
2. Fake.
3. Stub.
4. Interaction mock.

Chỉ mock external boundary khó kiểm soát. Không mock chi tiết nội bộ để ép test pass.

### 8.7. Hardware evidence

Test cần thiết bị thật phải ghi:

- Thiết bị.
- Firmware/driver/DLL version.
- Channel.
- Baud rate.
- DBC/config.
- Timestamp source.
- Test operator.
- Raw log.
- Expected và actual.
- Điều kiện môi trường.
- `NEEDS_VERIFY` nếu chưa chạy thật.

Một lần chạy thành công không tự động chứng minh mọi scenario hoặc toàn bộ tiêu chuẩn.

---

## 9. Debugging Rules

### 9.1. `debugging-and-error-recovery`

Dùng cho lỗi thường ngày:

1. Reproduce.
2. Localize.
3. Reduce.
4. Fix root cause tối thiểu.
5. Add guard/regression test.
6. Verify.

Không sửa hàng loạt dựa trên stack trace chưa hiểu.

### 9.2. `diagnosing-bugs`

Chỉ dùng khi:

- Bug không ổn định.
- Không tái hiện đơn giản.
- Race condition.
- Performance regression.
- Lỗi phụ thuộc driver/hardware.
- Fix đầu tiên thất bại.
- Nhiều hypothesis cạnh tranh.

Loop:

```text
Reproduce
→ Minimise
→ Hypothesise
→ Instrument
→ Collect evidence
→ Falsify
→ Fix
→ Regression test
```

Phân loại mọi kết luận:

- `CONFIRMED ROOT CAUSE`
- `EVIDENCE-BACKED INFERENCE`
- `HYPOTHESIS`
- `NEEDS_VERIFY`

Không ghi “root cause” nếu chỉ có tương quan.

### 9.3. Stop-the-line

Dừng thay đổi tiếp nếu:

- UI/asset bị phá.
- Data/evidence bị ghi đè.
- Native resource leak.
- Build/test baseline thay đổi ngoài phạm vi.
- Source read-only bị ghi.
- Agent không biết runtime version.
- Requirement safety/timing mâu thuẫn.

---

## 10. UI và tài sản hiện hữu

### 10.1. Không tự ý thay UI

Nếu task chỉ liên quan:

- Backend.
- Service.
- Worker.
- Data model.
- Spec/test-spec.
- Driver.
- Runtime logic.

Thì không được sửa UI/asset nếu user không yêu cầu.

### 10.2. UI Python

Khi chỉnh UI Python:

1. Đọc `.agents/skills/pcan-python-ui/SKILL.md`.
2. Đọc `frontend-ui-engineering`.
3. Tra `<FRAMEWORK_UI_PYTHON_CHUẨN>`.
4. Ưu tiên `qfluentwidgets`.
5. Giữ layout PCAN 5-zone nếu không có yêu cầu redesign.
6. Tách backend/service khỏi widget.
7. UI chỉ gọi application service/public contract.

Không tự phát minh control/style nếu framework đã có pattern tương đương.

### 10.3. Cấm

- Ghi đè cả file UI để sửa một phần.
- Thay UI thật bằng placeholder.
- Phục dựng UI theo phỏng đoán.
- Xóa asset không do agent tạo.
- Truy cập native driver trực tiếp từ UI component.
- Dùng trạng thái hiển thị như bằng chứng duy nhất rằng frame đã TX/RX.

### 10.4. Khi UI bị phản ánh là hỏng

- Dừng mọi write tiếp.
- Kiểm tra Git history, file gốc hoặc mẫu user.
- Xác định phạm vi bị ảnh hưởng.
- Đề xuất khôi phục có căn cứ.
- Không ghi đè thêm để “chữa nhanh”.

---

## 11. End-to-End Traceability

### 11.1. Chuỗi bắt buộc

Mọi behavior runtime phải truy được:

```text
Standard/Vendor Source
→ Requirement
→ Design
→ Test Spec
→ Implementation
→ Runtime Caller
→ Output/Log
→ Evidence
```

### 11.2. Các lỗi phải chủ động tìm

- `PARSE_BUT_UNUSED`
- `CONFIG_NOT_WIRED`
- `HELPER_NOT_CALLED`
- `UI_NO_BACKEND_BINDING`
- `DECLARED_NOT_EXECUTED`
- `MISSING_RUNTIME_WIRING`
- `SPEC_IMPLEMENTATION_MISMATCH`
- `TEST_DOES_NOT_PROVE_REQUIREMENT`
- `VENDOR_CONTRACT_LEAK`
- `EVIDENCE_MISSING`

### 11.3. Ba lớp xác minh

Không đánh dấu `OK` nếu chưa có:

1. Static existence.
2. Runtime-path wiring.
3. Behavior evidence.

### 11.4. Trạng thái chuẩn

- `OK` — đủ trace và evidence.
- `PARTIAL` — có implementation nhưng thiếu wiring/evidence.
- `BUG` — behavior sai.
- `NEEDS_VERIFY` — chưa đủ dữ liệu, thiết bị hoặc nguồn.
- `BLOCKED` — không thể tiếp tục vì thiếu approval/source/runtime.

---

## 12. Code Review chuẩn

Output tối thiểu:

```markdown
## Specification Compliance

### Missing or partial requirements
### Incorrect behavior
### Scope creep
### Verification gaps

## Engineering Standards

### Correctness
### Readability and Simplicity
### Architecture
### Security
### Performance

## Runtime Trace Review

### Wiring gaps
### Resource lifecycle
### Logging and evidence

## Findings

| Severity | File/Area | Finding | Evidence | Required Action |
|---|---|---|---|---|
```

Severity:

- `Critical` — data loss, safety/security issue, broken behavior, resource corruption.
- `Required` — phải sửa trước merge.
- `Optional` — cải thiện có giá trị nhưng không bắt buộc.
- `Nit` — style nhỏ.
- `FYI` — thông tin.

Quy tắc:

- Review test trước implementation.
- Repo standards ghi thành văn bản thắng heuristic chung.
- Tooling-enforced style không cần agent lặp lại.
- Findings phải chỉ vị trí và bằng chứng.
- Khi chỉ ra vấn đề kiến trúc, phải đề xuất restructuring cụ thể.
- Không rubber-stamp.
- Không sửa ngoài phạm vi trong lúc review nếu chưa được yêu cầu.

---

## 13. Tiêu chuẩn và compliance

### 13.1. Xác định chuẩn phù hợp

Trước task T2, xác định chuẩn hoặc hướng dẫn liên quan, chẳng hạn:

- ISO/IEC/IEEE.
- ISO 26262.
- Automotive SPICE.
- ASAM.
- AUTOSAR.
- MISRA.
- CERT.
- IETF.
- OWASP.
- WCAG.

Không gắn nhãn tiêu chuẩn chỉ để tăng độ trang trọng.

### 13.2. Truy vết

Spec/test-spec phải ghi:

```text
standard/principle
→ project requirement
→ implementation mechanism
→ verification/evidence
```

### 13.3. Không overclaim

Chỉ dùng:

- `COMPLIANT`
- `CERTIFIED`
- “tuân thủ/chứng nhận”

khi có phạm vi, tiêu chí, review và evidence đầy đủ.

Nếu mới áp dụng nguyên tắc:

- `ALIGNED WITH`
- `INFORMED BY`
- `NEEDS_VERIFY`

### 13.4. Deviation

Nếu lệch chuẩn hoặc chưa verify:

- Mô tả deviation.
- Lý do.
- Ảnh hưởng.
- Mitigation.
- Approval owner.
- Kế hoạch đóng gap.

---

## 14. Approval Gates và Human-in-the-Loop

### 14.1. Agent được tự làm

- Đọc file trong workspace.
- Đọc source vendor read-only.
- Phân tích requirement.
- Tạo plan/spec/test-spec.
- Tạo hoặc sửa test trong phạm vi được phép.
- Build.
- Format phần đã sửa.
- Chạy targeted test.
- Chạy static analysis.
- Tạo review report.
- Cập nhật Work Log.
- Tạo branch local khi cần và repository sạch.

### 14.2. Phải hỏi trước

- Xóa file.
- Ghi đè UI/asset.
- Cài hoặc nâng dependency.
- Sửa environment/toolchain/config dùng chung.
- Thay public API.
- Thay adapter/core contract.
- Thay DBC semantics.
- Thay CAN ID, scaling, endian hoặc invalid value.
- Thay timing tolerance.
- Thay fault activation/recovery.
- Thay safety-related behavior.
- Thay test acceptance criteria.
- Chạy hardware/HIL/vehicle operation.
- Gửi code/dữ liệu ra dịch vụ ngoài.
- Commit nếu user chưa yêu cầu.
- Push, mở PR, merge, tag hoặc release.

### 14.3. Act Mode

Act Mode chỉ có hiệu lực trong phạm vi user cho phép.

Nó không tự động cho phép:

- Push.
- Merge.
- Release.
- Hardware execution.
- Xóa dữ liệu.
- Thay đổi safety requirement.
- Vượt qua source/approval gate.

---

## 15. Quy tắc đọc file và quản lý context

### 15.1. Đọc tối thiểu nhưng đủ chứng minh

Được đọc:

- File user chỉ định.
- File đang sửa.
- Import/caller trực tiếp.
- Spec/test liên quan.
- Vendor source cần để xác minh.
- `CONTEXT.md`/ADR liên quan.

Không được scan recursive toàn workspace chỉ để “hiểu dự án”.

### 15.2. Khi nào được mở rộng

Được mở rộng có mục tiêu khi:

- Cần truy caller/runtime path.
- Cần xác định public seam.
- Cần tìm contract tương đương.
- Cần đánh giá impact.
- Cần xác minh không còn reference.
- Task T2 đã có plan điều tra.

Ghi rõ lý do trước khi mở rộng đáng kể.

### 15.3. Context engineering

- Nạp đúng phần spec/source theo task.
- Không đưa toàn bộ docs vào context nếu không cần.
- Khi context dài, tạo handoff thay vì tiếp tục mất kiểm soát.
- Dùng vocabulary ngắn, nhất quán từ `CONTEXT.md`.
- Không lặp lại nguồn đã được ghi trong spec.

---

## 16. Bảo vệ nguồn read-only

`<DỰ_ÁN_CŨ_READ_ONLY>` là read-only tuyệt đối.

Cấm:

- Chạy/import.
- Build/test.
- Formatter/linter.
- Tạo cache.
- Tạo `.pyc`, `__pycache__`, log hoặc artifact.
- Sửa metadata.
- Rename/move/delete.
- Dùng làm working directory.

Nếu cần thử nghiệm:

- Tái hiện tối thiểu trong workspace hiện tại.
- Tạo fixture mới.
- Không sao chép implementation nguyên khối.
- Chỉ đối chiếu behavior.

`Docs/Vendor_Vector/` mặc định read-only, ngoại trừ README/provenance do dự án quản lý. Runtime không được load DLL/Python trực tiếp từ thư mục tài liệu.

---

## 17. Git Workflow

### 17.1. Branch first

Không commit trực tiếp lên `<NHÁNH_CHÍNH>`.

Tên branch:

```text
feat/<ten-tinh-nang>
fix/<ten-loi>
refactor/<pham-vi>
docs/<noi-dung>
chore/<cong-viec>
```

Trước sửa:

- Kiểm tra branch.
- Kiểm tra uncommitted changes.
- Không ghi đè thay đổi của người khác.

### 17.2. Commit

- Conventional Commits.
- Một commit là một checkpoint logic.
- Tách refactor lớn khỏi behavior change.
- Không commit generated artifact không cần thiết.
- Chỉ commit khi user yêu cầu hoặc policy task cho phép rõ.

### 17.3. Push/release

Không push, mở PR, merge, tag hoặc release khi chưa có yêu cầu rõ.

---

## 18. Security và dependency discipline

- Không commit `.env`, key, token, credential, OEM confidential data, DBC/log proprietary.
- External input luôn là untrusted.
- Validate tại system boundary.
- Không log secret hoặc raw sensitive payload không cần thiết.
- Ưu tiên standard library và dependency sẵn có.
- Trước dependency mới phải kiểm tra:
  - Có thật sự cần không.
  - Maintenance.
  - License.
  - Known vulnerability.
  - Runtime compatibility.
  - Native/packaging impact.
- Ghim version.
- Dùng môi trường cô lập.
- Không cài global.
- Không tải script rồi pipe trực tiếp vào shell.

---

## 19. Coding Principles

- Code rõ ràng hơn code “thông minh”.
- Abstraction phải giảm concept, không chỉ chuyển complexity.
- Không thêm feature ngoài scope.
- Không thêm compatibility shim khi không có requirement.
- Không comment phần không sửa.
- Không để TODO thay cho behavior bắt buộc.
- Không nuốt error.
- Validate ở boundary, không rải validate vô nghĩa.
- Public interface phải nhỏ, rõ và test được.
- Feature-specific logic nằm trong module sở hữu domain.
- Shared core không chứa vendor branching không cần thiết.
- Không thêm dependency để tránh viết vài dòng code rõ ràng.
- Không tối ưu khi chưa đo.
- Không tự redesign toàn kiến trúc vì một bug cục bộ.

---

## 20. Documentation, ADR và Work Log

### 20.1. Tài liệu

Cập nhật khi thay đổi liên quan:

- Architecture.
- Public interface.
- Runtime workflow.
- Vendor integration.
- Test process.
- Deployment/release.
- Domain vocabulary.

### 20.2. ADR

Chỉ tạo ADR khi đồng thời:

1. Khó đảo ngược.
2. Có trade-off thật.
3. Người đọc tương lai sẽ khó hiểu nếu thiếu lý do.

ADR không thay thế spec.

### 20.3. Work Log bắt buộc

Mọi thay đổi có chủ đích so với baseline phải ghi vào `<FILE_TRẠNG_THÁI>`.

Mỗi mục có:

1. Baseline cũ.
2. Vấn đề quan sát được.
3. Root cause hoặc mức độ chắc chắn.
4. Quyết định và lý do.
5. Nguồn/spec/standard chi phối.
6. Nội dung thay đổi.
7. Phần giữ nguyên.
8. Impact và risk.
9. Rollback/deviation.
10. Verification/evidence.
11. `NEEDS_VERIFY` còn lại.

Work Log phải trả lời:

- Vì sao đổi?
- Nguyên nhân thực sự là gì?
- Đã đổi gì và giữ gì?
- Bằng chứng nào cho thấy behavior mới đúng hơn?
- Còn gì chưa chứng minh?

---

## 21. Definition of Done

Không dùng “done” nếu thiếu bất kỳ mục bắt buộc nào.

### Task T0

- File đúng.
- Format/compile phù hợp.
- Không ảnh hưởng ngoài phạm vi.

### Task T1

- Acceptance criteria đạt.
- Targeted test pass.
- Regression liên quan pass.
- Review không còn `Critical`/`Required`.
- Work Log cập nhật khi behavior thay đổi.

### Task T2

- Domain term đã rõ.
- Spec và test-spec được truy vết.
- Source vendor/standard được ghi.
- Test seam rõ.
- Implementation theo vertical slice.
- Unit/component/integration phù hợp pass.
- E2E/runtime trace đủ.
- Hardware evidence hoặc `NEEDS_VERIFY` rõ.
- Two-axis review hoàn tất.
- Không còn blocker.
- Approval gate được đáp ứng.
- Status/Work Log cập nhật.

### Báo cáo cuối task

Phải nói rõ:

- Đã thay đổi gì.
- File nào.
- Skill đã áp dụng.
- Test/verification đã chạy.
- Kết quả.
- Bước bị bỏ qua.
- Risk hoặc `NEEDS_VERIFY`.
- Có commit/push hay không.

---

## 22. Anti-patterns bị cấm

- Vibe coding khi requirement còn mơ hồ.
- Đọc toàn repo không mục tiêu.
- Cài toàn bộ skill cho mọi task.
- Dùng hai skill TDD song song.
- Viết code trước test-spec cho task T2.
- Test private implementation.
- Tạo test tautological.
- Mock mọi dependency.
- Giả định UI hiển thị đồng nghĩa hardware đã chạy.
- Parse config nhưng không wire runtime.
- Tạo helper nhưng không có caller.
- Ghi “parity” mà không trace behavior.
- Dùng comment làm evidence.
- Tự tuyên bố ISO compliant.
- Sửa source read-only.
- Thay UI thật bằng placeholder.
- Silent fallback sang Mock.
- Bỏ qua failed test để pipeline xanh.
- Push/merge/release ngoài approval.
- Viết abstraction cho nhu cầu chưa tồn tại.
- Mở rộng scope vì “tiện thể”.
- Rubber-stamp code review.

---

## 23. Cheat Sheet

| Tình huống | Phản ứng bắt buộc |
|---|---|
| Typo/tài liệu nhỏ | T0, sửa và verify tối thiểu |
| Requirement mơ hồ | `interview-me` hoặc `grill-with-docs`; chưa code |
| Thuật ngữ không thống nhất | `domain-modeling`; cập nhật `CONTEXT.md` |
| Feature mới | Spec → test-spec → implementation |
| API/vendor chưa chắc | `source-driven-development`; tra nguồn chính thức |
| Logic mới | RED → GREEN → REFACTOR qua public seam |
| Build/test fail | `debugging-and-error-recovery` |
| Bug khó/race/performance | `diagnosing-bugs` |
| UI không nằm trong yêu cầu | Không sửa UI |
| Review PR/branch | Spec Compliance + Engineering Standards |
| Timing/fault/safety thay đổi | Dừng và xin approval |
| Hardware chưa chạy | Giữ `NEEDS_VERIFY` |
| Công việc vượt một session | `wayfinder` + `handoff` |
| Chuẩn bị kết thúc | Verify → review → Work Log → status |

---

## 24. Ghi chú nguồn skill

Bộ rule này giả định `.agents/skills/` đã được hợp nhất có chọn lọc từ:

- `mattpocock/skills`
- `addyosmani/agent-skills`

Các skill phải giữ attribution và license tương ứng trong `.agents/SOURCE-ATTRIBUTION.md` và `.agents/licenses/`.

Không cập nhật hai upstream trực tiếp vào project mà không chạy lại:

- Overlap analysis.
- Conflict resolution.
- Link/dependency validation.
- Scenario validation.
- Review thủ công các thay đổi workflow.

---

**Tóm tắt vận hành:**

```text
Hiểu domain
→ Chốt requirement
→ Viết spec
→ Xác định test seam và test-spec
→ Triển khai vertical slice bằng TDD
→ Trace runtime End-to-End
→ Review Spec + Standards
→ Lưu evidence và Work Log
→ Chỉ ship khi đã qua approval
```
