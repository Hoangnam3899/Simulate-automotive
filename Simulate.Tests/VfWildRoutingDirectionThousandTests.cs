using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class VfWildRoutingDirectionThousandTests
    {
        private const string VfWildRoutingDir = @"C:\Users\Hnam\Downloads\VF_Wild_v4.0.0_20260825_final\Routing";
        private static readonly Dictionary<string, DbcDocument> _dbcCache = new(StringComparer.OrdinalIgnoreCase);

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                return;
            }

            string[] dbcFiles = Directory.GetFiles(VfWildRoutingDir, "*.dbc");
            foreach (string file in dbcFiles)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    var result = DbcParser.Parse(content);
                    if (result.Document != null)
                    {
                        _dbcCache[Path.GetFileName(file)] = result.Document;
                    }
                }
                catch
                {
                    // Cache only successful DBCs
                }
            }
        }

        private static DbcDocument GetCachedOrLoad(string fileName)
        {
            if (_dbcCache.TryGetValue(fileName, out var doc))
            {
                return doc;
            }

            string fullPath = Path.Combine(VfWildRoutingDir, fileName);
            if (File.Exists(fullPath))
            {
                string content = File.ReadAllText(fullPath);
                var result = DbcParser.Parse(content);
                if (result.Document != null)
                {
                    _dbcCache[fileName] = result.Document;
                    return result.Document;
                }
            }

            throw new AssertFailedException($"Cannot load DBC file: {fileName}");
        }

        public static IEnumerable<object[]> GetDbcFilesForValidation()
        {
            string[] knownFiles = [
                "01_ICAN_v4.0.0_20260825.dbc",
                "02_BCAN_v4.0.0_20260824.dbc",
                "03_CCAN_v4.0.0_20260815.dbc",
                "04_PCAN_v4.0.0_20260825.dbc",
                "05_LCAN_v4.0.0_20260815.dbc",
                "06_SCAN_v4.0.0_20260815.dbc",
                "07_DCAN_v3.1.0_20260617.dbc",
                "08_REV_CAN_v4.0.0_20260807.dbc",
                "09_C2_CAN_v3.0.1_20260601.dbc"
            ];
            foreach (string file in knownFiles)
            {
                yield return new object[] { file };
            }
        }

        public static IEnumerable<object[]> GetNormalEvaluationCases()
        {
            // 350 test cases: parameterize across messages from VF Wild DBCs
            string[] candidateFiles = [
                "04_PCAN_v4.0.0_20260825.dbc",
                "01_ICAN_v4.0.0_20260825.dbc",
                "02_BCAN_v4.0.0_20260824.dbc",
                "03_CCAN_v4.0.0_20260815.dbc"
            ];

            int count = 0;
            foreach (string fileName in candidateFiles)
            {
                DbcDocument doc;
                try
                {
                    doc = GetCachedOrLoad(fileName);
                }
                catch
                {
                    continue;
                }

                foreach (DbcMessage msg in doc.Messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Transmitter) || msg.Transmitter.Contains("Vector__XXX"))
                    {
                        continue;
                    }

                    yield return new object[] { fileName, msg.Identifier, msg.IsExtendedIdentifier, msg.Name, msg.Transmitter, count };
                    count++;
                    if (count >= 350)
                    {
                        yield break;
                    }
                }
            }

            // Fill up to 350 if fewer distinct messages
            while (count < 350)
            {
                yield return new object[] { "04_PCAN_v4.0.0_20260825.dbc", 294u, false, "BMS_AllowChargeCurr", "BMS", count };
                count++;
            }
        }

        public static IEnumerable<object[]> GetInvertedEvaluationCases()
        {
            // 350 test cases: parameterize across messages from VF Wild DBCs
            string[] candidateFiles = [
                "04_PCAN_v4.0.0_20260825.dbc",
                "01_ICAN_v4.0.0_20260825.dbc",
                "02_BCAN_v4.0.0_20260824.dbc",
                "03_CCAN_v4.0.0_20260815.dbc"
            ];

            int count = 0;
            foreach (string fileName in candidateFiles)
            {
                DbcDocument doc;
                try
                {
                    doc = GetCachedOrLoad(fileName);
                }
                catch
                {
                    continue;
                }

                foreach (DbcMessage msg in doc.Messages)
                {
                    if (string.IsNullOrWhiteSpace(msg.Transmitter) || msg.Transmitter.Contains("Vector__XXX"))
                    {
                        continue;
                    }

                    yield return new object[] { fileName, msg.Identifier, msg.IsExtendedIdentifier, msg.Name, msg.Transmitter, count };
                    count++;
                    if (count >= 350)
                    {
                        yield break;
                    }
                }
            }

            while (count < 350)
            {
                yield return new object[] { "04_PCAN_v4.0.0_20260825.dbc", 294u, false, "BMS_AllowChargeCurr", "BMS", count };
                count++;
            }
        }

        public static IEnumerable<object[]> GetMultiCycleConnectDisconnectCases()
        {
            // 250 test cases: test repeated Connect -> Feed -> Verify -> Disconnect -> Reset -> Reconnect cycles
            for (int i = 0; i < 250; i++)
            {
                bool testInverted = (i % 2 == 1);
                yield return new object[] { i, testInverted };
            }
        }

        public static IEnumerable<object[]> GetEdgeCasesAndNoiseImmunity()
        {
            // 50 edge cases
            for (int i = 0; i < 50; i++)
            {
                yield return new object[] { i };
            }
        }

        // --- 1. 9 Tests: Validate All 9 VF Wild Routing DBC Files ---
        [DataTestMethod]
        [DynamicData(nameof(GetDbcFilesForValidation), DynamicDataSourceType.Method)]
        public void Test_01_VfWild_Dbc_File_Integrity_And_Transmitter_Validation(string fileName)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                Assert.Inconclusive("VF Wild Routing folder not found on disk.");
                return;
            }

            DbcDocument doc = GetCachedOrLoad(fileName);
            Assert.IsNotNull(doc, $"DBC document '{fileName}' must parse successfully.");
            Assert.IsTrue(doc.Messages.Count > 0, $"DBC '{fileName}' must contain at least 1 message.");
            Assert.IsTrue(doc.Nodes.Count > 0, $"DBC '{fileName}' must contain at least 1 node.");

            bool hasValidTransmitter = doc.Messages.Any(m => !string.IsNullOrWhiteSpace(m.Transmitter) && !m.Transmitter.Contains("Vector__XXX"));
            Assert.IsTrue(hasValidTransmitter, $"DBC '{fileName}' must have at least one message with a valid transmitter node.");
        }

        // --- 2. 350 Tests: Normal Direction Evaluation (Connected Correctly to RX) ---
        [DataTestMethod]
        [DynamicData(nameof(GetNormalEvaluationCases), DynamicDataSourceType.Method)]
        public void Test_02_Normal_Channel_Direction_Evaluation(
            string fileName,
            uint canId,
            bool isExtended,
            string messageName,
            string transmitter,
            int caseIndex)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                Assert.Inconclusive("VF Wild Routing folder not found on disk.");
                return;
            }

            DbcDocument doc = GetCachedOrLoad(fileName);
            var analyzer = new CanChannelDirectionAnalyzer();
            analyzer.StartSampling(doc);

            Assert.IsTrue(analyzer.IsSampling);
            Assert.IsFalse(analyzer.IsEvaluated);
            Assert.AreEqual(ChannelDirectionStatus.Unknown, analyzer.CurrentStatus);

            DirectionEvaluationResult? firedResult = null;
            analyzer.DirectionEvaluated += res => firedResult = res;

            byte[] dummyData = new byte[8];
            var frame = isExtended
                ? CanFrame.CreateClassic(canId, true, dummyData)
                : CanFrame.CreateClassic(canId, false, dummyData);

            // Feed frames to RX (Correct physical connection)
            for (int i = 0; i < CanChannelDirectionAnalyzer.MinimumSampleThreshold; i++)
            {
                analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Rx, frame, DateTimeOffset.UtcNow));
            }

            Assert.IsTrue(analyzer.IsEvaluated, $"Analyzer must complete evaluation for case {caseIndex}.");
            Assert.IsFalse(analyzer.IsSampling);
            Assert.AreEqual(ChannelDirectionStatus.Normal, analyzer.CurrentStatus);
            Assert.IsNotNull(firedResult);
            Assert.AreEqual(ChannelDirectionStatus.Normal, firedResult.Status);
            Assert.AreEqual(transmitter, firedResult.TransmitterNode);
            StringAssert.Contains(firedResult.Details, transmitter);
        }

        // --- 3. 350 Tests: Inverted Direction Evaluation (Connected In Reverse to TX) ---
        [DataTestMethod]
        [DynamicData(nameof(GetInvertedEvaluationCases), DynamicDataSourceType.Method)]
        public void Test_03_Inverted_Channel_Direction_Evaluation(
            string fileName,
            uint canId,
            bool isExtended,
            string messageName,
            string transmitter,
            int caseIndex)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                Assert.Inconclusive("VF Wild Routing folder not found on disk.");
                return;
            }

            DbcDocument doc = GetCachedOrLoad(fileName);
            var analyzer = new CanChannelDirectionAnalyzer();
            analyzer.StartSampling(doc);

            DirectionEvaluationResult? firedResult = null;
            analyzer.DirectionEvaluated += res => firedResult = res;

            byte[] dummyData = new byte[8];
            var frame = isExtended
                ? CanFrame.CreateClassic(canId, true, dummyData)
                : CanFrame.CreateClassic(canId, false, dummyData);

            // Feed frames to TX (Inverted physical connection)
            for (int i = 0; i < CanChannelDirectionAnalyzer.MinimumSampleThreshold; i++)
            {
                analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Tx, frame, DateTimeOffset.UtcNow));
            }

            Assert.IsTrue(analyzer.IsEvaluated, $"Analyzer must complete evaluation for case {caseIndex}.");
            Assert.IsFalse(analyzer.IsSampling);
            Assert.AreEqual(ChannelDirectionStatus.SuspectedInverted, analyzer.CurrentStatus);
            Assert.IsNotNull(firedResult);
            Assert.AreEqual(ChannelDirectionStatus.SuspectedInverted, firedResult.Status);
            Assert.AreEqual(transmitter, firedResult.TransmitterNode);
            StringAssert.Contains(firedResult.Details, "Nghi vấn cắm ngược");
        }

        // --- 4. 250 Tests: Multi-Cycle Connect / Disconnect / Reconnect Idempotent Lifecycle ---
        [DataTestMethod]
        [DynamicData(nameof(GetMultiCycleConnectDisconnectCases), DynamicDataSourceType.Method)]
        public void Test_04_MultiCycle_Connect_Disconnect_Idempotent_Lifecycle(int cycleIndex, bool testInverted)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                Assert.Inconclusive("VF Wild Routing folder not found on disk.");
                return;
            }

            DbcDocument doc = GetCachedOrLoad("04_PCAN_v4.0.0_20260825.dbc");
            var analyzer = new CanChannelDirectionAnalyzer();

            // Simulate repeated Connect -> Feed -> Disconnect -> Reconnect cycles
            for (int round = 0; round < 3; round++)
            {
                // 1. Connect & StartSampling
                analyzer.StartSampling(doc);
                Assert.IsTrue(analyzer.IsSampling);
                Assert.AreEqual(ChannelDirectionStatus.Unknown, analyzer.CurrentStatus);

                DirectionEvaluationResult? firedResult = null;
                analyzer.DirectionEvaluated += res => firedResult = res;

                var frame = CanFrame.CreateClassic(294u, false, new byte[8]); // BMS_AllowChargeCurr
                CanGatewaySide side = testInverted ? CanGatewaySide.Tx : CanGatewaySide.Rx;

                for (int f = 0; f < CanChannelDirectionAnalyzer.MinimumSampleThreshold; f++)
                {
                    analyzer.ProcessFrame(new RoutedCanFrame(side, frame, DateTimeOffset.UtcNow));
                }

                Assert.IsTrue(analyzer.IsEvaluated);
                Assert.AreEqual(testInverted ? ChannelDirectionStatus.SuspectedInverted : ChannelDirectionStatus.Normal, analyzer.CurrentStatus);
                Assert.IsNotNull(firedResult);

                // 2. Disconnect & Reset
                analyzer.Reset();
                Assert.IsFalse(analyzer.IsSampling);
                Assert.IsFalse(analyzer.IsEvaluated);
                Assert.AreEqual(ChannelDirectionStatus.Unknown, analyzer.CurrentStatus);
            }
        }

        // --- 5. 50 Tests: Edge Cases & Noise Immunity ---
        [DataTestMethod]
        [DynamicData(nameof(GetEdgeCasesAndNoiseImmunity), DynamicDataSourceType.Method)]
        public void Test_05_Edge_Cases_And_Noise_Immunity(int caseIndex)
        {
            if (!Directory.Exists(VfWildRoutingDir))
            {
                Assert.Inconclusive("VF Wild Routing folder not found on disk.");
                return;
            }

            DbcDocument doc = GetCachedOrLoad("04_PCAN_v4.0.0_20260825.dbc");
            var analyzer = new CanChannelDirectionAnalyzer();

            switch (caseIndex % 5)
            {
                case 0:
                    // Processing null frame does not throw
                    analyzer.StartSampling(doc);
                    analyzer.ProcessFrame(null!);
                    Assert.IsTrue(analyzer.IsSampling);
                    analyzer.Reset();
                    break;

                case 1:
                    // Frame not in DBC is ignored safely
                    analyzer.StartSampling(doc);
                    var unknownFrame = CanFrame.CreateClassic(0x7FF, false, new byte[8]);
                    for (int i = 0; i < 50; i++)
                    {
                        analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Rx, unknownFrame, DateTimeOffset.UtcNow));
                    }
                    Assert.IsFalse(analyzer.IsEvaluated, "Unknown frames must not trigger false evaluation.");
                    analyzer.Reset();
                    break;

                case 2:
                    // Rapid Reset calls are idempotent
                    for (int r = 0; r < 20; r++)
                    {
                        analyzer.Reset();
                    }
                    Assert.AreEqual(ChannelDirectionStatus.Unknown, analyzer.CurrentStatus);
                    break;

                case 3:
                    // Reset while sampling cancels sampling cleanly
                    analyzer.StartSampling(doc);
                    Assert.IsTrue(analyzer.IsSampling);
                    analyzer.Reset();
                    Assert.IsFalse(analyzer.IsSampling);
                    Assert.IsFalse(analyzer.IsEvaluated);
                    break;

                case 4:
                    // Mixed traffic: dominant RX (80%) vs occasional TX (20%) resolves to Normal
                    analyzer.StartSampling(doc);
                    var bmsFrame = CanFrame.CreateClassic(294u, false, new byte[8]);
                    for (int i = 0; i < 16; i++)
                    {
                        analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Rx, bmsFrame, DateTimeOffset.UtcNow));
                    }
                    analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Tx, bmsFrame, DateTimeOffset.UtcNow));
                    analyzer.ProcessFrame(new RoutedCanFrame(CanGatewaySide.Tx, bmsFrame, DateTimeOffset.UtcNow));

                    Assert.IsTrue(analyzer.IsEvaluated);
                    Assert.AreEqual(ChannelDirectionStatus.Normal, analyzer.CurrentStatus);
                    analyzer.Reset();
                    break;
            }
        }
    }
}
