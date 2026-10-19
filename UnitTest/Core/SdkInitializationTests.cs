using System.IO;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Service setup without credentials: SDK tools get their exec bit during Initialize (PR #118) and
    /// the Steam login status can be cleared, which the Steam login button relies on (PR #121).
    /// </summary>
    [Category("BuildUploader.Core")]
    public class SdkInitializationTests
    {
        private string m_previousSteamPath;
        private string m_previousSteamUser;
        private AuthStatus m_previousSteamStatus;
        private string m_previousItchioPath;

        [SetUp]
        public void SetUp()
        {
            m_previousSteamPath = SteamSDK.SteamSDKPath;
            m_previousSteamUser = SteamSDK.UserName;
            m_previousSteamStatus = SteamSDK.CachedStatus;
            m_previousItchioPath = Itchio.ItchioSDKPath;
        }

        [TearDown]
        public void TearDown()
        {
            SteamSDK.SteamSDKPath = m_previousSteamPath;
            SteamSDK.UserName = m_previousSteamUser; // Changing path or user clears the recorded login status
            if (m_previousSteamStatus != AuthStatus.Unknown)
            {
                SteamSDK.CachedStatus = m_previousSteamStatus;
            }
            SteamSDK.Instance.Initialize();
            Itchio.ItchioSDKPath = m_previousItchioPath;
            Itchio.Instance.Initialize();
        }

        [Test]
        public void ItchioInitializeMakesButlerExecutable()
        {
            string directory = TestSupport.OutputDirectory("FakeButler");
#if UNITY_EDITOR_WIN
            string butler = Path.Combine(directory, "butler.exe");
#else
            string butler = Path.Combine(directory, "butler");
#endif
            File.WriteAllText(butler, "#!/bin/sh\n");

            Itchio.ItchioSDKPath = directory;
            Itchio.Instance.Initialize();

            Assert.IsTrue(Itchio.Instance.IsInitialized);
            Assert.IsTrue(ProcessUtilsTests.IsExecutable(butler), "Initialize must make Butler executable.");
        }

        [Test]
        public void SteamInitializeMakesSteamCMDAndItsBinaryExecutable()
        {
            FakeSteamSdk sdk = FakeSteamSdk.Create();

            SteamSDK.SteamSDKPath = sdk.Root;
            SteamSDK.Instance.Initialize();

            Assert.IsTrue(SteamSDK.Instance.IsInitialized);
            Assert.IsTrue(Utils.ComparePaths(sdk.SteamCmd, SteamSDK.SteamSDKEXEPath), SteamSDK.SteamSDKEXEPath);
            foreach (string executable in sdk.Executables)
            {
                Assert.IsTrue(ProcessUtilsTests.IsExecutable(executable), "Initialize must make this executable: " + executable);
            }
        }

        [Test]
        public void NintendoInitializeMakesAuthoringToolExecutable()
        {
            string root = TestSupport.OutputDirectory("FakeNintendoSdk");
            string tool = FakeTool(Path.Combine(root, "NintendoSDK", "Tools"), "AuthoringTool");
            string previous = NintendoSDK.NintendoSDKPath;
            try
            {
                NintendoSDK.NintendoSDKPath = root;
                NintendoSDK.Instance.Initialize();

                Assert.IsTrue(NintendoSDK.Instance.IsInitialized);
                Assert.IsTrue(ProcessUtilsTests.IsExecutable(tool), "Initialize must make AuthoringTool executable.");
            }
            finally
            {
                NintendoSDK.NintendoSDKPath = previous;
                NintendoSDK.Instance.Initialize();
            }
        }

        [Test]
        public void PlayStationInitializeMakesPublishingToolExecutable()
        {
            string root = TestSupport.OutputDirectory("FakePlayStationSdk");
            string tool = FakeTool(Path.Combine(root, "host_tools", "bin"), "prospero-pub-cmd");
            string previous = PlayStationSDK.PlayStationSDKPath;
            try
            {
                PlayStationSDK.PlayStationSDKPath = root;
                PlayStationSDK.Instance.Initialize();

                Assert.IsTrue(PlayStationSDK.Instance.IsInitialized);
                Assert.IsTrue(ProcessUtilsTests.IsExecutable(tool), "Initialize must make the publishing tool executable.");
            }
            finally
            {
                PlayStationSDK.PlayStationSDKPath = previous;
                PlayStationSDK.Instance.Initialize();
            }
        }

        [Test]
        public void InvalidateLoginStatusForgetsCachedResult()
        {
            SteamSDK.UserName = "build-uploader-test";
            SteamSDK.CachedStatus = AuthStatus.RequiresLogin;
            Assert.AreEqual(AuthStatus.RequiresLogin, SteamSDK.CachedStatus);

            SteamSDK.InvalidateLoginStatus();

            Assert.AreEqual(AuthStatus.Unknown, SteamSDK.CachedStatus,
                "A cleared status lets the login button probe again instead of reopening SteamCMD.");
            Assert.AreEqual("", SteamSDK.LastProbeMessage);
        }

        [Test]
        public void CachedLoginStatusBelongsToOneUser()
        {
            SteamSDK.UserName = "build-uploader-test-a";
            SteamSDK.CachedStatus = AuthStatus.Authorized;

            SteamSDK.UserName = "build-uploader-test-b";

            Assert.AreEqual(AuthStatus.Unknown, SteamSDK.CachedStatus);
        }

        /// <summary>A tool file without the exec bit; ".exe" is appended on Windows.</summary>
        private static string FakeTool(string directory, string name)
        {
            Directory.CreateDirectory(directory);
#if UNITY_EDITOR_WIN
            name += ".exe";
#endif
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, "#!/bin/sh\n");
            return path;
        }
    }
}
