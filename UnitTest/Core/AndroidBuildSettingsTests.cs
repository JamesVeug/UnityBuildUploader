using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Android build settings that need no Android module: App Bundle and architectures in a BuildConfig, the
    /// desktop architecture kept off Android, the keystore passwords Unity forgets, and build meta data for
    /// packaged players.
    /// </summary>
    [Category("BuildUploader.Core")]
    public class AndroidBuildSettingsTests
    {
        private bool m_previousBuildAppBundle;
        private AndroidArchitecture m_previousArchitectures;
        private bool m_previousUseCustomKeystore;
        private string m_previousKeystorePass;
        private string m_previousKeyaliasPass;
        private string m_previousStoredKeystorePassword;
        private string m_previousStoredKeyAliasPassword;

        [SetUp]
        public void SetUp()
        {
            m_previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            m_previousArchitectures = PlayerSettings.Android.targetArchitectures;
            m_previousUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            m_previousKeystorePass = PlayerSettings.Android.keystorePass;
            m_previousKeyaliasPass = PlayerSettings.Android.keyaliasPass;
            m_previousStoredKeystorePassword = AndroidSigning.KeystorePassword;
            m_previousStoredKeyAliasPassword = AndroidSigning.KeyAliasPassword;
        }

        [TearDown]
        public void TearDown()
        {
            EditorUserBuildSettings.buildAppBundle = m_previousBuildAppBundle;
            PlayerSettings.Android.targetArchitectures = m_previousArchitectures;
            PlayerSettings.Android.useCustomKeystore = m_previousUseCustomKeystore;
            PlayerSettings.Android.keystorePass = m_previousKeystorePass;
            PlayerSettings.Android.keyaliasPass = m_previousKeyaliasPass;
            AndroidSigning.KeystorePassword = m_previousStoredKeystorePassword;
            AndroidSigning.KeyAliasPassword = m_previousStoredKeyAliasPassword;
            BuildUploaderProjectSettings.DeleteFromResources();
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

        [Test]
        public void StoredKeystorePasswordsFillTheOnesUnityForgot()
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            AndroidSigning.KeystorePassword = "store-secret";
            AndroidSigning.KeyAliasPassword = "alias-secret";

            Assert.IsTrue(AndroidSigning.TryApplyPasswords(out string error), error);
            Assert.AreEqual("store-secret", PlayerSettings.Android.keystorePass);
            Assert.AreEqual("alias-secret", PlayerSettings.Android.keyaliasPass);
        }

        [Test]
        public void PasswordsEnteredInPlayerSettingsAreKept()
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = "typed-store";
            PlayerSettings.Android.keyaliasPass = "typed-alias";
            AndroidSigning.KeystorePassword = "store-secret";
            AndroidSigning.KeyAliasPassword = "alias-secret";

            Assert.IsTrue(AndroidSigning.TryApplyPasswords(out _));
            Assert.AreEqual("typed-store", PlayerSettings.Android.keystorePass);
            Assert.AreEqual("typed-alias", PlayerSettings.Android.keyaliasPass);
        }

        [Test]
        public void MissingKeystorePasswordsFailWithTheReason()
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            AndroidSigning.KeystorePassword = "";
            AndroidSigning.KeyAliasPassword = "";

            Assert.IsFalse(AndroidSigning.TryApplyPasswords(out string error));
            StringAssert.Contains("Android Signing", error);
        }

        [Test]
        public void DebugSignedBuildsNeedNoPasswords()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            AndroidSigning.KeystorePassword = "";
            AndroidSigning.KeyAliasPassword = "";

            Assert.IsTrue(AndroidSigning.TryApplyPasswords(out string error));
            Assert.IsNull(error);
        }

        [Test]
        public void PackagedPlayersGetMetaDataThroughResources()
        {
            Assert.IsTrue(BuildUploaderProjectSettings.CanAddMetaDataAfterBuild(BuildTargetGroup.Standalone));
            Assert.IsTrue(BuildUploaderProjectSettings.CanAddMetaDataAfterBuild(BuildTargetGroup.WebGL));
            Assert.IsFalse(BuildUploaderProjectSettings.CanAddMetaDataAfterBuild(BuildTargetGroup.Android));

            BuildUploaderProjectSettings.SaveToResources(new BuildMetaData { BuildNumber = 42, UploadNumber = 7 });

            TextAsset resource = Resources.Load<TextAsset>(BuildMetaData.ResourceName);
            Assert.IsNotNull(resource, "BuildMetaData.Get() reads this in the player");
            BuildMetaData loaded = JsonUtility.FromJson<BuildMetaData>(resource.text);
            Assert.AreEqual(42, loaded.BuildNumber);
            Assert.AreEqual(7, loaded.UploadNumber);

            BuildUploaderProjectSettings.DeleteFromResources();

            Assert.IsFalse(Directory.Exists(BuildUploaderProjectSettings.MetaDataResourcesFolder));
            Assert.IsFalse(File.Exists(BuildUploaderProjectSettings.MetaDataResourcesFolder + ".meta"));
        }
    }
}
