using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Services;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class CrashReporterTests
    {
        [TestMethod]
        public void CrashReporter_Directory_ExistsAndAccessible()
        {
            string dir = CrashReporter.CrashReportDirectory;
            Assert.IsTrue(Directory.Exists(dir), "CrashReports directory must exist.");
            Assert.IsTrue(dir.Contains("CrashReports"), "Directory path must contain 'CrashReports'.");
        }

        [TestMethod]
        public void CrashReporter_WriteCrashReport_CreatesValidTxtFileWithForensicDetails()
        {
            var innerEx = new InvalidOperationException("Inner CAN hardware failed.");
            var outerEx = new ApplicationException("Outer engine faulted unexpectedly.", innerEx);

            string reportPath = CrashReporter.WriteCrashReport(
                outerEx,
                "UnitTest Fault Simulation",
                isTerminating: false);

            Assert.IsTrue(File.Exists(reportPath), "Crash report file must exist on disk.");
            string content = File.ReadAllText(reportPath);

            // Assert forensic headers
            Assert.IsTrue(content.Contains("SIMULATE AUTOMOTIVE - HE THONG BAO CAO SU CO (CRASH REPORT)"));
            Assert.IsTrue(content.Contains("UnitTest Fault Simulation"));
            Assert.IsTrue(content.Contains("ApplicationException"));
            Assert.IsTrue(content.Contains("Outer engine faulted unexpectedly."));

            // Assert inner exception diagnostics
            Assert.IsTrue(content.Contains("INNER EXCEPTION CAP 1:"));
            Assert.IsTrue(content.Contains("InvalidOperationException"));
            Assert.IsTrue(content.Contains("Inner CAN hardware failed."));

            // Assert process & environment diagnostics
            Assert.IsTrue(content.Contains("Ten tien trinh (PID):"));
            Assert.IsTrue(content.Contains("He dieu hanh        :"));
            Assert.IsTrue(content.Contains("Bo nho RAM"));
        }

        [TestMethod]
        public void CrashReporter_Initialize_IsIdempotentAndSafe()
        {
            // Calling Initialize multiple times must never throw
            CrashReporter.Initialize(null);
            CrashReporter.Initialize(null);
        }
    }
}
