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

**Normalized CAN identifier**:
The CAN identifier value without the DBC extended-identifier flag, paired with an explicit extended-identifier state.
_Avoid_: raw DBC identifier

**Parse issue**:
A line-specific diagnostic emitted while reading an external DBC document; it is either an error that invalidates the document or a warning about an unrepresented construct.
_Avoid_: hardware failure
