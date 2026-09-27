---
trigger: always_on
---

# UDS Diagnostics (ISO 14229) & ISO-TP (ISO 15765-2) Rules

Domain rules and protocol specifications extracted directly from the reference diagnostic system (`Basic-tool-connect-and-Read-DTC-DID-C-with-vxlapi.dll`):

## 1. UDS Service 0x14 — Clear Diagnostic Information
- **Request Format**: `14 FF FF FF` (Clears all DTC groups across all categories).
- **Positive Response**: `54` (Positive confirmation). Note: Some legacy ECUs may return an empty payload ACK, which must be treated as successful.
- **Negative Response**: `7F 14 <NRC>`.
  - NRC `0x78` (RequestCorrectlyReceived-ResponsePending): Must enter a polling wait loop up to 5,000ms.
  - Other NRCs: Report specific error code (`NRC-{code:X2}`).

## 2. UDS Service 0x19 (Subfunction 0x02) — Read DTC Information By Status Mask
- **Request Format**: `19 02 {statusMask}` (e.g., `0x09` for TF|CONF, `0xFF` for all available status flags).
- **Positive Response**: `59 02 [SAM] [(DFI?)] [DTC(3) + Status]...`
  - `SAM` (Status Availability Mask): 1 byte indicating supported status bits by the ECU.
  - `DFI` (DTC Fault Indication): Optional byte present in specific ECUs (typically `0x01`, `0x02`, or `0x10`). If not in this set, parse immediately as DTC record.
  - `DTC Record`: 4 bytes = 3 bytes DTC code + 1 byte status mask.
  - Padding cutoff: If `a == 0 && b == 0 && c == 0`, stop parsing records immediately.
- **SAE J2012 DTC Code Mapping**:
  - Two high bits of Byte 1 map to category character:
    - `00` -> `P` (Powertrain)
    - `01` -> `C` (Chassis)
    - `10` -> `B` (Body)
    - `11` -> `U` (Network)
  - Next 2 bits: First numeric digit `D1` (0-3).
  - Low 4 bits: Second digit `D2` (0-F).
  - Format string: `"{category}{D1}{D2:X1}{byte2:X2}{byte3:X2}"`.
- **Status Byte Bit Definitions (ISO 14229)**:
  - Bit 0 (`0x01`): `TF` (Test Failed)
  - Bit 1 (`0x02`): `TFMC` (Test Failed This Operation Cycle)
  - Bit 2 (`0x04`): `PEND` (Pending DTC)
  - Bit 3 (`0x08`): `CONF` (Confirmed DTC)
  - Bit 4 (`0x10`): `NC_SLC` (Test Not Completed Since Last Clear)
  - Bit 5 (`0x20`): `F_SLC` (Test Failed Since Last Clear)
  - Bit 6 (`0x40`): `NC_MC` (Test Not Completed This Operation Cycle)
  - Bit 7 (`0x80`): `WIR` (Warning Indicator Requested)

## 3. UDS Service 0x22 — Read Data By Identifier (DID)
- **Request Format**: `22 {DID_High} {DID_Low}` (e.g., `22 F1 87` for Part Number, `22 F1 89` for Revision, `22 F1 90` for VIN).
- **Positive Response**: `62 {DID_High} {DID_Low} {Payload...}`.
- **Payload Parsing Rules**:
  - For multipart ECU identifiers (> 3 bytes):
    - Bytes 0..2: ASCII string prefix (e.g., vendor code).
    - Bytes 3..6: Hexadecimal representation (up to 4 bytes).
    - Bytes 7+: ASCII suffix (version, metadata).
  - For short payloads (<= 3 bytes): Direct hexadecimal formatting or ASCII depending on target schema.

## 4. ISO-TP (ISO 15765-2) Multi-frame Transport Protocol
- **PCI Frame Types**:
  - `0x0` (Single Frame): Length in low nibble (1..7 bytes).
  - `0x1` (First Frame): Total length `((pci[0] & 0x0F) << 8) | pci[1]`.
  - `0x2` (Consecutive Frame): Sequence counter `seq = pci[0] & 0x0F` (starts at 1, wraps 1..15..0).
  - `0x3` (Flow Control): Flow Status in low nibble (`0x30` = CTS - Clear To Send).
- **Flow Control Transmission**:
  - When receiver sees First Frame, immediately reply with FC frame: `[0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]` (CTS, BlockSize = 0, STmin = 0).
- **Flow Control Reception & Timing**:
  - Parse BlockSize (`fc[1]`) and STmin (`fc[2]`).
  - If `stMin <= 0x7F`: Delay in milliseconds (`Thread.Sleep(stMin)`).
  - If `0xF1 <= stMin <= 0xF9`: Delay in microseconds (~100..900us, safely clamped to 1ms or Sleep(0)).
- **Retry & Pending Policy**:
  - Default response timeout: 500ms - 800ms.
  - If timeout occurs on first attempt, back off for 150ms and perform exactly 1 retry.
  - When NRC `0x78` (Response Pending) is returned, restart the reception timeout and wait up to 5,000ms.

## 5. Vector XL Driver Queue-Level Reception
- Always query `xlGetReceiveQueueLevel(portHandle, out queueLevel)` before `xlReceive` to consume all pending events in bulk without starvation.
- Process both `XL_RECEIVE_MSG` and `XL_TRANSMIT_MSG` appropriately to ensure accurate time correlation.
