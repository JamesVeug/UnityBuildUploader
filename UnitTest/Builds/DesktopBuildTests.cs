using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

namespace Wireframe.UnitTest
{
    public class DesktopBuildTests
    {
        [UnityEngine.TestTools.UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        public System.Collections.IEnumerator OneBuildForActiveDesktopTarget()
        {
            BuildTarget target = ActiveDesktopTarget();
            string root = TestSupport.OutputDirectory("Builds/" + target);

            yield return Build(target, CreateSource(target, false), root);
        }

#if BUILD_UPLOADER_ADDRESSABLES && UNITY_2021_2_OR_NEWER
        // Only compiled with Addressables installed: the CI runner fails a suite with skipped cases
        [UnityEngine.TestTools.UnityTest]
        [Category("BuildUploader.PlayerBuild")]
        public System.Collections.IEnumerator OneBuildWithAddressablesContent()
        {
            BuildTarget target = ActiveDesktopTarget();
            if (!AddressablesSupport.IsAvailable)
                Assert.Ignore(AddressablesSupport.Requirement + " to run this test.");
            if (!AddressablesSupport.Implementation.SettingsExist)
                Assert.Ignore("Create Addressables settings in the project to run this test.");
            string root = TestSupport.OutputDirectory("Builds/" + target + "-Addressables");

            yield return Build(target, CreateSource(target, true), root);

            string[] catalogs = Directory.GetFiles(root, "catalog.*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(Path.GetDirectoryName(path)) == "aa" &&
                               Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))) == "StreamingAssets")
                .ToArray();
            Assert.IsNotEmpty(catalogs, "The player has no StreamingAssets/aa/catalog under " + root);
        }
#endif

        private static BuildTarget ActiveDesktopTarget()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
                Assert.Ignore("Run this test with a supported desktop -buildTarget.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                Assert.Ignore("Build support module is not installed for " + target);
            return target;
        }

        private static AUploadSource CreateSource(BuildTarget target, bool buildAddressables)
        {
#if UNITY_6000_0_OR_NEWER
            string profilePath = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_BUILD_PROFILE");
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            Assert.IsNotNull(profile, "Not a Build Profile asset: " + profilePath);
            var wrapper = new BuildProfileWrapper(profile);
            Assert.AreEqual(target, wrapper.GetTarget, "The configured profile targets a different platform.");
            Assert.IsTrue(BuildProfileWrapper.GetScenesForBuild(profile).Any(scene => scene.enabled && File.Exists(scene.path)),
                "The test profile needs at least one enabled scene.");
            var source = new BuildProfileSource(profile, true);
#else
            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = "Build Uploader UploadTask smoke build";
            buildConfig.ProductName = "SmokeBuild";
            Assert.IsNotEmpty(buildConfig.SceneGUIDs, "Configure enabled scenes in Build Settings.");
            var source = new BuildConfigSource(buildConfig, true);
            source.SetPlatformOverride(BuildTargetGroup.Standalone, 0, target, BuildUtils.Architecture.x64);
#endif
            // Clean too, so the test proves the content is built with the player rather than left from an earlier build
            source.SetBuildAddressables(buildAddressables, buildAddressables);
            return source;
        }

        private static System.Collections.IEnumerator Build(BuildTarget target, AUploadSource source, string root)
        {
            var config = new UploadConfig();
            config.AddSource(source);
            config.AddDestination(new LocalPathDestination(root));
            var uploadTask = new UploadTask("Desktop UploadTask build " + target, config);
            uploadTask.SetBuildDescription("UploadTask build-profile integration test");
            yield return TestSupport.Run(uploadTask);

            string extension = target == BuildTarget.StandaloneOSX ? ".app" : target == BuildTarget.StandaloneWindows64 ? ".exe" : ".x86_64";
            Assert.IsTrue(Directory.GetFiles(root, "*" + extension, SearchOption.AllDirectories).Length > 0 ||
                          Directory.GetDirectories(root, "*" + extension, SearchOption.AllDirectories).Length > 0,
                "UploadTask reported success without a player output under " + root);
        }
    }
}
