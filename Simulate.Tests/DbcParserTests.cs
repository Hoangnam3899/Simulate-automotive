using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class DbcParserTests
    {
        [TestMethod]
        public void Parse_returns_nodes_messages_and_signals_with_their_declared_metadata()
        {
            const string documentText = """
                VERSION "1.0"
                BU_: Engine Gateway
                BO_ 291 EngineData: 8 Engine
                 SG_ EngineSpeed : 0|16@1+ (0.125,-40) [-40|8031.875] "rpm" Gateway
                 SG_ CoolantTemperature : 7|8@0- (1,-40) [-40|215] "degC" Gateway
                """;
            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Issues.Count);

            DbcDocument document = result.Document ?? throw new AssertFailedException("A valid DBC document must be returned.");
            Assert.AreEqual(2, document.Nodes.Count);
            Assert.AreEqual("Engine", document.Nodes[0].Name);
            Assert.AreEqual("Gateway", document.Nodes[1].Name);
            Assert.AreEqual(1, document.Messages.Count);

            DbcMessage message = document.Messages[0];
            Assert.AreEqual("EngineData", message.Name);
            Assert.AreEqual((uint)291, message.Identifier);
            Assert.IsFalse(message.IsExtendedIdentifier);
            Assert.AreEqual(8, message.PayloadLength);
            Assert.AreEqual("Engine", message.Transmitter);
            Assert.AreEqual(2, message.Signals.Count);

            DbcSignal engineSpeed = message.Signals[0];
            Assert.AreEqual("EngineSpeed", engineSpeed.Name);
            Assert.AreEqual(0, engineSpeed.StartBit);
            Assert.AreEqual(16, engineSpeed.BitLength);
            Assert.AreEqual(DbcByteOrder.LittleEndian, engineSpeed.ByteOrder);
            Assert.IsFalse(engineSpeed.IsSigned);
            Assert.AreEqual(0.125d, engineSpeed.Factor);
            Assert.AreEqual(-40d, engineSpeed.Offset);
            Assert.AreEqual("rpm", engineSpeed.Unit);

            DbcSignal coolantTemperature = message.Signals[1];
            Assert.AreEqual(DbcByteOrder.BigEndian, coolantTemperature.ByteOrder);
            Assert.IsTrue(coolantTemperature.IsSigned);
            Assert.AreEqual(-40d, coolantTemperature.Minimum);
            Assert.AreEqual(215d, coolantTemperature.Maximum);
        }

        [TestMethod]
        public void Parse_normalizes_the_dbc_extended_identifier_flag()
        {
            const string documentText = """
                BU_: Gateway
                BO_ 2147483939 ExtendedData: 8 Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            DbcDocument document = result.Document ?? throw new AssertFailedException("A valid DBC document must be returned.");
            Assert.AreEqual(1, document.Messages.Count);
            Assert.AreEqual((uint)0x123, document.Messages[0].Identifier);
            Assert.IsTrue(document.Messages[0].IsExtendedIdentifier);
        }

        [TestMethod]
        public void Parse_accepts_identifier_and_payload_length_boundaries_supported_by_can()
        {
            const string documentText = """
                BO_ 2047 LastStandardId: 0 Gateway
                BO_ 2684354559 LastExtendedId: 64 Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            DbcDocument document = result.Document ?? throw new AssertFailedException("A valid DBC document must be returned.");
            Assert.AreEqual((uint)0x7FF, document.Messages[0].Identifier);
            Assert.IsFalse(document.Messages[0].IsExtendedIdentifier);
            Assert.AreEqual(0, document.Messages[0].PayloadLength);
            Assert.AreEqual(CanFrame.MaximumExtendedIdentifier, document.Messages[1].Identifier);
            Assert.IsTrue(document.Messages[1].IsExtendedIdentifier);
            Assert.AreEqual(CanFrame.MaximumPayloadLength, document.Messages[1].PayloadLength);
        }

        [TestMethod]
        public void Parse_returns_line_context_when_a_standard_identifier_exceeds_eleven_bits()
        {
            const string documentText = "BO_ 2048 InvalidStandardId: 8 Gateway";

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Document);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(DbcParseIssueCode.InvalidMessageIdentifier, result.Issues[0].Code);
            Assert.AreEqual(1, result.Issues[0].LineNumber);
            Assert.AreEqual(documentText, result.Issues[0].Context);
        }

        [TestMethod]
        public void Parse_rejects_a_duplicate_normalized_message_identity_at_its_declaration_line()
        {
            const string documentText = """
                BO_ 2147483939 FirstExtendedMessage: 8 Gateway
                BO_ 2147483939 DuplicateExtendedMessage: 8 Gateway
                """;
            const string duplicateLine = "BO_ 2147483939 DuplicateExtendedMessage: 8 Gateway";

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Document);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(DbcParseIssueCode.DuplicateMessageIdentifier, result.Issues[0].Code);
            Assert.AreEqual(2, result.Issues[0].LineNumber);
            Assert.AreEqual(duplicateLine, result.Issues[0].Context);
        }

        [TestMethod]
        public void Parse_distinguishes_standard_and_extended_messages_with_the_same_normalized_identifier()
        {
            const string documentText = """
                BO_ 291 StandardMessage: 8 Gateway
                BO_ 2147483939 ExtendedMessage: 8 Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            DbcDocument document = result.Document ?? throw new AssertFailedException("A valid DBC document must be returned.");
            Assert.AreEqual(2, document.Messages.Count);
            Assert.AreEqual((uint)291, document.Messages[0].Identifier);
            Assert.IsFalse(document.Messages[0].IsExtendedIdentifier);
            Assert.AreEqual((uint)291, document.Messages[1].Identifier);
            Assert.IsTrue(document.Messages[1].IsExtendedIdentifier);
        }

        [TestMethod]
        public void Parse_rejects_a_duplicate_signal_name_at_its_declaration_line()
        {
            const string documentText = """
                BO_ 291 Status: 8 Gateway
                 SG_ Mode : 0|8@1+ (1,0) [0|255] "" Gateway
                 SG_ Mode : 8|8@1+ (1,0) [0|255] "" Gateway
                """;
            const string duplicateLine = " SG_ Mode : 8|8@1+ (1,0) [0|255] \"\" Gateway";

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Document);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(DbcParseIssueCode.DuplicateSignalName, result.Issues[0].Code);
            Assert.AreEqual(3, result.Issues[0].LineNumber);
            Assert.AreEqual(duplicateLine, result.Issues[0].Context);
        }

        [TestMethod]
        public void Parse_rejects_a_little_endian_signal_that_extends_beyond_its_payload()
        {
            const string documentText = """
                BO_ 291 LittleEndianOverflow: 8 Gateway
                 SG_ Invalid : 63|2@1+ (1,0) [0|3] "" Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Document);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueCode.InvalidSignalLayout, result.Issues[0].Code);
            Assert.AreEqual(2, result.Issues[0].LineNumber);
        }

        [TestMethod]
        public void Parse_rejects_a_big_endian_signal_that_extends_beyond_its_payload()
        {
            const string documentText = """
                BO_ 291 BigEndianOverflow: 8 Gateway
                 SG_ Invalid : 56|2@0+ (1,0) [0|3] "" Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsFalse(result.IsSuccess);
            Assert.IsNull(result.Document);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueCode.InvalidSignalLayout, result.Issues[0].Code);
            Assert.AreEqual(2, result.Issues[0].LineNumber);
        }

        [TestMethod]
        public void Parse_reports_multiplexing_as_a_warning_without_creating_a_partial_signal()
        {
            const string documentText = """
                BO_ 291 MultiplexedData: 8 Gateway
                 SG_ Selector M : 0|8@1+ (1,0) [0|255] "" Gateway
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            DbcDocument document = result.Document ?? throw new AssertFailedException("A document without core errors must be returned.");
            Assert.AreEqual(0, document.Messages[0].Signals.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueSeverity.Warning, result.Issues[0].Severity);
            Assert.AreEqual(DbcParseIssueCode.UnsupportedStatement, result.Issues[0].Code);
            Assert.AreEqual(2, result.Issues[0].LineNumber);
        }

        [TestMethod]
        public void Parse_attaches_typed_value_descriptions_to_their_signal()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (0.5,-1) [-1|6.5] "" Gateway
                VAL_ 291 Mode 0 "Off" 3 "On";
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Issues.Count);

            DbcSignal signal = result.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("A valid value-description fixture must return its signal.");
            Assert.AreEqual(2, signal.ValueDescriptions.Count);
            Assert.AreEqual(0L, signal.ValueDescriptions[0].RawValue);
            Assert.AreEqual(-1d, signal.ValueDescriptions[0].PhysicalValue);
            Assert.AreEqual("Off", signal.ValueDescriptions[0].Description);
            Assert.AreEqual(3L, signal.ValueDescriptions[1].RawValue);
            Assert.AreEqual(0.5d, signal.ValueDescriptions[1].PhysicalValue);
            Assert.AreEqual("On", signal.ValueDescriptions[1].Description);
        }

        [TestMethod]
        public void Parse_does_not_attach_partial_metadata_from_an_invalid_value_description_statement()
        {
            const string documentText = """
                BO_ 291 Status: 1 Gateway
                 SG_ Mode : 0|4@1+ (1,0) [0|15] "" Gateway
                VAL_ 291 Mode 0 "Off" invalid;
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueCode.InvalidValueDescription, result.Issues[0].Code);
            DbcSignal signal = result.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("A non-core warning must still return its signal.");
            Assert.AreEqual(0, signal.ValueDescriptions.Count);
        }

        [TestMethod]
        public void Parse_reads_every_supplied_dbc_document_without_silently_accepting_invalid_core_definitions()
        {
            string repositoryRoot = FindRepositoryRoot();
            string[] filePaths = Directory.GetFiles(
                Path.Combine(repositoryRoot, "DBC"),
                "*.dbc",
                SearchOption.AllDirectories);

            Assert.AreEqual(8, filePaths.Length);

            foreach (string filePath in filePaths)
            {
                DbcParseResult result = DbcParser.Parse(File.ReadAllText(filePath));
                string errors = string.Join(
                    Environment.NewLine,
                    result.Issues
                        .Where(issue => issue.Severity == DbcParseIssueSeverity.Error)
                        .Take(3)
                        .Select(issue => $"Line {issue.LineNumber}: {issue.Message} Context: {issue.Context}"));

                Assert.IsTrue(result.IsSuccess, $"Expected '{filePath}' to have no parser errors.{Environment.NewLine}{errors}");
                DbcDocument document = result.Document ?? throw new AssertFailedException($"Expected '{filePath}' to return a document.");
                Assert.IsTrue(document.Messages.Count > 0, $"Expected '{filePath}' to contain messages.");
                Assert.IsTrue(
                    document.Messages.Sum(message => message.Signals.Count) > 0,
                    $"Expected '{filePath}' to expose parsed signal metadata.");
                Assert.IsTrue(
                    document.Messages.Sum(message =>
                        message.Signals.Sum(signal => signal.ValueDescriptions.Count)) > 0,
                    $"Expected '{filePath}' to expose typed VAL_ metadata.");
            }
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Simulate.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new AssertFailedException("Could not locate the repository root containing Simulate.sln.");
        }
    }
}
