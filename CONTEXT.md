# Simulate automotive diagnostics context

The shared language for the Automotive Fault Injector domain. Use these terms
consistently in tickets, plans, code, tests, and reviews.

## Network and data language

**CAN Message**:
A CAN or CAN FD frame identified by an arbitration ID and carrying a payload.
_Avoid_: packet, signal message

**Signal**:
A named, encoded value within a CAN Message payload.
_Avoid_: field, parameter

**DBC**:
A database that describes CAN Messages, Signals, and their encodings.
_Avoid_: CAN configuration file

**CAN FD**:
The CAN Flexible Data-rate network variant used when the project distinguishes
nominal and data-phase communication.
_Avoid_: fast CAN

**E2E Protocol**:
An AUTOSAR end-to-end protection protocol for data integrity and communication
error detection.
_Avoid_: CRC-only protection

## Fault-injection language

**Fault Injection**:
A controlled test action that changes, delays, suppresses, or corrupts a target
Message or Signal according to a configured fault.
_Avoid_: random error simulation

**Fault Queue**:
The ordered collection of configured fault-injection actions awaiting, scheduled
for, or undergoing execution.
_Avoid_: fault list

**Injection Mode**:
The mechanism by which a configured fault affects its target, such as message
override, CRC corruption, or frame drop.
_Avoid_: fault type

## Vector XL language

**Vector XL API**:
The driver API reference set supplied in `docs-guide/` for communication with
Vector XL hardware and drivers.
_Avoid_: CANoe API, generic CAN driver

**Reference Corpus**:
The standards, diagnostic specifications, spreadsheets, and Vector XL reference
materials in `docs-guide/` that govern work in their applicable subject areas.
_Avoid_: optional reading
