// BuildUploader-Hosts: OSXUniversal Linux64
// steamcmd.exe cannot be faked with a script, so these only run where SteamCMD is a shell script.
#if !UNITY_EDITOR_WIN
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// SteamSDK.ProbeLoginAsync and the offline credential check against a fake SteamCMD, so no account is
    /// needed. Covers PR #118 (coloured SteamCMD output) and PRs #120/#121 (Linux SteamCMD gets its own HOME,
    /// and its saved login is read from there).
    /// </summary>
    [Category("BuildUploader.Core")]
    public class SteamLoginProbeTests
    {
        private const string User = "build-uploader-test";
        private const string Escape = "\u001b";
        private const double Timeout = 60;

        private FakeSteamSdk m_sdk;
        private string m_previousSteamPath;
        private string m_previousSteamUser;
        private AuthStatus m_previousSteamStatus;

        [SetUp]
        public void SetUp()
        {
            m_previousSteamPath = SteamSDK.SteamSDKPath;
            m_previousSteamUser = SteamSDK.UserName;
            m_previousSteamStatus = SteamSDK.CachedStatus;

            m_sdk = FakeSteamSdk.Create();
            SteamSDK.SteamSDKPath = m_sdk.Root;
            SteamSDK.UserName = User;
            SteamSDK.Instance.Initialize();
            Assert.IsTrue(SteamSDK.Instance.IsInitialized, "The fake Steamworks SDK was not accepted.");
        }

        [TearDown]
        public void TearDown()
        {
            SteamSDK.SteamSDKPath = m_previousSteamPath;
            SteamSDK.UserName = m_previousSteamUser;
            if (m_previousSteamStatus != AuthStatus.Unknown)
            {
                SteamSDK.CachedStatus = m_previousSteamStatus;
            }
            SteamSDK.Instance.Initialize();
        }

        [UnityTest]
        public IEnumerator ColouredLoginOutputIsAuthorized()
        {
            // Linux SteamCMD colours piped output; the success line must still be recognised.
            File.WriteAllText(m_sdk.FakeOutputPath, Escape + "[0mLogging in user '" + User + "' [U:1:1] to Steam Public..." +
                                                    Escape + "[0mOK\n" + Escape + "[0mWaiting for user info..." + Escape + "[0mOK\n");

            yield return Probe(AuthStatus.Authorized);

            Assert.AreEqual(AuthStatus.Authorized, SteamSDK.CachedStatus);
        }

        [UnityTest]
        public IEnumerator SteamGuardPromptRequiresLogin()
        {
            File.WriteAllText(m_sdk.FakeOutputPath, "Logging in user '" + User + "' to Steam Public...\nThis computer has not been authenticated for your account using Steam Guard.\n");

            yield return Probe(AuthStatus.RequiresLogin);

            Assert.AreEqual(AuthStatus.RequiresLogin, SteamSDK.CachedStatus);
        }

        [UnityTest]
        public IEnumerator RateLimitIsNotRecorded()
        {
            SteamSDK.CachedStatus = AuthStatus.Authorized;
            File.WriteAllText(m_sdk.FakeOutputPath, "Logging in user '" + User + "' to Steam Public...FAILED (Rate Limit Exceeded)\n");

            yield return Probe(AuthStatus.Unknown);

            Assert.AreEqual(AuthStatus.Authorized, SteamSDK.CachedStatus, "An inconclusive probe must keep the previous status.");
        }

        [UnityTest]
        public IEnumerator SteamCMDRunsWithItsOwnHomeOnLinux()
        {
            File.WriteAllText(m_sdk.FakeOutputPath, "Logging in user '" + User + "' to Steam Public...OK\n");

            yield return Probe(AuthStatus.Authorized);

            string home = File.ReadAllText(m_sdk.HomeSeenPath).Trim();
#if UNITY_EDITOR_LINUX
            // With the Steam client installed, ~/.steam/steam points at the client, which overwrites SteamCMD's login.
            Assert.IsTrue(Utils.ComparePaths(Path.Combine(m_sdk.Builder, "home"), home), "SteamCMD HOME was " + home);
#else
            Assert.AreEqual(Environment.GetEnvironmentVariable("HOME"), home, "Only Linux overrides HOME.");
#endif
        }

        [Test]
        public void CredentialCheckReadsWhereSteamCMDSavesItsLogin()
        {
#if UNITY_EDITOR_LINUX
            // Verified with SteamCMD itself: with HOME=<builder_linux>/home it writes home/Steam/config/config.vdf.
            string config = Path.Combine(m_sdk.Builder, "home", "Steam", "config");
#else
            // A config folder next to steamcmd.sh wins, which keeps this test out of the real user's Steam folder.
            string config = Path.Combine(m_sdk.Builder, "config");
#endif
            Directory.CreateDirectory(config);
            File.WriteAllText(Path.Combine(config, "config.vdf"), "\"InstallConfigStore\" { \"Accounts\" { \"" + User + "\" { } } }");
            Assert.IsFalse(SteamSDK.HasCachedCredentials(User), "Without a sentry file the machine is not authorized.");

            File.WriteAllText(Path.Combine(config, "ssfn1234567890"), "sentry");

            Assert.IsTrue(SteamSDK.HasCachedCredentials(User));
            Assert.IsFalse(SteamSDK.HasCachedCredentials("someone-else"));
        }

        private static IEnumerator Probe(AuthStatus expected)
        {
            Task<AuthStatus> probe = SteamSDK.ProbeLoginAsync();
            yield return TestSupport.Await(probe, Timeout);
            Assert.AreEqual(expected, probe.Result, "Probe message: " + SteamSDK.LastProbeMessage);
        }
    }
}
#endif
