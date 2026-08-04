using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public class MockHardwareService : ICanHardwareDriver
    {
        public bool IsConnected { get; private set; }

        public List<HardwareChannel> GetAvailableChannels()
        {
            return new List<HardwareChannel>
            {
                new HardwareChannel { Name = "Virtual CAN 1", ChannelIndex = 0, ChannelMask = 1 },
                new HardwareChannel { Name = "Virtual CAN 2", ChannelIndex = 1, ChannelMask = 2 },
                new HardwareChannel { Name = "Virtual CAN 3", ChannelIndex = 2, ChannelMask = 4 },
                new HardwareChannel { Name = "Virtual CAN 4", ChannelIndex = 3, ChannelMask = 8 }
            };
        }

        public bool Connect(HardwareChannel txChannel, HardwareChannel rxChannel, uint baudrate, bool isCanFd)
        {
            if (txChannel == null || rxChannel == null) return false;
            IsConnected = true;
            return true;
        }

        public bool Disconnect()
        {
            IsConnected = false;
            return true;
        }
    }
}
