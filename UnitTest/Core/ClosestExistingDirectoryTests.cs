using System.IO;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Which folder the path buttons' Show and Open in Explorer go to when the path is missing.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class ClosestExistingDirectoryTests
    {
        private string m_root;

        [SetUp]
        public void SetUp()
        {
            m_root = Path.Combine(Path.GetTempPath(), "BuildUploaderClosestDir_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(m_root, "Existing"));
            File.WriteAllText(Path.Combine(m_root, "Existing", "file.txt"), "");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_root))
            {
                Directory.Delete(m_root, true);
            }
        }

        [Test]
        public void ExistingDirectoryIsItself()
        {
            string existing = Path.Combine(m_root, "Existing");
            Assert.AreEqual(Path.GetFullPath(existing), Utils.GetClosestExistingDirectory(existing));
        }

        [Test]
        public void MissingFoldersFallBackToTheClosestExistingParent()
        {
            string missing = Path.Combine(m_root, "Existing", "Missing", "AlsoMissing");
            Assert.AreEqual(Path.GetFullPath(Path.Combine(m_root, "Existing")), Utils.GetClosestExistingDirectory(missing));
        }

        [Test]
        public void ExistingFileGoesToItsFolder()
        {
            string file = Path.Combine(m_root, "Existing", "file.txt");
            Assert.AreEqual(Path.GetFullPath(Path.Combine(m_root, "Existing")), Utils.GetClosestExistingDirectory(file));
        }

        [Test]
        public void EmptyOrInvalidPathHasNoDirectory()
        {
            Assert.IsNull(Utils.GetClosestExistingDirectory(null));
            Assert.IsNull(Utils.GetClosestExistingDirectory(""));
            Assert.IsNull(Utils.GetClosestExistingDirectory("C:/bad\0path"));
        }
    }
}
