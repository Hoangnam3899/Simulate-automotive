using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Converters;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class StatusToLocalizedConverterTests
    {
        [TestMethod]
        public void Convert_NullOrEmpty_ReturnsSafely()
        {
            var converter = new StatusToLocalizedConverter();

            object? nullRes = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.AreEqual(string.Empty, nullRes);

            object? emptyRes = converter.Convert("   ", typeof(string), null, CultureInfo.InvariantCulture);
            Assert.AreEqual("   ", emptyRes);
        }

        [TestMethod]
        public void Convert_UnknownStatus_ReturnsRawValue()
        {
            var converter = new StatusToLocalizedConverter();
            const string unknown = "CUSTOM_NON_EXISTENT_STATE";

            object? res = converter.Convert(unknown, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.AreEqual(unknown, res);
        }

        [TestMethod]
        public void Convert_KnownStatusValues_ReturnsNonNullString()
        {
            var converter = new StatusToLocalizedConverter();
            string[] testStatuses = new[]
            {
                "Connected",
                "● Connected",
                "Disconnected",
                "○ Disconnected",
                "Connecting...",
                "Running",
                "● Running",
                "Paused",
                "⏸ Paused",
                "Stopped",
                "■ Stopped",
                "Idle",
                "● Idle",
                "Standby",
                "● Standby",
                "Active",
                "● Active",
                "No Data",
                "● No Data",
                "Injected",
                "● Injected",
                "Optimal",
                "Warning",
                "Critical"
            };

            foreach (string status in testStatuses)
            {
                object? res = converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);
                Assert.IsNotNull(res, $"Result for '{status}' should not be null.");
                Assert.IsTrue(res is string, $"Result for '{status}' should be a string.");
            }
        }

        [TestMethod]
        public void ConvertBack_ThrowsNotSupportedException()
        {
            var converter = new StatusToLocalizedConverter();
            Assert.ThrowsException<NotSupportedException>(() =>
                converter.ConvertBack("test", typeof(string), null, CultureInfo.InvariantCulture));
        }
    }
}
