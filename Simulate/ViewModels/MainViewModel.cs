using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Simulate.ViewModels
{
    public class MessageModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Dlc { get; set; }
        public string Cycle { get; set; } = string.Empty;
        public string GatewayMode { get; set; } = string.Empty;
        public string SendType { get; set; } = string.Empty;
        public int SignalCount { get; set; }
        public bool IsEnabled { get; set; }
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
        public string MessageId { get; set; } = string.Empty;
        public string MessageName { get; set; } = string.Empty;
        public bool IsOverridden { get; set; }
        public string Cycle { get; set; } = string.Empty;
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

        public SimulationViewModel Simulation { get; }

        public ObservableCollection<MessageModel> Messages => Simulation.Messages;
        public ObservableCollection<SignalModel> Signals => Simulation.Signals;
        public ObservableCollection<FaultQueueModel> FaultQueue => Simulation.FaultQueue;

        public MainViewModel()
            : this(ApplicationComposition.CreateConnectionViewModel(), new SimulationViewModel())
        {
        }

        /// <summary>
        /// Initializes a view model with caller-composed connection and simulation dependencies.
        /// </summary>
        /// <param name="connection">The connection state exposed to existing bindings.</param>
        /// <param name="simulation">The simulation projection exposed to existing bindings.</param>
        public MainViewModel(ConnectionViewModel connection, SimulationViewModel simulation)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }
    }
}
