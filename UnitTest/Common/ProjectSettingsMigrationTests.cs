using System;
using System.IO;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    public class ProjectSettingsMigrationTests
    {
        [Test]
        public void LegacyProjectSettingsMigrateIntoSeparateState()
        {
            string root = Path.Combine(Path.GetTempPath(), "BuildUploaderMigrationTests", Guid.NewGuid().ToString("N"));
            try
            {
                string migrationDirectory = Path.Combine(root, "Migration");
                string settingsPath = Path.Combine(migrationDirectory, "ProjectSettings.json");
                string statePath = Path.Combine(migrationDirectory, "ProjectState.json");
                Directory.CreateDirectory(migrationDirectory);
                File.WriteAllText(settingsPath,
                    "{\"Version\":1,\"IncludeBuildMetaDataInStreamingDataFolder\":false," +
                    "\"LastBuildNumber\":42,\"TotalUploadTasksStarted\":73," +
                    "\"AutoGenerateMenuItemPath\":\"Generated/MenuItems\"}");

                BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Load(
                    settingsPath, statePath, out BuildUploaderProjectState state);

                Assert.AreEqual(2, settings.Version);
                Assert.IsFalse(settings.IncludeBuildMetaDataInStreamingDataFolder);
                Assert.AreEqual("Generated/MenuItems", settings.AutoGenerateMenuItemPath);
                Assert.AreEqual(1, state.Version);
                Assert.AreEqual(42, state.LastBuildNumber);
                Assert.AreEqual(73, state.TotalUploadTasksStarted);

                string migratedSettingsJson = File.ReadAllText(settingsPath);
                string migratedStateJson = File.ReadAllText(statePath);
                StringAssert.DoesNotContain("LastBuildNumber", migratedSettingsJson);
                StringAssert.DoesNotContain("TotalUploadTasksStarted", migratedSettingsJson);

                BuildUploaderProjectSettings secondSettings = BuildUploaderProjectSettings.Load(
                    settingsPath, statePath, out BuildUploaderProjectState secondState);

                Assert.AreEqual(2, secondSettings.Version);
                Assert.AreEqual(42, secondState.LastBuildNumber);
                Assert.AreEqual(73, secondState.TotalUploadTasksStarted);
                Assert.AreEqual(migratedSettingsJson, File.ReadAllText(settingsPath));
                Assert.AreEqual(migratedStateJson, File.ReadAllText(statePath));

                string precedenceDirectory = Path.Combine(root, "ExistingState");
                string precedenceSettingsPath = Path.Combine(precedenceDirectory, "ProjectSettings.json");
                string precedenceStatePath = Path.Combine(precedenceDirectory, "ProjectState.json");
                Directory.CreateDirectory(precedenceDirectory);
                File.WriteAllText(precedenceSettingsPath,
                    "{\"IncludeBuildMetaDataInStreamingDataFolder\":true," +
                    "\"LastBuildNumber\":100,\"TotalUploadTasksStarted\":200," +
                    "\"AutoGenerateMenuItemPath\":\"Existing/State\"}");
                File.WriteAllText(precedenceStatePath,
                    "{\"Version\":1,\"LastBuildNumber\":7,\"TotalUploadTasksStarted\":9}");
                string existingStateJson = File.ReadAllText(precedenceStatePath);

                BuildUploaderProjectSettings precedenceSettings = BuildUploaderProjectSettings.Load(
                    precedenceSettingsPath, precedenceStatePath, out BuildUploaderProjectState precedenceState);

                Assert.AreEqual(2, precedenceSettings.Version);
                Assert.IsTrue(precedenceSettings.IncludeBuildMetaDataInStreamingDataFolder);
                Assert.AreEqual("Existing/State", precedenceSettings.AutoGenerateMenuItemPath);
                Assert.AreEqual(7, precedenceState.LastBuildNumber);
                Assert.AreEqual(9, precedenceState.TotalUploadTasksStarted);
                Assert.AreEqual(existingStateJson, File.ReadAllText(precedenceStatePath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
