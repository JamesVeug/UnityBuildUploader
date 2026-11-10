using NUnit.Framework;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// When the Welcome window opens on its own after a script reload.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class WelcomeWindowStartupTests
    {
        [Test]
        public void WelcomeWindowNeverOpensWithoutAUser()
        {
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.OnStartup, "3.3.0", null, false, true),
                "Batch mode and test runs have nobody to see the window.");
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.WhenUpdated, "3.3.0", "3.2.0", false, true),
                "A test run must not use up the When updated popup.");
        }

        [Test]
        public void WelcomeWindowOnStartupOpensOncePerSession()
        {
            Assert.IsTrue(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.OnStartup, "3.3.0", "3.3.0", false, false));
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.OnStartup, "3.3.0", "3.3.0", true, false));
        }

        [Test]
        public void WelcomeWindowWhenUpdatedOpensForANewVersion()
        {
            Assert.IsTrue(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.WhenUpdated, "3.3.0", "3.2.0", false, false));
            Assert.IsTrue(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.WhenUpdated, "3.3.0", null, false, false));
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.WhenUpdated, "3.3.0", "3.3.0", false, false));
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.WhenUpdated, "", "3.2.0", false, false),
                "An unreadable package version doesn't open it every reload.");
        }

        [Test]
        public void WelcomeWindowSetToNeverStaysClosed()
        {
            Assert.IsFalse(ScriptReloadWatcher.ShouldShowWelcome(Preferences.WelcomeWindowPopup.Never, "3.3.0", "3.2.0", false, false));
        }
    }
}
