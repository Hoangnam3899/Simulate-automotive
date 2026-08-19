using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class DbcManagementViewModelTests
    {
        private const string SampleDbc = @"
VERSION ""1.0""

NS_ :

BS_:

BU_: NodeA NodeB

BO_ 256 EngineData: 8 NodeA
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" NodeB
 SG_ EngineTemp : 16|8@1+ (1,-40) [-40|215] ""degC"" NodeB

BO_ 512 VehicleSpeed: 4 NodeA
 SG_ Speed : 0|16@1+ (0.01,0) [0|250] ""km/h"" NodeB
";

        [TestMethod]
        public void Initial_state_is_unloaded_with_zero_counts()
        {
            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));

            Assert.IsFalse(viewModel.IsDbcLoaded);
            Assert.IsNull(viewModel.LoadedFileName);
            Assert.AreEqual("No DBC Loaded", viewModel.LoadedFileNameDisplay);
            Assert.AreEqual(0, viewModel.MessageCount);
            Assert.AreEqual(0, viewModel.NodeCount);
            Assert.AreEqual(0, viewModel.SignalCount);
            Assert.AreEqual(Visibility.Collapsed, viewModel.CheckmarkVisibility);
            Assert.IsTrue(viewModel.CanLoadDbc);
            Assert.IsFalse(viewModel.CanUnloadDbc);
            Assert.IsNull(viewModel.LastErrorMessage);
        }

        [TestMethod]
        public void LoadDbcText_populates_metadata_and_counts_correctly()
        {
            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));

            bool success = viewModel.LoadDbcText("Sample.dbc", null, SampleDbc);

            Assert.IsTrue(success);
            Assert.IsTrue(viewModel.IsDbcLoaded);
            Assert.AreEqual("Sample.dbc", viewModel.LoadedFileName);
            Assert.AreEqual("Sample.dbc", viewModel.LoadedFileNameDisplay);
            Assert.AreEqual(2, viewModel.MessageCount);
            Assert.AreEqual(2, viewModel.NodeCount);
            Assert.AreEqual(3, viewModel.SignalCount);
            Assert.AreEqual(Visibility.Visible, viewModel.CheckmarkVisibility);
            Assert.IsTrue(viewModel.CanLoadDbc);
            Assert.IsTrue(viewModel.CanUnloadDbc);
            Assert.IsNull(viewModel.LastErrorMessage);
        }

        [TestMethod]
        public void UnloadDbc_clears_document_and_resets_counts()
        {
            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));
            viewModel.LoadDbcText("Sample.dbc", null, SampleDbc);

            bool eventFired = false;
            viewModel.DocumentUnloaded += (s, e) => eventFired = true;

            viewModel.UnloadDbcCommand.Execute(null);

            Assert.IsTrue(eventFired);
            Assert.IsFalse(viewModel.IsDbcLoaded);
            Assert.IsNull(viewModel.LoadedFileName);
            Assert.AreEqual("No DBC Loaded", viewModel.LoadedFileNameDisplay);
            Assert.AreEqual(0, viewModel.MessageCount);
            Assert.AreEqual(0, viewModel.NodeCount);
            Assert.AreEqual(0, viewModel.SignalCount);
            Assert.AreEqual(Visibility.Collapsed, viewModel.CheckmarkVisibility);
            Assert.IsFalse(viewModel.CanUnloadDbc);
        }

        [TestMethod]
        public async Task LoadDbcCommand_invokes_file_dialog_and_loads_file()
        {
            string tempFile = Path.GetTempFileName();
            string dbcPath = Path.ChangeExtension(tempFile, ".dbc");
            File.Move(tempFile, dbcPath);

            try
            {
                await File.WriteAllTextAsync(dbcPath, SampleDbc);

                var fileDialog = new FakeFileDialogService(dbcPath);
                var viewModel = new DbcManagementViewModel(fileDialog);

                await viewModel.LoadDbcCommand.ExecuteAsync(null);

                Assert.IsTrue(viewModel.IsDbcLoaded);
                Assert.AreEqual(Path.GetFileName(dbcPath), viewModel.LoadedFileName);
                Assert.AreEqual(2, viewModel.MessageCount);
                Assert.AreEqual(3, viewModel.SignalCount);
            }
            finally
            {
                if (File.Exists(dbcPath))
                {
                    File.Delete(dbcPath);
                }
            }
        }

        [TestMethod]
        public async Task LoadDbcCommand_does_nothing_when_dialog_is_canceled()
        {
            var fileDialog = new FakeFileDialogService(null);
            var viewModel = new DbcManagementViewModel(fileDialog);

            await viewModel.LoadDbcCommand.ExecuteAsync(null);

            Assert.IsFalse(viewModel.IsDbcLoaded);
            Assert.AreEqual(0, viewModel.MessageCount);
        }

        [TestMethod]
        public async Task LoadDbcFileAsync_reports_error_for_nonexistent_file()
        {
            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));

            bool success = await viewModel.LoadDbcFileAsync("C:\\nonexistent_file_path.dbc");

            Assert.IsFalse(success);
            Assert.IsFalse(viewModel.IsDbcLoaded);
            Assert.IsNotNull(viewModel.LastErrorMessage);
            StringAssert.Contains(viewModel.LastErrorMessage, "File not found");
        }

        [TestMethod]
        public void LoadDbcText_reports_error_for_invalid_syntax()
        {
            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));
            string invalidDbc = "BO_ 256 InvalidPayloadLength: 999 NodeA\n SG_ Sig : 0|8@1+ (1,0) [0|255] \"\" NodeB";

            bool success = viewModel.LoadDbcText("Broken.dbc", null, invalidDbc);

            Assert.IsFalse(success);
            Assert.IsFalse(viewModel.IsDbcLoaded);
            Assert.IsNotNull(viewModel.LastErrorMessage);
            StringAssert.Contains(viewModel.LastErrorMessage, "DBC parsing failed");
        }

        [TestMethod]
        public async Task Real_DBC_files_from_corpus_load_successfully()
        {
            string dbcFolder = @"c:\Users\Hnam\Desktop\Simulate\DBC";
            if (!Directory.Exists(dbcFolder))
            {
                Assert.Inconclusive("DBC test folder not found on disk.");
                return;
            }

            string[] dbcFiles = Directory.GetFiles(dbcFolder, "*.dbc", SearchOption.AllDirectories);
            Assert.IsTrue(dbcFiles.Length > 0, "Expected at least one real DBC file.");

            var viewModel = new DbcManagementViewModel(new FakeFileDialogService(null));

            foreach (string file in dbcFiles)
            {
                bool success = await viewModel.LoadDbcFileAsync(file);
                Assert.IsTrue(success, $"Failed to load real DBC file: {file}. Error: {viewModel.LastErrorMessage}");
                Assert.IsTrue(viewModel.MessageCount > 0, $"Expected messages > 0 in {file}");
                Assert.IsTrue(viewModel.SignalCount > 0, $"Expected signals > 0 in {file}");
            }
        }

        [TestMethod]
        public void MainViewModel_integration_leaves_messages_empty_until_user_adds_from_DBC()
        {
            var mainViewModel = new MainViewModel();

            Assert.AreEqual(0, mainViewModel.Messages.Count);
            Assert.AreEqual(0, mainViewModel.Signals.Count);

            mainViewModel.Dbc.LoadDbcText("Sample.dbc", null, SampleDbc);

            // Messages list remains empty initially upon loading DBC
            Assert.AreEqual(0, mainViewModel.Messages.Count);
            Assert.AreEqual(0, mainViewModel.Signals.Count);
            Assert.IsTrue(mainViewModel.Simulation.CanAddMessages);

            // User explicitly adds messages from the loaded DBC
            mainViewModel.Simulation.AddMessages(mainViewModel.Dbc.LoadedDocument!.Messages);

            Assert.AreEqual(2, mainViewModel.Messages.Count);
            Assert.AreEqual(3, mainViewModel.Signals.Count);
            Assert.AreEqual("EngineData", mainViewModel.Messages[0].Name);
            Assert.AreEqual("VehicleSpeed", mainViewModel.Messages[1].Name);
            Assert.AreEqual("EngineSpeed", mainViewModel.Signals[0].Name);

            mainViewModel.Dbc.UnloadDbc();

            Assert.AreEqual(0, mainViewModel.Messages.Count);
            Assert.AreEqual(0, mainViewModel.Signals.Count);
            Assert.IsFalse(mainViewModel.Simulation.CanAddMessages);
        }

        private sealed class FakeFileDialogService : IFileDialogService
        {
            private readonly string? _returnPath;

            public FakeFileDialogService(string? returnPath)
            {
                _returnPath = returnPath;
            }

            public string? OpenFileDialog(string filter, string title)
            {
                return _returnPath;
            }
        }
    }
}
