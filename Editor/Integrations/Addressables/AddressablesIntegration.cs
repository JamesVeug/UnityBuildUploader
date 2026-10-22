using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[assembly: InternalsVisibleTo("BuildUploader.UnitTest.Addressables")]

namespace Wireframe
{
    /// <summary>
    /// Lets build sources build Addressables content with the player. Registered when scripts load, which in batch
    /// mode is before -uploadProfile runs.
    /// </summary>
    [InitializeOnLoad]
    internal class AddressablesIntegration : IAddressablesSupport
    {
        /// <summary>
        /// The preference Addressables reads when its settings defer to it (AddressablesPreferences.kBuildAddressablesWithPlayerBuildKey, which is internal)
        /// </summary>
        internal const string BuildWithPlayerPreferenceKey = "Addressables.BuildAddressablesWithPlayerBuild";

        static AddressablesIntegration()
        {
            AddressablesSupport.Implementation = new AddressablesIntegration();
        }

        public bool SettingsExist => AddressableAssetSettingsDefaultObject.SettingsExists;

        public bool BuildsWithPlayerByDefault => BuildsWithPlayer(AddressableAssetSettingsDefaultObject.Settings);

        public bool CleanContent(UploadTaskReport.StepResult stepResult)
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                stepResult.AddError("Cannot clean Addressables content: this project has no Addressables settings.");
                return false;
            }

            try
            {
                AddressableAssetSettings.CleanPlayerContent();
            }
            catch (Exception e)
            {
                stepResult.AddException(e);
                return false;
            }

            stepResult.AddLog("Cleaned previously built Addressables content");
            return true;
        }

        public IDisposable ForceBuildWithPlayer(UploadTaskReport.StepResult stepResult)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Cannot build Addressables content: this project has no Addressables settings.");
            }

            return new BuildWithPlayerScope(settings, stepResult);
        }

        /// <summary>
        /// True if a player build also builds Addressables content, as Addressables' own player build processor decides it.
        /// </summary>
        internal static bool BuildsWithPlayer(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            switch (settings.BuildAddressablesWithPlayerBuild)
            {
                case AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer:
                    return true;
                case AddressableAssetSettings.PlayerBuildOption.PreferencesValue:
                    return EditorPrefs.GetBool(BuildWithPlayerPreferenceKey, true);
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Sets "Build Addressables on Player Build" to Build With Player until disposed, so Addressables builds the content
    /// inside BuildPipeline.BuildPlayer. Also watches for Addressables reporting a failed content build, which
    /// Addressables 1.x only logs while the player build carries on without the content.
    /// </summary>
    internal sealed class BuildWithPlayerScope : IDisposable
    {
        // Start of the error Addressables logs (1.x) or throws as a BuildFailedException (2.x+) when the content build fails
        internal const string FailedContentBuildMessage = "Failed to build Addressables content";

        private readonly AddressableAssetSettings m_settings;
        private readonly UploadTaskReport.StepResult m_stepResult;
        private readonly AddressableAssetSettings.PlayerBuildOption m_previousOption;
        private readonly bool m_changedOption;
        private readonly string m_assetPath;
        private readonly string m_assetTextBefore;
        private string m_contentBuildError;
        private bool m_disposed;

        public BuildWithPlayerScope(AddressableAssetSettings settings, UploadTaskReport.StepResult stepResult)
        {
            m_settings = settings;
            m_stepResult = stepResult;
            m_previousOption = settings.BuildAddressablesWithPlayerBuild;
            Application.logMessageReceived += OnLogMessage;

            if (AddressablesIntegration.BuildsWithPlayer(settings))
            {
                stepResult.AddLog($"Addressables already builds its content with every player build ({m_previousOption}), so its settings are left unchanged.");
                return;
            }

            m_assetPath = AssetDatabase.GetAssetPath(settings);
            m_assetTextBefore = ReadAsset(m_assetPath);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            m_changedOption = true;
            stepResult.AddLog($"Building Addressables content with this player build. Build Addressables on Player Build is set to {AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer} until the build finishes (was {m_previousOption}).");
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            Application.logMessageReceived -= OnLogMessage;

            if (m_changedOption)
            {
                m_settings.BuildAddressablesWithPlayerBuild = m_previousOption;

                // The setter does not mark the settings dirty, so the forced option only reaches the file if the build
                // saved the settings. Save them again so the file ends up with the project's own option.
                if (ReadAsset(m_assetPath) != m_assetTextBefore)
                {
                    EditorUtility.SetDirty(m_settings);
                    AssetDatabase.SaveAssetIfDirty(m_settings);
                }

                m_stepResult.AddLog($"Build Addressables on Player Build restored to {m_previousOption}");
            }

            if (m_contentBuildError != null)
            {
                m_stepResult.SetFailed("Addressables content build failed, so the player does not have its Addressables content: " + m_contentBuildError);
            }
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (m_contentBuildError == null && (type == LogType.Error || type == LogType.Exception) &&
                condition != null && condition.Contains(FailedContentBuildMessage))
            {
                m_contentBuildError = condition;
            }
        }

        private static string ReadAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return null;
            }

            return File.ReadAllText(assetPath);
        }
    }
}
