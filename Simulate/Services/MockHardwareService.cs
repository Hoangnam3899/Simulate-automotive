using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public class MockHardwareService : ICanHardwareDriver
    {
        public bool IsConnected { get; private set; }

        public List<HardwareInterface> GetAvailableInterfaces()
        {
            var virtualCan = new HardwareInterface
            {
                Name = "Virtual CAN Bus",
                Channels = new List<HardwareChannel>
                {
                    new HardwareChannel { Name = "Virtual CAN 1", ChannelIndex = 0, ChannelMask = 1 },
                    new HardwareChannel { Name = "Virtual CAN 2", ChannelIndex = 1, ChannelMask = 2 }
                }
            };
            
            var vn1640 = new HardwareInterface
            {
                Name = "Vector VN1640",
                Channels = new List<HardwareChannel>
                {
                    new HardwareChannel { Name = "VN1640 Channel 1", ChannelIndex = 2, ChannelMask = 4 },
                    new HardwareChannel { Name = "VN1640 Channel 2", ChannelIndex = 3, ChannelMask = 8 }
                }
            };

            return new List<HardwareInterface> { virtualCan, vn1640 };
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
