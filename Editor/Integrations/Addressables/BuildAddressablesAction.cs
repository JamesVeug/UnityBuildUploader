using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Wireframe
{
    /// <summary>
    /// Builds Addressables content for the editor's active build target.
    ///
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki(nameof(BuildAddressablesAction), "actions",
        "Builds Addressables content for the editor's active build target, as Build > New Build does in the Addressables Groups window. " +
        "Actions run before any source switches platform, so this is for content-only uploads (eg: remote content uploaded with a Folder source) or when the active platform is already the one being built. " +
        "To include Addressables content in a player build, enable Build Addressables on the Build Config or Build Profile source instead. " +
        "A failed content build stops the task unless Stop task if this fails is turned off. Requires the Addressables package (1.19.4 or newer) on Unity 2021.2 or newer.")]
    [UploadAction("Build Addressables")]
    public partial class BuildAddressablesAction : AUploadAction
    {
        [Wiki("Clean", "If enabled, previously built Addressables content is deleted first so the content is rebuilt from scratch.", 1)]
        private bool m_Clean;

        public override bool DefaultStopTaskIfFailed => true;

        public bool Clean
        {
            get => m_Clean;
            set => m_Clean = value;
        }

        public BuildAddressablesAction() : base()
        {
            // Required for reflection
        }

        public BuildAddressablesAction(bool clean) : base()
        {
            m_Clean = clean;
        }

        public override Task<bool> Execute(UploadTaskReport.StepResult stepResult)
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                stepResult.SetFailed("This project has no Addressables settings. Create them in Window > Asset Management > Addressables > Groups.");
                return Task.FromResult(false);
            }

            stepResult.AddLog($"Building Addressables content for the active build target {EditorUserBuildSettings.activeBuildTarget}");
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (m_Clean)
            {
                stepResult.AddLog("Cleaning previously built Addressables content");
                AddressableAssetSettings.CleanPlayerContent();
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null)
            {
                stepResult.SetFailed("Addressables did not return a build result.");
                return Task.FromResult(false);
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                stepResult.SetFailed("Addressables content build failed: " + result.Error);
                return Task.FromResult(false);
            }

            stepResult.AddLog($"Built Addressables content to {Addressables.BuildPath} in {stopwatch.ElapsedMilliseconds} ms");
            return Task.FromResult(true);
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                errors.Add(new GUIContent("This project has no Addressables settings. Create them in Window > Asset Management > Addressables > Groups."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "clean", m_Clean },
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_Clean = data != null && data.TryGetValue("clean", out object clean) && clean is bool cleanValue && cleanValue;
        }
    }
}
