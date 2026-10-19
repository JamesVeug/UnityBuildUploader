using System.Collections.Generic;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// JSON escaping fix: the serializer used to write backslashes raw, so files in BuildUploader/ hold
    /// paths such as "C:\new_build\MyGame". Those files must keep loading with their backslashes intact,
    /// and the next save writes valid JSON.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class JSONLegacyFileTests
    {
        // As the old serializer wrote it: backslashes raw, while newlines and quotes were escaped.
        private const string LegacyDestination = @"{
	""m_localPath"": ""C:\new_build\MyGame"",
	""m_fileName"": ""D:\builds"",
	""m_share"": ""\\nas\Builds\temp"",
	""m_regex"": "".*\.pdb$"",
	""m_message"": ""Uploaded\nsaid \""done\""""
}";

        [Test]
        public void LegacyUnescapedWindowsPathsLoadUnchanged()
        {
            Dictionary<string, object> data = JSON.DeserializeObject<Dictionary<string, object>>(LegacyDestination);

            Assert.AreEqual(@"C:\new_build\MyGame", data["m_localPath"]);
            Assert.AreEqual(@"D:\builds", data["m_fileName"]);
            Assert.AreEqual(@"\\nas\Builds\temp", data["m_share"]);
            Assert.AreEqual(@".*\.pdb$", data["m_regex"]);
            Assert.AreEqual("Uploaded\nsaid \"done\"", data["m_message"], "A string without raw backslashes decodes normally.");
        }

        [Test]
        public void LegacyFileIsSavedAsValidJsonAndReloads()
        {
            Dictionary<string, object> legacy = JSON.DeserializeObject<Dictionary<string, object>>(LegacyDestination);

            string saved = JSON.SerializeObject(legacy);
            StringAssert.Contains(@"""m_localPath"": ""C:\\new_build\\MyGame""", saved);
            StringAssert.Contains(@"""m_share"": ""\\\\nas\\Builds\\temp""", saved);

            Dictionary<string, object> reloaded = JSON.DeserializeObject<Dictionary<string, object>>(saved);
            CollectionAssert.AreEquivalent(legacy, reloaded);
        }

        [Test]
        public void BackslashAndQuoteCharsRoundTrip()
        {
            Assert.AreEqual(@"""\\""", JSON.SerializeObject('\\'));
            Assert.AreEqual(@"""\""""", JSON.SerializeObject('"'));
            Assert.AreEqual('\\', JSON.DeserializeObject<char>(JSON.SerializeObject('\\')));
            Assert.AreEqual('"', JSON.DeserializeObject<char>(JSON.SerializeObject('"')));
        }

        [Test]
        public void BackspaceAndFormFeedAreWrittenAsUnicodeEscapes()
        {
            // A bare \b or \f marks a legacy raw path (C:\builds), so the serializer must never write one.
            const string text = "a\bb\fc";
            string json = JSON.SerializeObject(text);

            Assert.AreEqual(@"""a\u0008b\u000cc""", json);
            Assert.AreEqual(text, JSON.DeserializeObject<string>(json));
        }
    }
}
