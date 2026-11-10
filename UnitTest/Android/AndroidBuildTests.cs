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
    /// Builds Android players through an UploadTask. Its own assembly, so desktop jobs never list it; run it with
    /// -buildTarget Android and the Android Build Support module (SDK, NDK and OpenJDK) installed.
    /// </summary>
    public class AndroidBuildTests
    {
        private bool m_previousOpenBuildFolders;
        private bool m_previousBuildAppBundle;
        private bool m_previousIncludeMetaData;

        [SetUp]
        public void SetUp()
        {
            m_previousOpenBuildFolders = Preferences.OpenBuildFoldersAfterUpload;
            Preferences.OpenBuildFoldersAfterUpload = false;
            m_previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false;
            m_previousIncludeMetaData = BuildUploaderProjectSettings.Instance.IncludeBuildMetaDataInStreamingDataFolder;
            BuildUploaderProjectSettings.Instance.IncludeBuildMetaDataInStreamingDataFolder = true;
        }

        [TearDown]
        public void TearDown()
        {
            Preferences.OpenBuildFoldersAfterUpload = m_previousOpenBuildFolders;
            EditorUserBuildSettings.buildAppBundle = m_previousBuildAppBundle;
            BuildUploaderProjectSettings.Instance.IncludeBuildMetaDataInStreamingDataFolder = m_previousIncludeMetaData;
        }

        [UnityEngine.TestTools.UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        public System.Collections.IEnumerator OneAndroidBuild()
        {
            RequireAndroid();
            string root = TestSupport.OutputDirectory("Builds/Android");

            UploadTask task = CreateTask(CreateSource(), root);
            yield return TestSupport.Run(task);

            Assert.IsNotEmpty(Directory.GetFiles(root, "*.apk", SearchOption.AllDirectories),
                "UploadTask reported success without an APK under " + root);
            CheckReport(task);
        }

        [UnityEngine.TestTools.UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        public System.Collections.IEnumerator OneAndroidAppBundleBuild()
        {
            RequireAndroid();
            string root = TestSupport.OutputDirectory("Builds/AndroidAppBundle");

            // A BuildConfig on every version: Build Profiles already named App Bundles .aab
            BuildConfig buildConfig = NewBuildConfig("Build Uploader UploadTask Android App Bundle");
            buildConfig.AndroidBuildAppBundle = true;
            UploadTask task = CreateTask(new BuildConfigSource(buildConfig, true), root);
            yield return TestSupport.Run(task);

            Assert.IsNotEmpty(Directory.GetFiles(root, "*.aab", SearchOption.AllDirectories),
                "UploadTask reported success without an App Bundle under " + root);
            Assert.IsEmpty(Directory.GetFiles(root, "*.apk", SearchOption.AllDirectories),
                "An App Bundle was named .apk under " + root);
            CheckReport(task);
        }

        private static void RequireAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                Assert.Ignore("Run this test with -buildTarget Android.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                Assert.Ignore("Android Build Support is not installed.");
        }

        private static UploadTask CreateTask(AUploadSource source, string root)
        {
            var config = new UploadConfig();
            config.AddSource(source);
            config.AddDestination(new LocalPathDestination(root));
            var uploadTask = new UploadTask("Android UploadTask build", config);
            uploadTask.SetBuildDescription("UploadTask Android build integration test");
            return uploadTask;
        }

        /// <summary>
        /// The meta data went in through Resources and was removed again
        /// </summary>
        private static void CheckReport(UploadTask task)
        {
            string report = task.Report.GetReport();
            StringAssert.Contains("Adding build meta data to Resources for the build", report);
            Assert.IsFalse(Directory.Exists(BuildUploaderProjectSettings.MetaDataResourcesFolder),
                "The build meta data was left in the project");
        }

        private static BuildConfig NewBuildConfig(string name)
        {
            // No platform override: the source builds for the active target, Android
            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = name;
            buildConfig.ProductName = "SmokeBuild";
            Assert.IsNotEmpty(buildConfig.SceneGUIDs, "Configure enabled scenes in Build Settings.");
            return buildConfig;
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
            return new BuildConfigSource(NewBuildConfig("Build Uploader UploadTask Android build"), true);
#endif
        }
    }
}
