using System;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    public class RequestWrapperMaskTests
    {
        [TestCase("https://a.com/b", "https://a.com/b")]
        [TestCase("https://chat.googleapis.com/v1/spaces/S/messages?key=K&token=T", "https://chat.googleapis.com/v1/spaces/S/messages?key=xxx-xxx-xxx&token=xxx-xxx-xxx")]
        [TestCase("https://x.blob.core.windows.net/c/f.zip?sv=1&sig=abc", "https://x.blob.core.windows.net/c/f.zip?sv=xxx-xxx-xxx&sig=xxx-xxx-xxx")]
        [TestCase("https://a.com/b?flag", "https://a.com/b?flag=xxx-xxx-xxx")]
        [TestCase("", "")]
        public void MaskQueryHidesEveryValue(string url, string expected)
        {
            Assert.AreEqual(expected, RequestWrapper.MaskQuery(url));
        }

        [TestCase("https://hooks.example.com/services/T0/B0/secret?x=1", "https://hooks.example.com/xxx-xxx-xxx")]
        [TestCase("https://a.com:8080/secret", "https://a.com:8080/xxx-xxx-xxx")]
        [TestCase("not a url", "xxx-xxx-xxx")]
        public void MaskPathAndQueryKeepsOnlyHost(string url, string expected)
        {
            Assert.AreEqual(expected, RequestWrapper.MaskPathAndQuery(url));
        }

        [Test]
        public void NullMaskedURLThrows()
        {
            Assert.Throws<ArgumentNullException>(() => RequestWrapper.Get("https://a.com", null));
        }
    }
}
