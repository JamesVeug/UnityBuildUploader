using System;
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
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
                Assert.Ignore("Run this test with a supported desktop -buildTarget.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                Assert.Ignore("Build support module is not installed for " + target);

            string root = TestSupport.OutputDirectory("Builds/" + target);
            AUploadSource source;
#if UNITY_6000_0_OR_NEWER
            string profilePath = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_BUILD_PROFILE");
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
            Assert.IsNotNull(profile, "Not a Build Profile asset: " + profilePath);
            var wrapper = new BuildProfileWrapper(profile);
            Assert.AreEqual(target, wrapper.GetTarget, "The configured profile targets a different platform.");
            Assert.IsTrue(BuildProfileWrapper.GetScenesForBuild(profile).Any(scene => scene.enabled && File.Exists(scene.path)),
                "The test profile needs at least one enabled scene.");
            source = new BuildProfileSource(profile, true);
#else
            var buildConfig = new BuildConfig();
            buildConfig.SetEditorSettings();
            buildConfig.BuildName = "Build Uploader UploadTask smoke build";
            buildConfig.ProductName = "SmokeBuild";
            Assert.IsNotEmpty(buildConfig.SceneGUIDs, "Configure enabled scenes in Build Settings.");
            var legacySource = new BuildConfigSource(buildConfig, true);
            legacySource.SetPlatformOverride(BuildTargetGroup.Standalone, 0, target, BuildUtils.Architecture.x64);
            source = legacySource;
#endif
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
