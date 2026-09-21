using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class LoggingTests
    {
        [TestMethod]
        public void LogEntry_FormatsLineWithTimestampLevelAndModule()
        {
            var now = new DateTimeOffset(2026, 9, 21, 14, 30, 25, 123, TimeSpan.Zero);
            var entry = new LogEntry(now, LogLevel.Info, "Connection", "Connected to CAN 1");

            Assert.AreEqual("Connection", entry.SourceModule);
            Assert.AreEqual("Connected to CAN 1", entry.Message);
            Assert.AreEqual(LogLevel.Info, entry.Level);
            Assert.IsTrue(entry.FormattedLine.Contains("[14:30:25.123]"));
            Assert.IsTrue(entry.FormattedLine.Contains("[INFO   ]"));
            Assert.IsTrue(entry.FormattedLine.Contains("[Connection]"));
            Assert.IsTrue(entry.FormattedLine.Contains("Connected to CAN 1"));
        }

        [TestMethod]
        public void LogService_EnforcesMaxCapacityFIFO()
        {
            var service = new LogService(maxCapacity: 3);

            service.LogInfo("Test", "Msg 1");
            service.LogInfo("Test", "Msg 2");
            service.LogInfo("Test", "Msg 3");
            Assert.AreEqual(3, service.GetEntries().Count);
            Assert.AreEqual("Msg 1", service.GetEntries()[0].Message);

            service.LogInfo("Test", "Msg 4");
            Assert.AreEqual(3, service.GetEntries().Count);
            Assert.AreEqual("Msg 2", service.GetEntries()[0].Message);
            Assert.AreEqual("Msg 4", service.GetEntries()[2].Message);
        }

        [TestMethod]
        public void LogService_Clear_EmptiesAllEntries()
        {
            var service = new LogService();
            service.LogInfo("Test", "Hello");
            service.LogError("Test", "Oops");
            Assert.AreEqual(2, service.GetEntries().Count);

            bool clearedFired = false;
            service.Cleared += (s, e) => clearedFired = true;

            service.Clear();
            Assert.AreEqual(0, service.GetEntries().Count);
            Assert.IsTrue(clearedFired);
        }

        [TestMethod]
        public void LogService_ConcurrentLogging_DoesNotThrow()
        {
            var service = new LogService(maxCapacity: 500);

            Parallel.For(0, 1000, i =>
            {
                service.LogInfo("Worker", $"Message {i}");
            });

            Assert.AreEqual(500, service.GetEntries().Count);
        }

        [TestMethod]
        public void LoggingViewModel_Filter_ShowsOnlyMatchingLevels()
        {
            var service = new LogService();
            var dialog = new FakeFileDialog(null);
            var vm = new LoggingViewModel(service, dialog);

            service.LogInfo("ModuleA", "Info message");
            service.LogWarning("ModuleB", "Warning message");
            service.LogError("ModuleC", "Error message");

            // All levels
            vm.SelectedLevel = "All Levels";
            Assert.IsTrue(vm.FormattedLogText.Contains("Info message"));
            Assert.IsTrue(vm.FormattedLogText.Contains("Warning message"));
            Assert.IsTrue(vm.FormattedLogText.Contains("Error message"));

            // Error only
            vm.SelectedLevel = "Error";
            Assert.IsFalse(vm.FormattedLogText.Contains("Info message"));
            Assert.IsFalse(vm.FormattedLogText.Contains("Warning message"));
            Assert.IsTrue(vm.FormattedLogText.Contains("Error message"));

            // Warning only
            vm.SelectedLevel = "Warning";
            Assert.IsFalse(vm.FormattedLogText.Contains("Info message"));
            Assert.IsTrue(vm.FormattedLogText.Contains("Warning message"));
            Assert.IsFalse(vm.FormattedLogText.Contains("Error message"));

            // Info only
            vm.SelectedLevel = "Info";
            Assert.IsTrue(vm.FormattedLogText.Contains("Info message"));
            Assert.IsFalse(vm.FormattedLogText.Contains("Warning message"));
            Assert.IsFalse(vm.FormattedLogText.Contains("Error message"));
        }

        [TestMethod]
        public void LoggingViewModel_ClearCommand_ClearsLog()
        {
            var service = new LogService();
            var dialog = new FakeFileDialog(null);
            var vm = new LoggingViewModel(service, dialog);

            service.LogInfo("ModuleA", "Pre-clear message");
            Assert.IsTrue(vm.FormattedLogText.Contains("Pre-clear message"));

            vm.ClearCommand.Execute(null);

            Assert.IsFalse(vm.FormattedLogText.Contains("Pre-clear message"));
            Assert.IsTrue(vm.FormattedLogText.Contains("Log cleared by user."));
        }

        [TestMethod]
        public async Task LoggingViewModel_ExportCommand_WritesLogToFile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"AFI_Test_{Guid.NewGuid():N}.log");
            try
            {
                var service = new LogService();
                var dialog = new FakeFileDialog(tempFile);
                var vm = new LoggingViewModel(service, dialog);

                service.LogInfo("TestModule", "Exported sample content line 1");
                service.LogWarning("TestModule", "Exported sample content line 2");

                await vm.ExportCommand.ExecuteAsync(null);

                Assert.IsTrue(File.Exists(tempFile));
                string fileContent = await File.ReadAllTextAsync(tempFile);
                Assert.IsTrue(fileContent.Contains("Exported sample content line 1"));
                Assert.IsTrue(fileContent.Contains("Exported sample content line 2"));
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void MainViewModel_InitializesWithSystemLog()
        {
            var mainVm = new MainViewModel();

            Assert.IsNotNull(mainVm.Logging);
            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("AFI - Automotive Fault Injector initialized."));
        }

        [TestMethod]
        public void MainViewModel_DbcLoadedAndUnloaded_LogsActions()
        {
            var mainVm = new MainViewModel();

            const string sampleDbc = @"
VERSION ""1.0""
BO_ 256 EngineData: 8 Vector__XXX
 SG_ EngineSpeed : 0|16@1+ (1,0) [0|8000] ""rpm"" Vector__XXX
";
            mainVm.Dbc.LoadDbcText("SampleNet.dbc", null, sampleDbc);

            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("Loaded DBC file 'SampleNet.dbc'"));
            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("1 messages"));

            mainVm.Dbc.UnloadDbcCommand.Execute(null);
            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("DBC document unloaded."));
        }

        [TestMethod]
        public void MainViewModel_SignalOverrideToggle_LogsUserAction()
        {
            var mainVm = new MainViewModel();
            const string sampleDbc = @"
VERSION ""1.0""
BO_ 256 EngineData: 8 Vector__XXX
 SG_ EngineSpeed : 0|16@1+ (1,0) [0|8000] ""rpm"" Vector__XXX
";
            mainVm.Dbc.LoadDbcText("SampleNet.dbc", null, sampleDbc);
            mainVm.Simulation.AddMessages(mainVm.Dbc.LoadedDocument!.Messages);

            SignalModel sig = mainVm.Signals.First(s => s.Name == "EngineSpeed");

            sig.Value = 1500;
            sig.IsOverridden = true;

            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("Signal 'EngineSpeed' override enabled (Value: 1500 rpm)."));

            sig.Value = 2000;
            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("Signal 'EngineSpeed' value set to 2000 rpm."));

            sig.IsOverridden = false;
            Assert.IsTrue(mainVm.Logging.FormattedLogText.Contains("Signal 'EngineSpeed' override disabled"));
        }

        private sealed class FakeFileDialog : IFileDialogService
        {
            private readonly string? _path;

            public FakeFileDialog(string? path)
            {
                _path = path;
            }

            public string? OpenFileDialog(string filter, string title) => _path;

            public string? SaveFileDialog(string filter, string title, string? defaultFileName = null) => _path;
        }
    }
}
