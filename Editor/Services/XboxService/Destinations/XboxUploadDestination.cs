using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    [Experimental]
    [Wiki("XboxUploadDestination", "destinations", "Upload a game package to Microsoft Partner Center via the Store Submission API.")]
    [UploadDestination("Xbox")]
    public partial class XboxUploadDestination : AUploadDestination
    {
        [Wiki("App", "The Xbox app entry whose credentials will be used for this submission.", 0)]
        private XboxConfig.XboxApp m_app;

        [Wiki("WaitForCertification", "Wait for a submission status after committing and log the result. " +
              "Adds extra time but confirms the submission was accepted.", 2)]
        private bool m_waitForCertification = false;
        
        [Wiki("RemoveFailedPendingSubmission", "Delete the current pending submission if there is one before creating a new submission. " +
                                      "Checks on xbox if there is a pending submission and deletes it before we start a new submission.", 3)]
        private bool m_removeFailedPendingSubmission = false;

        [Wiki("Dry Run", "Create the submission and upload the package, then delete the submission instead of committing it. " +
                         "Nothing is sent for certification.", 4)]
        private bool m_dryRun;

        private string m_cachedSubmissionId = "";

        public XboxUploadDestination() : base() { }

        public XboxUploadDestination(XboxConfig.XboxApp app) : base()
        {
            m_app = app;
        }

        public void SetApp(XboxConfig.XboxApp app)
        {
            m_app = app;
        }

        public void SetDryRun(bool dryRun)
        {
            m_dryRun = dryRun;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            if (m_app == null)
            {
                result.SetFailed("Xbox: No app selected.");
                return false;
            }

            string tenantId     = m_app.TenantId;
            string clientId     = m_app.ClientId;
            string clientSecret = m_app.ClientSecret;
            string productId    = m_app.ProductId;

            if (string.IsNullOrEmpty(productId))
            {
                result.SetFailed("Xbox: Product ID is empty. Set it in Project Settings → Build Uploader → Services → Xbox.");
                return false;
            }

            string zipPath = await GetPackagePath(result);
            if (string.IsNullOrEmpty(zipPath))
                return false;

            result.AddLog("Xbox: Acquiring access token...");
            string token = await Xbox.GetAccessToken(tenantId, clientId, clientSecret, result);
            if (string.IsNullOrEmpty(token))
                return false;

            if (m_removeFailedPendingSubmission)
            {
                string pendingId = await Xbox.GetPendingSubmissionId(productId, token, result);
                if (!string.IsNullOrEmpty(pendingId))
                {
                    result.AddLog($"Xbox: Deleting existing pending submission {pendingId}...");
                    bool deleted = await Xbox.DeletePendingSubmission(productId, pendingId, token, result);
                    if (!deleted)
                        return false;
                }
            }

            result.AddLog("Xbox: Creating new submission...");
            XboxCreateSubmissionResponse submission = await Xbox.CreateSubmission(productId, token, result);
            if (!submission.Successful)
                return false;

            m_cachedSubmissionId = submission.SubmissionId;

            result.AddLog($"Xbox: Uploading package from {zipPath}...");
            bool uploaded = await Xbox.UploadPackage(submission.FileUploadUrl, zipPath, result);

            if (m_dryRun)
            {
                // Delete the submission even if the package upload failed so a dry run never leaves one pending
                result.AddLog($"Xbox: Dry run, deleting submission {submission.SubmissionId} instead of committing it...");
                bool deleted = await Xbox.DeletePendingSubmission(productId, submission.SubmissionId, token, result);
                return uploaded && deleted;
            }

            if (!uploaded)
                return false;

            result.AddLog("Xbox: Committing submission...");
            bool committed = await Xbox.CommitSubmission(productId, submission.SubmissionId, token, result);
            if (!committed)
                return false;

            if (m_waitForCertification)
            {
                result.AddLog("Xbox: Waiting for certification status...");
                string status = await Xbox.PollSubmissionStatus(productId, submission.SubmissionId, token, result);
                result.AddLog($"Xbox: Final submission status = {status}");

                if (status == "CommitFailed" || status == "PublishFailed")
                {
                    result.SetFailed($"Xbox: Submission ended with status '{status}'.");
                    return false;
                }
            }

            result.AddLog("Xbox: Submission complete.");
            return true;
        }

        private async Task<string> GetPackagePath(UploadTaskReport.StepResult result)
        {
            string[] files = Directory.GetFiles(m_taskContentsFolder, "*.*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                result.SetFailed("Xbox: No files were found to upload in: " + m_taskContentsFolder);
                return null;
            }
            
            if (files.Length == 1)
            {
                result.AddLog($"Xbox: File to upload: '{files[0]}'.");
                return files[0];
            }
            
            string zipPath = Path.Combine(m_taskContentsFolder + "_zipped", "contents.zip");
            result.AddLog($"Xbox: Zipping content to: {zipPath}");
            if (!await ZipUtils.Zip(m_taskContentsFolder, zipPath, result))
            {
                return null;
            }

            return zipPath;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);
            
            XboxService service = InternalUtils.GetService<XboxService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Xbox App is not set."));
            }
            else
            {
                if (string.IsNullOrEmpty(m_app.ProductId))
                    errors.Add(service.ProjectSettingsLink("Xbox App has no Product ID.", ""));
                if (string.IsNullOrEmpty(m_app.TenantId))
                    errors.Add(service.ProjectSettingsLink("Xbox App has no Tenant ID.", ""));
                if (string.IsNullOrEmpty(m_app.ClientId))
                    errors.Add(service.ProjectSettingsLink("Xbox App has no Client ID.", ""));
                if (string.IsNullOrEmpty(m_app.ClientSecret))
                    errors.Add(service.PreferencesLink($"Xbox App '{m_app.Name}' has no Client Secret.", ""));
            }
        }

        public override string Summary()
        {
            return m_app != null ? $"App: {m_app.DisplayName}" : "No app selected";
        }

        public override Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "appId",              m_app?.Id ?? 0 },
                { "waitForCertification", m_waitForCertification },
                { "removeFailedPendingSubmission", m_removeFailedPendingSubmission },
                { "submissionIdFormat", m_submissionIdFormat?.Key ?? "" },
                { "dryRun", m_dryRun },
            };
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            var apps = XboxUIUtils.AppPopup.Values;
            if (data.TryGetValue("appId", out object appId))
                m_app = apps?.FirstOrDefault(a => a.Id == (int)(long)appId);

            if (data.TryGetValue("waitForCertification", out object wfc))
                m_waitForCertification = wfc is bool b ? b : wfc?.ToString() == "True";

            if (data.TryGetValue("removeFailedPendingSubmission", out object rfps))
                m_removeFailedPendingSubmission = rfps is bool b ? b : rfps?.ToString() == "True";

            if (m_submissionIdFormat != null && data.TryGetValue("submissionIdFormat", out object key))
                m_submissionIdFormat.Key = key?.ToString() ?? "";

            m_dryRun = data.TryGetValue("dryRun", out object dryRun) && dryRun is bool dryRunValue && dryRunValue;
        }
    }
}
