using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// When the Upload tab switches to its thin layout, and how that layout names each Upload Config's tab.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class UploadTabThinLayoutTests
    {
        private const float Width = WindowUploadTab.ThinLayoutWidth;
        private const float Margin = WindowUploadTab.ThinLayoutWidthMargin;

        [Test]
        public void WideLayoutSwitchesToThinBelowTheMargin()
        {
            Assert.IsFalse(WindowUploadTab.ShouldUseThinLayout(Width * 2, false));
            Assert.IsFalse(WindowUploadTab.ShouldUseThinLayout(Width - 1, false), "A wide window stays wide inside the margin.");
            Assert.IsTrue(WindowUploadTab.ShouldUseThinLayout(Width - Margin - 1, false));
        }

        [Test]
        public void ThinLayoutSwitchesToWideAboveTheMargin()
        {
            Assert.IsTrue(WindowUploadTab.ShouldUseThinLayout(Width / 2, true));
            Assert.IsTrue(WindowUploadTab.ShouldUseThinLayout(Width + 1, true), "A thin window stays thin inside the margin.");
            Assert.IsFalse(WindowUploadTab.ShouldUseThinLayout(Width + Margin, true));
        }

        [Test]
        public void TabLabelNamesTheFirstEnabledSourceAndDestination()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData(new FileSource(), false));
            config.AddSource(new UploadConfig.SourceData(new FolderSource()));
            config.AddDestination(new UploadConfig.DestinationData(new LocalPathDestination()));

            Assert.AreEqual("2. Folder → Local Path", WindowUploadTab.GetUploadConfigLabel(1, config));
        }

        [Test]
        public void TabLabelFallsBackWhenNothingIsChosen()
        {
            UploadConfig config = new UploadConfig();
            config.AddSource(new UploadConfig.SourceData { Enabled = true });
            Assert.AreEqual("1. Upload Config", WindowUploadTab.GetUploadConfigLabel(0, config));

            config.AddDestination(new UploadConfig.DestinationData(new LocalPathDestination()));
            Assert.AreEqual("1. Local Path", WindowUploadTab.GetUploadConfigLabel(0, config));
        }
    }
}
