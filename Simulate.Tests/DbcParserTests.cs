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
        public void Parse_reports_an_unrepresented_dbc_statement_without_silently_interpreting_it()
        {
            const string documentText = """
                BO_ 291 Status: 8 Gateway
                VAL_ 291 Mode 0 "Off" 1 "On";
                """;

            DbcParseResult result = DbcParser.Parse(documentText);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(DbcParseIssueSeverity.Warning, result.Issues[0].Severity);
            Assert.AreEqual(DbcParseIssueCode.UnsupportedStatement, result.Issues[0].Code);
            Assert.AreEqual(2, result.Issues[0].LineNumber);
            Assert.AreEqual("VAL_ 291 Mode 0 \"Off\" 1 \"On\";", result.Issues[0].Context);
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
