namespace Simulate.Models
{
    public sealed class HardwareChannel
    {
        public string Name { get; init; } = string.Empty;
        public int ChannelIndex { get; init; }
        public ulong ChannelMask { get; init; }
        public uint DefaultBaudrate { get; init; } = 500000;

        public override string ToString() => Name;
    }
}
