using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    public class ItchioWebGLTests
    {
        private bool m_previousOpenBuildFolders;

        [SetUp]
        public void SetUp()
        {
            m_previousOpenBuildFolders = Preferences.OpenBuildFoldersAfterUpload;
            Preferences.OpenBuildFoldersAfterUpload = false;
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.OpenBuildFoldersAfterUpload = m_previousOpenBuildFolders;
        }

        [UnityTest, Category("BuildUploader.DryRun")]
        public IEnumerator OneWebGLDryUpload()
        {
            string executable = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_BUTLER");
            Assert.IsTrue(File.Exists(executable), "Butler executable is missing.");
            string previousPath = Itchio.ItchioSDKPath;
            bool previousEnabled = Itchio.Enabled;
            try
            {
                Itchio.ItchioSDKPath = Path.GetDirectoryName(Path.GetFullPath(executable));
                Itchio.Enabled = true;
                Itchio.Instance.Initialize();
                Assert.IsTrue(Itchio.Instance.IsInitialized);
                var destination = new ItchioDestination("build-uploader-test", "webgl-fixture", new[] { "html5" });
                destination.SetDryRun(true);
                var task = TestSupport.FileUploadTask("Itchio WebGL dry run",
                    TestSupport.FixtureDirectory("ItchioWebGL", "<!doctype html><html><body>WebGL upload fixture</body></html>"), destination);
                yield return TestSupport.Run(task);
                StringAssert.Contains("Dry run, listing files we would push", task.Report.GetReport());
                StringAssert.Contains("index.html", task.Report.GetReport());
                StringAssert.Contains("Itch.io upload successful", task.Report.GetReport());
            }
            finally
            {
                Itchio.ItchioSDKPath = previousPath;
                Itchio.Enabled = previousEnabled;
                Itchio.Instance.Initialize();
            }
        }
    }
}
