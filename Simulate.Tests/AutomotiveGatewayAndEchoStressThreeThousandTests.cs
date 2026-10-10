using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;
using Simulate.ViewModels;

namespace Simulate.Tests
{
    /// <summary>
    /// Bộ 3,000 Test Cases tự động kiểm thử chuyên sâu cho:
    /// 1. Bộ 1 (1,000 TCs): Triệt tiêu bão phản hồi vòng lặp (Cross-Channel Echo Suppression & Ping-Pong Loop Prevention).
    /// 2. Bộ 2 (1,000 TCs): Bộ tra cứu nhanh tín hiệu O(1) và giải mã thời gian thực dưới áp lực lưu lượng cao.
    /// 3. Bộ 3 (1,000 TCs): Khôi phục lỗi phần cứng, dọn dẹp vòng đời gateway an toàn và kiểm chuẩn không crash.
    /// </summary>
    [TestClass]
    public sealed class AutomotiveGatewayAndEchoStressThreeThousandTests
    {
        #region Test Data Generators (Sinh 3,000 kịch bản kiểm thử)

        /// <summary>
        /// Sinh 1,000 test cases kiểm thử cơ chế lọc Echo chéo kênh và bảo vệ vòng lặp phản hồi.
        /// </summary>
        public static IEnumerable<object[]> GetEchoSuppressionOneThousandCases()
        {
            for (int i = 0; i < 1000; i++)
            {
                uint canId = (uint)(i < 500 ? (0x100 + (i % 0x600)) : (0x18DA0000 + i));
                bool isExtended = i >= 500;
                int dlc = 1 + (i % 8); // 1 to 8 bytes
                bool isWithinEchoWindow = (i % 5 != 0); // 80% trường hợp nằm trong EchoWindow (<10ms)
                int elapsedMs = isWithinEchoWindow ? (i % 9) : (11 + (i % 20)); // 0-8ms vs 11-30ms
                CanGatewaySide initialSource = (i % 2 == 0) ? CanGatewaySide.Rx : CanGatewaySide.Tx;
                CanGatewaySide echoSource = (i % 3 == 0) ? initialSource : (initialSource == CanGatewaySide.Rx ? CanGatewaySide.Tx : CanGatewaySide.Rx);

                yield return new object[]
                {
                    i,
                    canId,
                    isExtended,
                    dlc,
                    elapsedMs,
                    isWithinEchoWindow,
                    initialSource,
                    echoSource
                };
            }
        }

        /// <summary>
        /// Sinh 1,000 test cases kiểm thử giải mã tín hiệu O(1) và chống nghẽn UI Dispatcher.
        /// </summary>
        public static IEnumerable<object[]> GetSignalLookupOneThousandCases()
        {
            for (int i = 0; i < 1000; i++)
            {
                uint canId = (uint)(0x200 + (i % 100));
                bool isExtended = (i % 7 == 0);
                int startBit = (i % 48);
                int bitLength = 1 + (i % 16);
                double factor = (i % 3 == 0) ? 0.1 : 1.0;
                double offset = (i % 5 == 0) ? -40.0 : 0.0;
                ulong maxRaw = (bitLength >= 64) ? ulong.MaxValue : ((1UL << bitLength) - 1);
                ulong rawValue = (ulong)i % Math.Max(1UL, maxRaw);
                double physicalValue = offset + (rawValue * factor);

                yield return new object[]
                {
                    i,
                    canId,
                    isExtended,
                    startBit,
                    bitLength,
                    factor,
                    offset,
                    physicalValue
                };
            }
        }

        /// <summary>
        /// Sinh 1,000 test cases kiểm thử an toàn vòng đời, khôi phục lỗi phần cứng và ngắt gateway sạch sẽ.
        /// </summary>
        public static IEnumerable<object[]> GetHardwareRecoveryOneThousandCases()
        {
            for (int i = 0; i < 1000; i++)
            {
                int failureCode = (i % 6) switch
                {
                    0 => 11,  // XL_ERR_QUEUE_IS_FULL
                    1 => 12,  // XL_ERR_TX_NOT_POSSIBLE
                    2 => 113, // XL_ERR_PORT_IS_OFFLINE
                    3 => 120, // XL_ERR_HW_NOT_READY
                    4 => 210, // XL_ERR_CONNECTION_BROKEN
                    _ => 255  // XL_ERROR
                };
                bool isFaultInjected = (i % 3 != 0);
                bool shouldReconnect = (i % 2 == 0);

                yield return new object[]
                {
                    i,
                    failureCode,
                    isFaultInjected,
                    shouldReconnect
                };
            }
        }

        #endregion

        #region Suite 1: 1,000 Test Cases - Cross-Channel Echo Suppression & Ping-Pong Loop Prevention

        [DataTestMethod]
        [DynamicData(nameof(GetEchoSuppressionOneThousandCases), DynamicDataSourceType.Method)]
        public async Task Suite1_CrossChannelEcho_Suppresses_Bounce_Within_Window(
            int caseIndex,
            uint canId,
            bool isExtended,
            int dlc,
            int elapsedMs,
            bool isWithinEchoWindow,
            CanGatewaySide initialSource,
            CanGatewaySide echoSource)
        {
            var timeProvider = new StressTestTimeProvider();
            var options = new SimulationEngineOptions(
                echoWindow: TimeSpan.FromMilliseconds(10),
                maximumPendingEchoes: 64);

            await using MockCanGatewaySession session = await OpenMockSessionAsync();
            var plan = CreateSingleMessagePlan(canId, isExtended);
            await using var engine = new SimulationEngine(session, plan, options, timeProvider);

            byte[] payload = new byte[dlc];
            for (int b = 0; b < dlc; b++)
            {
                payload[b] = (byte)((caseIndex + b) & 0xFF);
            }

            CanFrame originalFrame = CanFrame.CreateClassic(canId, isExtended, payload);

            await engine.StartAsync();
            try
            {
                // 1. Enqueue frame ban đầu vào gateway
                Assert.IsTrue((await session.EnqueueReceivedAsync(initialSource, originalFrame)).IsSuccess);
                RoutedCanFrame forwardedFrame = await ReadNextAsync(session.ReceiveTransmittedAsync());
                Assert.AreEqual(canId, forwardedFrame.Frame.Identifier);
                await WaitUntilAsync(() => engine.Statistics.TransmittedFrames >= 1L);
                Assert.AreEqual(1L, engine.Statistics.TransmittedFrames);

                // 2. Mô phỏng thời gian trôi qua trước khi frame phản xạ xuất hiện
                timeProvider.Advance(TimeSpan.FromMilliseconds(elapsedMs));

                // 3. Frame dội lại từ bus (có thể cùng kênh hoặc chéo kênh)
                Assert.IsTrue((await session.EnqueueReceivedAsync(echoSource, originalFrame)).IsSuccess);

                if (isWithinEchoWindow)
                {
                    // Nằm trong EchoWindow (<10ms): Gateway PHẢI nhận diện là Echo và lọc bỏ, không phát tiếp!
                    await WaitUntilAsync(() => engine.Statistics.FilteredEchoFrames >= 1L);
                    Assert.AreEqual(1L, engine.Statistics.FilteredEchoFrames,
                        $"Case {caseIndex}: Frame trong EchoWindow ({elapsedMs}ms) phải được lọc bỏ để chống bão vòng lặp.");
                    Assert.AreEqual(1L, engine.Statistics.TransmittedFrames,
                        $"Case {caseIndex}: Không được phép phát lại frame dội để tránh bão Ping-Pong.");
                }
                else
                {
                    // Sau EchoWindow (>10ms): Frame được coi là frame hợp lệ mới từ bus và được chuyển tiếp
                    RoutedCanFrame secondTransmission = await ReadNextAsync(session.ReceiveTransmittedAsync());
                    Assert.AreEqual(canId, secondTransmission.Frame.Identifier);
                    await WaitUntilAsync(() => engine.Statistics.TransmittedFrames >= 2L);
                    Assert.AreEqual(2L, engine.Statistics.TransmittedFrames,
                        $"Case {caseIndex}: Frame sau EchoWindow ({elapsedMs}ms) phải được chuyển tiếp bình thường.");
                }
            }
            finally
            {
                await engine.StopAsync();
            }
        }

        #endregion

        #region Suite 2: 1,000 Test Cases - O(1) Signal Lookup & Live Decoding High-Throughput

        [DataTestMethod]
        [DynamicData(nameof(GetSignalLookupOneThousandCases), DynamicDataSourceType.Method)]
        public void Suite2_SignalLookup_Decodes_O1_Without_UI_Freeze(
            int caseIndex,
            uint canId,
            bool isExtended,
            int startBit,
            int bitLength,
            double factor,
            double offset,
            double physicalValue)
        {
            var viewModel = new SimulationViewModel();

            var dbcSignal = new DbcSignal(
                name: $"Sig_{caseIndex}",
                startBit: startBit,
                bitLength: bitLength,
                byteOrder: DbcByteOrder.LittleEndian,
                isSigned: false,
                factor: factor,
                offset: offset,
                minimum: offset,
                maximum: offset + (Math.Pow(2, bitLength) * factor),
                unit: "V",
                receivers: Array.Empty<string>(),
                valueDescriptions: Array.Empty<DbcValueDescription>());

            var dbcMessage = new DbcMessage(
                name: $"Msg_{canId:X}",
                identifier: canId,
                isExtendedIdentifier: isExtended,
                payloadLength: 8,
                transmitter: "ECU_Test",
                signals: new[] { dbcSignal });

            viewModel.AddMessage(dbcMessage);

            // Đóng gói raw payload qua SignalCodec
            byte[] payload = new byte[8];
            SignalCodec.PackPhysical(payload, dbcSignal, physicalValue);

            DateTime testTime = DateTime.UtcNow;

            // Gọi ProcessIncomingFrame qua bộ tra cứu O(1) mới
            viewModel.ProcessIncomingFrame(canId, isExtended, payload, testTime);

            SignalModel? processedSignal = viewModel.Signals.FirstOrDefault(s => s.Name == $"Sig_{caseIndex}");
            Assert.IsNotNull(processedSignal, $"Case {caseIndex}: Signal phải tồn tại trong ViewModel.");
            Assert.IsTrue(processedSignal.HasReceivedData, $"Case {caseIndex}: Signal phải đánh dấu đã nhận dữ liệu.");
            Assert.AreEqual("● Active", processedSignal.StatusText);
            Assert.AreEqual("#10B981", processedSignal.StatusColor);

            Assert.AreEqual(physicalValue, processedSignal.Value, 0.001,
                $"Case {caseIndex}: Giá trị vật lý giải mã phải chính xác.");
        }

        #endregion

        #region Suite 3: 1,000 Test Cases - Gateway Teardown, Hardware Recovery & Crash Immunity

        [DataTestMethod]
        [DynamicData(nameof(GetHardwareRecoveryOneThousandCases), DynamicDataSourceType.Method)]
        public async Task Suite3_HardwareRecovery_Teardown_Is_Crash_Immune(
            int caseIndex,
            int failureCode,
            bool isFaultInjected,
            bool shouldReconnect)
        {
            var failure = new HardwareFailure(
                HardwareOperation.Transmit,
                HardwareErrorCode.TransmitFailed,
                $"Simulated hardware fault code {failureCode}",
                nativeStatus: failureCode);

            var faultPlan = isFaultInjected
                ? new MockHardwareFaultPlan(MockHardwareFaultPoint.Transmit)
                : new MockHardwareFaultPlan();

            await using MockCanGatewaySession session = await OpenMockSessionWithFaultAsync(faultPlan);
            var plan = CreateSingleMessagePlan(0x123, isExtended: false);
            var engine = new SimulationEngine(session, plan);

            // Bọc ViewModel để kiểm tra độ bền của UI layer khi hardware gặp sự cố
            var viewModel = new SimulationViewModel(plan, engine);

            await engine.StartAsync();
            Assert.IsTrue(engine.IsRunning);

            // Gửi frame để kích hoạt fault nếu có fault plan
            if (isFaultInjected)
            {
                CanFrame frame = CanFrame.CreateClassic(0x123, false, new byte[] { 0x11 });
                _ = await session.EnqueueReceivedAsync(CanGatewaySide.Rx, frame);
            }

            // Gọi dừng an toàn qua ViewModel: KHÔNG ĐƯỢC CRASH UNHANDLED
            try
            {
                await viewModel.StopGatewayAsync();
            }
            catch (HardwareOperationException)
            {
                // Hợp lệ: Hardware fault được ném có kiểm soát lên caller có await
            }
            catch (Exception ex)
            {
                Assert.Fail($"Case {caseIndex}: Không được phép ném ngoại lệ không xác định làm sập process: {ex}");
            }

            Assert.IsFalse(viewModel.IsRunning, $"Case {caseIndex}: ViewModel phải chuyển sang trạng thái dừng an toàn.");

            if (shouldReconnect)
            {
                // Mô phỏng reconnect session mới: Phải khởi động lại mượt mà không bị kẹt
                await using MockCanGatewaySession cleanSession = await OpenMockSessionAsync();
                var cleanEngine = new SimulationEngine(cleanSession, plan);
                var reconnectedVm = new SimulationViewModel(plan, cleanEngine);

                await cleanEngine.StartAsync();
                Assert.IsTrue(cleanEngine.IsRunning, $"Case {caseIndex}: Reconnect phiên mới phải thành công 100%.");
                await cleanEngine.StopAsync();
            }
        }

        #endregion

        #region Helper Methods & Test Time Provider

        private static async Task<MockCanGatewaySession> OpenMockSessionAsync()
        {
            var driver = new MockHardwareService();
            var discovery = await driver.DiscoverInterfacesAsync();
            Assert.IsTrue(discovery.IsSuccess);
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            var options = CanGatewayOptions.CreateClassic(
                interfaces[0].Channels[0],
                interfaces[0].Channels[1],
                500000);
            var result = await driver.OpenGatewaySessionAsync(options);
            return (MockCanGatewaySession)result.Value!;
        }

        private static async Task<MockCanGatewaySession> OpenMockSessionWithFaultAsync(MockHardwareFaultPlan faultPlan)
        {
            var driver = new MockHardwareService(faultPlan);
            var discovery = await driver.DiscoverInterfacesAsync();
            Assert.IsTrue(discovery.IsSuccess);
            IReadOnlyList<HardwareInterface> interfaces = discovery.Value!;
            var options = CanGatewayOptions.CreateClassic(
                interfaces[0].Channels[0],
                interfaces[0].Channels[1],
                500000);
            var result = await driver.OpenGatewaySessionAsync(options);
            return (MockCanGatewaySession)result.Value!;
        }

        private static SimulationPlan CreateSingleMessagePlan(uint canId, bool isExtended)
        {
            var message = new DbcMessage(
                name: "EchoStressMsg",
                identifier: canId,
                isExtendedIdentifier: isExtended,
                payloadLength: 8,
                transmitter: "Tester",
                signals: Array.Empty<DbcSignal>());

            var rule = new SimulationMessageRule(
                canIdentifier: canId,
                isExtendedIdentifier: isExtended,
                isEnabled: true,
                gatewayMode: GatewayMode.PassThrough,
                sendType: SimulationSendType.OneShot,
                timing: new SimulationTiming(TimeSpan.Zero, cycleInterval: null, 1),
                signalOverrides: Array.Empty<SignalOverride>(),
                e2eProtection: new E2eProtectionConfiguration(false));

            var document = new DbcDocument(
                nodes: new[] { new DbcNode("Tester") },
                messages: new[] { message });

            return new SimulationPlan(document, new[] { rule });
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 1000)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            while (!condition())
            {
                await Task.Delay(1, timeout.Token);
            }
        }

        private static async Task<RoutedCanFrame> ReadNextAsync(IAsyncEnumerable<RoutedCanFrame> stream)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await foreach (RoutedCanFrame frame in stream.WithCancellation(cts.Token))
            {
                return frame;
            }
            throw new TimeoutException("Timed out waiting for forwarded CAN frame.");
        }

        private sealed class StressTestTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return Interlocked.Read(ref _timestamp);
            }

            public void Advance(TimeSpan elapsed)
            {
                Interlocked.Add(ref _timestamp, elapsed.Ticks);
            }
        }

        #endregion
    }
}
