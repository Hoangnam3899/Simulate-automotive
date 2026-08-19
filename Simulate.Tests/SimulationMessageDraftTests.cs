using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SimulationMessageDraftTests
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

BO_ 768 TransmissionStatus: 2 NodeA
 SG_ Gear : 0|4@1+ (1,0) [0|10] """" NodeB
";

        private static DbcDocument CreateSampleDocument()
        {
            DbcParseResult result = DbcParser.Parse(SampleDbc);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Document);
            return result.Document;
        }

        [TestMethod]
        public void Initial_state_has_empty_messages_and_null_selected_message()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));

            Assert.AreEqual(0, viewModel.Messages.Count);
            Assert.AreEqual(0, viewModel.Signals.Count);
            Assert.IsNull(viewModel.SelectedMessage);
            Assert.IsFalse(viewModel.CanAddMessages);
            Assert.IsFalse(viewModel.CanDeleteMessage);
            Assert.IsFalse(viewModel.CanDeleteAllMessages);
            Assert.IsFalse(viewModel.CanMoveUp);
            Assert.IsFalse(viewModel.CanMoveDown);
        }

        [TestMethod]
        public void LoadDocument_enables_CanAddMessages_and_leaves_Messages_empty()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();

            viewModel.LoadDocument(doc);

            Assert.AreEqual(0, viewModel.Messages.Count);
            Assert.AreEqual(0, viewModel.Signals.Count);
            Assert.IsNull(viewModel.SelectedMessage);
            Assert.IsTrue(viewModel.CanAddMessages);
            Assert.IsFalse(viewModel.CanDeleteMessage);
            Assert.IsFalse(viewModel.CanDeleteAllMessages);
        }

        [TestMethod]
        public void AddMessage_adds_single_message_with_correct_fields_and_display_index()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);

            viewModel.AddMessage(doc.Messages[0]);

            Assert.AreEqual(1, viewModel.Messages.Count);
            MessageModel msg = viewModel.Messages[0];
            Assert.AreEqual(1, msg.DisplayIndex);
            Assert.AreEqual("0x100", msg.Id);
            Assert.AreEqual("EngineData", msg.Name);
            Assert.AreEqual(8, msg.Dlc);
            Assert.AreEqual(2, msg.SignalCount);
            Assert.AreEqual("100 ms", msg.Cycle);
            Assert.AreEqual("PassThrough", msg.GatewayMode);
            Assert.AreEqual("Cyclic", msg.SendType);
            Assert.IsTrue(msg.IsEnabled);
            Assert.AreEqual("—", msg.LastSent);

            Assert.AreEqual(2, viewModel.Signals.Count);
            Assert.AreSame(msg, viewModel.SelectedMessage);
            Assert.IsTrue(viewModel.CanDeleteMessage);
            Assert.IsTrue(viewModel.CanDeleteAllMessages);
            Assert.IsFalse(viewModel.CanMoveUp);
            Assert.IsFalse(viewModel.CanMoveDown);
        }

        [TestMethod]
        public void AddMessages_adds_multiple_messages_with_sequential_display_indices()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);

            viewModel.AddMessages(doc.Messages);

            Assert.AreEqual(3, viewModel.Messages.Count);
            Assert.AreEqual(1, viewModel.Messages[0].DisplayIndex);
            Assert.AreEqual(2, viewModel.Messages[1].DisplayIndex);
            Assert.AreEqual(3, viewModel.Messages[2].DisplayIndex);
            Assert.AreEqual("0x100", viewModel.Messages[0].Id);
            Assert.AreEqual("0x200", viewModel.Messages[1].Id);
            Assert.AreEqual("0x300", viewModel.Messages[2].Id);
        }

        [TestMethod]
        public void AddMessages_ignores_duplicate_messages()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);

            viewModel.AddMessage(doc.Messages[0]);
            viewModel.AddMessages(new[] { doc.Messages[0], doc.Messages[1] });

            Assert.AreEqual(2, viewModel.Messages.Count);
            Assert.AreEqual(1, viewModel.Messages[0].DisplayIndex);
            Assert.AreEqual(2, viewModel.Messages[1].DisplayIndex);
        }

        [TestMethod]
        public void DeleteMessage_removes_selected_and_reindexes()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessages(doc.Messages);

            viewModel.SelectedMessage = viewModel.Messages[1]; // Select 0x200 (VehicleSpeed)
            viewModel.DeleteMessage();

            Assert.AreEqual(2, viewModel.Messages.Count);
            Assert.AreEqual("EngineData", viewModel.Messages[0].Name);
            Assert.AreEqual(1, viewModel.Messages[0].DisplayIndex);
            Assert.AreEqual("TransmissionStatus", viewModel.Messages[1].Name);
            Assert.AreEqual(2, viewModel.Messages[1].DisplayIndex);
            Assert.AreSame(viewModel.Messages[1], viewModel.SelectedMessage);
        }

        [TestMethod]
        public void DeleteAllMessages_clears_entire_list()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessages(doc.Messages);

            viewModel.DeleteAllMessages();

            Assert.AreEqual(0, viewModel.Messages.Count);
            Assert.AreEqual(0, viewModel.Signals.Count);
            Assert.IsNull(viewModel.SelectedMessage);
            Assert.IsFalse(viewModel.CanDeleteMessage);
            Assert.IsFalse(viewModel.CanDeleteAllMessages);
        }

        [TestMethod]
        public void MoveUp_and_MoveDown_reorders_messages_and_updates_indices()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessages(doc.Messages);

            // Select middle item (index 1: VehicleSpeed)
            viewModel.SelectedMessage = viewModel.Messages[1];
            Assert.IsTrue(viewModel.CanMoveUp);
            Assert.IsTrue(viewModel.CanMoveDown);

            viewModel.MoveUp();

            Assert.AreEqual("VehicleSpeed", viewModel.Messages[0].Name);
            Assert.AreEqual(1, viewModel.Messages[0].DisplayIndex);
            Assert.AreEqual("EngineData", viewModel.Messages[1].Name);
            Assert.AreEqual(2, viewModel.Messages[1].DisplayIndex);
            Assert.AreSame(viewModel.Messages[0], viewModel.SelectedMessage);
            Assert.IsFalse(viewModel.CanMoveUp);
            Assert.IsTrue(viewModel.CanMoveDown);

            viewModel.MoveDown();

            Assert.AreEqual("EngineData", viewModel.Messages[0].Name);
            Assert.AreEqual("VehicleSpeed", viewModel.Messages[1].Name);
            Assert.AreEqual(2, viewModel.Messages[1].DisplayIndex);
        }

        [TestMethod]
        public void ClearDocument_resets_messages_and_disables_commands()
        {
            var viewModel = new SimulationViewModel(new FakeMessageDialogService(null));
            DbcDocument doc = CreateSampleDocument();
            viewModel.LoadDocument(doc);
            viewModel.AddMessages(doc.Messages);

            viewModel.ClearDocument();

            Assert.AreEqual(0, viewModel.Messages.Count);
            Assert.AreEqual(0, viewModel.Signals.Count);
            Assert.IsNull(viewModel.SelectedMessage);
            Assert.IsFalse(viewModel.CanAddMessages);
        }

        [TestMethod]
        public void AddMessagesCommand_invokes_dialog_service_and_adds_returned_messages()
        {
            DbcDocument doc = CreateSampleDocument();
            var fakeDialog = new FakeMessageDialogService(new[] { doc.Messages[1] });
            var viewModel = new SimulationViewModel(fakeDialog);
            viewModel.LoadDocument(doc);

            viewModel.AddMessagesCommand.Execute(null);

            Assert.AreEqual(1, viewModel.Messages.Count);
            Assert.AreEqual("VehicleSpeed", viewModel.Messages[0].Name);
        }

        [TestMethod]
        public void SelectMessageWindow_initializes_without_xaml_parse_exceptions()
        {
            Exception? caughtException = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    DbcDocument doc = CreateSampleDocument();
                    var window = new Simulate.Views.SelectMessageWindow(doc);
                    Assert.IsNotNull(window);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtException, $"SelectMessageWindow failed with: {caughtException}");
        }

        private sealed class FakeMessageDialogService : IMessageDialogService
        {
            private readonly IReadOnlyList<DbcMessage>? _messagesToReturn;

            public FakeMessageDialogService(IReadOnlyList<DbcMessage>? messagesToReturn)
            {
                _messagesToReturn = messagesToReturn;
            }

            public IReadOnlyList<DbcMessage>? SelectMessages(DbcDocument document, IReadOnlyList<DbcMessage> alreadyAddedMessages)
            {
                return _messagesToReturn;
            }
        }
    }
}
