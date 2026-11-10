using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Builds an Android APK through an UploadTask. Its own assembly, so desktop jobs never list it; run it with
    /// -buildTarget Android and the Android Build Support module (SDK, NDK and OpenJDK) installed.
    /// </summary>
    public class AndroidBuildTests
    {
        private bool m_previousOpenBuildFolders;
        private bool m_previousBuildAppBundle;

        [SetUp]
        public void SetUp()
        {
            m_previousOpenBuildFolders = Preferences.OpenBuildFoldersAfterUpload;
            Preferences.OpenBuildFoldersAfterUpload = false;
            m_previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.OpenBuildFoldersAfterUpload = m_previousOpenBuildFolders;
            EditorUserBuildSettings.buildAppBundle = m_previousBuildAppBundle;
        }

        [UnityEngine.TestTools.UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        public System.Collections.IEnumerator OneAndroidBuild()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                Assert.Ignore("Run this test with -buildTarget Android.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                Assert.Ignore("Android Build Support is not installed.");
            string root = TestSupport.OutputDirectory("Builds/Android");

            var config = new UploadConfig();
            config.AddSource(CreateSource());
            config.AddDestination(new LocalPathDestination(root));
            var uploadTask = new UploadTask("Android UploadTask build", config);
            uploadTask.SetBuildDescription("UploadTask Android build integration test");
            yield return TestSupport.Run(uploadTask);

            Assert.IsNotEmpty(Directory.GetFiles(root, "*.apk", SearchOption.AllDirectories),
                "UploadTask reported success without an APK under " + root);
        }

        private static AUploadSource CreateSource()
        {
#if UNITY_6000_0_OR_NEWER
            string profilePath = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_BUILD_PROFILE");
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            Assert.IsNotNull(profile, "Not a Build Profile asset: " + profilePath);
            Assert.AreEqual(BuildTarget.Android, new BuildProfileWrapper(profile).GetTarget,
                "The configured profile targets a different platform.");
            Assert.IsTrue(BuildProfileWrapper.GetScenesForBuild(profile).Any(scene => scene.enabled && File.Exists(scene.path)),
                "The test profile needs at least one enabled scene.");
            return new BuildProfileSource(profile, true);
#else
            // No platform override: the source builds for the active target, Android
            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = "Build Uploader UploadTask Android build";
            buildConfig.ProductName = "SmokeBuild";
            Assert.IsNotEmpty(buildConfig.SceneGUIDs, "Configure enabled scenes in Build Settings.");
            return new BuildConfigSource(buildConfig, true);
#endif
        }
    }
}
