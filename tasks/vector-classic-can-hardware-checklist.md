# Vector Classic CAN Hardware Verification Checklist

## Phạm vi

Checklist này xác minh Task 4 trên Vector hardware thật qua `ICanGatewaySession`.
Nó không yêu cầu và không cho phép sửa UI/XAML. UI hiện tại chưa gọi frame I/O mới;
phép thử hardware phải dùng test harness/debug harness gọi trực tiếp
`VectorHardwareService.OpenGatewaySessionAsync`.

## Nguồn kỹ thuật đã đối chiếu

- `Doc/XL_Driver_Library_Manual_EN.pdf`, version 20.30:
  - pp. 43-49: open port V3, activate, flush RX và `xlReceive`;
  - pp. 75-76: `XLevent`, `chanIndex`, timestamp và tag data;
  - pp. 79-80: Classic CAN calling sequence;
  - pp. 90-94: `xlCanTransmit`, flush TX, standard/extended ID và DLC 0..8.
- `Doc/XLDriver.txt`: chữ ký wrapper `XL_Receive`, `XL_CanTransmit`,
  `XL_FlushReceiveQueue`, `XL_CanFlushTransmitQueue`.
- `Doc/XLClass.txt` và `Doc/XLDefine.txt`: `xl_event`, `xl_can_msg`,
  `XL_RECEIVE_MSG`, `XL_TRANSMIT_MSG`, `XL_CAN_EXT_MSG_ID` và message flags.
- DLL ứng dụng: `Simulate/lib/vxlapi_NET.dll` version `25.20.14.0`.

## Verification không cần hardware

| Mục | Trạng thái | Bằng chứng |
|---|---|---|
| Classic mở port bằng interface V3 và combined RX/TX mask | PASS | Unit test qua fake Vector SDK |
| RX/TX khác physical channel | PASS | `CanGatewayOptions` validation + test |
| RX hai phía, standard/extended ID và payload | PASS | Unit tests qua `ICanGatewaySession` |
| TX chọn đúng destination mask | PASS | Unit tests RX=`0x1`, TX=`0x2` |
| Typed receive/transmit/flush errors giữ native status | PASS | Unit tests status `XL_Status` |
| Flush RX queue và TX queue trên combined mask | PASS | Unit test combined mask `0x3` |
| Caller cancellation và stop pending receive | PASS | Unit tests hữu hạn thời gian |
| Build/test/UI diff | PASS | Ghi lại kết quả cuối trong `tasks/todo.md` |

## Điều kiện bench bắt buộc

- [ ] `NEEDS_VERIFY` Vector device và Vector XL Driver đúng version được nhận diện.
- [ ] `NEEDS_VERIFY` Hai CAN physical channel riêng biệt, không trùng channel index/mask.
- [ ] `NEEDS_VERIFY` Bitrate, termination và transceiver mode đúng với bench.
- [ ] `NEEDS_VERIFY` Chỉ thử trên bench an toàn; không inject vào xe hoặc mạng an toàn đang vận hành.
- [ ] `NEEDS_VERIFY` Có CAN analyzer/peer node để phát và quan sát frame ở cả hai phía.

## Trình tự test hardware

1. [ ] `NEEDS_VERIFY` Discover device; ghi device name, RX/TX channel index và mask.
2. [ ] `NEEDS_VERIFY` Mở Classic session ở `500000` bit/s; xác nhận V3, port hợp lệ,
   full init permission cho combined mask và activate thành công.
3. [ ] `NEEDS_VERIFY` Từ phía RX phát standard frame `0x123` payload
   `AA BB CC`; session phải trả `Source=Rx`, đúng ID và payload.
4. [ ] `NEEDS_VERIFY` Từ phía TX phát extended frame `0x18DAF110` payload
   `01 02 03 04`; session phải trả `Source=Tx`, giữ extended ID và payload.
5. [ ] `NEEDS_VERIFY` Gọi `TransmitAsync(Tx, ...)`; analyzer phía TX phải thấy đúng
   ID, DLC và payload, không xuất hiện ở physical RX do chọn sai mask.
6. [ ] `NEEDS_VERIFY` Gọi `TransmitAsync(Rx, ...)`; analyzer phía RX phải thấy đúng
   ID, DLC và payload, không xuất hiện ở physical TX do chọn sai mask.
7. [ ] `NEEDS_VERIFY` Gọi `FlushAsync`; xác nhận RX queue không còn stale event và
   transmit queue của cả hai mask được xử lý không lỗi.
8. [ ] `NEEDS_VERIFY` Hủy receive token khi queue rỗng; receive phải kết thúc hữu hạn,
   không treo thread gọi.
9. [ ] `NEEDS_VERIFY` Gọi `StopAsync` rồi `DisposeAsync` lặp lại; xác nhận cleanup theo
   thứ tự deactivate → close port → close driver và không còn handle/port mở.
10. [ ] `NEEDS_VERIFY` Lặp tối thiểu 50 chu kỳ open → RX/TX → flush → stop; không tăng
    handle, không có `XL_ERR_INVALID_PORTHANDLE` hoặc trạng thái cleanup bất thường.
11. [ ] `NEEDS_VERIFY` Ghi latency/CPU của receive loop và đối chiếu timestamp hardware;
    đây là phép đo runtime, không được suy ra từ fake SDK.

## Thu thập bằng chứng

- Vector device/driver version:
- RX channel/index/mask:
- TX channel/index/mask:
- Nominal bitrate:
- `XL_Status` bất thường:
- Analyzer trace/log không chứa credential hoặc dữ liệu nhạy cảm:
- Kết quả cuối: `PASS` / `FAIL` / `NEEDS_VERIFY`

## Cleanup khi test thất bại

Luôn gọi `StopAsync` và `DisposeAsync`. Nếu app/harness mất quyền điều khiển, dừng phát
frame từ analyzer, ngắt bench an toàn, đóng port bằng Vector tooling và ghi lại native
status trước khi thử lại. Không đánh dấu PASS nếu chỉ có kết quả từ fake SDK.
