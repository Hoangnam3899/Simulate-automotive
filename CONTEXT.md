# Automotive CAN Simulation

This context models the static CAN-network definitions that drive automotive simulation. It keeps DBC terminology distinct from the runtime CAN frames carried through the hardware gateway.

## Language

**DBC document**:
A network-description file containing the nodes, messages, and signals known to one CAN network.
_Avoid_: CAN log, frame trace

**Node**:
An ECU or other named participant declared by a DBC document.
_Avoid_: channel, gateway side

**Message**:
A DBC-defined CAN payload identified by a normalized CAN identifier, payload length, and transmitter.
_Avoid_: signal, runtime frame

**Signal**:
A named bit field within one DBC message, with bit layout and physical conversion metadata.
_Avoid_: message field, payload byte

**Raw signal value**:
The signed or unsigned integer represented directly by a signal's payload bits before physical conversion.
_Avoid_: physical value, displayed value

**Physical signal value**:
The engineering value obtained from a raw signal value through its declared factor and offset.
_Avoid_: raw value, encoded bits

**Normalized CAN identifier**:
The CAN identifier value without the DBC extended-identifier flag, paired with an explicit extended-identifier state.
_Avoid_: raw DBC identifier

**Parse issue**:
A line-specific diagnostic emitted while reading an external DBC document; it is either an error that invalidates the document or a warning about an unrepresented construct.
_Avoid_: hardware failure

**E2E protection**:
Per-message payload protection made of a checksum and an alive counter; it is distinct from the CAN link-layer CRC.
_Avoid_: CAN CRC, complete AUTOSAR profile

**Alive counter**:
A bounded sequence value carried in protected payload bits to expose repetition, loss, or incorrect ordering.
_Avoid_: message count, transmit statistic

**Simulation plan**:
An immutable set of simulation message rules bound to one parsed DBC document.
_Avoid_: UI setup, editable grid state

**Simulation message rule**:
The configuration that selects one DBC message and declares its gateway behavior, send behavior, timing, signal overrides, and E2E settings.
_Avoid_: runtime frame, UI row

**Gateway mode**:
The selected treatment for a matching message: pass through, block, or inject.
_Avoid_: send type, scheduler state

**Simulation send type**:
The trigger shape for a configured send: one-shot, cyclic, or event-driven.
_Avoid_: gateway mode, UI button label

**Simulation timing**:
The start delay, optional cycle interval, and repeat count declared for a simulation rule.
_Avoid_: wall-clock timestamp, hardware latency

**Signal override**:
A finite physical value selected to replace one named DBC signal during injection.
_Avoid_: raw payload replacement, live signal value

## Benchmark Test Data & Simulation Hardware Environment

> **BẮT BUỘC TUÂN THỦ TRONG QUÁ TRÌNH KIỂM THỬ VÀ VẬN HÀNH SIMULATOR (UI-07 VÀ TOÀN HỆ THỐNG):**

### 1. Kho dữ liệu DBC chuẩn (Test DBC Benchmark)
- **Tệp DBC**: `DBC/VF EBUS6M_PCAN_V2.0.0_20250524.dbc` (Đường truyền Powertrain CAN — PCAN).
- **Quy mô dữ liệu**: 61 Messages, 370 Signals, 11 Nodes, hỗ trợ đầy đủ bảng tra cứu giá trị `VAL_`.
- **Các thông điệp cốt lõi**: `VCU_NM` (CAN ID `0x52C`), `VCU_ASR_Ctrl` (CAN ID `0x18FF60D0`), v.v.

### 2. Cấu hình phần cứng giả lập Vector CANoe (Virtual CAN Harness)
- **Interface / Driver**: `Virtual CAN` (Vector XL Virtual Channel).
- **Kênh TX (Phát)**: `Virtual CAN Bus 1 (000100) - Channel 1` (hiển thị trên giao diện là `Virtual Bus 1 - Channel 1`).
- **Kênh RX (Nhận)**: `Virtual CAN Bus 2 (000101) - Channel 1` (hiển thị trên giao diện là `Virtual Bus 2 - Channel 1`).
- **Baudrate chuẩn**: TX = `500000` bps, RX = `500000` bps.
- **CAN FD**: `Enabled`.

### 3. Quy tắc hành vi kiểm thử cốt lõi (Core Simulation Behavioral Requirement)
- Khi Simulator khởi chạy phát dữ liệu mô phỏng đường PCAN:
  - Đường **TX** (`Virtual Bus 1 - Channel 1`) phát các gói tin ra bus.
  - Khi người dùng thay đổi/can thiệp bất kỳ tín hiệu nào qua **Bảng 5 (Fault Configuration)** và **Bảng 6 (Signal Value Configuration)** (chế độ `Signal Override`, `Cyclic`, `One-Shot`, hoặc `Sequence`), khi TX phát ra thì phía **RX** (`Virtual Bus 2 - Channel 1`) **bắt buộc phải nhận được chính xác giá trị đã can thiệp đó**.
  - Đây là tiêu chí kiểm chứng tối cao (Ground Truth) để đánh giá tính đúng đắn khi triển khai Bảng 7 (Execution Control) và các module liên quan.
