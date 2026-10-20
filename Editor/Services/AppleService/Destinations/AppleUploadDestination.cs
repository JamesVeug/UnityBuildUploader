using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Uploads an .ipa to App Store Connect / TestFlight via xcrun altool, then polls the
    /// App Store Connect REST API to recover the Build resource ID so downstream actions
    /// (e.g. add to a TestFlight group) can reference it via {appleBuildId}.
    ///
    /// Apple's REST API does not expose a public binary upload endpoint, so the binary
    /// step is intentionally a subprocess. macOS only.
    /// </summary>
    [Experimental]
    [Wiki("Apple", "destinations", "Upload an .ipa to App Store Connect / TestFlight via xcrun altool.")]
    [UploadDestination("Apple TestFlight")]
    public partial class AppleUploadDestination : AUploadDestination
    {
        [Wiki("API Key", "Which App Store Connect API Key to authenticate with.", 1)]
        private AppleConfig.AppleApiKey m_apiKey;

        [Wiki("App", "Which App Store Connect app receives the upload.", 2)]
        private AppleConfig.AppleApp m_app;

        [Wiki("Build Version",
            "CFBundleShortVersionString to match when polling App Store Connect for the uploaded build. " +
            "Defaults to {version}.", 4)]
        private string m_buildVersionFormat = Context.VERSION_KEY;

        [Wiki("Build Number",
            "CFBundleVersion (numeric build number) to match when polling App Store Connect for the uploaded build. " +
            "Defaults to {buildNumber}.", 5)]
        private string m_buildNumberFormat = Context.BUILD_NUMBER_KEY;

        [Wiki("Find Build Timeout (seconds)",
            "How long to wait for the uploaded build to appear in App Store Connect after altool completes. " +
            "Apple's processing pipeline can take several minutes; 600 (10 minutes) is the default.", 6)]
        private int m_findBuildTimeoutSeconds = 600;

        [Wiki("Dry Run",
            "Check the API Key and App with App Store Connect without uploading the .ipa. " +
            "altool is not run, so this also works outside macOS.", 7)]
        private bool m_dryRun;

        // Captured during Upload() so the StringContextModifier can publish them as
        // {appleBuildId} / {appleBuildVersion} / {appleBuildNumber} for downstream actions.
        private string m_lastBuildId;
        private string m_lastBuildVersion;
        private string m_lastBuildNumber;

        public AppleUploadDestination() : base()
        {
            // Required for reflection
        }

        public void SetApiKey(AppleConfig.AppleApiKey apiKey)
        {
            m_apiKey = apiKey;
        }

        public void SetApp(AppleConfig.AppleApp app)
        {
            m_app = app;
        }

        public void SetBuildVersionFormat(string format)
        {
            m_buildVersionFormat = format;
        }

        public void SetBuildNumberFormat(string format)
        {
            m_buildNumberFormat = format;
        }

        public void SetDryRun(bool dryRun)
        {
            m_dryRun = dryRun;
        }

        public override string Summary()
        {
            string appName = m_app != null ? m_app.Name : "<no app>";
            return $"TestFlight: {appName}";
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            m_lastBuildId = null;
            m_lastBuildVersion = null;
            m_lastBuildNumber = null;

            string ipaPath = GetFilePath(result);
            if (string.IsNullOrEmpty(ipaPath))
            {
                return false;
            }

            string shortVersion = m_context.FormatString(m_buildVersionFormat);
            string buildNumber = m_context.FormatString(m_buildNumberFormat);
            m_lastBuildVersion = shortVersion;
            m_lastBuildNumber = buildNumber;

            if (m_dryRun)
            {
                AppleSimpleResponse appResponse = await Apple.GetApp(m_apiKey, m_app.AppStoreConnectID, result);
                if (!appResponse.Successful)
                {
                    return false;
                }

                result.AddLog($"Dry run: would upload {Path.GetFileName(ipaPath)} ({shortVersion} ({buildNumber})) with xcrun altool");
                return true;
            }

            int progressId = ProgressUtils.Start("Apple TestFlight", $"Uploading {Path.GetFileName(ipaPath)} via altool...");
            try
            {
                AppleAltoolUploadResponse uploadResponse = await Apple.UploadIPA(ipaPath, m_app.Platform, m_apiKey, result);
                if (!uploadResponse.Successful)
                {
                    return false;
                }

                ProgressUtils.Report(progressId, 0.8f, "Waiting for App Store Connect to register the build...");

                AppleFindBuildResponse findResponse = await Apple.FindBuildByVersion(
                    m_apiKey, m_app.AppStoreConnectID,
                    shortVersion, buildNumber,
                    m_findBuildTimeoutSeconds, result);

                if (findResponse.Successful)
                {
                    m_lastBuildId = findResponse.BuildId;
                    result.AddLog($"Apple upload complete. Build ID: {m_lastBuildId}");
                    return true;
                }

                // altool succeeded but we couldn't recover the build ID. Treat as partial:
                // the upload is in flight on Apple's side but later actions that depend on
                // {appleBuildId} cannot run.
                result.SetFailed("altool upload succeeded but the build did not appear in App Store Connect within the timeout. " +
                    "Downstream actions that reference {appleBuildId} will not run.");
                return false;
            }
            finally
            {
                ProgressUtils.Remove(progressId);
            }
        }

        private string GetFilePath(UploadTaskReport.StepResult result)
        {
            // Two valid shapes for the source:
            //   1. m_taskContentsFolder is the .ipa itself
            //   2. m_taskContentsFolder is a directory containing the .ipa
            if (File.Exists(m_taskContentsFolder)
                && m_taskContentsFolder.EndsWith(".ipa", System.StringComparison.OrdinalIgnoreCase))
            {
                return m_taskContentsFolder;
            }

            if (!Directory.Exists(m_taskContentsFolder))
            {
                result.SetFailed($"Source path does not exist: {m_taskContentsFolder}");
                return null;
            }

            string[] ipas = Directory.GetFiles(m_taskContentsFolder, "*.ipa", SearchOption.TopDirectoryOnly);
            if (ipas.Length == 0)
            {
                result.SetFailed($"No .ipa file found in source folder: {m_taskContentsFolder}");
                return null;
            }
            if (ipas.Length > 1)
            {
                result.SetFailed(
                    $"Multiple .ipa files found in source folder. Set 'IPA File Name' to disambiguate: " +
                    string.Join(", ", ipas.Select(Path.GetFileName)));
                return null;
            }

            return ipas[0];
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            AppleService service = InternalUtils.GetService<AppleService>();
            if (!service.IsReadyToStartBuild(out GUIContent reason) && !(m_dryRun && Apple.Enabled))
            {
                // A dry run only calls the App Store Connect API, so it does not need macOS
                errors.Add(reason);
            }

            if (m_apiKey == null)
            {
                errors.Add(new GUIContent("API Key is not set."));
            }
            else
            {
                if (string.IsNullOrEmpty(m_apiKey.IssuerID))
                    errors.Add(AppleService.Instance.PreferencesLink($"API Key '{m_apiKey.Name}' is missing Issuer ID.", ""));

                if (string.IsNullOrEmpty(m_apiKey.KeyID))
                    errors.Add(AppleService.Instance.PreferencesLink($"API Key '{m_apiKey.Name}' is missing Key ID.", ""));

                if (string.IsNullOrEmpty(m_apiKey.PrivateKeyPath))
                    errors.Add(AppleService.Instance.PreferencesLink($"API Key '{m_apiKey.Name}' has no .p8 file path.", ""));
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.AppStoreConnectID))
            {
                errors.Add(AppleService.Instance.ProjectSettingsLink($"App '{m_app.Name}' is missing its App Store Connect ID.", ""));
            }

            if (string.IsNullOrEmpty(m_buildVersionFormat))
                errors.Add(new GUIContent("Build Version format is empty."));

            if (string.IsNullOrEmpty(m_buildNumberFormat))
                errors.Add(new GUIContent("Build Number format is empty."));

            if (m_findBuildTimeoutSeconds <= 0)
                errors.Add(new GUIContent("Find Build Timeout must be greater than zero."));
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "apiKey", m_apiKey?.Id ?? 0 },
                { "app", m_app?.Id ?? 0 },
                { "buildVersionFormat", m_buildVersionFormat ?? "" },
                { "buildNumberFormat", m_buildNumberFormat ?? "" },
                { "findBuildTimeoutSeconds", m_findBuildTimeoutSeconds },
                { "dryRun", m_dryRun }
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            AppleConfig.AppleApiKey[] keys = AppleUIUtils.ApiKeyPopup.Values;
            if (data.TryGetValue("apiKey", out object apiKeyId) && apiKeyId != null)
            {
                m_apiKey = keys.FirstOrDefault(a => a.Id == (long)apiKeyId);
            }

            AppleConfig.AppleApp[] apps = AppleUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object appId) && appId != null)
            {
                m_app = apps.FirstOrDefault(a => a.Id == (long)appId);
            }

            m_buildVersionFormat = data.TryGetValue("buildVersionFormat", out object bv) ? bv?.ToString() ?? Context.VERSION_KEY : Context.VERSION_KEY;
            m_buildNumberFormat = data.TryGetValue("buildNumberFormat", out object bn) ? bn?.ToString() ?? Context.BUILD_NUMBER_KEY : Context.BUILD_NUMBER_KEY;

            if (data.TryGetValue("findBuildTimeoutSeconds", out object timeout) && timeout != null)
            {
                // JSON numbers come back as long by default in this project's JSON helper.
                m_findBuildTimeoutSeconds = timeout is long l ? (int)l : System.Convert.ToInt32(timeout);
            }
            else
            {
                m_findBuildTimeoutSeconds = 600;
            }

            m_dryRun = data.TryGetValue("dryRun", out object dryRun) && dryRun is bool dryRunValue && dryRunValue;
        }
    }
}
