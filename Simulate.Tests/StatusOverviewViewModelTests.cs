using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class StatusOverviewViewModelTests
    {
        [TestMethod]
        public void Default_state_shows_disconnected_offline_and_unloaded_dbc()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var busHealth = new BusHealthViewModel(
                () => connection.IsConnected,
                () => simulation.IsRunning,
                () => (uint)(connection.BaudrateTx * 1000),
                () => simulation.Statistics,
                () => connection.LastFailure,
                () => 0,
                startTimer: false);

            using var overview = new StatusOverviewViewModel(connection, dbc, simulation, busHealth);

            Assert.AreEqual("○ Disconnected", overview.ConnectionText);
            Assert.AreEqual("#64748B", overview.ConnectionColor);
            Assert.AreEqual("None Selected", overview.DriverText);
            Assert.AreEqual("Offline", overview.BusStateText);
            Assert.AreEqual("#64748B", overview.BusStateColor);
            Assert.AreEqual("— None Loaded —", overview.DbcText);
            Assert.AreEqual("Offline", overview.BusHealthText);
            Assert.AreEqual("#64748B", overview.BusHealthColor);
            Assert.AreEqual("Enabled (2 Mbps)", overview.CanFdText);
        }

        [TestMethod]
        public void Connected_without_simulation_shows_standby_bus_state_and_driver_name()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var busHealth = new BusHealthViewModel(
                () => connection.IsConnected,
                () => simulation.IsRunning,
                () => (uint)(connection.BaudrateTx * 1000),
                () => simulation.Statistics,
                () => connection.LastFailure,
                () => 0,
                startTimer: false);

            using var overview = new StatusOverviewViewModel(connection, dbc, simulation, busHealth);

            // Simulate interface selection and connected state
            connection.SelectedInterface = new HardwareInterface { Name = "Vector VN1610" };
            connection.IsConnected = true;
            overview.UpdateOverview();

            Assert.AreEqual("● Connected", overview.ConnectionText);
            Assert.AreEqual("#10B981", overview.ConnectionColor);
            Assert.AreEqual("Vector VN1610", overview.DriverText);
            Assert.AreEqual("Standby", overview.BusStateText);
            Assert.AreEqual("#F59E0B", overview.BusStateColor);
        }

        [TestMethod]
        public void Running_simulation_shows_active_bus_state()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var busHealth = new BusHealthViewModel(
                () => connection.IsConnected,
                () => simulation.IsRunning,
                () => (uint)(connection.BaudrateTx * 1000),
                () => simulation.Statistics,
                () => connection.LastFailure,
                () => 0,
                startTimer: false);

            using var overview = new StatusOverviewViewModel(connection, dbc, simulation, busHealth);

            connection.IsConnected = true;
            simulation.IsRunning = true;
            overview.UpdateOverview();

            Assert.AreEqual("Active", overview.BusStateText);
            Assert.AreEqual("#10B981", overview.BusStateColor);
        }

        [TestMethod]
        public void Loaded_dbc_updates_dbc_display_text()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var busHealth = new BusHealthViewModel(
                () => connection.IsConnected,
                () => simulation.IsRunning,
                () => (uint)(connection.BaudrateTx * 1000),
                () => simulation.Statistics,
                () => connection.LastFailure,
                () => 0,
                startTimer: false);

            using var overview = new StatusOverviewViewModel(connection, dbc, simulation, busHealth);

            dbc.LoadedFileName = "Powertrain.dbc";
            dbc.MessageCount = 42;
            overview.UpdateOverview();

            Assert.AreEqual("Powertrain.dbc (42 msgs)", overview.DbcText);

            dbc.LoadedFileName = null;
            dbc.MessageCount = 0;
            overview.UpdateOverview();

            Assert.AreEqual("— None Loaded —", overview.DbcText);
        }

        [TestMethod]
        public void CanFd_mode_reflects_connection_settings()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var busHealth = new BusHealthViewModel(
                () => connection.IsConnected,
                () => simulation.IsRunning,
                () => (uint)(connection.BaudrateTx * 1000),
                () => simulation.Statistics,
                () => connection.LastFailure,
                () => 0,
                startTimer: false);

            using var overview = new StatusOverviewViewModel(connection, dbc, simulation, busHealth);

            connection.IsCanFdEnabled = true;
            connection.DataBaudrateTx = 4000000;
            overview.UpdateOverview();

            Assert.AreEqual("Enabled (4 Mbps)", overview.CanFdText);

            connection.IsCanFdEnabled = false;
            overview.UpdateOverview();

            Assert.AreEqual("Disabled (Classic)", overview.CanFdText);
        }

        [TestMethod]
        public void MainViewModel_exposes_status_overview_and_disposes_cleanly()
        {
            var driver = new MockHardwareService();
            var connection = new ConnectionViewModel(driver);
            var dbc = new DbcManagementViewModel();
            var simulation = new SimulationViewModel();
            var logging = new LoggingViewModel();
            var busHealth = new BusHealthViewModel();

            var main = new MainViewModel(connection, dbc, simulation, logging, busHealth);

            Assert.IsNotNull(main.StatusOverview);
            Assert.AreEqual("○ Disconnected", main.StatusOverview.ConnectionText);
            Assert.AreEqual("Offline", main.StatusOverview.BusStateText);
            Assert.AreEqual("— None Loaded —", main.StatusOverview.DbcText);
        }
    }
}
