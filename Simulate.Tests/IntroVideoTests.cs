using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Simulate.Views;

namespace Simulate.Tests
{
    [TestClass]
    public sealed class IntroVideoTests
    {
        [TestMethod]
        public void IntroVideo_AssetFile_ExistsInProjectAndOutput()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Kiểm tra tệp trong output directory
            string outputVideoPath = Path.Combine(baseDir, "Assets", "clipStart.mp4");
            // Kiểm tra tệp trong source directory
            string sourceVideoPath = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Simulate\Assets\clipStart.mp4"));

            bool outputExists = File.Exists(outputVideoPath);
            bool sourceExists = File.Exists(sourceVideoPath);

            Assert.IsTrue(outputExists || sourceExists, "Tệp clipStart.mp4 phải tồn tại trong project hoặc output directory.");

            string actualPath = outputExists ? outputVideoPath : sourceVideoPath;
            var fileInfo = new FileInfo(actualPath);
            Assert.IsTrue(fileInfo.Length > 1_000_000, "Tệp clipStart.mp4 phải có dung lượng lớn hơn 1MB.");
        }

        [TestMethod]
        public void IntroVideoWindow_CloseIntro_CanBeInvokedSafely()
        {
            Exception? threadEx = null;
            var staThread = new Thread(() =>
            {
                try
                {
                    var window = new IntroVideoWindow();
                    Assert.AreEqual(720, window.Width);
                    Assert.AreEqual(405, window.Height);
                    Assert.AreEqual(System.Windows.WindowStyle.None, window.WindowStyle);

                    // Đóng cửa sổ ngay lập tức
                    window.CloseIntro();
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join(5000);

            Assert.IsNull(threadEx, $"Khởi tạo và đóng IntroVideoWindow không được ném lỗi: {threadEx?.Message}");
        }
    }
}
