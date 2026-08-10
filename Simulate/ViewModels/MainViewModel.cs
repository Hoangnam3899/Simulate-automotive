using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Simulate.Services;

namespace Simulate.ViewModels
{
    public class MessageModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Dlc { get; set; }
        public string Cycle { get; set; } = string.Empty;
    }

    public class SignalModel
    {
        public string Name { get; set; } = string.Empty;
        public int StartBit { get; set; }
        public int Length { get; set; }
        public double Factor { get; set; }
        public double Offset { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double Min { get; set; }
        public double Max { get; set; }
        public double Value { get; set; }
    }

    public class FaultQueueModel
    {
        public int Index { get; set; }
        public string MsgId { get; set; } = string.Empty;
        public string MsgName { get; set; } = string.Empty;
        public string Signal { get; set; } = string.Empty;
        public string FaultType { get; set; } = string.Empty;
        public string FaultValue { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Repeat { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#10B981";
        public string StatusBg { get; set; } = "#064E3B";
    }

    public partial class MainViewModel : ObservableObject
    {
        public ConnectionViewModel Connection { get; }

        public ObservableCollection<MessageModel> Messages { get; set; }
        public ObservableCollection<SignalModel> Signals { get; set; }
        public ObservableCollection<FaultQueueModel> FaultQueue { get; set; }

        public MainViewModel()
        {
            ICanConnectionDriver hardwareDriver = new VectorHardwareService();
            Connection = new ConnectionViewModel(hardwareDriver);

            Messages = new ObservableCollection<MessageModel>
            {
                new MessageModel { Id = "0x100", Name = "EngineData",     Dlc = 8, Cycle = "10 ms"  },
                new MessageModel { Id = "0x101", Name = "VehicleSpeed",   Dlc = 8, Cycle = "20 ms"  },
                new MessageModel { Id = "0x102", Name = "BrakeStatus",    Dlc = 8, Cycle = "10 ms"  },
                new MessageModel { Id = "0x103", Name = "SteeringAngle",  Dlc = 8, Cycle = "20 ms"  },
                new MessageModel { Id = "0x104", Name = "BatteryStatus",  Dlc = 8, Cycle = "100 ms" },
                new MessageModel { Id = "0x105", Name = "DoorStatus",     Dlc = 8, Cycle = "50 ms"  },
                new MessageModel { Id = "0x106", Name = "LightStatus",    Dlc = 8, Cycle = "100 ms" },
                new MessageModel { Id = "0x107", Name = "HVACStatus",     Dlc = 8, Cycle = "100 ms" },
            };

            Signals = new ObservableCollection<SignalModel>
            {
                new SignalModel { Name = "EngineSpeed", StartBit = 0,  Length = 16, Factor = 0.125, Offset = 0,   Unit = "rpm", Min = 0,   Max = 8000, Value = 1250  },
                new SignalModel { Name = "EngineTemp",  StartBit = 16, Length = 8,  Factor = 1,     Offset = -40, Unit = "°C",  Min = -40, Max = 215,  Value = 90    },
                new SignalModel { Name = "ThrottlePos", StartBit = 24, Length = 8,  Factor = 0.4,   Offset = 0,   Unit = "%",   Min = 0,   Max = 100,  Value = 16.0  },
                new SignalModel { Name = "OilPressure", StartBit = 32, Length = 8,  Factor = 0.1,   Offset = 0,   Unit = "kPa", Min = 0,   Max = 500,  Value = 312.5 },
                new SignalModel { Name = "FuelLevel",   StartBit = 40, Length = 8,  Factor = 0.4,   Offset = 0,   Unit = "%",   Min = 0,   Max = 100,  Value = 50.0  },
                new SignalModel { Name = "EngineStatus",StartBit = 48, Length = 8,  Factor = 1,     Offset = 0,   Unit = "-",   Min = 0,   Max = 255,  Value = 1     },
            };

            FaultQueue = new ObservableCollection<FaultQueueModel>
            {
                new FaultQueueModel { Index = 1, MsgId = "0x100", MsgName = "EngineData",  Signal = "EngineSpeed", FaultType = "Stuck at Value", FaultValue = "0x0000 (0 rpm)", StartTime = "0 s", Duration = "10 s", Repeat = "Infinite", Mode = "Override", Status = "Active",    StatusColor = "#10B981", StatusBg = "#064E3B" },
                new FaultQueueModel { Index = 2, MsgId = "0x102", MsgName = "BrakeStatus", Signal = "BrakeApplied", FaultType = "Bit Flip", FaultValue = "Bit 0", StartTime = "5 s", Duration = "15 s", Repeat = "1", Mode = "Override", Status = "Scheduled", StatusColor = "#38BDF8", StatusBg = "#0C2A4A" },
                new FaultQueueModel { Index = 3, MsgId = "0x101", MsgName = "VehicleSpeed",Signal = "VehicleSpeed", FaultType = "Offset",   FaultValue = "+20 km/h", StartTime = "0 s", Duration = "20 s", Repeat = "3", Mode = "Override", Status = "Pending",   StatusColor = "#94A3B8", StatusBg = "#1E2C3A" },
            };
        }
    }
}
