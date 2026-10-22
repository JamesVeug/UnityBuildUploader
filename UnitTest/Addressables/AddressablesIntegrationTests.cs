using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using PlayerBuildOption = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.PlayerBuildOption;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// The Addressables integration: forcing "Build Addressables on Player Build" for one build and putting it back,
    /// failing the build step when the content build fails, and the Build Addressables action.
    /// Uses its own settings objects and never replaces the project's default Addressables settings.
    /// </summary>
    [Category("BuildUploader.Addressables")]
    public class AddressablesIntegrationTests
    {
        private readonly List<string> m_createdFolders = new List<string>();
        private readonly List<AddressableAssetSettings> m_temporarySettings = new List<AddressableAssetSettings>();
        private bool m_hadPreference;
        private bool m_preference;

        [SetUp]
        public void SetUp()
        {
            m_hadPreference = EditorPrefs.HasKey(AddressablesIntegration.BuildWithPlayerPreferenceKey);
            m_preference = EditorPrefs.GetBool(AddressablesIntegration.BuildWithPlayerPreferenceKey, true);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_hadPreference)
                EditorPrefs.SetBool(AddressablesIntegration.BuildWithPlayerPreferenceKey, m_preference);
            else
                EditorPrefs.DeleteKey(AddressablesIntegration.BuildWithPlayerPreferenceKey);

            foreach (AddressableAssetSettings settings in m_temporarySettings)
            {
                if (settings != null)
                    UnityEngine.Object.DestroyImmediate(settings);
            }
            m_temporarySettings.Clear();

            foreach (string folder in m_createdFolders)
            {
                AssetDatabase.DeleteAsset(folder);
            }
            m_createdFolders.Clear();
        }

        [Test]
        public void IntegrationIsRegisteredWhenScriptsLoad()
        {
            Assert.IsInstanceOf<AddressablesIntegration>(AddressablesSupport.Implementation);
        }

        [Test]
        public void BuildWithPlayerIsForcedOnlyWhileTheScopeIsOpen()
        {
            AddressableAssetSettings settings = TemporarySettings(PlayerBuildOption.DoNotBuildWithPlayer);
            UploadTaskReport.StepResult result = TestSupport.Report();

            using (new BuildWithPlayerScope(settings, result))
            {
                Assert.AreEqual(PlayerBuildOption.BuildWithPlayer, settings.BuildAddressablesWithPlayerBuild);
                Assert.IsTrue(AddressablesIntegration.BuildsWithPlayer(settings));
            }

            Assert.AreEqual(PlayerBuildOption.DoNotBuildWithPlayer, settings.BuildAddressablesWithPlayerBuild);
            Assert.IsTrue(result.Successful, result.FailReason);
        }

        [Test]
        public void OptionIsRestoredWhenTheBuildThrows()
        {
            AddressableAssetSettings settings = TemporarySettings(PlayerBuildOption.DoNotBuildWithPlayer);

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (new BuildWithPlayerScope(settings, TestSupport.Report()))
                {
                    throw new InvalidOperationException("The player build threw");
                }
            });

            Assert.AreEqual(PlayerBuildOption.DoNotBuildWithPlayer, settings.BuildAddressablesWithPlayerBuild);
        }

        [Test]
        public void SettingsThatAlreadyBuildWithThePlayerAreLeftAlone()
        {
            AddressableAssetSettings buildWithPlayer = TemporarySettings(PlayerBuildOption.BuildWithPlayer);
            using (new BuildWithPlayerScope(buildWithPlayer, TestSupport.Report()))
            {
                Assert.AreEqual(PlayerBuildOption.BuildWithPlayer, buildWithPlayer.BuildAddressablesWithPlayerBuild);
            }
            Assert.AreEqual(PlayerBuildOption.BuildWithPlayer, buildWithPlayer.BuildAddressablesWithPlayerBuild);

            AddressableAssetSettings preference = TemporarySettings(PlayerBuildOption.PreferencesValue);
            EditorPrefs.SetBool(AddressablesIntegration.BuildWithPlayerPreferenceKey, true);
            using (new BuildWithPlayerScope(preference, TestSupport.Report()))
            {
                Assert.AreEqual(PlayerBuildOption.PreferencesValue, preference.BuildAddressablesWithPlayerBuild,
                    "The preference already builds with the player, so the settings are not touched.");
            }

            EditorPrefs.SetBool(AddressablesIntegration.BuildWithPlayerPreferenceKey, false);
            using (new BuildWithPlayerScope(preference, TestSupport.Report()))
            {
                Assert.AreEqual(PlayerBuildOption.BuildWithPlayer, preference.BuildAddressablesWithPlayerBuild);
            }
            Assert.AreEqual(PlayerBuildOption.PreferencesValue, preference.BuildAddressablesWithPlayerBuild);
        }

        [Test]
        public void SettingsSavedDuringTheBuildEndUpAsTheyStarted()
        {
            AddressableAssetSettings settings = TemporarySettings(PlayerBuildOption.DoNotBuildWithPlayer, persisted: true);
            string path = AssetDatabase.GetAssetPath(settings);
            string before = File.ReadAllText(path);

            using (new BuildWithPlayerScope(settings, TestSupport.Report()))
            {
                // As a build that changes and saves the settings would
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Assert.AreNotEqual(before, File.ReadAllText(path), "The forced option was not saved, so this test checks nothing.");
            }

            Assert.AreEqual(before, File.ReadAllText(path), "The settings file must end up as it started.");
            Assert.AreEqual(PlayerBuildOption.DoNotBuildWithPlayer, settings.BuildAddressablesWithPlayerBuild);
        }

        [Test]
        public void SettingsNotSavedDuringTheBuildAreNotWritten()
        {
            AddressableAssetSettings settings = TemporarySettings(PlayerBuildOption.DoNotBuildWithPlayer, persisted: true);
            string path = AssetDatabase.GetAssetPath(settings);
            string before = File.ReadAllText(path);
            DateTime written = File.GetLastWriteTimeUtc(path);

            using (new BuildWithPlayerScope(settings, TestSupport.Report()))
            {
            }

            Assert.AreEqual(before, File.ReadAllText(path));
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(path), "Settings nobody saved must not be written.");
            Assert.IsFalse(EditorUtility.IsDirty(settings), "Forcing the option must not leave the settings dirty.");
        }

        [Test]
        public void FailedContentBuildFailsTheBuildStep()
        {
            AddressableAssetSettings settings = TemporarySettings(PlayerBuildOption.DoNotBuildWithPlayer);
            UploadTaskReport.StepResult result = TestSupport.Report();
            string error = BuildWithPlayerScope.FailedContentBuildMessage + ", content not included in Player Build. \"unit test error\"";
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(error)));

            using (new BuildWithPlayerScope(settings, result))
            {
                // What Addressables 1.x logs while the player build carries on
                Debug.LogError(error);
            }

            Assert.IsFalse(result.Successful);
            StringAssert.Contains("unit test error", result.FailReason);
        }

        [Test]
        public void BuildAddressablesActionIsSavedAndStopsTheTaskByDefault()
        {
            var action = new BuildAddressablesAction(true);
            var reloaded = new BuildAddressablesAction();
            reloaded.Deserialize(JSON.DeserializeObject<Dictionary<string, object>>(JSON.SerializeObject(action.Serialize())));

            Assert.IsTrue(reloaded.Clean);
            Assert.IsTrue(new UploadConfig.UploadActionData(reloaded).StopTaskIfFailed);
        }

        [Test]
        public void BuildAddressablesActionBuildsContentForTheActiveTarget()
        {
            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                Assert.Ignore("Needs Addressables settings in the project. The test builds the project's own content and never creates settings.");

            UploadTaskReport.StepResult result = TestSupport.Report();
            Task<bool> execute = new BuildAddressablesAction(true).Execute(result);

            Assert.IsTrue(execute.IsCompleted, "The content build is synchronous.");
            Assert.IsTrue(execute.Result, result.FailReason);
            Assert.IsTrue(Directory.Exists(Addressables.BuildPath), "Nothing was built to " + Addressables.BuildPath);
            Assert.IsNotEmpty(Directory.GetFiles(Addressables.BuildPath, "catalog*", SearchOption.AllDirectories),
                "No catalog was built to " + Addressables.BuildPath);
        }

        private AddressableAssetSettings TemporarySettings(PlayerBuildOption option, bool persisted = false)
        {
            AddressableAssetSettings settings;
            if (persisted)
            {
                string folder = "Assets/BuildUploaderUnitTest_" + Guid.NewGuid().ToString("N");
                m_createdFolders.Add(folder);
                settings = AddressableAssetSettings.Create(folder, "UnitTestAddressableAssetSettings", false, true);
                settings.BuildAddressablesWithPlayerBuild = option;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            else
            {
                settings = AddressableAssetSettings.Create("Assets/Unused", "UnitTestAddressableAssetSettings", false, false);
                settings.BuildAddressablesWithPlayerBuild = option;
                m_temporarySettings.Add(settings);
            }

            return settings;
        }
    }
}
