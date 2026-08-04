using System;
using System.Collections.Generic;
using Simulate.Models;
using vxlapi_NET;

namespace Simulate.Services
{
    public class VectorHardwareService : ICanHardwareDriver
    {
        private XLDriver _driver;
        private int _portHandle = -1;
        private ulong _accessMask;

        public bool IsConnected { get; private set; }

        public VectorHardwareService()
        {
            _driver = new XLDriver();
        }

        public List<HardwareInterface> GetAvailableInterfaces()
        {
            var interfaces = new List<HardwareInterface>();
            
            XLDefine.XL_Status status = _driver.XL_OpenDriver();
            if (status != XLDefine.XL_Status.XL_SUCCESS) return interfaces;

            XLClass.xl_driver_config config = new XLClass.xl_driver_config();
            status = _driver.XL_GetDriverConfig(ref config);

            if (status == XLDefine.XL_Status.XL_SUCCESS)
            {
                var dict = new Dictionary<string, HardwareInterface>();
                for (int i = 0; i < config.channelCount; i++)
                {
                    if (config.channel[i].hwType != XLDefine.XL_HardwareType.XL_HWTYPE_NONE && (config.channel[i].busParams.busType == XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN))
                    {
                        string deviceKey = $"{config.channel[i].hwType}_{config.channel[i].hwIndex}";
                        if (!dict.TryGetValue(deviceKey, out HardwareInterface? iface))
                        {
                            string devName;
                            if (config.channel[i].hwType == XLDefine.XL_HardwareType.XL_HWTYPE_VIRTUAL)
                            {
                                devName = $"Virtual CAN Bus {config.channel[i].hwIndex + 1}";
                            }
                            else
                            {
                                string typeStr = config.channel[i].hwType.ToString().Replace("XL_HWTYPE_", "");
                                devName = $"{typeStr} {config.channel[i].hwIndex + 1}";
                            }

                            iface = new HardwareInterface { Name = devName };
                            dict[deviceKey] = iface;
                        }

                        string chName = string.IsNullOrWhiteSpace(config.channel[i].transceiverName) 
                            ? $"Channel {config.channel[i].hwChannel + 1}"
                            : $"{config.channel[i].transceiverName} (CH {config.channel[i].hwChannel + 1})";

                        uint rate = config.channel[i].busParams.dataCan.bitrate;
                        if (rate == 0) rate = 500000;

                        iface.Channels.Add(new HardwareChannel
                        {
                            Name = chName,
                            ChannelIndex = config.channel[i].channelIndex,
                            ChannelMask = config.channel[i].channelMask,
                            DefaultBaudrate = rate
                        });
                    }
                }
                interfaces.AddRange(dict.Values);
            }
            
            _driver.XL_CloseDriver();
            return interfaces;
        }

        public bool Connect(HardwareChannel txChannel, HardwareChannel rxChannel, uint baudrate, bool isCanFd)
        {
            if (IsConnected) return true;

            XLDefine.XL_Status status = _driver.XL_OpenDriver();
            if (status != XLDefine.XL_Status.XL_SUCCESS) return false;

            ulong permissionMask = txChannel.ChannelMask | rxChannel.ChannelMask;
            _accessMask = txChannel.ChannelMask | rxChannel.ChannelMask;

            status = _driver.XL_OpenPort(ref _portHandle, "Simulate", _accessMask, ref permissionMask, 8192, XLDefine.XL_InterfaceVersion.XL_INTERFACE_VERSION, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN);
            
            if (status != XLDefine.XL_Status.XL_SUCCESS)
            {
                _driver.XL_CloseDriver();
                return false;
            }

            if (isCanFd)
            {
                // Can FD config
                XLClass.XLcanFdConf fdConf = new XLClass.XLcanFdConf();
                fdConf.arbitrationBitRate = baudrate;
                fdConf.sjwAbr = 2;
                fdConf.tseg1Abr = 5;
                fdConf.tseg2Abr = 2;
                fdConf.dataBitRate = baudrate * 4;
                fdConf.sjwDbr = 2;
                fdConf.tseg1Dbr = 5;
                fdConf.tseg2Dbr = 2;
                fdConf.options = (byte)XLDefine.XL_CANFD_ConfigOptions.XL_CANFD_CONFOPT_NO_ISO;
                _driver.XL_CanFdSetConfiguration(_portHandle, _accessMask, fdConf);
            }
            else
            {
                // Classic CAN config
                _driver.XL_CanSetChannelBitrate(_portHandle, _accessMask, baudrate);
            }

            status = _driver.XL_ActivateChannel(_portHandle, _accessMask, XLDefine.XL_BusTypes.XL_BUS_TYPE_CAN, XLDefine.XL_AC_Flags.XL_ACTIVATE_NONE);
            
            if (status == XLDefine.XL_Status.XL_SUCCESS)
            {
                IsConnected = true;
                return true;
            }

            Disconnect();
            return false;
        }

        public bool Disconnect()
        {
            if (!IsConnected) return true;
            
            if (_portHandle != -1)
            {
                _driver.XL_DeactivateChannel(_portHandle, _accessMask);
                _driver.XL_ClosePort(_portHandle);
                _portHandle = -1;
            }
            
            _driver.XL_CloseDriver();
            IsConnected = false;
            return true;
        }
    }
}
