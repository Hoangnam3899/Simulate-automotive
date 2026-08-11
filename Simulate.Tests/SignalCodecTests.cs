using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Models;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class SignalCodecTests
    {
        [TestMethod]
        public void PackPhysical_writes_a_scaled_little_endian_value_without_changing_neighbor_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 3 Gateway
                 SG_ VehicleSpeed : 4|12@1+ (0.5,-10) [-10|2037.5] "km/h" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0x0F, 0x00, 0xA5];

            SignalCodec.PackPhysical(payload, signal, 281d);

            CollectionAssert.AreEqual(new byte[] { 0x6F, 0x24, 0xA5 }, payload);
        }

        [TestMethod]
        public void UnpackPhysical_reads_a_scaled_little_endian_value_from_a_golden_payload()
        {
            const string documentText = """
                BO_ 291 VehicleData: 3 Gateway
                 SG_ VehicleSpeed : 4|12@1+ (0.5,-10) [-10|2037.5] "km/h" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0x6F, 0x24, 0xA5];

            double physicalValue = SignalCodec.UnpackPhysical(payload, signal);

            Assert.AreEqual(281d, physicalValue);
        }

        [TestMethod]
        public void PackPhysical_writes_a_big_endian_value_in_dbc_sawtooth_order_without_changing_neighbor_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 3 Gateway
                 SG_ StatusWord : 7|12@0+ (1,0) [0|4095] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0x00, 0x0F, 0x5A];

            SignalCodec.PackPhysical(payload, signal, 0xABC);

            CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCF, 0x5A }, payload);
        }

        [TestMethod]
        public void UnpackPhysical_sign_extends_a_scaled_big_endian_value()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ SignedTemperature : 7|12@0- (0.25,-10) [-522|501.75] "degC" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0xFE, 0xC5];

            double physicalValue = SignalCodec.UnpackPhysical(payload, signal);

            Assert.AreEqual(-15d, physicalValue);
        }

        [DataTestMethod]
        [DataRow(-128d, (byte)0x80)]
        [DataRow(127d, (byte)0x7F)]
        public void PackPhysical_accepts_the_declared_signed_minimum_and_maximum(
            double physicalValue,
            byte expectedRawByte)
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ SignedValue : 0|8@1- (1,0) [-128|127] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0xAA, 0x5A];

            SignalCodec.PackPhysical(payload, signal, physicalValue);

            CollectionAssert.AreEqual(new byte[] { expectedRawByte, 0x5A }, payload);
        }

        [TestMethod]
        public void PackPhysical_rejects_a_short_payload_before_changing_any_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 2 Gateway
                 SG_ StatusWord : 7|12@0+ (1,0) [0|4095] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0xA5];

            Assert.ThrowsException<ArgumentException>(
                () => SignalCodec.PackPhysical(payload, signal, 0xABC));

            CollectionAssert.AreEqual(new byte[] { 0xA5 }, payload);
        }

        [TestMethod]
        public void PackPhysical_rejects_a_value_outside_the_declared_range_before_changing_any_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 1 Gateway
                 SG_ LimitedValue : 0|8@1+ (1,0) [10|20] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0xA5];

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => SignalCodec.PackPhysical(payload, signal, 21d));

            CollectionAssert.AreEqual(new byte[] { 0xA5 }, payload);
        }

        [TestMethod]
        public void PackPhysical_rejects_a_raw_value_that_does_not_fit_the_signal_before_changing_any_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 1 Gateway
                 SG_ SmallValue : 0|4@1+ (1,0) [0|100] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = [0xA5];

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => SignalCodec.PackPhysical(payload, signal, 16d));

            CollectionAssert.AreEqual(new byte[] { 0xA5 }, payload);
        }

        [TestMethod]
        public void PackPhysical_rejects_a_payload_larger_than_can_fd_before_changing_any_bits()
        {
            const string documentText = """
                BO_ 291 VehicleData: 1 Gateway
                 SG_ Value : 0|8@1+ (1,0) [0|255] "" Gateway
                """;
            DbcParseResult parseResult = DbcParser.Parse(documentText);
            DbcSignal signal = parseResult.Document?.Messages[0].Signals[0]
                ?? throw new AssertFailedException("The signal fixture must parse successfully.");
            byte[] payload = Enumerable.Repeat((byte)0xA5, CanFrame.MaximumPayloadLength + 1).ToArray();
            byte[] originalPayload = payload.ToArray();

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => SignalCodec.PackPhysical(payload, signal, 0x12));

            CollectionAssert.AreEqual(originalPayload, payload);
        }
    }
}
