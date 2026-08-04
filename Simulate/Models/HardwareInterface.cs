using System.Collections.Generic;

namespace Simulate.Models
{
    public class HardwareInterface
    {
        public string Name { get; set; } = string.Empty;
        public List<HardwareChannel> Channels { get; set; } = new();
    }
}
