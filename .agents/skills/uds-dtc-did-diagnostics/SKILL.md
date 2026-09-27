---
name: uds-dtc-did-diagnostics
description: Automotive UDS Diagnostics (ISO 14229) & ISO-TP (ISO 15765-2) implementation and verification guide. Covers Read DTC (0x19 02), Clear DTC (0x14 FF FF FF), Read DID (0x22), SAE J2012 fault code decoding, ISO-TP Single/Multi-frame transmission with Flow Control (0x30 CTS), NRC 0x78 response pending loop, and Vector XL queue-level reception.
---

# UDS Diagnostics & ISO-TP Protocol Skill

## 1. Overview & Protocol Hierarchy

This skill guides the implementation, extension, and testing of UDS Diagnostic Services (ISO 14229-1) layered over ISO-TP Transport Protocol (ISO 15765-2) using Vector XL Hardware drivers (`vxlapi.dll` / `vxlapi_NET.dll` / `TSMaster.dll`).

```
┌────────────────────────────────────────────────────────┐
│ Application / Diagnostic UI / ViewModel (WPF / MVVM)   │
├────────────────────────────────────────────────────────┤
│ UDS Layer (ISO 14229-1):                               │
│ - 0x14: ClearDiagnosticInformation                     │
│ - 0x19 02: ReadDTCInformation (ReportDTCByStatusMask)  │
│ - 0x22: ReadDataByIdentifier (DID)                     │
│ - 0x3E: TesterPresent (Keep-Alive)                     │
├────────────────────────────────────────────────────────┤
│ ISO-TP Layer (ISO 15765-2):                            │
│ - SF (0x0): Single Frame (<= 7 bytes)                  │
│ - FF (0x1): First Frame (>= 8 bytes)                   │
│ - FC (0x30): Flow Control (CTS, BS, STmin)             │
│ - CF (0x2): Consecutive Frame (Sequence 1..15..0)      │
├────────────────────────────────────────────────────────┤
│ Hardware Transport Layer:                              │
│ - Vector XL Driver (vxlapi_NET / vxlapi.dll) / Mock   │
└────────────────────────────────────────────────────────┘
```

---

## 2. ISO-TP Multi-Frame Transmission & Flow Control

### 2.1. Transmission Flow
1. **Single Frame (SF)**: Payload length <= 7 bytes:
   - Byte 0: `0x00 | Length`
   - Bytes 1..7: Data bytes (padded to 8 bytes).
2. **First Frame (FF)**: Payload length >= 8 bytes:
   - Byte 0: `0x10 | ((TotalLength >> 8) & 0x0F)`
   - Byte 1: `TotalLength & 0xFF`
   - Bytes 2..7: First 6 bytes of data.
3. **Wait for Flow Control (FC)**:
   - Filter incoming frames for `(data[0] & 0xF0) == 0x30` (CTS).
   - Parse `BlockSize = data[1]`, `STmin = data[2]`.
   - Delay calculation:
     - If `STmin <= 0x7F`: delay is in milliseconds (`STmin` ms).
     - If `0xF1 <= STmin <= 0xF9`: delay is in microseconds (clamp safely to 1ms or `Thread.Sleep(0)`).
4. **Consecutive Frames (CF)**:
   - Byte 0: `0x20 | (Seq & 0x0F)` where `Seq` starts at 1, increments 1..15, wraps to 0.
   - Bytes 1..7: Next chunk of up to 7 data bytes.
   - Honor `BlockSize`: If `BlockSize > 0` and `cfSent == BlockSize`, wait for next Flow Control frame before resuming.

### 2.2. Reception Flow
1. **Detect First Frame (`pci == 1`)**:
   - Extract `TotalLength = ((data[0] & 0x0F) << 8) | data[1]`.
   - Copy initial 6 bytes.
   - **Immediately transmit Flow Control**: `[0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]` (CTS, BS=0, STmin=0).
2. **Consume Consecutive Frames (`pci == 2`)**:
   - Verify sequence counter `seq == nextSeq`.
   - Append slice until `totalLength` is satisfied.
   - Reset CF arrival watchdog timer on each valid frame.

---

## 3. UDS Diagnostic Services Implementation

### 3.1. Clear Diagnostic Information (Service 0x14)
- **Send**: `[0x14, 0xFF, 0xFF, 0xFF]` to request clearing all fault categories.
- **Evaluate Response**:
  - Positive: `0x54` -> Clear Successful. (Empty payload ACK from specific ECUs is also accepted).
  - Negative: `0x7F, 0x14, <NRC>`:
    - If `NRC == 0x78` (Response Pending): Enter wait loop up to 5,000ms.
    - Other NRC: Return failure with `NRC-{code:X2}`.
  - Timeout: Return `No Response`.

### 3.2. Read DTC Information (Service 0x19, Subfunction 0x02)
- **Send**: `[0x19, 0x02, statusMask]` (e.g. `0x09` for TF|CONF, `0xFF` for all faults).
- **Evaluate Response**:
  - Expected positive header: `0x59, 0x02`.
  - Byte 2: `SAM` (Status Availability Mask).
  - Byte 3 (Optional DFI): If Byte 3 is `0x01`, `0x02`, or `0x10`, record starts at Byte 4; otherwise at Byte 3.
  - Record structure: 4 bytes per record `[DTC_High, DTC_Mid, DTC_Low, Status]`.
  - Termination: Stop parsing if padding `0x00, 0x00, 0x00` is encountered.
- **SAE J2012 DTC Code Conversion**:
  ```csharp
  char category = "PCBU"[(byte1 >> 6) & 0x03];
  int d1 = (byte1 >> 4) & 0x03;
  int d2 = byte1 & 0x0F;
  string j2012Code = $"{category}{d1}{d2:X1}{byte2:X2}{byte3:X2}";
  ```
- **Status Mask Breakdown**:
  - `0x01`: TF (Test Failed)
  - `0x02`: TFMC (Test Failed This Operation Cycle)
  - `0x04`: PEND (Pending DTC)
  - `0x08`: CONF (Confirmed DTC)
  - `0x10`: NC_SLC (Not Completed Since Last Clear)
  - `0x20`: F_SLC (Failed Since Last Clear)
  - `0x40`: NC_MC (Not Completed This Operation Cycle)
  - `0x80`: WIR (Warning Indicator Requested)

### 3.3. Read Data By Identifier (Service 0x22)
- **Send**: `[0x22, DID_High, DID_Low]`.
- **Evaluate Response**:
  - Positive: `0x62, DID_High, DID_Low, [Payload...]`.
  - Negative: `0x7F, 0x22, <NRC>` (handle NRC 0x78 pending).
  - Common DIDs:
    - `0xF190`: VIN (Vehicle Identification Number - 17 bytes ASCII).
    - `0xF187`: Part Number / VF_PN (ASCII prefix + HEX + ASCII suffix).
    - `0xF189`: Software Revision / Calibration ID.

---

## 4. Verification & Testing Discipline

1. **Unit Testing Seams**:
   - Mock CAN interface (`ICanHardwareDriver` / `MockHardwareService`) to test ISO-TP segmentation, reassembly, and NRC 0x78 pending loops deterministically without physical ECUs.
2. **Build Verification**:
   - `dotnet build` must compile with 0 warnings and 0 errors.
   - `dotnet test` must pass 100%.
