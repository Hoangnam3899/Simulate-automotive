namespace Simulate.Models
{
    public class HardwareChannel
    {
        public string Name { get; set; } = string.Empty;
        public int ChannelIndex { get; set; }
        public ulong ChannelMask { get; set; }
        public uint DefaultBaudrate { get; set; } = 500000;

        public override string ToString() => Name;
    }
}
