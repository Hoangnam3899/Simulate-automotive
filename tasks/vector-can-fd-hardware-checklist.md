# Vector CAN FD Hardware Verification Checklist

## Phạm vi

Checklist này xác minh Task 5 trên Vector hardware thật qua `ICanGatewaySession`.
Nó không yêu cầu và không cho phép sửa UI/XAML. UI hiện tại chưa gọi frame I/O mới;
phép thử hardware phải dùng test harness/debug harness gọi trực tiếp
`VectorHardwareService.OpenGatewaySessionAsync`.

## Nguồn kỹ thuật đã đối chiếu

- `Doc/XL_Driver_Library_Manual_EN.pdf`, version 20.30, luồng CAN FD interface V4
  tại pp. 103-104 và lifecycle/queue APIs liên quan.
- `Doc/XLDriver.txt`: chữ ký wrapper `XL_CanTransmitEx`, `XL_CanReceive`,
  `XL_FlushReceiveQueue` và `XL_CanFlushTransmitQueue`.
- `Doc/XLClass.txt`: `XLcanTxEvent`, `XL_CAN_TX_MSG`, `XLcanRxEvent`,
  `XL_CAN_EV_RX_MSG` và buffer payload 64 byte.
- `Doc/XLDefine.txt`: V4, RX/TX event tags, EDL/BRS/ESI/RTR/error flags,
  `XL_CAN_QUEUE_OVERFLOW`, extended-ID bit và DLC `0..15`.
- DLL ứng dụng: `Simulate/lib/vxlapi_NET.dll` version `25.20.14.0`.

## Verification không cần hardware

| Mục | Trạng thái | Bằng chứng |
|---|---|---|
| FD mở port bằng interface V4 và forward nominal/data bitrate | PASS | Contract tests qua fake Vector SDK |
| Protocol mode mặc định là ISO CAN FD (`options=0`) | PASS | Contract test khóa mode ISO; adapter switch map ISO sang `0` |
| DLC `0..15` ánh xạ `0..8,12,16,20,24,32,48,64` | PASS | 16 golden test rows |
| FD TX giữ standard/extended ID, EDL/BRS, DLC, payload và destination mask | PASS | Contract tests qua `ICanGatewaySession` |
| FD RX giữ source, standard/extended ID, EDL/BRS, DLC và payload | PASS | Contract tests qua `ICanGatewaySession` |
| Classic frame đi qua V4 không bị gắn EDL/BRS | PASS | TX/RX contract tests |
| Native RX/TX error, queue overflow và DLC sai trả typed failure | PASS | Fault-path tests |
| Flush, caller cancellation và stop pending receive | PASS | Contract tests hữu hạn thời gian |
| Classic V3 regression | PASS | Toàn bộ Classic test suite vẫn đạt |
| Build/test/UI diff | PASS | Ghi kết quả checkpoint cuối trong `tasks/todo.md` |

## Điều kiện bench bắt buộc

- [ ] `NEEDS_VERIFY` Vector device và XL Driver đúng version được nhận diện.
- [ ] `NEEDS_VERIFY` Hai CAN physical channel riêng biệt, không trùng channel index/mask.
- [ ] `NEEDS_VERIFY` Peer/analyzer hỗ trợ ISO CAN FD mặc định (`options=0`). Nếu bench
  yêu cầu legacy non-ISO, không đổi ngầm; phải mở quyết định/configuration riêng.
- [ ] `NEEDS_VERIFY` Nominal bitrate, data bitrate, sample point, termination và transceiver mode khớp bench.
- [ ] `NEEDS_VERIFY` Chỉ thử trên bench an toàn; không inject vào xe hoặc mạng an toàn đang vận hành.
- [ ] `NEEDS_VERIFY` Analyzer ghi được BRS, EDL, extended ID, DLC và payload ở cả hai phía.

## Trình tự test hardware

1. [ ] `NEEDS_VERIFY` Discover device; ghi device name, RX/TX channel index và mask.
2. [ ] `NEEDS_VERIFY` Mở FD session nominal `500000` bit/s, data `2000000` bit/s;
   xác nhận V4, full init permission cho combined mask, cấu hình và activate thành công.
3. [ ] `NEEDS_VERIFY` Từ phía RX phát FD standard frame `0x123`, BRS bật, DLC 9
   với payload 12 byte; session phải trả `Source=Rx`, đúng format/BRS/DLC/payload.
4. [ ] `NEEDS_VERIFY` Từ phía TX phát FD extended frame `0x18DAF110`, BRS bật,
   DLC 15 với payload 64 byte; session phải trả `Source=Tx` và giữ extended ID.
5. [ ] `NEEDS_VERIFY` Lặp RX với các DLC biên 0, 8, 9, 14, 15; độ dài quan sát
   phải lần lượt là 0, 8, 12, 48, 64 byte.
6. [ ] `NEEDS_VERIFY` Gọi `TransmitAsync(Tx, ...)` với FD+BRS; analyzer phía TX
   phải thấy đúng EDL/BRS, ID, DLC và payload, không phát nhầm physical RX.
7. [ ] `NEEDS_VERIFY` Gọi `TransmitAsync(Rx, ...)` với FD không BRS; analyzer phía RX
   phải thấy EDL bật, BRS tắt và đúng destination mask.
8. [ ] `NEEDS_VERIFY` Trên cùng V4 session, RX/TX một Classic frame DLC 8;
   analyzer và session phải thấy EDL/BRS tắt, không đổi hành vi Classic.
9. [ ] `NEEDS_VERIFY` Gọi `FlushAsync`; xác nhận RX queue không còn stale event và
   transmit queue của combined mask được flush không lỗi.
10. [ ] `NEEDS_VERIFY` Hủy receive token khi queue rỗng; receive phải kết thúc hữu hạn,
    không treo thread gọi.
11. [ ] `NEEDS_VERIFY` Gọi `StopAsync` rồi `DisposeAsync` lặp lại; xác nhận cleanup
    deactivate → close port → close driver và không còn handle/port mở.
12. [ ] `NEEDS_VERIFY` Lặp tối thiểu 50 chu kỳ open → RX/TX → flush → stop; không tăng
    handle, không có queue overflow hoặc trạng thái cleanup bất thường.
13. [ ] `NEEDS_VERIFY` Đối chiếu timestamp/latency và CPU receive loop trên runtime;
    không suy ra timestamp hardware từ fake SDK.

## Thu thập bằng chứng

- Vector device/driver version:
- RX channel/index/mask:
- TX channel/index/mask:
- Nominal/data bitrate và CAN FD ISO mode:
- Sample point/termination/transceiver mode:
- `XL_Status` bất thường:
- Analyzer trace/log không chứa credential hoặc dữ liệu nhạy cảm:
- Kết quả cuối: `PASS` / `FAIL` / `NEEDS_VERIFY`

## Cleanup khi test thất bại

Luôn gọi `StopAsync` và `DisposeAsync`. Nếu app/harness mất quyền điều khiển, dừng phát
frame từ analyzer, ngắt bench an toàn, đóng port bằng Vector tooling và ghi lại native
status trước khi thử lại. Không đánh dấu PASS nếu chỉ có kết quả từ fake SDK.
