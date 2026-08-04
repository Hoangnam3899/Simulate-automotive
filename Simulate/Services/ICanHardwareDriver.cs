using System.Collections.Generic;
using Simulate.Models;

namespace Simulate.Services
{
    public interface ICanHardwareDriver
    {
        bool IsConnected { get; }
        List<HardwareChannel> GetAvailableChannels();
        bool Connect(HardwareChannel txChannel, HardwareChannel rxChannel, uint baudrate, bool isCanFd);
        bool Disconnect();
    }
}
