using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Android build settings that need no Android module: App Bundle and architectures in a BuildConfig, and the
    /// desktop architecture kept off Android.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class AndroidBuildSettingsTests
    {
        private bool m_previousBuildAppBundle;
        private AndroidArchitecture m_previousArchitectures;

        [SetUp]
        public void SetUp()
        {
            m_previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            m_previousArchitectures = PlayerSettings.Android.targetArchitectures;
        }

        [TearDown]
        public void TearDown()
        {
            EditorUserBuildSettings.buildAppBundle = m_previousBuildAppBundle;
            PlayerSettings.Android.targetArchitectures = m_previousArchitectures;
        }

        private static BuildConfig AndroidConfig(bool appBundle)
        {
            var config = new BuildConfig();
            config.Clear();
            config.TargetPlatform = BuildTargetGroup.Android;
            config.Target = BuildTarget.Android;
            config.AndroidBuildAppBundle = appBundle;
            return config;
        }

        [Test]
        public void AppBundleBuildsAreNamedAab()
        {
            // Before, a BuildConfig always named an Android build .apk, even when Unity built an App Bundle
            Assert.AreEqual(".aab", AndroidConfig(true).GetProductExtension());
            Assert.AreEqual(".apk", AndroidConfig(false).GetProductExtension());
        }

        [Test]
        public void AndroidSettingsAreSavedAndLoaded()
        {
            BuildConfig config = AndroidConfig(true);
            config.AndroidTargetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            var fromJson = new BuildConfig();
            fromJson.Deserialize(JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(config.Serialize())));
            var copied = new BuildConfig();
            copied.Deserialize(config.Serialize()); // What BuildConfigSource does before applying a config

            foreach (BuildConfig loaded in new[] { fromJson, copied })
            {
                Assert.IsTrue(loaded.AndroidBuildAppBundle);
                Assert.AreEqual(AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64, loaded.AndroidTargetArchitectures);
            }
        }

        [Test]
        public void OlderConfigsKeepTheEditorAndroidSettings()
        {
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            Dictionary<string, object> data = AndroidConfig(false).Serialize();
            data.Remove("AndroidBuildAppBundle");
            data.Remove("AndroidTargetArchitectures");

            var loaded = new BuildConfig();
            loaded.Deserialize(data);

            Assert.IsTrue(loaded.AndroidBuildAppBundle);
            Assert.AreEqual(AndroidArchitecture.ARMv7, loaded.AndroidTargetArchitectures);
        }

        [Test]
        public void AndroidSettingsAreApplied()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            BuildConfig config = AndroidConfig(true);
            config.AndroidTargetArchitectures = AndroidArchitecture.ARM64;

            config.ApplyAndroidSettings();

            Assert.IsTrue(EditorUserBuildSettings.buildAppBundle);
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);

            // No architectures chosen keeps the project's
            config.AndroidTargetArchitectures = AndroidArchitecture.None;
            config.ApplyAndroidSettings();
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
        }

        [Test]
        public void DesktopArchitectureIsNotAppliedToAndroid()
        {
            Assert.IsFalse(BuildUtils.UsesTargetArchitecture(BuildTargetGroup.Android));
            Assert.IsTrue(BuildUtils.UsesTargetArchitecture(BuildTargetGroup.Standalone));
        }
    }
}
