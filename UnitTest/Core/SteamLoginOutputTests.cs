using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// How SteamSDK reads SteamCMD's login output, checked against text SteamCMD prints, so it runs on every host
    /// without SteamCMD or an account. Covers the login token warning that SteamCMD prints between "Logging in user"
    /// and its OK, which left the login button checking forever, and the Steam Login window's results.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class SteamLoginOutputTests
    {
        private const string LoginLine = "Logging in user 'build-uploader-test' [U:1:1] to Steam Public...";
        private const string Password = "pa\"ss word";
        private const string SteamGuardCode = "AB12C";

        [Test]
        public void LoginOnOneLineIsAuthorized()
        {
            AuthStatus status = Classify(LoginLine + "OK\nWaiting for client config...OK\n", out string message);

            Assert.AreEqual(AuthStatus.Authorized, status, message);
            Assert.AreEqual("", message);
        }

        [Test]
        public void LoginWithTokenWarningIsAuthorized()
        {
            string output = "Loading Steam API...OK\nLogging in using cached credentials.\n" + LoginLine + "\n\n" +
                            "Warning: Login token expires at Sat Nov 28 18:35:23 2026\n" +
                            "If using cached credentials, this may indicate your client cannot renew its token.\n" +
                            "This can occur if steamcmd cannot write the machine auth token to its config.vdf.\n" +
                            "OK\nWaiting for client config...OK\nWaiting for user info...OK\n";

            AuthStatus status = Classify(output, out string message);

            Assert.AreEqual(AuthStatus.Authorized, status, message);
            Assert.AreEqual("Warning: Login token expires at Sat Nov 28 18:35:23 2026", message);
        }

        [Test]
        public void LoginApprovedInSteamMobileAppIsAuthorized()
        {
            string output = LoginLine + "\nThis account is protected by a Steam Guard mobile authenticator.\n" +
                            "Please confirm the login in the Steam Mobile app on your phone.\n\n" +
                            "Waiting for confirmation...\nWaiting for confirmation...OK\nWaiting for client config...OK\n";

            AuthStatus status = Classify(output, out string message);

            Assert.AreEqual(AuthStatus.Authorized, status, message);
        }

        [Test]
        public void WrongPasswordAsksForThePasswordAgain()
        {
            SteamLoginResult result = Login(LoginLine + "FAILED (Invalid Password)\n", "");

            Assert.AreEqual(AuthStatus.RequiresLogin, result.Status);
            Assert.IsFalse(result.NeedsSteamGuardCode);
            StringAssert.Contains("password is wrong", result.Message);
        }

        [Test]
        public void SteamGuardEmailAsksForACode()
        {
            string output = LoginLine + "\nThis computer has not been authenticated for your account using Steam Guard.\n" +
                            "Please check your email for the message from Steam, and enter the Steam Guard\n" +
                            " code from that message.\nSteam Guard code:FAILED (Account Logon Denied)\n";

            SteamLoginResult result = Login(output, "");

            Assert.AreEqual(AuthStatus.RequiresLogin, result.Status);
            Assert.IsTrue(result.NeedsSteamGuardCode);
            StringAssert.Contains("needs a code", result.Message);
        }

        [Test]
        public void WrongSteamGuardCodeAsksForANewOne()
        {
            SteamLoginResult result = Login(LoginLine + "FAILED (Two-factor code mismatch)\n", SteamGuardCode);

            Assert.AreEqual(AuthStatus.RequiresLogin, result.Status);
            Assert.IsTrue(result.NeedsSteamGuardCode);
            StringAssert.Contains("wrong or has expired", result.Message);
        }

        [Test]
        public void PasswordAndCodeAreHiddenFromTheMessage()
        {
            // A timed out run reports the arguments it was started with
            string reason = "Timed out after 120000ms: steamcmd.exe +login \"build-uploader-test\" " +
                            ProcessUtils.QuoteArgument(Password) + " " + ProcessUtils.QuoteArgument(SteamGuardCode) + " +quit";

            SteamLoginResult result = SteamSDK.ClassifyLogin(ProcessUtils.ProcessResult.Failed(reason), Password, SteamGuardCode);

            Assert.AreEqual(AuthStatus.Unknown, result.Status);
            StringAssert.DoesNotContain("ss word", result.Message);
            StringAssert.DoesNotContain(SteamGuardCode, result.Message);
            StringAssert.Contains("********", result.Message);
        }

        private static AuthStatus Classify(string output, out string message)
        {
            return SteamSDK.ClassifyLoginOutput(ProcessUtils.ProcessResult.Successful(output), out message);
        }

        private static SteamLoginResult Login(string output, string steamGuardCode)
        {
            // SteamCMD exits non-zero on a refused login and explains itself on stdout
            return SteamSDK.ClassifyLogin(ProcessUtils.ProcessResult.Failed("", output, 5), Password, steamGuardCode);
        }
    }
}
