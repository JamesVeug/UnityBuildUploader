using NUnit.Framework;

namespace Wireframe.UnitTest
{
    public class WebImagePreviewTests
    {
        [TestCase("https://example.com/avatar.png", true)]
        [TestCase("https://example.com/avatar.JPG", true)]
        [TestCase("https://example.com/avatar.jpeg", true)]
        [TestCase("https://example.com/avatar.gif", true)]
        [TestCase("http://example.com/avatar.webp?size=64", true)]
        [TestCase("https://example.com/avatar", false)]
        [TestCase("https://example.com/avatar.bmp", false)]
        [TestCase("https://example.com/avatar?file=a.png", false)]
        [TestCase("ftp://example.com/avatar.png", false)]
        [TestCase("example.com/avatar.png", false)]
        [TestCase("not a url", false)]
        [TestCase("", false)]
        public void IsImageURLNeedsHttpAndImageExtension(string url, bool expected)
        {
            Assert.AreEqual(expected, WebImagePreview.IsImageURL(url));
        }
    }
}
