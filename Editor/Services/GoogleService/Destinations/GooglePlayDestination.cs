using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload an Android .aab (preferred) or .apk to Google Play, then assign the
    /// resulting versionCode to a track and commit the edit. Authentication uses an
    /// OAuth2 access token with the androidpublisher scope, supplied via the shared
    /// GoogleConfig.GoogleApp Token.
    ///
    /// NOTE: This class's name path is saved in the JSON file so avoid renaming.
    /// </summary>
    [Experimental]
    [Wiki(nameof(GooglePlayDestination), "destinations", "Upload an Android bundle/APK to Google Play with track rolling optional.")]
    [UploadDestination("Google Play")]
    public partial class GooglePlayDestination : AUploadDestination
    {
        [Wiki("App", "Which Google App to use to upload to to (must include the androidpublisher scope).", 1)]
        private GoogleConfig.GoogleApp m_app;

        [Wiki("Play App", "Which Google Play application to publish to.", 2)]
        private GoogleConfig.GooglePlayApp m_playApp;

        [Wiki("Track", "Which release track receives the upload.", 3)]
        private GooglePlayTrack m_track = GooglePlayTrack.Internal;

        [Wiki("Release Status", "Status of the release on the track. 'completed' rolls out immediately; 'draft' stages it for manual rollout.", 4)]
        private string m_releaseStatusFormat = "completed";

        [Wiki("Release Name", "Optional release name shown in the Play Console. Supports string formatting like {version}.", 5)]
        private string m_releaseNameFormat = "{taskProfileName} {version}";

        [Wiki("Release Notes", "Optional release notes (en-US). Supports string formatting like {version}.", 6)]
        private string m_releaseNotesFormat = Context.TASK_DESCRIPTION_KEY;

        [Wiki("Binary File Name", "When the source is a folder, the file name to upload. eg: 'game.aab'. Leave empty to auto-detect a single .aab or .apk in the source folder.", 7)]
        private string m_binaryFileName = "";

        [Wiki("Dry Run", "Open an edit to check the credentials and package name, then delete it. " +
                         "The binary is not uploaded and nothing is committed.", 8)]
        private bool m_dryRun;

        public GooglePlayDestination() : base()
        {
            // Required for reflection
        }

        public GooglePlayDestination(string appName, string packageName, GooglePlayTrack track) : base()
        {
            m_app = new GoogleConfig.GoogleApp { Name = appName };
            m_playApp = new GoogleConfig.GooglePlayApp { PackageName = packageName };
            m_track = track;
        }

        public void SetApp(string appName)
        {
            m_app = new GoogleConfig.GoogleApp { Name = appName };
        }

        public void SetPlayApp(string playAppName, string packageName)
        {
            m_playApp = new GoogleConfig.GooglePlayApp { Name = playAppName, PackageName = packageName };
        }

        public void SetTrack(GooglePlayTrack track)
        {
            m_track = track;
        }

        public void SetReleaseStatus(string releaseStatus)
        {
            m_releaseStatusFormat = releaseStatus;
        }

        public void SetReleaseName(string releaseName)
        {
            m_releaseNameFormat = releaseName;
        }

        public void SetReleaseNotes(string releaseNotes)
        {
            m_releaseNotesFormat = releaseNotes;
        }

        public void SetBinaryFileName(string binaryFileName)
        {
            m_binaryFileName = binaryFileName;
        }

        public void SetDryRun(bool dryRun)
        {
            m_dryRun = dryRun;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            m_recordedVersionCode = 0;
            m_recordedEditId = null;

            string binaryPath = GetBinaryPath(result);
            if (string.IsNullOrEmpty(binaryPath))
            {
                return false;
            }

            string token = m_app.Token;
            string packageName = m_playApp.PackageName;
            string track = GooglePlay.TrackName(m_track);
            string releaseStatus = m_context.FormatString(m_releaseStatusFormat);
            string releaseName = m_context.FormatString(m_releaseNameFormat);
            string releaseNotes = m_context.FormatString(m_releaseNotesFormat);

            int progressId = ProgressUtils.Start("Google Play", $"Publishing {packageName} to {track}...");
            try
            {
                GooglePlayUploadResponse response = m_dryRun
                    ? await GooglePlay.DryRun(binaryPath, packageName, track, token, result)
                    : await GooglePlay.PublishBinary(
                        binaryPath, packageName, track,
                        releaseStatus, releaseName, releaseNotes,
                        token, result);

                if (response.Successful)
                {
                    m_recordedVersionCode = response.VersionCode;
                    m_recordedEditId = response.EditId;
                }

                return response.Successful;
            }
            finally
            {
                ProgressUtils.Remove(progressId);
            }
        }

        /// <summary>
        /// Resolve the .aab/.apk to upload.
        /// Three valid source shapes:
        ///   1. m_taskContentsFolder is the .aab/.apk itself.
        ///   2. m_taskContentsFolder is a directory and BinaryFileName names a file in it.
        ///   3. m_taskContentsFolder is a directory containing exactly one .aab or .apk.
        /// </summary>
        private string GetBinaryPath(UploadTaskReport.StepResult result)
        {
            if (File.Exists(m_taskContentsFolder))
            {
                string ext = Path.GetExtension(m_taskContentsFolder);
                if (string.Equals(ext, ".aab", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".apk", System.StringComparison.OrdinalIgnoreCase))
                {
                    return m_taskContentsFolder;
                }

                result.SetFailed($"Source file is not a .aab or .apk: {m_taskContentsFolder}");
                return null;
            }

            if (!Directory.Exists(m_taskContentsFolder))
            {
                result.SetFailed($"Source path does not exist: {m_taskContentsFolder}");
                return null;
            }

            string explicitName = m_context.FormatString(m_binaryFileName);
            if (!string.IsNullOrEmpty(explicitName))
            {
                string explicitPath = Path.Combine(m_taskContentsFolder, explicitName);
                if (File.Exists(explicitPath))
                {
                    return explicitPath;
                }

                result.SetFailed($"Binary file '{explicitName}' not found in source folder: {m_taskContentsFolder}");
                return null;
            }

            string[] bundles = Directory.GetFiles(m_taskContentsFolder, "*.aab", SearchOption.TopDirectoryOnly);
            string[] apks = Directory.GetFiles(m_taskContentsFolder, "*.apk", SearchOption.TopDirectoryOnly);
            string[] all = bundles.Concat(apks).ToArray();

            if (all.Length == 0)
            {
                result.SetFailed($"No .aab or .apk file found in source folder: {m_taskContentsFolder}");
                return null;
            }
            if (all.Length > 1)
            {
                result.SetFailed(
                    $"Multiple Android binaries found in source folder. Set 'Binary File Name' to disambiguate: " +
                    string.Join(", ", all.Select(Path.GetFileName)));
                return null;
            }

            return all[0];
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            GoogleService service = InternalUtils.GetService<GoogleService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Google App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.Token))
            {
                errors.Add(service.PreferencesLink($"Google App {m_app.Name} does not have an OAuth2 access token set.", ""));
            }

            if (m_playApp == null)
            {
                errors.Add(new GUIContent("Play App is not set."));
            }
            else if (string.IsNullOrEmpty(m_playApp.PackageName))
            {
                errors.Add(service.ProjectSettingsLink($"Play App '{m_playApp.Name}' is missing its package name.", ""));
            }

            if (string.IsNullOrEmpty(m_releaseStatusFormat))
            {
                errors.Add(new GUIContent("Release Status is not set."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "app", m_app?.Id ?? 0 },
                { "playApp", m_playApp?.Id ?? 0 },
                { "track", (int)m_track },
                { "releaseStatus", m_releaseStatusFormat },
                { "releaseName", m_releaseNameFormat },
                { "releaseNotes", m_releaseNotesFormat },
                { "binaryFileName", m_binaryFileName },
                { "dryRun", m_dryRun }
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            GoogleConfig.GoogleApp[] apps = GoogleUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object appId) && appId != null)
            {
                m_app = apps.FirstOrDefault(a => a.Id == (long)appId);
            }

            GoogleConfig.GooglePlayApp[] playApps = GoogleUIUtils.PlayAppPopup.Values;
            if (data.TryGetValue("playApp", out object playAppId) && playAppId != null)
            {
                m_playApp = playApps.FirstOrDefault(a => a.Id == (long)playAppId);
            }

            if (data.TryGetValue("track", out object trackObj) && trackObj != null)
            {
                int trackInt = trackObj is long l ? (int)l : System.Convert.ToInt32(trackObj);
                m_track = (GooglePlayTrack)trackInt;
            }
            else
            {
                m_track = GooglePlayTrack.Internal;
            }

            m_releaseStatusFormat = data.TryGetValue("releaseStatus", out object rs) ? rs?.ToString() ?? "completed" : "completed";
            m_releaseNameFormat = data.TryGetValue("releaseName", out object rn) ? rn?.ToString() ?? "{taskProfileName} {version}" : "{taskProfileName} {version}";
            m_releaseNotesFormat = data.TryGetValue("releaseNotes", out object notes) ? notes?.ToString() ?? Context.TASK_DESCRIPTION_KEY : Context.TASK_DESCRIPTION_KEY;
            m_binaryFileName = data.TryGetValue("binaryFileName", out object bin) ? bin?.ToString() ?? "" : "";
            m_dryRun = data.TryGetValue("dryRun", out object dryRun) && dryRun is bool dryRunValue && dryRunValue;
        }
    }
}
